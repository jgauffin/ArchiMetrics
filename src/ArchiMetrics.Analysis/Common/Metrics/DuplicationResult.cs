namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    public sealed class DuplicationResult
    {
        public DuplicationResult(IReadOnlyList<CloneClass> clones)
        {
            Clones = clones;
        }

        public IReadOnlyList<CloneClass> Clones { get; }
    }
}
