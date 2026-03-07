namespace ArchiMetrics.Analysis.Metrics
{
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    internal static class SyntaxNormalizer
    {
        public static string Normalize(SyntaxNode node)
        {
            var sb = new StringBuilder();
            foreach (var token in node.DescendantTokens())
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

            return sb.ToString().TrimEnd();
        }
    }
}
