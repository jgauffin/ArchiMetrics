namespace ArchiMetrics.Analysis
{
    using System.Collections.Generic;
    using System.Linq;
    using Common.CodeReview;
    using Common.Metrics;

    /// <summary>
    /// Aggregates CWE-mapped <see cref="EvaluationResult"/> violations and LOC metrics
    /// into an <see cref="Iso5055Report"/>. This class performs pure data transformation —
    /// it does not run any analysis itself. The <see cref="CodeAnalysisAgent"/> orchestrates
    /// the analysis pipeline and passes results here for reporting.
    /// </summary>
    public static class Iso5055ReportGenerator
    {
        /// <summary>
        /// Builds an ISO/IEC 5055-aligned report from pre-computed evaluation results
        /// and namespace metrics.
        /// </summary>
        /// <param name="evaluations">All evaluation results from <see cref="NodeReviewer"/>.</param>
        /// <param name="namespaceMetrics">Namespace metrics providing LOC totals.</param>
        /// <param name="rules">The set of rules that were loaded, used to report CWE coverage.</param>
        public static Iso5055Report Generate(
            IEnumerable<EvaluationResult> evaluations,
            IEnumerable<INamespaceMetric> namespaceMetrics,
            IEnumerable<IEvaluation> rules)
        {
            var totalLoc = namespaceMetrics.Sum(n => n.LinesOfCode);

            // Only violations that have a CWE mapping participate in the ISO 5055 report.
            var cweViolations = evaluations
                .Where(e => e.CweIds != null && e.CweIds.Count > 0)
                .ToList();

            var coveredCweIds = rules
                .OfType<ICweMapping>()
                .SelectMany(r => r.CweIds)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var security = BuildCategoryResult(Iso5055Category.Security, cweViolations, totalLoc);
            var reliability = BuildCategoryResult(Iso5055Category.Reliability, cweViolations, totalLoc);
            var performance = BuildCategoryResult(Iso5055Category.PerformanceEfficiency, cweViolations, totalLoc);
            var maintainability = BuildCategoryResult(Iso5055Category.Maintainability, cweViolations, totalLoc);

            return new Iso5055Report(
                totalLoc,
                security,
                reliability,
                performance,
                maintainability,
                cweViolations,
                coveredCweIds);
        }

        private static Iso5055CategoryResult BuildCategoryResult(
            Iso5055Category category,
            IReadOnlyList<EvaluationResult> allCweViolations,
            int totalLoc)
        {
            var violations = allCweViolations
                .Where(v => v.Iso5055Category.HasValue && v.Iso5055Category.Value.HasFlag(category))
                .ToList();

            var criticalCount = violations
                .Count(v => v.Quality <= CodeQuality.NeedsReEngineering);

            var density = totalLoc > 0
                ? violations.Count / (totalLoc / 1000.0)
                : 0.0;

            return new Iso5055CategoryResult(
                category,
                violations.Count,
                criticalCount,
                density,
                violations);
        }
    }
}
