namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Common.Metrics;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class NeedsDocsOrRefactorAnalyzerTests
    {
        public class GivenAnalysis
        {
            [Fact]
            public async Task WhenMethodHasMagicLiteralsThenDetected()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public double Calculate(double value)
        {
            var step1 = value * 3.14159;
            var step2 = step1 + 2.71828;
            var step3 = step2 / 1.41421;
            if (step3 > 42.0)
            {
                step3 = step3 - 9.81;
            }
            return step3 * 1000;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var provider = new ConstantEmbeddingProvider();
                var analyzer = new NeedsDocsOrRefactorAnalyzer(provider, string.Empty, minimumTokens: 10);

                var results = await analyzer.Analyze(new[] { tree });

                Assert.Single(results);
                Assert.True(results[0].MagicLiteralCount > 0,
                    $"Expected magic literals but got {results[0].MagicLiteralCount}");
            }

            [Fact]
            public async Task WhenMethodIsComplexThenHighComplexity()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Process(int x, int y, int z)
        {
            var result = 0;
            if (x > 0)
            {
                if (y > 0)
                {
                    for (var i = 0; i < x; i++)
                    {
                        if (i % 2 == 0)
                        {
                            result += i;
                        }
                        else if (i % 3 == 0)
                        {
                            result -= i;
                        }
                    }
                }
                else
                {
                    while (z > 0)
                    {
                        result += z;
                        z--;
                    }
                }
            }
            return result > 100 ? 100 : result;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var provider = new ConstantEmbeddingProvider();
                var analyzer = new NeedsDocsOrRefactorAnalyzer(provider, string.Empty, minimumTokens: 10);

                var results = await analyzer.Analyze(new[] { tree });

                Assert.Single(results);
                Assert.True(results[0].CyclomaticComplexity >= 5,
                    $"Expected high complexity but got {results[0].CyclomaticComplexity}");
            }

            [Fact]
            public async Task WhenMethodHasDeepNestingThenDetected()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int DeepMethod(int x)
        {
            var result = 0;
            if (x > 0)
            {
                for (var i = 0; i < x; i++)
                {
                    if (i > 5)
                    {
                        while (result < 100)
                        {
                            result += i;
                        }
                    }
                }
            }
            return result;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var provider = new ConstantEmbeddingProvider();
                var analyzer = new NeedsDocsOrRefactorAnalyzer(provider, string.Empty, minimumTokens: 10);

                var results = await analyzer.Analyze(new[] { tree });

                Assert.Single(results);
                Assert.True(results[0].NestingDepth >= 3,
                    $"Expected deep nesting but got {results[0].NestingDepth}");
            }

            [Fact]
            public async Task WhenNoMethodsMeetThresholdThenReturnsEmpty()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo() { return 1; }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var provider = new ConstantEmbeddingProvider();
                var analyzer = new NeedsDocsOrRefactorAnalyzer(provider, string.Empty, minimumTokens: 50);

                var results = await analyzer.Analyze(new[] { tree });

                Assert.Empty(results);
            }

            [Fact]
            public async Task WhenMultipleMethodsThenAllAnalyzed()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Method1(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100) { result = result / 2; }
            }
            return result;
        }

        public int Method2(int y)
        {
            var sum = 0;
            for (var j = 0; j < y; j++)
            {
                sum += j * 3;
                if (sum > 200) { sum = sum / 3; }
            }
            return sum;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var provider = new ConstantEmbeddingProvider();
                var analyzer = new NeedsDocsOrRefactorAnalyzer(provider, string.Empty, minimumTokens: 10);

                var results = await analyzer.Analyze(new[] { tree });

                Assert.Equal(2, results.Count);
            }
        }

        public class GivenSplitIdentifier
        {
            [Fact]
            public void WhenSplittingPascalCaseThenSplitsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("GetUserName");
                Assert.Equal("get user name", result);
            }

            [Fact]
            public void WhenSplittingCamelCaseThenSplitsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("getUserName");
                Assert.Equal("get user name", result);
            }

            [Fact]
            public void WhenSplittingConstructorThenFormatsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("MyClass.ctor");
                Assert.Equal("my class constructor", result);
            }

            [Fact]
            public void WhenSplittingGetAccessorThenFormatsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("Value.get");
                Assert.Equal("value get", result);
            }

            [Fact]
            public void WhenSplittingSetAccessorThenFormatsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("Value.set");
                Assert.Equal("value set", result);
            }

            [Fact]
            public void WhenSplittingUnderscoreSeparatedThenSplitsCorrectly()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("get_user_name");
                Assert.Equal("get user name", result);
            }

            [Fact]
            public void WhenSplittingSingleWordThenReturnsLowercase()
            {
                var result = NeedsDocsOrRefactorAnalyzer.SplitIdentifier("Calculate");
                Assert.Equal("calculate", result);
            }
        }

        /// <summary>
        /// Simple embedding provider that returns constant unit vectors,
        /// giving a consistent similarity for all name/body comparisons.
        /// </summary>
        private class ConstantEmbeddingProvider : IEmbeddingProvider
        {
            public Task<IReadOnlyList<float[]>> GetEmbeddings(
                IReadOnlyList<string> texts,
                CancellationToken cancellationToken = default)
            {
                var dim = 64;
                var result = new List<float[]>();
                for (var i = 0; i < texts.Count; i++)
                {
                    var vec = new float[dim];
                    // All vectors point in same direction → similarity = 1.0
                    vec[0] = 1.0f;
                    result.Add(vec);
                }

                return Task.FromResult<IReadOnlyList<float[]>>(result);
            }
        }
    }
}
