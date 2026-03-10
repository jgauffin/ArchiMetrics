namespace ArchiMetrics.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Metrics;
    using Metrics;
    using Microsoft.CodeAnalysis;

    public sealed class CodeAnalysisAgent : IDisposable
    {
        private readonly ICodeMetricsCalculator _metricsCalculator;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IProjectMetricsCalculator _projectMetricsCalculator;
        private readonly Workspace _workspace;
        private readonly string _rootFolder;
        private readonly bool _ownsEmbeddingProvider;

        public CodeAnalysisAgent(
            Workspace workspace,
            string rootFolder,
            IEmbeddingProvider embeddingProvider = null)
            : this(workspace, rootFolder, new CodeMetricsCalculator(), embeddingProvider, ownsEmbeddingProvider: false)
        {
        }

        public CodeAnalysisAgent(
            Workspace workspace,
            string rootFolder,
            ICodeMetricsCalculator metricsCalculator,
            IEmbeddingProvider embeddingProvider = null)
            : this(workspace, rootFolder, metricsCalculator, embeddingProvider, ownsEmbeddingProvider: false)
        {
        }

        private CodeAnalysisAgent(
            Workspace workspace,
            string rootFolder,
            ICodeMetricsCalculator metricsCalculator,
            IEmbeddingProvider embeddingProvider,
            bool ownsEmbeddingProvider)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _rootFolder = rootFolder ?? string.Empty;
            _metricsCalculator = metricsCalculator ?? throw new ArgumentNullException(nameof(metricsCalculator));
            _embeddingProvider = embeddingProvider;
            _ownsEmbeddingProvider = ownsEmbeddingProvider;
            _projectMetricsCalculator = new ProjectMetricsCalculator(_metricsCalculator);
        }

        public static CodeAnalysisAgent WithOnnxModel(
            Workspace workspace,
            string rootFolder,
            string modelDirectory,
            int maxSequenceLength = 512)
        {
            var modelPath = Path.Combine(modelDirectory, "model.onnx");
            var vocabPath = Path.Combine(modelDirectory, "vocab.json");
            var mergesPath = Path.Combine(modelDirectory, "merges.txt");

            var provider = OnnxEmbeddingProvider.Create(modelPath, vocabPath, mergesPath, maxSequenceLength);
            return new CodeAnalysisAgent(workspace, rootFolder, new CodeMetricsCalculator(), provider, ownsEmbeddingProvider: true);
        }

        public void Dispose()
        {
            if (_ownsEmbeddingProvider && _embeddingProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public Task<PagedResult<INamespaceMetric>> CalculateMetrics(
            string projectName = null,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            return CalculateMetrics(_workspace.CurrentSolution, projectName, skip, take, cancellationToken);
        }

        public async Task<PagedResult<INamespaceMetric>> CalculateMetrics(
            Solution solution,
            string projectName = null,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            var results = await CalculateAllNamespaces(solution, projectName, cancellationToken).ConfigureAwait(false);
            var sorted = results.OrderBy(n => n.MaintainabilityIndex).ToList();
            return PagedResult<INamespaceMetric>.Create(sorted, skip, take);
        }

        public Task<PagedResult<CloneClass>> DetectDuplication(
            string projectName = null,
            int minimumTokens = 50,
            double similarityThreshold = 0.85,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            return DetectDuplication(_workspace.CurrentSolution, projectName, minimumTokens, similarityThreshold, skip, take, cancellationToken);
        }

        public async Task<PagedResult<CloneClass>> DetectDuplication(
            Solution solution,
            string projectName = null,
            int minimumTokens = 50,
            double similarityThreshold = 0.85,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            var trees = await GetSyntaxTrees(solution, projectName, cancellationToken).ConfigureAwait(false);
            var detector = new DuplicationDetector(
                _rootFolder,
                _embeddingProvider,
                minimumTokens,
                similarityThreshold);
            var result = await detector.Detect(trees, cancellationToken).ConfigureAwait(false);
            var sorted = result.Clones.OrderByDescending(c => c.Instances.Count).ToList();
            return PagedResult<CloneClass>.Create(sorted, skip, take);
        }

        public Task<PagedResult<NeedsDocsOrRefactorCandidate>> FindNeedsDocsOrRefactor(
            string projectName = null,
            int minimumTokens = 20,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            return FindNeedsDocsOrRefactor(_workspace.CurrentSolution, projectName, minimumTokens, skip, take, cancellationToken);
        }

        public async Task<PagedResult<NeedsDocsOrRefactorCandidate>> FindNeedsDocsOrRefactor(
            Solution solution,
            string projectName = null,
            int minimumTokens = 20,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            if (_embeddingProvider == null)
            {
                throw new InvalidOperationException(
                    "An IEmbeddingProvider is required for NeedsDocsOrRefactor analysis. " +
                    "Pass an embedding provider when creating the CodeAnalysisAgent.");
            }

            var trees = await GetSyntaxTrees(solution, projectName, cancellationToken).ConfigureAwait(false);
            var analyzer = new NeedsDocsOrRefactorAnalyzer(
                _embeddingProvider,
                _rootFolder,
                minimumTokens);
            var results = await analyzer.Analyze(trees, cancellationToken).ConfigureAwait(false);
            var sorted = results.OrderByDescending(c => c.OpacityScore).ToList();
            return PagedResult<NeedsDocsOrRefactorCandidate>.Create(sorted, skip, take);
        }

        /// <summary>
        /// Returns the worst-offending namespaces across the entire solution, ranked by
        /// maintainability index (lowest first). Each result is a flat
        /// <see cref="NamespaceSummary"/> with no nested type or member trees, keeping
        /// the payload small enough for an agent to page through large codebases.
        /// </summary>
        public Task<PagedResult<NamespaceSummary>> GetWorstNamespaces(
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            return GetWorstNamespaces(_workspace.CurrentSolution, projectName, skip, take, cancellationToken);
        }

        public async Task<PagedResult<NamespaceSummary>> GetWorstNamespaces(
            Solution solution,
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var namespaces = await CalculateAllNamespaces(solution, projectName, cancellationToken).ConfigureAwait(false);
            var sorted = namespaces
                .Select(NamespaceSummary.From)
                .OrderBy(n => n.MaintainabilityIndex)
                .ToList();
            return PagedResult<NamespaceSummary>.Create(sorted, skip, take);
        }

        /// <summary>
        /// Drills into a single namespace and returns a flat <see cref="TypeSummary"/>
        /// for each type it contains, ranked by maintainability index (lowest first).
        /// This lets an agent inspect the types inside a namespace that was flagged by
        /// <see cref="GetWorstNamespaces"/> without pulling the entire solution tree.
        /// </summary>
        public Task<PagedResult<TypeSummary>> GetNamespaceTypes(
            string namespaceName,
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            return GetNamespaceTypes(_workspace.CurrentSolution, namespaceName, projectName, skip, take, cancellationToken);
        }

        public async Task<PagedResult<TypeSummary>> GetNamespaceTypes(
            Solution solution,
            string namespaceName,
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var namespaces = await CalculateAllNamespaces(solution, projectName, cancellationToken).ConfigureAwait(false);
            var types = namespaces
                .Where(n => n.Name == namespaceName)
                .SelectMany(n => n.TypeMetrics.Select(t => TypeSummary.From(n.Name, t)))
                .OrderBy(t => t.MaintainabilityIndex)
                .ToList();
            return PagedResult<TypeSummary>.Create(types, skip, take);
        }

        /// <summary>
        /// Returns the worst-offending types across all namespaces in the solution,
        /// ranked by maintainability index (lowest first). This is a flat,
        /// cross-cutting view that lets an agent jump straight to the most
        /// problematic types regardless of which namespace they belong to.
        /// </summary>
        public Task<PagedResult<TypeSummary>> GetWorstTypes(
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            return GetWorstTypes(_workspace.CurrentSolution, projectName, skip, take, cancellationToken);
        }

        public async Task<PagedResult<TypeSummary>> GetWorstTypes(
            Solution solution,
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var namespaces = await CalculateAllNamespaces(solution, projectName, cancellationToken).ConfigureAwait(false);
            var sorted = namespaces
                .SelectMany(n => n.TypeMetrics.Select(t => TypeSummary.From(n.Name, t)))
                .OrderBy(t => t.MaintainabilityIndex)
                .ToList();
            return PagedResult<TypeSummary>.Create(sorted, skip, take);
        }

        public Task<string> GenerateWorkspaceSummary()
        {
            return GenerateWorkspaceSummary(_workspace.CurrentSolution);
        }

        public Task<string> GenerateWorkspaceSummary(Solution solution)
        {
            var summary = new WorkspaceMetricsSummary(_projectMetricsCalculator);
            return summary.GenerateSummary(solution);
        }

        private async Task<List<INamespaceMetric>> CalculateAllNamespaces(
            Solution solution, string projectName, CancellationToken cancellationToken)
        {
            var projects = projectName != null
                ? solution.Projects.Where(p => p.Name == projectName)
                : solution.Projects;

            var results = new List<INamespaceMetric>();
            foreach (var project in projects)
            {
                var metrics = await _metricsCalculator.Calculate(project, solution).ConfigureAwait(false);
                results.AddRange(metrics);
            }

            return results;
        }

        private async Task<IReadOnlyList<SyntaxTree>> GetSyntaxTrees(
            Solution solution, string projectName, CancellationToken cancellationToken)
        {
            var projects = projectName != null
                ? solution.Projects.Where(p => p.Name == projectName)
                : solution.Projects;

            var trees = new List<SyntaxTree>();
            foreach (var project in projects)
            {
                foreach (var document in project.Documents)
                {
                    var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    if (tree != null)
                    {
                        trees.Add(tree);
                    }
                }
            }

            return trees;
        }
    }
}
