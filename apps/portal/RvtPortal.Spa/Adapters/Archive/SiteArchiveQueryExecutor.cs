// File summary: Executes canonical PostgreSQL site archive queries using parameterized EF Core raw SQL.
// Major updates:
// - 2026-07-25 pending Removed provider metadata and always creates Npgsql archive parameters.
// - 2026-07-09 pending Added streaming query execution and provider-specific site-id parameters for archive exports.

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Adapters.Archive;

internal interface ISiteArchiveQueryExecutor
{
    // Function summary: Streams archive query rows without materializing the full result set.
    IAsyncEnumerable<T> StreamAsync<T>(string sql, Guid siteId, CancellationToken cancellationToken)
        where T : class;

    // Function summary: Creates an Npgsql site id parameter for archive queries.
    NpgsqlParameter CreateSiteIdParameter(Guid siteId);
}

internal sealed class SiteArchiveQueryExecutor : ISiteArchiveQueryExecutor
{
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes canonical PostgreSQL archive query execution with the domain context.
    public SiteArchiveQueryExecutor(RVTDbContext domainContext)
    {
        this.domainContext = domainContext;
    }

    /// <summary>
    /// Streams archive query rows through EF Core raw SQL using a parameterized site id.
    /// <para>
    /// Written as an iterator so the token it is handed actually reaches the reader. It used to be returned
    /// unused: both callers happen to apply <c>.WithCancellation</c> themselves, so nothing was broken, but a
    /// third caller would have had a silently uncancellable export.
    /// </para>
    /// </summary>
    public async IAsyncEnumerable<T> StreamAsync<T>(
        string sql,
        Guid siteId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : class
    {
        IAsyncEnumerable<T> rows = domainContext.Database
            .SqlQueryRaw<T>(sql, CreateSiteIdParameter(siteId))
            .AsAsyncEnumerable();
        await foreach (T row in rows.WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }

    // Function summary: Creates an Npgsql site id parameter for EF Core raw SQL execution.
    public NpgsqlParameter CreateSiteIdParameter(Guid siteId)
    {
        return new NpgsqlParameter("@SiteId", siteId);
    }
}
