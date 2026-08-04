namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
    /// The outcome of a duplication scan: every clone class found across the analysed code.
    /// </summary>
    public sealed class DuplicationResult
    {
        /// <summary>
        /// Initialises a duplication result.
        /// </summary>
        /// <param name="clones">The clone classes found.</param>
        public DuplicationResult(IReadOnlyList<CloneClass> clones)
        {
            Clones = clones;
        }

        /// <summary>
        /// Gets the clone classes found. Empty when nothing was duplicated — or when every candidate was
        /// smaller than the minimum token threshold the scan was given.
        /// </summary>
        public IReadOnlyList<CloneClass> Clones { get; }
    }
}
