namespace ArchiMetrics.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Common.Metrics;
    using Microsoft.CodeAnalysis;

    public class WorkspaceMetricsSummary
    {
        private readonly IProjectMetricsCalculator _calculator;

        public WorkspaceMetricsSummary()
            : this(new ProjectMetricsCalculator(new CodeMetricsCalculator()))
        {
        }

        public WorkspaceMetricsSummary(IProjectMetricsCalculator calculator)
        {
            _calculator = calculator;
        }

        public Task<string> GenerateSummary(Workspace workspace)
        {
            return GenerateSummary(workspace.CurrentSolution);
        }

        public async Task<string> GenerateSummary(Solution solution)
        {
            var projectMetrics = await _calculator.Calculate(solution).ConfigureAwait(false);

            var sb = new StringBuilder();

            foreach (var project in projectMetrics.OrderBy(p => p.Name))
            {
                var projectScore = GetWorstChildScore(project.NamespaceMetrics);
                sb.AppendLine($"- {project.Name} [{projectScore} - {ScoreLabel(projectScore)}]");
                sb.AppendLine($"  Maintainability: {project.MaintainabilityIndex:F0}, Complexity: {project.CyclomaticComplexity}, Lines: {project.LinesOfCode}");

                foreach (var ns in project.NamespaceMetrics.OrderBy(n => n.Name))
                {
                    var nsScore = GetWorstTypeScore(ns.TypeMetrics);
                    sb.AppendLine();
                    sb.AppendLine($"  - {ns.Name} [{nsScore} - {ScoreLabel(nsScore)}]");
                    sb.AppendLine($"    Maintainability: {ns.MaintainabilityIndex:F0}, Complexity: {ns.CyclomaticComplexity}, Lines: {ns.LinesOfCode}");

                    foreach (var type in ns.TypeMetrics.OrderByDescending(t => ScoreType(t)))
                    {
                        var score = ScoreType(type);
                        sb.AppendLine();
                        sb.AppendLine($"    - {type.Name} [{score} - {ScoreLabel(score)}]");
                        sb.AppendLine($"      Maintainability: {type.MaintainabilityIndex:F0}, Complexity: {type.CyclomaticComplexity}, Inheritance Depth: {type.DepthOfInheritance}");
                        sb.AppendLine($"      Afferent Coupling: {type.AfferentCoupling}, Efferent Coupling: {type.EfferentCoupling}, Instability: {type.Instability:F2}");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string ScoreLabel(int score)
        {
            switch (score)
            {
                case 1: return "Healthy";
                case 2: return "Acceptable";
                case 3: return "Concerning";
                case 4: return "Problematic";
                default: return "Fix ASAP";
            }
        }

        private static int GetWorstChildScore(IEnumerable<INamespaceMetric> namespaces)
        {
            var scores = namespaces.Select(ns => GetWorstTypeScore(ns.TypeMetrics));
            return scores.Any() ? scores.Max() : 1;
        }

        private static int GetWorstTypeScore(IEnumerable<ITypeMetric> types)
        {
            var scores = types.Select(ScoreType);
            return scores.Any() ? scores.Max() : 1;
        }

        private static int ScoreType(ITypeMetric type)
        {
            return new[]
            {
                ScoreMaintainability(type.MaintainabilityIndex),
                ScoreComplexity(type.CyclomaticComplexity),
                ScoreInheritance(type.DepthOfInheritance),
                ScoreCoupling(type.EfferentCoupling)
            }.Max();
        }

        private static int ScoreMaintainability(double mi)
        {
            if (mi >= 70) return 1;
            if (mi >= 50) return 2;
            if (mi >= 30) return 3;
            if (mi >= 15) return 4;
            return 5;
        }

        private static int ScoreComplexity(int cc)
        {
            if (cc <= 10) return 1;
            if (cc <= 20) return 2;
            if (cc <= 30) return 3;
            if (cc <= 50) return 4;
            return 5;
        }

        private static int ScoreInheritance(int doi)
        {
            if (doi <= 3) return 1;
            if (doi <= 5) return 2;
            if (doi <= 6) return 3;
            if (doi <= 8) return 4;
            return 5;
        }

        private static int ScoreCoupling(int ce)
        {
            if (ce <= 10) return 1;
            if (ce <= 20) return 2;
            if (ce <= 30) return 3;
            if (ce <= 40) return 4;
            return 5;
        }
    }
}
