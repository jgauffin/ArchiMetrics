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

    /// <summary>
    /// The facade over ArchiMetrics: wraps a Roslyn <see cref="Workspace"/> and exposes metrics, duplication
    /// detection and documentation analysis over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the type to use rather than the individual calculators. It exists because the analysis
    /// pipeline underneath has a lot of moving parts — compilations, semantic models, embedding providers —
    /// and wiring them correctly is not something every caller should have to learn.
    /// </para>
    /// <para>
    /// Every query pages through <c>skip</c>/<c>take</c> and can be narrowed to one project. That is
    /// deliberate: it is designed to be driven by tools and coding agents, for which returning an entire
    /// solution's metric tree in one call is worse than useless.
    /// </para>
    /// <para>
    /// Metric values are meaningless without their scale. See <see cref="MetricThresholds"/> for the ranges,
    /// directions and rating bands, and use its <c>Rate*</c> methods rather than comparing against numbers
    /// of your own.
    /// </para>
    /// </remarks>
    public sealed class CodeAnalysisAgent : IDisposable
    {
        private readonly ICodeMetricsCalculator _metricsCalculator;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IProjectMetricsCalculator _projectMetricsCalculator;
        private readonly Workspace _workspace;
        private readonly string _rootFolder;
        private readonly bool _ownsEmbeddingProvider;

        /// <summary>
        /// Initialises the agent over a workspace, using the default metrics calculator.
        /// </summary>
        /// <param name="workspace">The Roslyn workspace to analyse. Required.</param>
        /// <param name="rootFolder">
        /// The folder that file paths in results are reported relative to. Pass the repository root so that
        /// results are portable between machines; <see langword="null"/> is treated as empty, which leaves
        /// absolute paths in the output.
        /// </param>
        /// <param name="embeddingProvider">
        /// Supplies the vectors used for semantic clone detection and documentation analysis. Optional:
        /// without it <see cref="DetectDuplication(string, int, double, int, int, CancellationToken)"/> still
        /// finds structural clones, but
        /// <see cref="FindNeedsDocsOrRefactor(string, int, int, int, CancellationToken)"/> throws. The caller
        /// keeps ownership and must dispose it; use <see cref="WithOnnxModel"/> to have the agent own one.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="workspace"/> is <see langword="null"/>.</exception>
        public CodeAnalysisAgent(
            Workspace workspace,
            string rootFolder,
            IEmbeddingProvider embeddingProvider = null)
            : this(workspace, rootFolder, new CodeMetricsCalculator(), embeddingProvider, ownsEmbeddingProvider: false)
        {
        }

        /// <summary>
        /// Initialises the agent with a specific metrics calculator, for callers substituting the default
        /// implementation — most often a test double.
        /// </summary>
        /// <param name="workspace">The Roslyn workspace to analyse. Required.</param>
        /// <param name="rootFolder">The folder that file paths in results are reported relative to.</param>
        /// <param name="metricsCalculator">The calculator to use. Required.</param>
        /// <param name="embeddingProvider">
        /// Optional embedding provider. The caller keeps ownership and must dispose it.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="workspace"/> or <paramref name="metricsCalculator"/> is <see langword="null"/>.
        /// </exception>
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

        /// <summary>
        /// Creates an agent backed by a local ONNX embedding model, enabling the semantic analyses.
        /// </summary>
        /// <remarks>
        /// The agent created this way <em>owns</em> the embedding provider and disposes it with itself, which
        /// is the difference from passing a provider to a constructor. Use this when the model exists only to
        /// serve this agent, so its lifetime cannot be forgotten.
        /// </remarks>
        /// <param name="workspace">The Roslyn workspace to analyse.</param>
        /// <param name="rootFolder">The folder that file paths in results are reported relative to.</param>
        /// <param name="modelDirectory">
        /// A directory holding <c>model.onnx</c>, <c>vocab.json</c> and <c>merges.txt</c>.
        /// </param>
        /// <param name="maxSequenceLength">
        /// The longest token sequence handed to the model. Longer methods are truncated, so raising it costs
        /// time and memory but stops very long methods being judged on their opening lines alone.
        /// </param>
        /// <returns>An agent that will dispose the embedding provider when it is itself disposed.</returns>
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

        /// <summary>
        /// Releases the embedding provider, but only if this agent created it via <see cref="WithOnnxModel"/>.
        /// A provider passed in by the caller is left alone, since the caller may still be using it.
        /// </summary>
        public void Dispose()
        {
            if (_ownsEmbeddingProvider && _embeddingProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Calculates namespace-level metrics across the workspace, worst first.
        /// </summary>
        /// <remarks>
        /// Results are ordered by maintainability index ascending, so the namespaces most in need of
        /// attention arrive first and a caller taking only the first page still sees the worst of the
        /// codebase. See <see cref="MetricThresholds"/> for what the values mean.
        /// </remarks>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of namespace metrics, ordered worst first.</returns>
        public Task<PagedResult<INamespaceMetric>> CalculateMetrics(
            string projectName = null,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            return CalculateMetrics(_workspace.CurrentSolution, projectName, skip, take, cancellationToken);
        }

        /// <summary>
        /// Calculates namespace-level metrics for a specific solution snapshot, worst first.
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of namespace metrics, ordered worst first.</returns>
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

        /// <summary>
        /// Finds duplicated code, grouped into clone classes and ordered by how many copies each has.
        /// </summary>
        /// <remarks>
        /// Detection runs in two layers. The first compares normalised syntax and catches exact and renamed
        /// copies. The second compares embeddings and catches code that does the same thing while looking
        /// different — that layer runs only if an embedding provider was supplied, so without one this finds
        /// structural duplication only, quietly.
        /// </remarks>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="minimumTokens">
        /// The size below which a method is ignored. Small methods resemble each other for uninteresting
        /// reasons — property accessors and guard clauses are all alike — so the floor keeps the noise out.
        /// </param>
        /// <param name="similarityThreshold">
        /// The cosine similarity, 0.0 to 1.0, above which two methods count as semantic clones. Lowering it
        /// finds looser matches at the cost of false positives.
        /// </param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of clone classes, most-copied first.</returns>
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

        /// <summary>
        /// Finds duplicated code in a specific solution snapshot, grouped into clone classes.
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="minimumTokens">The size below which a method is ignored.</param>
        /// <param name="similarityThreshold">Cosine similarity, 0.0 to 1.0, above which two methods are clones.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of clone classes, most-copied first.</returns>
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

        /// <summary>
        /// Finds methods whose names do not tell the reader what they do, ranked by how opaque they are.
        /// </summary>
        /// <remarks>
        /// The analysis compares an embedding of the method's name against an embedding of its body. A wide
        /// gap means the name is not describing the work, which leaves a reader no choice but to read the
        /// whole implementation. Such a method wants either a clearer name, a documentation comment, or
        /// breaking up — the result says a method is hard to understand, not which of the three to do.
        /// </remarks>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="minimumTokens">The size below which a method is ignored as too trivial to matter.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of candidates, most opaque first.</returns>
        /// <exception cref="InvalidOperationException">
        /// No embedding provider was supplied. Unlike duplication detection this analysis cannot degrade
        /// gracefully, because comparing a name to a body is the whole of what it does.
        /// </exception>
        public Task<PagedResult<NeedsDocsOrRefactorCandidate>> FindNeedsDocsOrRefactor(
            string projectName = null,
            int minimumTokens = 20,
            int skip = 0,
            int take = 0,
            CancellationToken cancellationToken = default)
        {
            return FindNeedsDocsOrRefactor(_workspace.CurrentSolution, projectName, minimumTokens, skip, take, cancellationToken);
        }

        /// <summary>
        /// Finds opaque methods in a specific solution snapshot, ranked by how opaque they are.
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="minimumTokens">The size below which a method is ignored.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of candidates, most opaque first.</returns>
        /// <exception cref="InvalidOperationException">No embedding provider was supplied.</exception>
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

        /// <summary>
        /// Returns the worst-offending namespaces in a specific solution snapshot, ranked by maintainability
        /// index (lowest first).
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of flat namespace summaries, worst first.</returns>
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

        /// <summary>
        /// Returns a flat summary of every type in one namespace of a specific solution snapshot, ranked by
        /// maintainability index (lowest first).
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="namespaceName">The namespace to drill into.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of flat type summaries, worst first.</returns>
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

        /// <summary>
        /// Returns the most complex methods in a specific solution snapshot, each carrying its fully
        /// qualified location so a caller can jump straight to it.
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of flat member summaries, most complex first.</returns>
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

        /// <summary>
        /// Returns the worst-offending types across every namespace in a specific solution snapshot, ranked
        /// by maintainability index (lowest first).
        /// </summary>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="skip">Results to skip, for paging.</param>
        /// <param name="take">Results to return; 0 returns everything from <paramref name="skip"/> on.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>A page of flat type summaries, worst first, regardless of namespace.</returns>
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

        /// <summary>
        /// Produces an ISO/IEC 5055-aligned report for a specific solution snapshot.
        /// </summary>
        /// <param name="inspector">The inspector loaded with the rules to evaluate.</param>
        /// <param name="rules">The same rules, used to determine which CWEs the report can speak to.</param>
        /// <param name="solution">The solution to analyse, rather than the workspace's current one.</param>
        /// <param name="projectName">Limits analysis to one project. <see langword="null"/> analyses all.</param>
        /// <param name="cancellationToken">Cancels the analysis.</param>
        /// <returns>The report, including the CWE identifiers the loaded rules actually cover.</returns>
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

        /// <summary>
        /// Renders the whole workspace as plain text: every project, namespace and type with its metrics and
        /// a health rating.
        /// </summary>
        /// <remarks>
        /// The text opens with a legend explaining every scale it uses, so the output can be handed to a
        /// reader — or a model — that has never seen this library. Without it "Maintainability: 42" and
        /// "[3 - Concerning]" are two numbers running in opposite directions with nothing to say so.
        /// </remarks>
        /// <returns>The summary text, or an empty string if the workspace holds no projects.</returns>
        public Task<string> GenerateWorkspaceSummary()
        {
            return GenerateWorkspaceSummary(_workspace.CurrentSolution);
        }

        /// <summary>
        /// Renders a specific solution snapshot as plain text, prefixed by the scale legend.
        /// </summary>
        /// <param name="solution">The solution to render, rather than the workspace's current one.</param>
        /// <returns>The summary text, or an empty string if the solution holds no projects.</returns>
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
