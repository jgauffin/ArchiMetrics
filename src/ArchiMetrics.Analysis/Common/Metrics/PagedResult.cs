namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One page of results, together with the total that matched.
    /// </summary>
    /// <remarks>
    /// Results are paged because the primary consumers are tools and coding agents working within a limited
    /// context. <see cref="TotalCount"/> is carried alongside the items so a caller can tell "these are the
    /// 20 worst of 4,000" from "these are all of them" — a distinction that changes what the numbers mean.
    /// </remarks>
    /// <typeparam name="T">The result type being paged.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Initialises a page directly. Prefer <see cref="Create"/>, which applies the page size cap.
        /// </summary>
        /// <param name="items">The items on this page.</param>
        /// <param name="totalCount">How many items matched in total, before paging.</param>
        public PagedResult(IReadOnlyList<T> items, int totalCount)
        {
            Items = items;
            TotalCount = totalCount;
        }

        /// <summary>
        /// Gets the items on this page, in the order the query defined — normally worst first, so a caller
        /// reading only the first page still sees what matters most.
        /// </summary>
        public IReadOnlyList<T> Items { get; }

        /// <summary>
        /// Gets how many items matched in total, ignoring paging. Compare against
        /// <see cref="Items"/>.Count to tell whether more remain.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Maximum number of items any single page can return.
        /// Keeps callers from requesting unbounded result sets
        /// (e.g. take: 10 000) that bloat memory and serialisation time.
        /// </summary>
        public const int MaxPageSize = 200;

        /// <summary>
        /// Takes a page from an already-sorted list, clamping the request to what actually exists.
        /// </summary>
        /// <remarks>
        /// Out-of-range values are clamped rather than rejected, so asking for a page past the end returns an
        /// empty page instead of throwing. A caller walking pages until one comes back empty is doing the
        /// natural thing and should not have to guard against overshooting by one.
        /// </remarks>
        /// <param name="sorted">The full result set, already in the order it should be presented.</param>
        /// <param name="skip">How many items to skip.</param>
        /// <param name="take">
        /// How many to return. Zero, negative or anything above <see cref="MaxPageSize"/> yields
        /// <see cref="MaxPageSize"/>.
        /// </param>
        /// <returns>The requested page, carrying the full count of <paramref name="sorted"/>.</returns>
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
