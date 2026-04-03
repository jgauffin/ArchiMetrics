namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A lightweight, flat snapshot of a single method or property's health metrics.
    /// Includes the fully qualified location (namespace + type) so that an agent can
    /// identify the worst methods across the entire solution without needing the full
    /// metric tree.
    /// </summary>
    public class MemberSummary
    {
        public string NamespaceName { get; init; }
        public string TypeName { get; init; }
        public string Name { get; init; }
        public string CodeFile { get; init; }
        public int LineNumber { get; init; }
        public AccessModifierKind AccessModifier { get; init; }
        public double MaintainabilityIndex { get; init; }
        public int CyclomaticComplexity { get; init; }
        public int LinesOfCode { get; init; }
        public int ClassCoupling { get; init; }
        public int NumberOfParameters { get; init; }
        public int NumberOfLocalVariables { get; init; }
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
                ClassCoupling = member.ClassCoupling,
                NumberOfParameters = member.NumberOfParameters,
                NumberOfLocalVariables = member.NumberOfLocalVariables,
                AfferentCoupling = member.AfferentCoupling,
            };
        }
    }
}
