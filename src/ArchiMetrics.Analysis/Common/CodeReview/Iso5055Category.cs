namespace ArchiMetrics.Analysis.Common.CodeReview
{
    using System;

    /// <summary>
    /// The four quality characteristic categories defined by ISO/IEC 5055.
    /// Each category maps to a set of CWE (Common Weakness Enumeration) patterns.
    /// A rule can belong to multiple categories when a single weakness affects
    /// more than one quality characteristic.
    /// </summary>
    [Flags]
    public enum Iso5055Category
    {
        /// <summary>
        /// Weaknesses that create exploitable vulnerabilities, such as
        /// injection flaws, broken cryptography, or hard-coded credentials.
        /// ISO 5055 requires zero critical violations in this category.
        /// </summary>
        Security = 1,

        /// <summary>
        /// Weaknesses that cause crashes, data corruption, or unpredictable
        /// behaviour, such as null-pointer dereferences, resource leaks, or
        /// unhandled error conditions.
        /// ISO 5055 requires zero critical violations in this category.
        /// </summary>
        Reliability = 2,

        /// <summary>
        /// Weaknesses that waste CPU, memory, or I/O, such as expensive
        /// operations inside loops or synchronous waits on async code.
        /// </summary>
        PerformanceEfficiency = 4,

        /// <summary>
        /// Weaknesses that make code harder to understand and change, such
        /// as high cyclomatic complexity, deep nesting, code duplication,
        /// or tight coupling.
        /// </summary>
        Maintainability = 8
    }
}
