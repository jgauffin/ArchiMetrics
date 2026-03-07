namespace ArchiMetrics.Analysis.Metrics
{
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class MethodExtractor : CSharpSyntaxWalker
    {
        private readonly List<CloneInstance> _instances = new List<CloneInstance>();
        private readonly string _rootFolder;
        private readonly int _minimumTokens;

        public MethodExtractor(string rootFolder, int minimumTokens = 50)
        {
            _rootFolder = rootFolder;
            _minimumTokens = minimumTokens;
        }

        public IReadOnlyList<CloneInstance> Extract(IEnumerable<SyntaxTree> trees)
        {
            foreach (var tree in trees)
            {
                Visit(tree.GetRoot());
            }

            return _instances;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            TryAddInstance(node, node.Body ?? (SyntaxNode)node.ExpressionBody);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            TryAddInstance(node, node.Body);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            TryAddInstance(node, node.Body ?? (SyntaxNode)node.ExpressionBody);
            base.VisitAccessorDeclaration(node);
        }

        private void TryAddInstance(SyntaxNode declaration, SyntaxNode body)
        {
            if (body == null)
            {
                return;
            }

            var tokens = body.DescendantTokens().Count();
            if (tokens < _minimumTokens)
            {
                return;
            }

            var location = declaration.GetLocation();
            var lineSpan = location.GetLineSpan();
            var filePath = lineSpan.Path.GetPathRelativeTo(_rootFolder);
            var memberName = GetMemberName(declaration);
            var normalized = SyntaxNormalizer.Normalize(body);

            _instances.Add(new CloneInstance(
                filePath,
                lineSpan.StartLinePosition.Line,
                lineSpan.EndLinePosition.Line,
                memberName,
                normalized));
        }

        private static string GetMemberName(SyntaxNode node)
        {
            switch (node)
            {
                case MethodDeclarationSyntax m:
                    return m.Identifier.Text;
                case ConstructorDeclarationSyntax c:
                    return c.Identifier.Text + ".ctor";
                case AccessorDeclarationSyntax a:
                    var property = a.Parent?.Parent as PropertyDeclarationSyntax;
                    return (property?.Identifier.Text ?? "") + "." + a.Keyword.Text;
                default:
                    return node.ToString().Substring(0, System.Math.Min(30, node.ToString().Length));
            }
        }
    }
}
