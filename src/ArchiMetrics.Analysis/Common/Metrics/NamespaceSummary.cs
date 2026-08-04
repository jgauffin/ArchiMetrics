namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a namespace's health metrics.
    /// Unlike <see cref="INamespaceMetric"/>, this deliberately excludes the full
    /// type and member trees so that an agent can page through hundreds of namespaces
    /// without blowing up its context window.
    /// </summary>
    /// <remarks>
    /// Every value here is an aggregate of the types in the namespace, so read it as a summary of a
    /// neighbourhood rather than a verdict on any one file. Use <see cref="MetricThresholds"/> to turn a
    /// value into a rating instead of comparing against numbers of your own — that is what keeps a report
    /// and the review rules telling the same story.
    /// </remarks>
    public class NamespaceSummary
    {
        /// <summary>
        /// The fully qualified namespace name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// The maintainability index, 0-100, <b>higher is better</b>. Weighted by executable statements
        /// across the namespace's types. See <see cref="MetricThresholds.Maintainability"/> for the bands.
        /// </summary>
        public double MaintainabilityIndex { get; init; }

        /// <summary>
        /// Total cyclomatic complexity across the namespace, <b>lower is better</b>. Because this is a sum
        /// rather than an average it grows with the size of the namespace, so compare it against namespaces
        /// of similar size.
        /// </summary>
        public int CyclomaticComplexity { get; init; }

        /// <summary>
        /// Source lines carrying code, excluding blanks, comments and documentation. A size measure with no
        /// good or bad value.
        /// </summary>
        public int LinesOfCode { get; init; }

        /// <summary>
        /// Executable statements — size measured in units of work rather than in text, and unaffected by
        /// formatting. This is the figure the maintainability index is built on.
        /// </summary>
        public int ExecutableStatements { get; init; }

        /// <summary>
        /// The deepest inheritance chain among the namespace's types, <b>lower is better</b>. See
        /// <see cref="MetricThresholds.DepthOfInheritance"/> for the bands.
        /// </summary>
        public int DepthOfInheritance { get; init; }

        /// <summary>
        /// The number of distinct types the namespace depends on, <b>lower is better</b>. A count with no
        /// upper bound.
        /// </summary>
        public int ClassCoupling { get; init; }

        /// <summary>
        /// The share of the namespace's types that are abstract, from 0.0 to 1.0. Neither end is good in
        /// itself: 0.0 means nothing here can be extended without editing it, 1.0 means nothing here
        /// actually does anything. It is only meaningful when read against how much depends on the
        /// namespace — a widely used namespace benefits from being abstract, a leaf one does not.
        /// </summary>
        public double Abstractness { get; init; }

        /// <summary>
        /// The number of types in the namespace. A count, with no good or bad value.
        /// </summary>
        public int TypeCount { get; init; }

        internal static NamespaceSummary From(INamespaceMetric ns)
        {
            return new NamespaceSummary
            {
                Name = ns.Name,
                MaintainabilityIndex = ns.MaintainabilityIndex,
                CyclomaticComplexity = ns.CyclomaticComplexity,
                LinesOfCode = ns.LinesOfCode,
                ExecutableStatements = ns.ExecutableStatements,
                DepthOfInheritance = ns.DepthOfInheritance,
                ClassCoupling = ns.ClassCoupling,
                Abstractness = ns.Abstractness,
                TypeCount = System.Linq.Enumerable.Count(ns.TypeMetrics),
            };
        }
    }
}
