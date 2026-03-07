namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis.Metrics;
    using Common;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class FileScopedNamespaceTests
    {
        public class GivenACodeMetricsCalculator
        {
            private readonly CodeMetricsCalculator _calculator;

            public GivenACodeMetricsCalculator()
            {
                _calculator = new CodeMetricsCalculator();
            }

            [Fact]
            public async Task CanCalculateMetricsForFileScopedNamespace()
            {
                const string code = @"
namespace MyApp.Services;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                Assert.Equal("MyApp.Services", metrics.First().Name);
            }

            [Fact]
            public async Task FileScopedNamespaceReturnsNonZeroLinesOfCode()
            {
                const string code = @"
namespace MyApp.Services;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                Assert.True(metrics.First().LinesOfCode > 0, "LinesOfCode should be greater than 0");
            }

            [Fact]
            public async Task FileScopedNamespaceFindsTypeMetrics()
            {
                const string code = @"
namespace MyApp.Models;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                var nsMetric = metrics.First();
                Assert.NotEmpty(nsMetric.TypeMetrics);
                Assert.Contains(nsMetric.TypeMetrics, t => t.Name == "Person");
            }

            [Fact]
            public async Task FileScopedNamespaceCalculatesCyclomaticComplexity()
            {
                const string code = @"
namespace MyApp.Logic;

public class Validator
{
    public bool IsValid(int value)
    {
        if (value > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                Assert.True(metrics.First().CyclomaticComplexity >= 2,
                    "CyclomaticComplexity should be at least 2 for if/else");
            }

            [Fact]
            public async Task FileScopedNamespaceWithMultipleClasses()
            {
                const string code = @"
namespace MyApp.Models;

public class Foo
{
    public int Value { get; set; }
}

public class Bar
{
    public string Name { get; set; }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                var nsMetric = metrics.First();
                Assert.Equal(2, nsMetric.TypeMetrics.Count());
            }

            [Fact]
            public async Task FileScopedNamespaceViaProjectCalculate()
            {
                const string code = @"
namespace MyApp.Services;

public class Greeter
{
    public string Greet(string name)
    {
        return ""Hello, "" + name;
    }
}
";
                var workspace = new AdhocWorkspace();
                var projectId = ProjectId.CreateNewId("testproject");
                var solution = workspace.CurrentSolution
                    .AddProject(projectId, "testproject", "testproject.dll", LanguageNames.CSharp);
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "Greeter.cs", code);
                var project = solution.Projects.First();

                var metrics = (await _calculator.Calculate(project, solution)).AsArray();

                Assert.NotEmpty(metrics);
                Assert.Equal("MyApp.Services", metrics.First().Name);
                Assert.True(metrics.First().LinesOfCode > 0);
            }

            [Fact]
            public async Task BlockScopedNamespaceStillWorks()
            {
                const string code = @"
namespace MyApp.Legacy
{
    public class OldClass
    {
        public void DoWork()
        {
            var x = 1 + 2;
        }
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                Assert.NotEmpty(metrics);
                Assert.Equal("MyApp.Legacy", metrics.First().Name);
                Assert.True(metrics.First().LinesOfCode > 0);
            }

            [Fact]
            public async Task MixedNamespaceStylesAcrossTrees()
            {
                const string fileScopedCode = @"
namespace MyApp.NewStyle;

public class NewClass
{
    public int Value { get; set; }
}
";
                const string blockScopedCode = @"
namespace MyApp.OldStyle
{
    public class OldClass
    {
        public string Name { get; set; }
    }
}
";
                var tree1 = CSharpSyntaxTree.ParseText(fileScopedCode);
                var tree2 = CSharpSyntaxTree.ParseText(blockScopedCode);
                var metrics = (await _calculator.Calculate(new[] { tree1, tree2 })).AsArray();

                Assert.Equal(2, metrics.Length);
                Assert.Contains(metrics, m => m.Name == "MyApp.NewStyle");
                Assert.Contains(metrics, m => m.Name == "MyApp.OldStyle");
            }
        }

        public class GivenAWorkspaceMetricsSummary
        {
            [Fact]
            public async Task SummaryShowsNonZeroMetricsForFileScopedNamespace()
            {
                const string code = @"
namespace MyApp.Services;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }
}
";
                var workspace = new AdhocWorkspace();
                var projectId = ProjectId.CreateNewId("testproject");
                var solution = workspace.CurrentSolution
                    .AddProject(projectId, "testproject", "testproject.dll", LanguageNames.CSharp);
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "Calculator.cs", code);

                var summary = new WorkspaceMetricsSummary();
                var result = await summary.GenerateSummary(solution);

                Assert.Contains("testproject", result);
                Assert.DoesNotContain("Lines: 0", result);
            }
        }
    }
}
