namespace ArchiMetrics.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Common;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Renders solution metrics as plain text, annotating every element with a health rating so
    /// that the output can be read without a separate rulebook.
    /// </summary>
    /// <remarks>
    /// The report opens with <see cref="MetricThresholds.DescribeScales"/> because raw metric values
    /// are ambiguous on their own — "Maintainability: 42" tells the reader nothing unless they know
    /// the scale is 0-100 and that higher is better. All band boundaries come from
    /// <see cref="MetricThresholds"/>; none are duplicated here.
    /// </remarks>
    public class WorkspaceMetricsSummary
    {
        private readonly IProjectMetricsCalculator _calculator;

        /// <summary>
        /// Initialises the summary with the default metrics calculator.
        /// </summary>
        public WorkspaceMetricsSummary()
            : this(new ProjectMetricsCalculator(new CodeMetricsCalculator()))
        {
        }

        /// <summary>
        /// Initialises the summary with a specific calculator, for callers substituting the default
        /// implementation — most often a test double.
        /// </summary>
        /// <param name="calculator">The calculator supplying the project metrics to render.</param>
        public WorkspaceMetricsSummary(IProjectMetricsCalculator calculator)
        {
            _calculator = calculator;
        }

        /// <summary>
        /// Renders the workspace's current solution as plain text.
        /// </summary>
        /// <param name="workspace">The workspace whose current solution is rendered.</param>
        /// <returns>The summary text, or an empty string if there are no projects.</returns>
        public Task<string> GenerateSummary(Workspace workspace)
        {
            return GenerateSummary(workspace.CurrentSolution);
        }

        /// <summary>
        /// Renders a solution as plain text: the scale legend, then every project, namespace and type with
        /// its metrics and health rating.
        /// </summary>
        /// <remarks>
        /// Types are listed worst first so that the part of a namespace needing attention is the part a
        /// reader meets first, rather than being buried alphabetically among healthy neighbours.
        /// </remarks>
        /// <param name="solution">The solution to render.</param>
        /// <returns>The summary text, or an empty string if the solution holds no projects.</returns>
        public async Task<string> GenerateSummary(Solution solution)
        {
            var projectMetrics = (await _calculator.Calculate(solution).ConfigureAwait(false)).AsArray();

            if (!projectMetrics.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine(MetricThresholds.DescribeScales());

            foreach (var project in projectMetrics.OrderBy(p => p.Name))
            {
                var projectScore = GetWorstChildScore(project.NamespaceMetrics);
                sb.AppendLine($"- {project.Name} [{(int)projectScore} - {MetricThresholds.Label(projectScore)}]");
                sb.AppendLine($"  Maintainability: {project.MaintainabilityIndex:F0}, Complexity: {project.CyclomaticComplexity}, Lines: {project.LinesOfCode}, Statements: {project.ExecutableStatements}");

                foreach (var ns in project.NamespaceMetrics.OrderBy(n => n.Name))
                {
                    var nsScore = GetWorstTypeScore(ns.TypeMetrics);
                    sb.AppendLine();
                    sb.AppendLine($"  - {ns.Name} [{(int)nsScore} - {MetricThresholds.Label(nsScore)}]");
                    sb.AppendLine($"    Maintainability: {ns.MaintainabilityIndex:F0}, Complexity: {ns.CyclomaticComplexity}, Lines: {ns.LinesOfCode}, Statements: {ns.ExecutableStatements}");

                    foreach (var type in ns.TypeMetrics.OrderByDescending(MetricThresholds.RateType))
                    {
                        var score = MetricThresholds.RateType(type);
                        sb.AppendLine();
                        sb.AppendLine($"    - {type.Name} [{(int)score} - {MetricThresholds.Label(score)}]");
                        sb.AppendLine($"      Maintainability: {type.MaintainabilityIndex:F0}, Complexity: {type.CyclomaticComplexity}, Inheritance Depth: {type.DepthOfInheritance}");
                        sb.AppendLine($"      Lines: {type.LinesOfCode}, Statements: {type.ExecutableStatements}");
                        // Formatted invariantly: this text is read by tools as often as by people, and on a
                        // machine with a comma decimal separator "1,00" invites a parser to read it as two
                        // fields.
                        var instability = type.Instability.ToString("F2", CultureInfo.InvariantCulture);
                        sb.AppendLine($"      Afferent Coupling: {type.AfferentCoupling}, Efferent Coupling: {type.EfferentCoupling}, Instability: {instability}");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Rolls a project up to the worst rating found anywhere beneath it, so that a single rotten
        /// type cannot be averaged away by its healthy neighbours.
        /// </summary>
        private static MetricRating GetWorstChildScore(IEnumerable<INamespaceMetric> namespaces)
        {
            return MetricThresholds.Worst(namespaces.Select(ns => GetWorstTypeScore(ns.TypeMetrics)));
        }

        /// <summary>
        /// Rolls a namespace up to the worst rating among its types.
        /// </summary>
        private static MetricRating GetWorstTypeScore(IEnumerable<ITypeMetric> types)
        {
            return MetricThresholds.Worst(types.Select(MetricThresholds.RateType));
        }
    }
}
