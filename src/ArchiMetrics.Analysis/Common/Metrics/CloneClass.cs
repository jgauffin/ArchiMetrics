namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    public sealed class CloneClass
    {
        public CloneClass(CloneType cloneType, IReadOnlyList<CloneInstance> instances, double similarity)
        {
            CloneType = cloneType;
            Instances = instances;
            Similarity = similarity;
        }

        public CloneType CloneType { get; }

        public IReadOnlyList<CloneInstance> Instances { get; }

        public double Similarity { get; }
    }
}
