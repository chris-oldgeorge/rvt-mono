// File summary: Provides one generic filter/order/page reader for the monitor time-series search tables, replacing the per-table repositories.
// Major updates:
// - 2026-07-09 pending Consolidated the twelve duplicated time-series repositories behind a single generic reader.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Ports.Persistence;
using RVT.Entities.Querying;

namespace RVT.DataAccess
{
    // Function summary: EF Core adapter that implements the core's time-series persistence port over the search context.
    public sealed class SearchQueryReader : ISearchQueryReader
    {
        private readonly RVTSearchContext context;

        // Function summary: Initializes the reader with the search context that owns the time-series tables.
        public SearchQueryReader(RVTSearchContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Function summary: Retrieves filtered rows from the TSource table and projects them to TResult.
        public async Task<SearchQueryResult<TResult>> ReadFilteredAsync<TSource, TResult>(
            List<Filter> whereFilter,
            OrderByProperty[] orderBy,
            int maximumRecords,
            Paging pagedata,
            Func<TSource, TResult> map,
            CancellationToken cancellationToken = default) where TSource : class
        {
            SearchQueryResult<TSource> source = await SearchQueryExecutor.ReadFilteredAsync<TSource>(
                context, whereFilter, orderBy, maximumRecords,
                pagedata.paged, pagedata.page, pagedata.pageSize, cancellationToken);

            List<TResult> records = source.Value.Select(map).ToList();
            return new SearchQueryResult<TResult>(source.WasSuccessful, source.ErrorMessage, records, source.RecordCount, string.Empty)
            {
                HasMore = source.HasMore
            };
        }
    }
}
