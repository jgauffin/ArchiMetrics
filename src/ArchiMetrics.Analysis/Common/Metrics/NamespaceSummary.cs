namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a namespace's health metrics.
    /// Unlike <see cref="INamespaceMetric"/>, this deliberately excludes the full
    /// type and member trees so that an agent can page through hundreds of namespaces
    /// without blowing up its context window.
    /// </summary>
    public class NamespaceSummary
    {
        public string Name { get; init; }
        public double MaintainabilityIndex { get; init; }
        public int CyclomaticComplexity { get; init; }
        public int LinesOfCode { get; init; }
        public int DepthOfInheritance { get; init; }
        public int ClassCoupling { get; init; }
        public double Abstractness { get; init; }
        public int TypeCount { get; init; }

        internal static NamespaceSummary From(INamespaceMetric ns)
        {
            return new NamespaceSummary
            {
                Name = ns.Name,
                MaintainabilityIndex = ns.MaintainabilityIndex,
                CyclomaticComplexity = ns.CyclomaticComplexity,
                LinesOfCode = ns.LinesOfCode,
                DepthOfInheritance = ns.DepthOfInheritance,
                ClassCoupling = ns.ClassCoupling,
                Abstractness = ns.Abstractness,
                TypeCount = System.Linq.Enumerable.Count(ns.TypeMetrics),
            };
        }
    }
}
