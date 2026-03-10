namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a single type's health metrics.
    /// Excludes the member-level detail tree so that an agent can page through
    /// many types without overwhelming its context window.
    /// </summary>
    public class TypeSummary
    {
        public string NamespaceName { get; init; }
        public string Name { get; init; }
        public TypeMetricKind Kind { get; init; }
        public AccessModifierKind AccessModifier { get; init; }
        public double MaintainabilityIndex { get; init; }
        public int CyclomaticComplexity { get; init; }
        public int LinesOfCode { get; init; }
        public int DepthOfInheritance { get; init; }
        public int ClassCoupling { get; init; }
        public int AfferentCoupling { get; init; }
        public int EfferentCoupling { get; init; }
        public double Instability { get; init; }
        public bool IsAbstract { get; init; }
        public int MemberCount { get; init; }

        internal static TypeSummary From(string namespaceName, ITypeMetric type)
        {
            return new TypeSummary
            {
                NamespaceName = namespaceName,
                Name = type.Name,
                Kind = type.Kind,
                AccessModifier = type.AccessModifier,
                MaintainabilityIndex = type.MaintainabilityIndex,
                CyclomaticComplexity = type.CyclomaticComplexity,
                LinesOfCode = type.LinesOfCode,
                DepthOfInheritance = type.DepthOfInheritance,
                ClassCoupling = type.ClassCoupling,
                AfferentCoupling = type.AfferentCoupling,
                EfferentCoupling = type.EfferentCoupling,
                Instability = type.Instability,
                IsAbstract = type.IsAbstract,
                MemberCount = System.Linq.Enumerable.Count(type.MemberMetrics),
            };
        }
    }
}
