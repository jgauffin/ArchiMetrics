namespace ArchiMetrics.Analysis.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public sealed class NeedsDocsOrRefactorAnalyzer
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly string _rootFolder;
        private readonly int _minimumTokens;

        // Weights for composite opacity score
        private readonly double _weightSemanticGap;
        private readonly double _weightComplexity;
        private readonly double _weightNesting;
        private readonly double _weightMagicLiterals;

        public NeedsDocsOrRefactorAnalyzer(
            IEmbeddingProvider embeddingProvider,
            string rootFolder,
            int minimumTokens = 20,
            double weightSemanticGap = 0.40,
            double weightComplexity = 0.30,
            double weightNesting = 0.15,
            double weightMagicLiterals = 0.15)
        {
            _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
            _rootFolder = rootFolder;
            _minimumTokens = minimumTokens;
            _weightSemanticGap = weightSemanticGap;
            _weightComplexity = weightComplexity;
            _weightNesting = weightNesting;
            _weightMagicLiterals = weightMagicLiterals;
        }

        public async Task<IReadOnlyList<NeedsDocsOrRefactorCandidate>> Analyze(
            IEnumerable<SyntaxTree> trees,
            CancellationToken cancellationToken = default)
        {
            var methods = new List<MethodInfo>();

            foreach (var tree in trees)
            {
                var root = tree.GetRoot(cancellationToken);
                CollectMethods(root, tree, methods);
            }

            if (methods.Count == 0)
            {
                return Array.Empty<NeedsDocsOrRefactorCandidate>();
            }

            // Build embedding inputs: pairs of [name, body] for each method
            var nameTexts = methods.Select(m => SplitIdentifier(m.Name)).ToList();
            var bodyTexts = methods.Select(m => m.BodyText).ToList();
            var allTexts = nameTexts.Concat(bodyTexts).ToList();

            var allEmbeddings = await _embeddingProvider
                .GetEmbeddings(allTexts, cancellationToken)
                .ConfigureAwait(false);

            var candidates = new List<NeedsDocsOrRefactorCandidate>();
            var count = methods.Count;

            for (var i = 0; i < count; i++)
            {
                var m = methods[i];
                var nameEmbedding = allEmbeddings[i];
                var bodyEmbedding = allEmbeddings[count + i];
                var similarity = CosineSimilarity(nameEmbedding, bodyEmbedding);

                var reasons = new List<string>();

                // Semantic gap
                var semanticGapScore = 1.0 - Math.Max(0, similarity);
                if (similarity < 0.4)
                {
                    reasons.Add($"Name does not describe what code does (similarity={similarity:F2})");
                }

                // Cyclomatic complexity (normalize: 1→0, 10+→1)
                var complexityNorm = Clamp01((m.CyclomaticComplexity - 1.0) / 9.0);
                if (m.CyclomaticComplexity >= 5)
                {
                    reasons.Add($"High cyclomatic complexity ({m.CyclomaticComplexity})");
                }

                // Nesting depth (normalize: 1→0, 4+→1)
                var nestingNorm = Clamp01((m.NestingDepth - 1.0) / 3.0);
                if (m.NestingDepth >= 3)
                {
                    reasons.Add($"Deep nesting ({m.NestingDepth} levels)");
                }

                // Magic literals (normalize: 0→0, 5+→1)
                var magicNorm = Clamp01(m.MagicLiteralCount / 5.0);
                if (m.MagicLiteralCount >= 2)
                {
                    reasons.Add($"Contains {m.MagicLiteralCount} magic literal(s)");
                }

                var opacityScore = (_weightSemanticGap * semanticGapScore)
                    + (_weightComplexity * complexityNorm)
                    + (_weightNesting * nestingNorm)
                    + (_weightMagicLiterals * magicNorm);

                candidates.Add(new NeedsDocsOrRefactorCandidate(
                    m.FilePath,
                    m.LineNumber,
                    m.EndLineNumber,
                    m.Name,
                    opacityScore,
                    similarity,
                    m.CyclomaticComplexity,
                    m.NestingDepth,
                    m.MagicLiteralCount,
                    reasons));
            }

            // Return all, ranked by opacity descending — consumer decides cutoff
            candidates.Sort((a, b) => b.OpacityScore.CompareTo(a.OpacityScore));
            return candidates;
        }

        private void CollectMethods(SyntaxNode root, SyntaxTree tree, List<MethodInfo> results)
        {
            foreach (var node in root.DescendantNodes())
            {
                SyntaxNode body;
                string name;

                switch (node)
                {
                    case MethodDeclarationSyntax method:
                        body = (SyntaxNode)method.Body ?? method.ExpressionBody;
                        name = method.Identifier.Text;
                        break;
                    case ConstructorDeclarationSyntax ctor:
                        body = ctor.Body;
                        name = ctor.Identifier.Text + ".ctor";
                        break;
                    case AccessorDeclarationSyntax accessor:
                        body = (SyntaxNode)accessor.Body ?? accessor.ExpressionBody;
                        var prop = accessor.Parent?.Parent as PropertyDeclarationSyntax;
                        name = (prop?.Identifier.Text ?? "") + "." + accessor.Keyword.Text;
                        break;
                    default:
                        continue;
                }

                if (body == null)
                {
                    continue;
                }

                var tokenCount = body.DescendantTokens().Count();
                if (tokenCount < _minimumTokens)
                {
                    continue;
                }

                var lineSpan = node.GetLocation().GetLineSpan();
                var filePath = lineSpan.Path.GetPathRelativeTo(_rootFolder);

                results.Add(new MethodInfo
                {
                    Name = name,
                    FilePath = filePath,
                    LineNumber = lineSpan.StartLinePosition.Line,
                    EndLineNumber = lineSpan.EndLinePosition.Line,
                    BodyText = body.ToFullString(),
                    CyclomaticComplexity = CountComplexity(body),
                    NestingDepth = CountMaxNesting(body),
                    MagicLiteralCount = CountMagicLiterals(body)
                });
            }
        }

        private static int CountComplexity(SyntaxNode body)
        {
            var cc = 1;
            foreach (var node in body.DescendantNodesAndSelf())
            {
                switch (node.Kind())
                {
                    case SyntaxKind.IfStatement:
                    case SyntaxKind.WhileStatement:
                    case SyntaxKind.ForStatement:
                    case SyntaxKind.ForEachStatement:
                    case SyntaxKind.CaseSwitchLabel:
                    case SyntaxKind.CatchClause:
                    case SyntaxKind.ConditionalExpression:
                    case SyntaxKind.CoalesceExpression:
                    case SyntaxKind.LogicalAndExpression:
                    case SyntaxKind.LogicalOrExpression:
                        cc++;
                        break;
                }
            }

            return cc;
        }

        private static int CountMaxNesting(SyntaxNode body)
        {
            return CountNesting(body, 0);
        }

        private static int CountNesting(SyntaxNode node, int depth)
        {
            var max = depth;
            foreach (var child in node.ChildNodes())
            {
                var childDepth = depth;
                switch (child.Kind())
                {
                    case SyntaxKind.IfStatement:
                    case SyntaxKind.ElseClause:
                    case SyntaxKind.WhileStatement:
                    case SyntaxKind.ForStatement:
                    case SyntaxKind.ForEachStatement:
                    case SyntaxKind.DoStatement:
                    case SyntaxKind.SwitchStatement:
                    case SyntaxKind.TryStatement:
                    case SyntaxKind.CatchClause:
                    case SyntaxKind.UsingStatement:
                    case SyntaxKind.LockStatement:
                        childDepth++;
                        break;
                }

                var nested = CountNesting(child, childDepth);
                if (nested > max)
                {
                    max = nested;
                }
            }

            return max;
        }

        private static int CountMagicLiterals(SyntaxNode body)
        {
            var count = 0;
            foreach (var token in body.DescendantTokens())
            {
                switch (token.Kind())
                {
                    case SyntaxKind.NumericLiteralToken:
                        // 0 and 1 are not magic
                        if (token.ValueText != "0" && token.ValueText != "1")
                        {
                            count++;
                        }

                        break;
                    case SyntaxKind.StringLiteralToken:
                        // Skip empty strings and single-char strings
                        var text = token.ValueText;
                        if (text.Length > 1)
                        {
                            // Skip strings that look like format specifiers, separators, etc.
                            if (!IsCommonStringLiteral(text))
                            {
                                count++;
                            }
                        }

                        break;
                }
            }

            return count;
        }

        private static bool IsCommonStringLiteral(string text)
        {
            // Common non-magic strings: whitespace, punctuation, format strings
            return text == " " || text == ", " || text == ": " || text == "." || text == "\n" || text == "\r\n"
                || text == "/" || text == "\\" || text == "|";
        }

        internal static string SplitIdentifier(string name)
        {
            // Remove .ctor suffix for constructors
            if (name.EndsWith(".ctor"))
            {
                name = name.Substring(0, name.Length - 5) + " constructor";
            }

            // Remove .get/.set suffix for accessors
            if (name.EndsWith(".get") || name.EndsWith(".set"))
            {
                var suffix = name.Substring(name.Length - 4);
                name = name.Substring(0, name.Length - 4);
                name = name + " " + suffix.Substring(1);
            }

            // Split PascalCase/camelCase: insert space before uppercase letters that follow lowercase
            var split = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1 $2");
            // Split on underscores
            split = split.Replace('_', ' ');
            return split.ToLowerInvariant().Trim();
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
            {
                return 0.0;
            }

            double dot = 0, normA = 0, normB = 0;
            for (var i = 0; i < a.Length; i++)
            {
                dot += a[i] * (double)b[i];
                normA += a[i] * (double)a[i];
                normB += b[i] * (double)b[i];
            }

            var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom == 0 ? 0.0 : dot / denom;
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private class MethodInfo
        {
            public string Name { get; set; }

            public string FilePath { get; set; }

            public int LineNumber { get; set; }

            public int EndLineNumber { get; set; }

            public string BodyText { get; set; }

            public int CyclomaticComplexity { get; set; }

            public int NestingDepth { get; set; }

            public int MagicLiteralCount { get; set; }
        }
    }
}
