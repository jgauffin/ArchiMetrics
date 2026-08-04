// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeReviewer.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the NodeReviewer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Common.CodeReview;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    public class NodeReviewer : INodeInspector
    {
        private readonly Dictionary<SyntaxKind, ITriviaEvaluation[]> _triviaEvaluations;
        private readonly Dictionary<SyntaxKind, ICodeEvaluation[]> _codeEvaluations;
        private readonly Dictionary<SyntaxKind, ISemanticEvaluation[]> _semanticEvaluations;
        private readonly Dictionary<SymbolKind, ISymbolEvaluation[]> _symbolEvaluations;

        public NodeReviewer(IEnumerable<IEvaluation> evaluations, IEnumerable<ISymbolEvaluation> symbolEvaluations)
        {
            var allEvaluations = evaluations.AsArray();
            _triviaEvaluations = allEvaluations.OfType<ITriviaEvaluation>().GroupBy(x => x.EvaluatedKind).ToDictionary(x => x.Key, x => x.AsArray());
            _codeEvaluations = allEvaluations.OfType<ICodeEvaluation>().GroupBy(x => x.EvaluatedKind).ToDictionary(x => x.Key, x => x.AsArray());
            _semanticEvaluations = allEvaluations.OfType<ISemanticEvaluation>().GroupBy(x => x.EvaluatedKind).ToDictionary(x => x.Key, x => x.AsArray());
            _symbolEvaluations = symbolEvaluations.GroupBy(x => x.EvaluatedKind).ToDictionary(x => x.Key, x => x.AsArray());
        }

        public async Task<IEnumerable<EvaluationResult>> Inspect(Solution solution, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (solution == null)
            {
                return Enumerable.Empty<EvaluationResult>();
            }

            // Build the shared reference index before fanning out. Every semantic rule queries it,
            // so if the documents start first they all queue up behind a scan that is competing
            // with them for the same threads. Warming it once means the lookups that follow are
            // already-completed awaits.
            await solution.WarmReferenceIndex().ConfigureAwait(false);

            var documents = (from project in solution.Projects
                             where project.HasDocuments
                             let compilation = project.GetCompilationAsync(cancellationToken)
                             from doc in project.Documents
                             let root = doc.SupportsSyntaxTree ? doc.GetSyntaxRootAsync(cancellationToken) : Task.FromResult<SyntaxNode>(null)
                             select new { project.FilePath, project.Name, compilation, root }).AsArray();

            // Bounded rather than one task per document: a large solution has hundreds of
            // documents, and letting them all run at once buys no extra parallelism while forcing
            // every compilation and syntax tree to stay live in memory simultaneously.
            var results = new ConcurrentBag<EvaluationResult[]>();
            await Parallel.ForEachAsync(
                documents,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                },
                async (document, _) =>
                {
                    var inspections = await GetInspections(document.FilePath, document.Name, document.compilation, document.root, solution).ConfigureAwait(false);
                    results.Add(inspections.AsArray());
                }).ConfigureAwait(false);

            return results.SelectMany(x => x).AsArray();
        }

        public async Task<IEnumerable<EvaluationResult>> Inspect(string projectPath, string projectName, SyntaxNode node, SemanticModel semanticModel, Solution solution)
        {
            var inspector = new InnerInspector(_triviaEvaluations, _codeEvaluations, _semanticEvaluations, semanticModel, solution);
            var inspectionTasks = inspector.Visit(node);
            var symbolInspectionTasks = Task.FromResult(Enumerable.Empty<EvaluationResult>());

            if (semanticModel != null)
            {
                var symbolInspector = new InnerSymbolAnalyzer(_symbolEvaluations, semanticModel);
                symbolInspectionTasks = symbolInspector.Visit(node);
            }

            await Task.WhenAll(inspectionTasks, symbolInspectionTasks).ConfigureAwait(false);

            var inspectionResults = inspectionTasks.Result;
            var symbolInspectionResults = symbolInspectionTasks.Result;
            var allResults = inspectionResults.Concat(symbolInspectionResults).AsArray();
            foreach (var result in allResults)
            {
                result.ProjectName = projectName;
                result.ProjectPath = projectPath;
            }

            return allResults.AsEnumerable();
        }

        private async Task<IEnumerable<EvaluationResult>> GetInspections(
            string filePath,
            string projectName,
            Task<Compilation> compilation,
            Task<SyntaxNode> root,
            Solution solution)
        {
            if (root == null || compilation == null || solution == null)
            {
                return Enumerable.Empty<EvaluationResult>();
            }

            var c = await compilation.ConfigureAwait(false);
            var r = await root.ConfigureAwait(false);
            var model = c.GetSemanticModel(r.SyntaxTree);
            return await Inspect(filePath, projectName, r, model, solution).ConfigureAwait(false);
        }

        private class InnerInspector : CSharpSyntaxVisitor<Task<IEnumerable<EvaluationResult>>>
        {
            // A set, not a list: this is tested against every node of every document, so a linear
            // scan through the supported kinds turns an O(1) check into O(kinds) on the hottest
            // path in the whole review.
            private readonly HashSet<SyntaxKind> _supportedSyntaxKinds;
            private readonly IDictionary<SyntaxKind, ITriviaEvaluation[]> _triviaEvaluations;
            private readonly IDictionary<SyntaxKind, ICodeEvaluation[]> _codeEvaluations;
            private readonly IDictionary<SyntaxKind, ISemanticEvaluation[]> _semanticEvaluations;
            private readonly SemanticModel _model;
            private readonly Solution _solution;

            public InnerInspector(IDictionary<SyntaxKind, ITriviaEvaluation[]> triviaEvaluations, IDictionary<SyntaxKind, ICodeEvaluation[]> codeEvaluations, IDictionary<SyntaxKind, ISemanticEvaluation[]> semanticEvaluations, SemanticModel model, Solution solution)
            {
                _supportedSyntaxKinds = new HashSet<SyntaxKind>(codeEvaluations.Select(_ => _.Key).Concat(semanticEvaluations.Select(_ => _.Key)));
                _triviaEvaluations = triviaEvaluations;
                _codeEvaluations = codeEvaluations;
                _semanticEvaluations = semanticEvaluations;
                _model = model;
                _solution = solution;
            }

            public override async Task<IEnumerable<EvaluationResult>> Visit(SyntaxNode node)
            {
                if (node == null)
                {
                    return Enumerable.Empty<EvaluationResult>();
                }

                var nodeChecks = CheckNodes(node.DescendantNodesAndSelf().Where(x => _supportedSyntaxKinds.Contains(x.Kind())).AsArray());
                var tokenResultTasks = node.DescendantTokens().SelectMany(VisitToken);
                var nodeResultTasks = await Task.WhenAll(nodeChecks).ConfigureAwait(false);

                var baseResults = nodeResultTasks.SelectMany(x => x).Concat(tokenResultTasks);
                return baseResults;
            }

            public override Task<IEnumerable<EvaluationResult>> DefaultVisit(SyntaxNode node)
            {
                return Task.FromResult(Enumerable.Empty<EvaluationResult>());
            }

            private static IEnumerable<EvaluationResult> GetTriviaEvaluations(SyntaxTrivia trivia, IEnumerable<ITriviaEvaluation> nodeEvaluations)
            {
                var results = nodeEvaluations.Select(
                    x =>
                    {
                        try
                        {
                            return x.Evaluate(trivia);
                        }
                        catch (Exception ex)
                        {
                            return new EvaluationResult
                            {
                                Title = ex.Message,
                                Suggestion = ex.StackTrace,
                                ErrorCount = 1,
                                Snippet = trivia.ToFullString(),
                                Quality = CodeQuality.Broken
                            };
                        }
                    })
                        .Where(x => x != null && x.Quality != CodeQuality.Good)
                        .AsArray();
                return results;
            }

            private static IEnumerable<EvaluationResult> GetCodeEvaluations(SyntaxNode node, IEnumerable<ICodeEvaluation> nodeEvaluations)
            {
                var results = nodeEvaluations
                    .Select(x =>
                    {
                        try
                        {
                            return x.Evaluate(node);
                        }
                        catch (Exception ex)
                        {
                            return new EvaluationResult
                            {
                                Title = ex.Message,
                                Suggestion = ex.StackTrace,
                                ErrorCount = 1,
                                Snippet = node.ToFullString(),
                                Quality = CodeQuality.Broken
                            };
                        }
                    })
                    .Where(x => x != null && x.Quality != CodeQuality.Good)
                    .AsArray();
                return results;
            }

            private static async Task<IEnumerable<EvaluationResult>> GetSemanticEvaluations(SyntaxNode node, IEnumerable<ISemanticEvaluation> nodeEvaluations, SemanticModel model, Solution solution)
            {
                if (model == null || solution == null)
                {
                    return Enumerable.Empty<EvaluationResult>();
                }

                var tasks = nodeEvaluations
                    .Select(async x =>
                        {
                            try
                            {
                                return await x.Evaluate(node, model, solution).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                return new EvaluationResult
                                {
                                    Title = ex.Message,
                                    Suggestion = ex.StackTrace,
                                    ErrorCount = 1,
                                    Snippet = node.ToFullString(),
                                    Quality = CodeQuality.Broken
                                };
                            }
                        });
                var results = (await Task.WhenAll(tasks).ConfigureAwait(false))
                    .Where(x => x != null && x.Quality != CodeQuality.Good)
                    .AsArray();
                return results;
            }

            private IEnumerable<EvaluationResult> VisitToken(SyntaxToken token)
            {
                var results = token.LeadingTrivia.Concat(token.TrailingTrivia)
                    .Where(x => _triviaEvaluations.ContainsKey(x.Kind()))
                    .SelectMany(trivia => GetTriviaEvaluations(trivia, _triviaEvaluations[trivia.Kind()]));

                return results;
            }

            private async Task<IEnumerable<EvaluationResult>> CheckNodes(SyntaxNode[] nodes)
            {
                var semanticResultTasks = nodes.Where(x => _semanticEvaluations.ContainsKey(x.Kind()))
                    .Select(x => CheckSemantics(x, x.Kind()));
                var codeResults = nodes.Where(x => _codeEvaluations.ContainsKey(x.Kind()))
                    .SelectMany(x => CheckCode(x, x.Kind()));
                var semanticResults = await Task.WhenAll(semanticResultTasks).ConfigureAwait(false);

                return semanticResults.SelectMany(x => x).Concat(codeResults);
            }

            private IEnumerable<EvaluationResult> CheckCode(SyntaxNode node, SyntaxKind kind)
            {
                var codeResults = GetCodeEvaluations(node, _codeEvaluations[kind]);
                return codeResults;
            }

            private async Task<IEnumerable<EvaluationResult>> CheckSemantics(SyntaxNode node, SyntaxKind kind)
            {
                var semanticResults = await GetSemanticEvaluations(node, _semanticEvaluations[kind], _model, _solution).ConfigureAwait(false);

                return semanticResults;
            }
        }

        private class InnerSymbolAnalyzer : CSharpSyntaxVisitor<Task<IEnumerable<EvaluationResult>>>
        {
            private readonly IDictionary<SymbolKind, ISymbolEvaluation[]> _evaluations;
            private readonly SemanticModel _model;

            public InnerSymbolAnalyzer(IDictionary<SymbolKind, ISymbolEvaluation[]> evaluations, SemanticModel model)
            {
                _evaluations = evaluations;
                _model = model;
            }

            public override Task<IEnumerable<EvaluationResult>> Visit(SyntaxNode node)
            {
                var results = Task.Run(() => node.DescendantNodesAndSelf()
                    .Select(x => _model.GetDeclaredSymbol(x))
                    .Where(x => x != null)
                    .Where(x => x.Kind.In(_evaluations.Keys))
                    .Select(x => new
                    {
                        Symbol = x,
                        Evaluations = _evaluations[x.Kind]
                    })
                    .SelectMany(x => x.Evaluations.Select(_ => _.Evaluate(x.Symbol, _model)))
                    .AsArray()
                    .AsEnumerable());

                return results;
            }
        }
    }
}
