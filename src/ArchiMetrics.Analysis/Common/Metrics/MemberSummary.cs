namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a single method or property's health metrics.
    /// Includes the fully qualified location (namespace + type) so that an agent can
    /// identify the worst methods across the entire solution without needing the full
    /// metric tree.
    /// </summary>
    /// <remarks>
    /// This is the level at which the metrics are actually measured — everything above it is an aggregate —
    /// so it is the level at which acting on them makes most sense. Use <see cref="MetricThresholds"/> to
    /// classify a value rather than comparing against your own numbers.
    /// </remarks>
    public class MemberSummary
    {
        /// <summary>
        /// The namespace containing the member.
        /// </summary>
        public string NamespaceName { get; init; }

        /// <summary>
        /// The type declaring the member.
        /// </summary>
        public string TypeName { get; init; }

        /// <summary>
        /// The member's signature.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Path to the source file, relative to the root folder the agent was given.
        /// </summary>
        public string CodeFile { get; init; }

        /// <summary>
        /// The line the member is declared on, so a caller can navigate straight to it.
        /// </summary>
        public int LineNumber { get; init; }

        /// <summary>
        /// The member's declared accessibility. The same complexity matters more in a public member, which
        /// callers outside the assembly depend on and which cannot be changed freely.
        /// </summary>
        public AccessModifierKind AccessModifier { get; init; }

        /// <summary>
        /// The maintainability index, 0-100, <b>higher is better</b>. Falls as the member grows in size,
        /// branching or vocabulary. Best used to rank members against each other rather than as an absolute
        /// score. See <see cref="MetricThresholds.Maintainability"/> for the bands.
        /// </summary>
        public double MaintainabilityIndex { get; init; }

        /// <summary>
        /// The number of independent paths through the member, <b>lower is better</b>, starting at 1. This
        /// is a lower bound on the test cases needed to cover it, which is why a high value is a testability
        /// problem rather than a style complaint. See <see cref="MetricThresholds.CyclomaticComplexity"/>.
        /// </summary>
        public int CyclomaticComplexity { get; init; }

        /// <summary>
        /// Source lines the member occupies, excluding blanks, comments and its own documentation. A size
        /// measure with no good or bad value.
        /// </summary>
        public int LinesOfCode { get; init; }

        /// <summary>
        /// Executable statements in the member — size in units of work rather than text, and unaffected by
        /// formatting. A member with no body, such as an abstract or interface declaration, scores 0.
        /// </summary>
        public int ExecutableStatements { get; init; }

        /// <summary>
        /// The number of distinct types the member references, <b>lower is better</b>. A count with no upper
        /// bound; high values usually mean the member is doing several jobs at once.
        /// </summary>
        public int ClassCoupling { get; init; }

        /// <summary>
        /// The number of parameters, <b>lower is better</b>. No threshold is applied here, though the
        /// review rules flag long parameter lists separately.
        /// </summary>
        public int NumberOfParameters { get; init; }

        /// <summary>
        /// The number of local variables declared, <b>lower is better</b>. Many locals in one member is a
        /// common sign that it is holding more state in the reader's head than it should.
        /// </summary>
        public int NumberOfLocalVariables { get; init; }

        /// <summary>
        /// How many places call this member. A count, not a quality measure — a high value means changing it
        /// is risky, not that it is badly written.
        /// </summary>
        public int AfferentCoupling { get; init; }

        internal static MemberSummary From(string namespaceName, string typeName, IMemberMetric member)
        {
            return new MemberSummary
            {
                NamespaceName = namespaceName,
                TypeName = typeName,
                Name = member.Name,
                CodeFile = member.CodeFile,
                LineNumber = member.LineNumber,
                AccessModifier = member.AccessModifier,
                MaintainabilityIndex = member.MaintainabilityIndex,
                CyclomaticComplexity = member.CyclomaticComplexity,
                LinesOfCode = member.LinesOfCode,
                ExecutableStatements = member.ExecutableStatements,
                ClassCoupling = member.ClassCoupling,
                NumberOfParameters = member.NumberOfParameters,
                NumberOfLocalVariables = member.NumberOfLocalVariables,
                AfferentCoupling = member.AfferentCoupling,
            };
        }
    }
}
