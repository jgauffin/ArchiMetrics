namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
    /// A group of members that are all duplicates of one another.
    /// </summary>
    /// <remarks>
    /// Clones are reported as classes rather than as pairs because that is the unit of work: five copies of
    /// the same method are one problem with one fix, not ten separate findings. The number of
    /// <see cref="Instances"/> is a fair proxy for how much is gained by consolidating them.
    /// </remarks>
    public sealed class CloneClass
    {
        /// <summary>
        /// Initialises a clone class.
        /// </summary>
        /// <param name="cloneType">How alike the members are.</param>
        /// <param name="instances">The members in the group.</param>
        /// <param name="similarity">The representative similarity across the group, 0.0 to 1.0.</param>
        public CloneClass(CloneType cloneType, IReadOnlyList<CloneInstance> instances, double similarity)
        {
            CloneType = cloneType;
            Instances = instances;
            Similarity = similarity;
        }

        /// <summary>
        /// Gets how alike the members are, which decides how safely they can be merged. Exact and renamed
        /// clones usually extract cleanly; semantic ones need judgement about whether they are really the
        /// same idea.
        /// </summary>
        public CloneType CloneType { get; }

        /// <summary>
        /// Gets the members in this group. Always two or more.
        /// </summary>
        public IReadOnlyList<CloneInstance> Instances { get; }

        /// <summary>
        /// Gets the representative similarity across the group.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0.0 to 1.0, where higher means more alike.</b> A large group with a similarity close to
        /// the detection threshold is often a shared pattern rather than copied code — think a set of
        /// handlers that all follow the same shape — and consolidating it may cost more clarity than it
        /// saves.
        /// </remarks>
        public double Similarity { get; }
    }
}
