namespace ArchiMetrics.Analysis.Common.Metrics
{
    public sealed class ClonePair
    {
        public ClonePair(CloneInstance left, CloneInstance right, CloneType cloneType, double similarity)
        {
            Left = left;
            Right = right;
            CloneType = cloneType;
            Similarity = similarity;
        }

        public CloneInstance Left { get; }

        public CloneInstance Right { get; }

        public CloneType CloneType { get; }

        public double Similarity { get; }
    }
}
