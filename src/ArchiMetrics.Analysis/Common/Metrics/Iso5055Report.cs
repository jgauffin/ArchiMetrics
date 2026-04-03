namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;
    using CodeReview;

    /// <summary>
    /// An ISO/IEC 5055-aligned report that counts CWE-mapped code review violations
    /// across the four quality categories (Security, Reliability, Performance Efficiency,
    /// Maintainability). Violations are also normalised as density per thousand lines of
    /// code so that reports are comparable across projects of different sizes.
    ///
    /// This is a partial report — it only includes violations that ArchiMetrics rules
    /// can detect via Roslyn static analysis. It does not replace dedicated SAST tools
    /// for taint-analysis-dependent CWEs (e.g. SQL injection, XSS).
    /// </summary>
    public class Iso5055Report
    {
        public Iso5055Report(
            int totalLinesOfCode,
            Iso5055CategoryResult security,
            Iso5055CategoryResult reliability,
            Iso5055CategoryResult performanceEfficiency,
            Iso5055CategoryResult maintainability,
            IReadOnlyList<EvaluationResult> allViolations,
            IReadOnlyList<string> coveredCweIds)
        {
            TotalLinesOfCode = totalLinesOfCode;
            Security = security;
            Reliability = reliability;
            PerformanceEfficiency = performanceEfficiency;
            Maintainability = maintainability;
            AllViolations = allViolations;
            CoveredCweIds = coveredCweIds;
        }

        /// <summary>
        /// Total lines of code across all analysed projects, used as the
        /// denominator for violations/KLOC density calculations.
        /// </summary>
        public int TotalLinesOfCode { get; }

        public Iso5055CategoryResult Security { get; }

        public Iso5055CategoryResult Reliability { get; }

        public Iso5055CategoryResult PerformanceEfficiency { get; }

        public Iso5055CategoryResult Maintainability { get; }

        /// <summary>
        /// Every CWE-mapped violation found during the scan, across all categories.
        /// A violation may appear in multiple categories if the originating rule
        /// maps to more than one <see cref="Iso5055Category"/>.
        /// </summary>
        public IReadOnlyList<EvaluationResult> AllViolations { get; }

        /// <summary>
        /// The set of CWE identifiers that the currently loaded rules can detect.
        /// Consumers should check this to understand the report's coverage scope.
        /// </summary>
        public IReadOnlyList<string> CoveredCweIds { get; }
    }

    /// <summary>
    /// Per-category breakdown within an ISO/IEC 5055 report.
    /// </summary>
    public class Iso5055CategoryResult
    {
        public Iso5055CategoryResult(
            Iso5055Category category,
            int violationCount,
            int criticalViolationCount,
            double violationsPerKloc,
            IReadOnlyList<EvaluationResult> violations)
        {
            Category = category;
            ViolationCount = violationCount;
            CriticalViolationCount = criticalViolationCount;
            ViolationsPerKloc = violationsPerKloc;
            Violations = violations;
        }

        public Iso5055Category Category { get; }

        /// <summary>
        /// Total number of violations in this category.
        /// </summary>
        public int ViolationCount { get; }

        /// <summary>
        /// Number of critical violations — those with <see cref="CodeQuality.Broken"/>
        /// or <see cref="CodeQuality.NeedsReEngineering"/> severity. ISO 5055 requires
        /// zero critical violations for Security and Reliability to "pass".
        /// </summary>
        public int CriticalViolationCount { get; }

        /// <summary>
        /// Violation density: violations per 1000 lines of code.
        /// </summary>
        public double ViolationsPerKloc { get; }

        /// <summary>
        /// Whether this category passes the ISO 5055 threshold of zero critical violations.
        /// Only meaningful for Security and Reliability; always true for the other categories
        /// since the standard does not define a hard threshold for them.
        /// </summary>
        public bool Passes =>
            Category != Iso5055Category.Security && Category != Iso5055Category.Reliability
            || CriticalViolationCount == 0;

        public IReadOnlyList<EvaluationResult> Violations { get; }
    }
}
