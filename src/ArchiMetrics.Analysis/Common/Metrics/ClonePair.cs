namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// Two members found to be duplicates of each other.
    /// </summary>
    /// <remarks>
    /// Pairs are the raw output of detection; they are then grouped into <see cref="CloneClass"/> clusters
    /// so that five copies of the same method are reported once rather than as ten pairs.
    /// </remarks>
    public sealed class ClonePair
    {
        /// <summary>
        /// Initialises a clone pair.
        /// </summary>
        /// <param name="left">The first member.</param>
        /// <param name="right">The second member.</param>
        /// <param name="cloneType">How alike the two are.</param>
        /// <param name="similarity">How strongly they matched, 0.0 to 1.0.</param>
        public ClonePair(CloneInstance left, CloneInstance right, CloneType cloneType, double similarity)
        {
            Left = left;
            Right = right;
            CloneType = cloneType;
            Similarity = similarity;
        }

        /// <summary>
        /// Gets the first member of the pair. The order carries no meaning — the relation is symmetric.
        /// </summary>
        public CloneInstance Left { get; }

        /// <summary>
        /// Gets the second member of the pair.
        /// </summary>
        public CloneInstance Right { get; }

        /// <summary>
        /// Gets how alike the two members are, which decides how safely they can be merged.
        /// </summary>
        public CloneType CloneType { get; }

        /// <summary>
        /// Gets how strongly the two matched.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0.0 to 1.0, where higher means more alike.</b> Exact and renamed clones score 1.0 by
        /// construction; for semantic clones this is the cosine similarity of their embeddings, and a value
        /// near the detection threshold deserves a look before acting on it.
        /// </remarks>
        public double Similarity { get; }
    }
}
