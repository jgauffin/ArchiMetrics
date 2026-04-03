namespace ArchiMetrics.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.CodeReview;
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
        /// Returns the methods with the highest cyclomatic complexity across the
        /// entire solution (or a single project). Each result is a flat
        /// <see cref="MemberSummary"/> that includes the fully qualified location
        /// (namespace, type, file, line number), so an agent can jump straight to
        /// the most complex methods without drilling through the namespace/type tree.
        /// </summary>
        public Task<PagedResult<MemberSummary>> GetWorstMethods(
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            return GetWorstMethods(_workspace.CurrentSolution, projectName, skip, take, cancellationToken);
        }

        public async Task<PagedResult<MemberSummary>> GetWorstMethods(
            Solution solution,
            string projectName = null,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var namespaces = await CalculateAllNamespaces(solution, projectName, cancellationToken).ConfigureAwait(false);
            var sorted = namespaces
                .SelectMany(n => n.TypeMetrics.SelectMany(t =>
                    t.MemberMetrics.Select(m => MemberSummary.From(n.Name, t.Name, m))))
                .OrderByDescending(m => m.CyclomaticComplexity)
                .ToList();
            return PagedResult<MemberSummary>.Create(sorted, skip, take);
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

        /// <summary>
        /// Produces an ISO/IEC 5055-aligned report by running the supplied code review
        /// rules against the workspace and combining the violations with LOC metrics.
        /// Does not require an embedding provider — only the rule engine and basic
        /// metrics are used, so this works on any <see cref="CodeAnalysisAgent"/> instance.
        /// </summary>
        /// <param name="inspector">
        /// A <see cref="NodeReviewer"/> (or other <see cref="INodeInspector"/>) loaded
        /// with the rules to evaluate. The caller controls which rules are loaded,
        /// keeping the Analysis project decoupled from the Rules assembly.
        /// </param>
        /// <param name="rules">
        /// The same set of rules passed to the inspector, used to determine CWE coverage.
        /// </param>
        public Task<Iso5055Report> GenerateIso5055Report(
            INodeInspector inspector,
            IEnumerable<IEvaluation> rules,
            string projectName = null,
            CancellationToken cancellationToken = default)
        {
            return GenerateIso5055Report(inspector, rules, _workspace.CurrentSolution, projectName, cancellationToken);
        }

        public async Task<Iso5055Report> GenerateIso5055Report(
            INodeInspector inspector,
            IEnumerable<IEvaluation> rules,
            Solution solution,
            string projectName = null,
            CancellationToken cancellationToken = default)
        {
            var metricsTask = CalculateAllNamespaces(solution, projectName, cancellationToken);
            var evaluationsTask = inspector.Inspect(solution, cancellationToken);

            await Task.WhenAll(metricsTask, evaluationsTask).ConfigureAwait(false);

            return Iso5055ReportGenerator.Generate(
                evaluationsTask.Result,
                metricsTask.Result,
                rules);
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

            var tasks = projects
                .Select(p => _metricsCalculator.Calculate(p, solution))
                .ToList();

            var allMetrics = await Task.WhenAll(tasks).ConfigureAwait(false);
            return allMetrics.SelectMany(m => m).ToList();
        }

        private async Task<IReadOnlyList<SyntaxTree>> GetSyntaxTrees(
            Solution solution, string projectName, CancellationToken cancellationToken)
        {
            var projects = projectName != null
                ? solution.Projects.Where(p => p.Name == projectName)
                : solution.Projects;

            var tasks = projects
                .SelectMany(p => p.Documents)
                .Select(d => d.GetSyntaxTreeAsync(cancellationToken))
                .ToList();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.Where(t => t != null).ToList();
        }
    }
}
