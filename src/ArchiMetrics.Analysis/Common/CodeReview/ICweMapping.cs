namespace ArchiMetrics.Analysis.Common.CodeReview
{
    using System.Collections.Generic;

    /// <summary>
    /// Optional companion interface for <see cref="IEvaluation"/> implementations
    /// that map to one or more CWE (Common Weakness Enumeration) identifiers.
    /// Rules that implement this interface participate in ISO/IEC 5055 reporting.
    /// Rules that do not implement it are unaffected — this is a non-breaking extension.
    /// </summary>
    public interface ICweMapping
    {
        /// <summary>
        /// The CWE identifiers this rule maps to (e.g. "CWE-476", "CWE-89").
        /// A single rule may map to multiple CWEs when it detects a pattern
        /// that constitutes more than one recognized weakness.
        /// </summary>
        IReadOnlyList<string> CweIds { get; }

        /// <summary>
        /// The ISO/IEC 5055 quality category (or categories) this rule contributes to.
        /// Uses flags so a single rule can span multiple categories when a weakness
        /// affects more than one quality characteristic.
        /// </summary>
        Iso5055Category Iso5055Category { get; }
    }
}
