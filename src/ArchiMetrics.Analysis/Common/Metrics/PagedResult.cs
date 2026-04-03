namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System;
    using System.Collections.Generic;

    public class PagedResult<T>
    {
        public PagedResult(IReadOnlyList<T> items, int totalCount)
        {
            Items = items;
            TotalCount = totalCount;
        }

        public IReadOnlyList<T> Items { get; }

        public int TotalCount { get; }

        /// <summary>
        /// Maximum number of items any single page can return.
        /// Keeps callers from requesting unbounded result sets
        /// (e.g. take: 10 000) that bloat memory and serialisation time.
        /// </summary>
        public const int MaxPageSize = 200;

        public static PagedResult<T> Create(IReadOnlyList<T> sorted, int skip, int take)
        {
            take = take > 0 ? Math.Min(take, MaxPageSize) : MaxPageSize;

            var totalCount = sorted.Count;
            var actualSkip = Math.Min(skip, totalCount);
            var actualTake = Math.Min(take, totalCount - actualSkip);

            var items = new List<T>(actualTake);
            for (var i = actualSkip; i < actualSkip + actualTake; i++)
            {
                items.Add(sorted[i]);
            }

            return new PagedResult<T>(items, totalCount);
        }
    }
}
