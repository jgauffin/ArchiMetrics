namespace ArchiMetrics.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Common.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class CodeAnalysisAgentTests
    {
        private static AdhocWorkspace CreateWorkspace(string code, string projectName = "testcode")
        {
            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId(projectName);
            var solution = workspace.CurrentSolution
                .AddProject(projectId, projectName, projectName + ".dll", LanguageNames.CSharp)
                .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "code.cs", code);
            workspace.TryApplyChanges(solution);
            return workspace;
        }

        public class CalculateMetrics
        {
            [Fact]
            public async Task ReturnsNamespaceMetricsForSimpleCode()
            {
                var code = @"
namespace Sample
{
    public class Calculator
    {
        public int Add(int a, int b) { return a + b; }
        public int Multiply(int a, int b) { return a * b; }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var result = await agent.CalculateMetrics();

                Assert.NotEmpty(result.Items);
                var ns = result.Items.First();
                Assert.Equal("Sample", ns.Name);
                Assert.NotEmpty(ns.TypeMetrics);
            }

            [Fact]
            public async Task CalculatesComplexityForNestedLogic()
            {
                var code = @"
namespace Sample
{
    public class Service
    {
        public int Process(int x)
        {
            if (x > 0)
            {
                for (int i = 0; i < x; i++)
                {
                    if (i % 2 == 0)
                    {
                        x += i;
                    }
                }
            }
            return x;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var result = await agent.CalculateMetrics();

                var type = result.Items.First().TypeMetrics.First();
                var member = type.MemberMetrics.First(m => m.Name.Contains("Process"));
                Assert.True(member.CyclomaticComplexity >= 3, $"Expected CC >= 3, got {member.CyclomaticComplexity}");
            }
        }

        public class DetectDuplication
        {
            [Fact]
            public async Task DetectsIdenticalMethods()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Calc(int x)
        {
            var r = 0;
            for (var i = 0; i < x; i++)
            {
                r += i * 2;
                if (r > 100) { r = r / 2; }
            }
            return r;
        }
    }

    public class B
    {
        public int Calc(int x)
        {
            var r = 0;
            for (var i = 0; i < x; i++)
            {
                r += i * 2;
                if (r > 100) { r = r / 2; }
            }
            return r;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var result = await agent.DetectDuplication(minimumTokens: 10);

                Assert.NotEmpty(result.Items);
            }

            [Fact]
            public async Task DetectsRenamedClones()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int input)
        {
            var total = 0;
            for (var idx = 0; idx < input; idx++)
            {
                total += idx * 3;
                if (total > 50) { total = total / 3; }
            }
            return total;
        }
    }

    public class B
    {
        public int Bar(int count)
        {
            var sum = 0;
            for (var j = 0; j < count; j++)
            {
                sum += j * 3;
                if (sum > 50) { sum = sum / 3; }
            }
            return sum;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var result = await agent.DetectDuplication(minimumTokens: 10);

                Assert.NotEmpty(result.Items);
                Assert.Contains(result.Items, c => c.CloneType == CloneType.Renamed);
            }

            [Fact]
            public async Task ReturnsNoClonesForDistinctMethods()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo(int x) { return x + 1; }
    }

    public class B
    {
        public string Bar(string s) { return s.ToUpper(); }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var result = await agent.DetectDuplication(minimumTokens: 5);

                Assert.Empty(result.Items);
            }

            [Fact]
            public async Task WithEmbeddingProviderDetectsSemanticClones()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Sum(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++) { result += i; }
            return result;
        }
    }

    public class B
    {
        public int Total(int n)
        {
            var acc = 0;
            var c = 0;
            while (c < n) { acc = acc + c; c++; }
            return acc;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new ConstantSimilarityEmbeddingProvider(0.95);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);
                var result = await agent.DetectDuplication(minimumTokens: 10, similarityThreshold: 0.80);

                Assert.Contains(result.Items, c => c.CloneType == CloneType.Semantic);
            }
        }

        public class FindNeedsDocsOrRefactor
        {
            [Fact]
            public async Task ThrowsWithoutEmbeddingProvider()
            {
                using var workspace = CreateWorkspace("namespace T { class C { void M() { int x = 1; } } }");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => agent.FindNeedsDocsOrRefactor());
            }

            [Fact]
            public async Task IdentifiesComplexMethodAsCandidate()
            {
                var code = @"
namespace Test
{
    public class Processor
    {
        public int Xfr(int a, int b, int c)
        {
            var r = 0;
            if (a > 10)
            {
                for (var i = 0; i < b; i++)
                {
                    if (i % 3 == 0)
                    {
                        r += i * 42;
                    }
                    else if (c > 5)
                    {
                        while (r < 1000)
                        {
                            r = r * 2 + 7;
                        }
                    }
                }
            }
            return r + 99;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new SemanticGapEmbeddingProvider(nameBodySimilarity: 0.15);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 10);

                Assert.NotEmpty(result.Items);
                var top = result.Items[0];
                Assert.Equal("Xfr", top.MemberName);
                Assert.True(top.OpacityScore > 0.5, $"Expected high opacity, got {top.OpacityScore:F2}");
                Assert.True(top.CyclomaticComplexity >= 4);
                Assert.True(top.NestingDepth >= 3);
                Assert.NotEmpty(top.Reasons);
            }

            [Fact]
            public async Task ClearMethodHasLowOpacity()
            {
                var code = @"
namespace Test
{
    public class MathHelper
    {
        public int CalculateSum(int count)
        {
            var result = 0;
            for (var i = 0; i < count; i++)
            {
                result += i;
            }
            return result;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new SemanticGapEmbeddingProvider(nameBodySimilarity: 0.85);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 10);

                Assert.NotEmpty(result.Items);
                var top = result.Items[0];
                Assert.True(top.OpacityScore < 0.3, $"Expected low opacity for clear method, got {top.OpacityScore:F2}");
            }

            [Fact]
            public async Task DetectsMagicLiterals()
            {
                var code = @"
namespace Test
{
    public class Config
    {
        public int Setup(int mode)
        {
            var timeout = 3600;
            var retries = 5;
            var port = 8080;
            if (mode == 42)
            {
                return timeout * retries + port;
            }
            return 0;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new SemanticGapEmbeddingProvider(nameBodySimilarity: 0.5);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 10);

                Assert.NotEmpty(result.Items);
                var candidate = result.Items.First(c => c.MemberName == "Setup");
                Assert.True(candidate.MagicLiteralCount >= 3,
                    $"Expected >= 3 magic literals, got {candidate.MagicLiteralCount}");
            }

            [Fact]
            public async Task RanksOpaqueMethodsFirst()
            {
                var code = @"
namespace Test
{
    public class Service
    {
        public int Xz(int a, int b)
        {
            var r = a ^ b;
            if ((r & 0xFF) > 128)
            {
                for (var i = 0; i < 32; i++)
                {
                    if (r % 7 == 0)
                    {
                        r = (r << 2) | 0x0F;
                    }
                }
            }
            return r;
        }

        public int AddNumbers(int first, int second)
        {
            var sum = first + second;
            return sum;
        }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new OrderedSimilarityEmbeddingProvider(
                    new[] { 0.1, 0.9 });
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 5);

                Assert.True(result.Items.Count >= 2);
                Assert.Equal("Xz", result.Items[0].MemberName);
                Assert.True(result.Items[0].OpacityScore > result.Items[1].OpacityScore);
            }

            [Fact]
            public async Task SkipsSmallMethods()
            {
                var code = @"
namespace Test
{
    public class Util
    {
        public int Get() { return 1; }
    }
}";
                using var workspace = CreateWorkspace(code);
                var provider = new SemanticGapEmbeddingProvider(nameBodySimilarity: 0.1);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 50);

                Assert.Empty(result.Items);
            }

            [Fact]
            public async Task ReturnsEmptyForNoMethods()
            {
                var code = @"namespace Test { public class Empty { } }";
                using var workspace = CreateWorkspace(code);
                var provider = new SemanticGapEmbeddingProvider(nameBodySimilarity: 0.5);
                var agent = new CodeAnalysisAgent(workspace, string.Empty, embeddingProvider: provider);

                var result = await agent.FindNeedsDocsOrRefactor(minimumTokens: 5);

                Assert.Empty(result.Items);
            }
        }

        public class GenerateWorkspaceSummary
        {
            [Fact]
            public async Task ReturnsFormattedSummaryForSimpleProject()
            {
                var code = @"
namespace Sample
{
    public class Calculator
    {
        public int Add(int a, int b) { return a + b; }
        public int Multiply(int a, int b) { return a * b; }
    }
}";
                using var workspace = CreateWorkspace(code);
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.NotNull(summary);
                Assert.Contains("testcode", summary);
                Assert.Contains("Sample", summary);
                Assert.Contains("Maintainability", summary);
            }

            [Fact]
            public async Task HealthyCodeShowsHealthyLabel()
            {
                var code = @"
namespace Clean
{
    public class Simple
    {
        public int Get() { return 1; }
    }
}";
                using var workspace = CreateWorkspace(code, "clean-project");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.Contains("Healthy", summary);
            }

            [Fact]
            public async Task SummaryIncludesTypeMetrics()
            {
                var code = @"
namespace Metrics
{
    public class Widget
    {
        public int Process(int x) { return x * 2; }
    }
}";
                using var workspace = CreateWorkspace(code, "metrics-project");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.Contains("Widget", summary);
                Assert.Contains("Complexity:", summary);
                Assert.Contains("Inheritance Depth:", summary);
                Assert.Contains("Afferent Coupling:", summary);
                Assert.Contains("Efferent Coupling:", summary);
                Assert.Contains("Instability:", summary);
            }

            [Fact]
            public async Task SummaryIncludesProjectLevelMetrics()
            {
                var code = @"
namespace Stats
{
    public class Foo
    {
        public void Bar() { }
    }
}";
                using var workspace = CreateWorkspace(code, "stats-project");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.Contains("Lines:", summary);
            }

            [Fact]
            public async Task ComplexCodeGetsWorseScore()
            {
                var code = @"
namespace Complex
{
    public class Tangled
    {
        public int Process(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k)
        {
            if (a > 0) { if (b > 0) { if (c > 0) { if (d > 0) { if (e > 0) {
                if (f > 0) { if (g > 0) { if (h > 0) { if (i > 0) { if (j > 0) {
                    if (k > 0) { return a + b + c + d + e + f + g + h + i + j + k; }
                } } } } }
            } } } } }
            return 0;
        }
    }
}";
                using var workspace = CreateWorkspace(code, "complex-project");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.DoesNotContain("Healthy", summary);
            }

            [Fact]
            public async Task EmptyProjectReturnsEmptySummary()
            {
                var workspace = new AdhocWorkspace();
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.NotNull(summary);
                Assert.Equal(string.Empty, summary);
            }

            [Fact]
            public async Task MultipleNamespacesAllAppearInSummary()
            {
                var code = @"
namespace Alpha
{
    public class One { public int Go() { return 1; } }
}
namespace Beta
{
    public class Two { public int Run() { return 2; } }
}";
                using var workspace = CreateWorkspace(code, "multi-ns");
                var agent = new CodeAnalysisAgent(workspace, string.Empty);
                var summary = await agent.GenerateWorkspaceSummary();

                Assert.Contains("Alpha", summary);
                Assert.Contains("Beta", summary);
            }
        }

        public class SplitIdentifierTests
        {
            [Theory]
            [InlineData("CalculateSum", "calculate sum")]
            [InlineData("getHTTPResponse", "get httpresponse")]
            [InlineData("parse_xml_data", "parse xml data")]
            [InlineData("MyClass.ctor", "my class constructor")]
            [InlineData("Value.get", "value get")]
            [InlineData("X", "x")]
            public void SplitsIdentifiersIntoNaturalLanguage(string input, string expected)
            {
                var result = Analysis.Metrics.NeedsDocsOrRefactorAnalyzer.SplitIdentifier(input);
                Assert.Equal(expected, result);
            }
        }

        #region Test Embedding Providers

        private class ConstantSimilarityEmbeddingProvider : IEmbeddingProvider
        {
            private readonly double _similarity;

            public ConstantSimilarityEmbeddingProvider(double similarity)
            {
                _similarity = similarity;
            }

            public Task<IReadOnlyList<float[]>> GetEmbeddings(
                IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            {
                var dim = 64;
                var result = new List<float[]>();
                for (var i = 0; i < texts.Count; i++)
                {
                    var vec = new float[dim];
                    if (i == 0)
                    {
                        vec[0] = 1.0f;
                    }
                    else
                    {
                        vec[0] = (float)_similarity;
                        vec[i % (dim - 1) + 1] = (float)Math.Sqrt(1.0 - _similarity * _similarity);
                    }

                    result.Add(vec);
                }

                return Task.FromResult<IReadOnlyList<float[]>>(result);
            }
        }

        private class SemanticGapEmbeddingProvider : IEmbeddingProvider
        {
            private readonly double _nameBodySimilarity;

            public SemanticGapEmbeddingProvider(double nameBodySimilarity)
            {
                _nameBodySimilarity = nameBodySimilarity;
            }

            public Task<IReadOnlyList<float[]>> GetEmbeddings(
                IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            {
                var dim = 64;
                var half = texts.Count / 2;
                var result = new List<float[]>();

                for (var i = 0; i < texts.Count; i++)
                {
                    var vec = new float[dim];
                    if (i < half)
                    {
                        vec[i % dim] = 1.0f;
                    }
                    else
                    {
                        var nameIdx = i - half;
                        var primaryAxis = nameIdx % dim;
                        var secondaryAxis = (nameIdx + 1) % dim;
                        if (secondaryAxis == primaryAxis) secondaryAxis = (primaryAxis + 2) % dim;

                        vec[primaryAxis] = (float)_nameBodySimilarity;
                        vec[secondaryAxis] = (float)Math.Sqrt(1.0 - _nameBodySimilarity * _nameBodySimilarity);
                    }

                    result.Add(vec);
                }

                return Task.FromResult<IReadOnlyList<float[]>>(result);
            }
        }

        private class OrderedSimilarityEmbeddingProvider : IEmbeddingProvider
        {
            private readonly double[] _similarities;

            public OrderedSimilarityEmbeddingProvider(double[] similarities)
            {
                _similarities = similarities;
            }

            public Task<IReadOnlyList<float[]>> GetEmbeddings(
                IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            {
                var dim = 64;
                var half = texts.Count / 2;
                var result = new List<float[]>();

                for (var i = 0; i < texts.Count; i++)
                {
                    var vec = new float[dim];
                    if (i < half)
                    {
                        var axis = (i * 2) % dim;
                        vec[axis] = 1.0f;
                    }
                    else
                    {
                        var nameIdx = i - half;
                        var sim = nameIdx < _similarities.Length ? _similarities[nameIdx] : 0.5;
                        var axis = (nameIdx * 2) % dim;
                        var secondAxis = (nameIdx * 2 + 1) % dim;

                        vec[axis] = (float)sim;
                        vec[secondAxis] = (float)Math.Sqrt(1.0 - sim * sim);
                    }

                    result.Add(vec);
                }

                return Task.FromResult<IReadOnlyList<float[]>>(result);
            }
        }

        #endregion
    }
}
