namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;
    using System.Linq;

    public class PagedResult<T>
    {
        public PagedResult(IReadOnlyList<T> items, int totalCount)
        {
            Items = items;
            TotalCount = totalCount;
        }

        public IReadOnlyList<T> Items { get; }

        public int TotalCount { get; }

        public static PagedResult<T> Create(IReadOnlyList<T> sorted, int skip, int take)
        {
            var totalCount = sorted.Count;
            IEnumerable<T> query = sorted.Skip(skip);
            if (take > 0)
            {
                query = query.Take(take);
            }

            return new PagedResult<T>(query.ToList(), totalCount);
        }
    }
}
