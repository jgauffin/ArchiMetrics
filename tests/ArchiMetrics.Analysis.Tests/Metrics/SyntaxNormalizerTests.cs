namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class SyntaxNormalizerTests
    {
        public class GivenNormalization
        {
            [Fact]
            public void WhenNormalizingIdentifiersThenReplacedWithIdToken()
            {
                var code = "{ var foo = 1; return foo; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("ID", normalized);
                Assert.DoesNotContain("foo", normalized);
            }

            [Fact]
            public void WhenNormalizingNumericLiteralsThenReplacedWithNumToken()
            {
                var code = "{ var x = 42; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("NUM", normalized);
                Assert.DoesNotContain("42", normalized);
            }

            [Fact]
            public void WhenNormalizingStringLiteralsThenReplacedWithStrToken()
            {
                var code = @"{ var x = ""hello""; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("STR", normalized);
                Assert.DoesNotContain("hello", normalized);
            }

            [Fact]
            public void WhenNormalizingKeywordsThenPreserved()
            {
                // 'var' is a contextual keyword treated as an identifier by Roslyn,
                // so we check 'return' which is a true keyword.
                var code = "{ int x = 1; return x; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("int", normalized);
                Assert.Contains("return", normalized);
            }

            [Fact]
            public void WhenNormalizingRenamedVariablesThenSameOutput()
            {
                var code1 = "{ var foo = 1; return foo; }";
                var code2 = "{ var bar = 1; return bar; }";

                var tree1 = CSharpSyntaxTree.ParseText(code1, new CSharpParseOptions(kind: SourceCodeKind.Script));
                var tree2 = CSharpSyntaxTree.ParseText(code2, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var norm1 = SyntaxNormalizer.Normalize(tree1.GetRoot());
                var norm2 = SyntaxNormalizer.Normalize(tree2.GetRoot());

                Assert.Equal(norm1, norm2);
            }

            [Fact]
            public void WhenNormalizingDifferentStructureThenDifferentOutput()
            {
                var code1 = "{ var x = 1; return x; }";
                var code2 = "{ if (true) { var x = 1; } return 0; }";

                var tree1 = CSharpSyntaxTree.ParseText(code1, new CSharpParseOptions(kind: SourceCodeKind.Script));
                var tree2 = CSharpSyntaxTree.ParseText(code2, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var norm1 = SyntaxNormalizer.Normalize(tree1.GetRoot());
                var norm2 = SyntaxNormalizer.Normalize(tree2.GetRoot());

                Assert.NotEqual(norm1, norm2);
            }

            [Fact]
            public void WhenNormalizingBoolLiteralsThenReplacedWithBoolToken()
            {
                var code = "{ var x = true; var y = false; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("BOOL", normalized);
            }

            [Fact]
            public void WhenNormalizingNullLiteralThenReplacedWithNullToken()
            {
                var code = "{ var x = null; }";
                var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));

                var normalized = SyntaxNormalizer.Normalize(tree.GetRoot());

                Assert.Contains("NULL", normalized);
            }
        }
    }
}
