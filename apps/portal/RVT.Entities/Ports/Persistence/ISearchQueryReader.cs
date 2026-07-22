// File summary: Driven (outbound) persistence port: a generic filtered/ordered/paged search reader owned by the core shared kernel.
// Major updates:
// - 2026-07-10 pending Introduced the persistence ports seam; moved the time-series reader contract out of the EF adapter into the core.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVT.Entities.Querying;

namespace RVT.Entities.Ports.Persistence
{
    // Function summary: Reads a time-series search table through the shared query path and projects each row into the caller's shape.
    public interface ISearchQueryReader
    {
        Task<SearchQueryResult<TResult>> ReadFilteredAsync<TSource, TResult>(
            List<Filter> whereFilter,
            OrderByProperty[] orderBy,
            int maximumRecords,
            Paging pagedata,
            Func<TSource, TResult> map,
            CancellationToken cancellationToken = default) where TSource : class;
    }
}
