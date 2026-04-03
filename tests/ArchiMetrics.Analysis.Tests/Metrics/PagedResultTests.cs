namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Collections.Generic;
    using System.Linq;
    using ArchiMetrics.Analysis.Common.Metrics;
    using Xunit;

    public sealed class PagedResultTests
    {
        public class GivenPagedResultCreate
        {
            [Fact]
            public void WhenSkipAndTakeThenReturnsCorrectSlice()
            {
                var items = Enumerable.Range(0, 10).ToList();

                var result = PagedResult<int>.Create(items, skip: 2, take: 3);

                Assert.Equal(3, result.Items.Count);
                Assert.Equal(2, result.Items[0]);
                Assert.Equal(3, result.Items[1]);
                Assert.Equal(4, result.Items[2]);
            }

            [Fact]
            public void WhenSkipAndTakeThenTotalCountIsFullListSize()
            {
                var items = Enumerable.Range(0, 10).ToList();

                var result = PagedResult<int>.Create(items, skip: 2, take: 3);

                Assert.Equal(10, result.TotalCount);
            }

            [Fact]
            public void WhenTakeIsZeroThenUsesMaxPageSize()
            {
                var items = Enumerable.Range(0, 10).ToList();

                var result = PagedResult<int>.Create(items, skip: 0, take: 0);

                Assert.Equal(10, result.Items.Count);
            }

            [Fact]
            public void WhenTakeExceedsMaxPageSizeThenClamps()
            {
                var items = Enumerable.Range(0, 300).ToList();

                var result = PagedResult<int>.Create(items, skip: 0, take: 500);

                Assert.Equal(PagedResult<int>.MaxPageSize, result.Items.Count);
            }

            [Fact]
            public void WhenSkipBeyondEndThenReturnsEmpty()
            {
                var items = Enumerable.Range(0, 5).ToList();

                var result = PagedResult<int>.Create(items, skip: 10, take: 5);

                Assert.Empty(result.Items);
                Assert.Equal(5, result.TotalCount);
            }

            [Fact]
            public void WhenNoSkipAndSmallTakeThenReturnsFirstItems()
            {
                var items = Enumerable.Range(0, 100).ToList();

                var result = PagedResult<int>.Create(items, skip: 0, take: 5);

                Assert.Equal(5, result.Items.Count);
                Assert.Equal(0, result.Items[0]);
                Assert.Equal(4, result.Items[4]);
            }

            [Fact]
            public void WhenTakeExceedsRemainingThenReturnsAvailable()
            {
                var items = Enumerable.Range(0, 10).ToList();

                var result = PagedResult<int>.Create(items, skip: 8, take: 5);

                Assert.Equal(2, result.Items.Count);
                Assert.Equal(8, result.Items[0]);
                Assert.Equal(9, result.Items[1]);
            }

            [Fact]
            public void WhenEmptyListThenReturnsEmptyWithZeroTotal()
            {
                var items = new List<int>();

                var result = PagedResult<int>.Create(items, skip: 0, take: 10);

                Assert.Empty(result.Items);
                Assert.Equal(0, result.TotalCount);
            }
        }
    }
}
