namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Linq;
    using System.Threading.Tasks;
    using ArchiMetrics.Analysis;
    using ArchiMetrics.Analysis.Common.Metrics;
    using Common;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    /// <summary>
    /// A source file may declare more than one type. Every declared type has to surface as its
    /// own entry in the metrics output: if one type silently takes over another type's slot, the
    /// report both hides real hotspots and skews every average computed over it.
    /// </summary>
    public sealed class MultipleTypesPerFileTests
    {
        public class GivenAFileWithSeveralTypes
        {
            private readonly CodeMetricsCalculator _calculator;

            public GivenAFileWithSeveralTypes()
            {
                _calculator = new CodeMetricsCalculator();
            }

            [Fact]
            public async Task WhenFileDeclaresTwoClassesThenEachIsReportedByItsOwnName()
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

                var typeNames = metrics.Single().TypeMetrics.Select(x => x.Name).AsArray();

                Assert.Equal(new[] { "Bar", "Foo" }, typeNames.OrderBy(x => x));
            }

            [Fact]
            public async Task WhenFileDeclaresRecordBeforeClassThenTheClassIsReported()
            {
                const string code = @"
namespace MyApp.Planning;

public record DayGroup(string Key, int Count);

public class DayGrouper
{
    public int Group(int value)
    {
        if (value > 0)
        {
            return value;
        }

        return 0;
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var typeNames = metrics.Single().TypeMetrics.Select(x => x.Name).AsArray();

                Assert.Contains("DayGrouper", typeNames);
            }

            [Fact]
            public async Task WhenFileDeclaresRecordBeforeClassThenTheClassKeepsItsOwnMembers()
            {
                const string code = @"
namespace MyApp.Planning;

public record DayGroup(string Key, int Count);

public class DayGrouper
{
    public int Group(int value)
    {
        if (value > 0)
        {
            return value;
        }

        return 0;
    }
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var grouper = metrics.Single().TypeMetrics.Single(x => x.Name == "DayGrouper");

                Assert.True(
                    grouper.CyclomaticComplexity >= 2,
                    "DayGrouper's own if-statement must be counted against DayGrouper.");
            }

            [Fact]
            public async Task WhenFileDeclaresRecordThenTheRecordIsReported()
            {
                const string code = @"
namespace MyApp.Planning;

public record DayGroup(string Key, int Count);

public class DayGrouper
{
    public int Group(int value) => value;
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var typeNames = metrics.Single().TypeMetrics.Select(x => x.Name).AsArray();

                Assert.Contains("DayGroup", typeNames);
            }

            [Fact]
            public async Task WhenFileDeclaresSeveralTypesThenNoTypeIsReportedTwice()
            {
                const string code = @"
namespace MyApp.Planning;

public record DayLeg(string From, string To);

public class DayLegRouter
{
    public string Route(string from) => from;
}

public interface IDayLegSink
{
    void Accept(string leg);
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var typeNames = metrics.Single().TypeMetrics.Select(x => x.Name).AsArray();

                Assert.Equal(typeNames.Length, typeNames.Distinct().Count());
            }

            /// <summary>
            /// Types declared outside a namespace are hoisted into a synthesized namespace, which
            /// rebuilds the syntax tree. The rebuild must keep each type pointing at its own
            /// declaration.
            /// </summary>
            [Fact]
            public async Task WhenTypesAreDeclaredOutsideANamespaceThenEachIsReportedByItsOwnName()
            {
                const string code = @"
public record AssembledDays(int Total);

public class DayAssembler
{
    public int Assemble(int value) => value;
}
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var typeNames = metrics.SelectMany(x => x.TypeMetrics).Select(x => x.Name).AsArray();

                Assert.Contains("AssembledDays", typeNames);
                Assert.Contains("DayAssembler", typeNames);
            }

            [Fact]
            public async Task WhenFileDeclaresARecordThenItIsReportedAsAClass()
            {
                const string code = @"
namespace MyApp.Planning;

public record AssembledDays(int Total);
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var record = metrics.Single().TypeMetrics.Single(x => x.Name == "AssembledDays");

                Assert.Equal(TypeMetricKind.Class, record.Kind);
            }

            [Fact]
            public async Task WhenFileDeclaresARecordStructThenItIsReportedAsAStruct()
            {
                const string code = @"
namespace MyApp.Planning;

public record struct DayIndex(int Value);
";
                var tree = CSharpSyntaxTree.ParseText(code);
                var metrics = (await _calculator.Calculate(new[] { tree })).AsArray();

                var record = metrics.Single().TypeMetrics.Single(x => x.Name == "DayIndex");

                Assert.Equal(TypeMetricKind.Struct, record.Kind);
            }
        }
    }
}
