namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a single type's health metrics.
    /// Excludes the member-level detail tree so that an agent can page through
    /// many types without overwhelming its context window.
    /// </summary>
    /// <remarks>
    /// Use <see cref="MetricThresholds.RateType"/> to turn these values into a single verdict. It takes the
    /// worst of the individual ratings rather than the average, because one unmaintainable method is a real
    /// cost to whoever has to change it however tidy the rest of the type is.
    /// </remarks>
    public class TypeSummary
    {
        /// <summary>
        /// The namespace containing the type.
        /// </summary>
        public string NamespaceName { get; init; }

        /// <summary>
        /// The type's name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// What kind of type this is — class, interface, struct or delegate.
        /// </summary>
        public TypeMetricKind Kind { get; init; }

        /// <summary>
        /// The type's declared accessibility. A public type's metrics carry more weight, since the outside
        /// world depends on it and it cannot be reshaped freely.
        /// </summary>
        public AccessModifierKind AccessModifier { get; init; }

        /// <summary>
        /// The maintainability index, 0-100, <b>higher is better</b>. Averaged over the type's members and
        /// weighted by executable statements. See <see cref="MetricThresholds.Maintainability"/>.
        /// </summary>
        public double MaintainabilityIndex { get; init; }

        /// <summary>
        /// Total cyclomatic complexity across the type's members, <b>lower is better</b>. A sum, so it grows
        /// with the number of members as well as with their branching. See
        /// <see cref="MetricThresholds.CyclomaticComplexity"/>.
        /// </summary>
        public int CyclomaticComplexity { get; init; }

        /// <summary>
        /// Source lines the type occupies, measured from its own declaration and so including the
        /// declaration line, its fields and the braces between members. It is therefore larger than the sum
        /// of its members' values. A size measure with no good or bad value.
        /// </summary>
        public int LinesOfCode { get; init; }

        /// <summary>
        /// Executable statements across the type's members — size in units of work, unaffected by
        /// formatting. This is what the maintainability index is built on.
        /// </summary>
        public int ExecutableStatements { get; init; }

        /// <summary>
        /// How many base types sit above this one, <b>lower is better</b>. Deep hierarchies make behaviour
        /// hard to locate, because the code that runs may be several files away from the one being read.
        /// See <see cref="MetricThresholds.DepthOfInheritance"/>.
        /// </summary>
        public int DepthOfInheritance { get; init; }

        /// <summary>
        /// The number of distinct types this type references, <b>lower is better</b>. A count with no upper
        /// bound.
        /// </summary>
        public int ClassCoupling { get; init; }

        /// <summary>
        /// How many types depend on this one. A count, not a quality measure — a high value marks a type as
        /// widely relied upon, which makes changing it risky but is not itself a fault.
        /// </summary>
        public int AfferentCoupling { get; init; }

        /// <summary>
        /// How many types this one depends on, <b>lower is better</b>. Many outgoing dependencies mean many
        /// reasons to change and a type that is hard to test in isolation. See
        /// <see cref="MetricThresholds.EfferentCoupling"/>.
        /// </summary>
        public int EfferentCoupling { get; init; }

        /// <summary>
        /// Efferent / (afferent + efferent), from 0.0 to 1.0. Neither end is good in itself. 0.0 is a stable
        /// type that many others depend on and that depends on nothing — safe to rely on, painful to
        /// change. 1.0 is unstable: it depends on much and nothing depends on it, so it is free to change.
        /// The warning sign is a type that is stable and concrete at once, since everything relies on it and
        /// nothing can be substituted for it.
        /// </summary>
        public double Instability { get; init; }

        /// <summary>
        /// Whether the type is abstract. Read alongside <see cref="Instability"/>: a type that is both
        /// stable and concrete is one that everything depends on and nothing can substitute.
        /// </summary>
        public bool IsAbstract { get; init; }

        /// <summary>
        /// The number of members on the type. A count, with no good or bad value.
        /// </summary>
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
                ExecutableStatements = type.ExecutableStatements,
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
