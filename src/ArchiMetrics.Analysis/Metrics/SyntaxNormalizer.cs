namespace ArchiMetrics.Analysis.Metrics
{
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    internal static class SyntaxNormalizer
    {
        public static (string Normalized, int TokenCount) NormalizeWithCount(SyntaxNode node)
        {
            var sb = new StringBuilder();
            var tokenCount = 0;
            foreach (var token in node.DescendantTokens())
            {
                tokenCount++;
                AppendToken(sb, token);
            }

            return (sb.ToString().TrimEnd(), tokenCount);
        }

        public static string Normalize(SyntaxNode node)
        {
            var sb = new StringBuilder();
            foreach (var token in node.DescendantTokens())
            {
                AppendToken(sb, token);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendToken(StringBuilder sb, SyntaxToken token)
        {
            var kind = token.Kind();
            switch (kind)
            {
                case SyntaxKind.IdentifierToken:
                    sb.Append("ID ");
                    break;
                case SyntaxKind.NumericLiteralToken:
                    sb.Append("NUM ");
                    break;
                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.InterpolatedStringTextToken:
                    sb.Append("STR ");
                    break;
                case SyntaxKind.CharacterLiteralToken:
                    sb.Append("CHR ");
                    break;
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.FalseKeyword:
                    sb.Append("BOOL ");
                    break;
                case SyntaxKind.NullKeyword:
                    sb.Append("NULL ");
                    break;
                default:
                    if (SyntaxFacts.IsKeywordKind(kind))
                    {
                        sb.Append(token.Text);
                        sb.Append(' ');
                    }
                    else if (SyntaxFacts.IsPunctuation(kind))
                    {
                        sb.Append(token.Text);
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append("TOK ");
                    }

                    break;
            }
        }
    }
}
