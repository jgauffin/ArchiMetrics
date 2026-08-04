// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PhysicalLinesCalculatorTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the PhysicalLinesCalculatorTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Linq;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Xunit;

    /// <summary>
    /// Pins the meaning of "lines of code" to something a reader can verify by looking at the file: the
    /// lines that actually carry code.
    /// </summary>
    public sealed class PhysicalLinesCalculatorTests
    {
        private PhysicalLinesCalculatorTests()
        {
        }

        public class GivenAMethodDeclaration
        {
            private static int Calculate(string source)
            {
                var method = CSharpSyntaxTree.ParseText(source)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .First();

                return new PhysicalLinesCalculator().Calculate(method);
            }

            [Fact]
            public void WhenMethodFitsOnOneLineThenCountIsOne()
            {
                var source = @"public class C
{
    public int Go() { return 1; }
}";

                Assert.Equal(1, Calculate(source));
            }

            [Fact]
            public void WhenMethodSpansSeveralLinesThenEachIsCounted()
            {
                var source = @"public class C
{
    public int Go()
    {
        return 1;
    }
}";

                Assert.Equal(4, Calculate(source));
            }

            /// <summary>
            /// Whitespace and commentary are not code. Counting them would mean a developer could inflate or
            /// deflate the reported size of a method purely by adding notes to it.
            /// </summary>
            [Fact]
            public void WhenMethodContainsBlankAndCommentLinesThenTheyAreNotCounted()
            {
                var source = @"public class C
{
    public int Go()
    {
        // Explain the return value.

        /* and a block comment */

        return 1;
    }
}";

                Assert.Equal(4, Calculate(source));
            }

            /// <summary>
            /// Documentation sits in the member's leading trivia. A well-documented method must not measure
            /// as a larger method than an undocumented one.
            /// </summary>
            [Fact]
            public void WhenMethodHasXmlDocumentationThenItIsNotCounted()
            {
                var source = @"public class C
{
    /// <summary>
    /// Returns one.
    /// </summary>
    /// <returns>One.</returns>
    public int Go() { return 1; }
}";

                Assert.Equal(1, Calculate(source));
            }

            [Fact]
            public void WhenSingleStatementWrapsOverLinesThenEachLineIsCounted()
            {
                var source = @"public class C
{
    public int Go()
    {
        return 1 +
            2;
    }
}";

                Assert.Equal(5, Calculate(source));
            }

            /// <summary>
            /// A verbatim string is one token covering several lines, so the calculator has to expand it
            /// rather than record only the line it starts on.
            /// </summary>
            [Fact]
            public void WhenTokenSpansLinesThenEveryLineItCoversIsCounted()
            {
                var source = @"public class C
{
    public string Go()
    {
        return @""first
second"";
    }
}";

                Assert.Equal(5, Calculate(source));
            }

            [Fact]
            public void WhenMethodIsAbstractThenOnlyItsSignatureCounts()
            {
                var source = @"public abstract class C
{
    public abstract int Go();
}";

                Assert.Equal(1, Calculate(source));
            }
        }

        public class GivenNothingToMeasure
        {
            [Fact]
            public void WhenNodeIsNullThenCountIsZero()
            {
                Assert.Equal(0, new PhysicalLinesCalculator().Calculate(null));
            }
        }
    }
}
