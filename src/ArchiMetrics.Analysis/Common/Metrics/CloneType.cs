namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// How alike the members in a clone class are, which decides how easily they can be merged.
    /// </summary>
    /// <remarks>
    /// The kinds run from most to least mechanical. <see cref="Exact"/> and <see cref="Renamed"/> clones can
    /// usually be extracted into a shared method with confidence. <see cref="Semantic"/> clones need
    /// judgement: the code only behaves alike, so merging may be the right call or may force two genuinely
    /// separate ideas into one abstraction.
    /// </remarks>
    public enum CloneType
    {
        /// <summary>Identical once formatting and comments are set aside.</summary>
        Exact,

        /// <summary>Identical in structure, differing only in the names of identifiers and literals.</summary>
        Renamed,

        /// <summary>Different in structure but doing the same work. Found by comparing embeddings.</summary>
        Semantic
    }
}
