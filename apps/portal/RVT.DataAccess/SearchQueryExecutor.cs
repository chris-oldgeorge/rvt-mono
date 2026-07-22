// File summary: Shared filter/order/page execution used by the generic and time-series search repositories.
// Major updates:
// - 2026-07-14 pending Made the shared read path async and no-tracking; replaced PagedList with SQL count+skip/take.
// - 2026-07-09 pending Extracted the generic repository's ReadFiltered core so the time-series reader can reuse it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RVT.Entities;
using RVT.Entities.Querying;


namespace RVT.DataAccess
{
    // Function summary: Executes the shared filtered/ordered/paged query used across search repositories.
    internal static class SearchQueryExecutor
    {
        // Function summary: Retrieves filtered rows for a set on the supplied context without tracking them.
        internal static async Task<SearchQueryResult<TEntity>> ReadFilteredAsync<TEntity>(
            DbContext context,
            List<Filter> whereFilter,
            OrderByProperty[] orderBy,
            int maximumRecords,
            bool paged,
            int page,
            int pageSize,
            CancellationToken cancellationToken) where TEntity : class
        {
            if (maximumRecords == 0)
            {
                maximumRecords = GenericRepository<TEntity>.DAO_MAX_RECORDS;
            }

            // Search reads are projected or returned straight to callers, so change tracking is pure overhead.
            IQueryable<TEntity> query = context.Set<TEntity>().AsNoTracking();

            if (whereFilter is { Count: > 0 })
            {
                // The filter builder emits plain comparison/boolean nodes (no Expression.Invoke), so the tree
                // needs no LinqKit expansion. Dropping AsExpandable() also keeps EF's query provider in place,
                // which the async operators below require.
                Expression<Func<TEntity, bool>> predicate =
                    FilterExpression.ExpressionBuilder.GetExpression<TEntity>(whereFilter);
                query = query.Where(predicate);
            }

            query = query.OrderedBy(orderBy);

            if (paged)
            {
                var totalCount = await query.CountAsync(cancellationToken);
                var pageRows = await query
                    .Skip(Math.Max(page - 1, 0) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return new SearchQueryResult<TEntity>(true, string.Empty, pageRows, totalCount, string.Empty);
            }

            // Read one row past the bound so a capped result can be reported rather than silently truncated.
            var rows = await query.Take(maximumRecords + 1).ToListAsync(cancellationToken);
            var hasMore = rows.Count > maximumRecords;
            if (hasMore)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            return new SearchQueryResult<TEntity>(true, string.Empty, rows, rows.Count, string.Empty)
            {
                HasMore = hasMore
            };
        }
    }
}
