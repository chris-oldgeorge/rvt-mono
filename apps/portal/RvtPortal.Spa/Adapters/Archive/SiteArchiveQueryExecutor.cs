// File summary: Executes provider-specific site archive queries using parameterized EF Core raw SQL.
// Major updates:
// - 2026-07-09 pending Added streaming query execution and provider-specific site-id parameters for archive exports.

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Adapters.Archive;

internal interface ISiteArchiveQueryExecutor
{
    // Function summary: Streams archive query rows without materializing the full result set.
    IAsyncEnumerable<T> StreamAsync<T>(string sql, Guid siteId, CancellationToken cancellationToken)
        where T : class;

    // Function summary: Creates a provider-specific site id parameter for archive queries.
    DbParameter CreateSiteIdParameter(Guid siteId);
}

internal sealed class SiteArchiveQueryExecutor : ISiteArchiveQueryExecutor
{
    private readonly RVTDbContext domainContext;
    private readonly IRvtDatabaseConnectionFactory connectionFactory;

    // Function summary: Initializes streaming archive query execution with the domain context and provider metadata.
    public SiteArchiveQueryExecutor(RVTDbContext domainContext, IRvtDatabaseConnectionFactory connectionFactory)
    {
        this.domainContext = domainContext;
        this.connectionFactory = connectionFactory;
    }

    // Function summary: Streams archive query rows through EF Core raw SQL using a parameterized site id.
    public IAsyncEnumerable<T> StreamAsync<T>(string sql, Guid siteId, CancellationToken cancellationToken)
        where T : class
    {
        return domainContext.Database
            .SqlQueryRaw<T>(sql, CreateSiteIdParameter(siteId))
            .AsAsyncEnumerable();
    }

    // Function summary: Creates a provider-specific site id parameter for EF Core raw SQL execution.
    public DbParameter CreateSiteIdParameter(Guid siteId)
    {
        return connectionFactory.Provider switch
        {
            RvtDatabaseProvider.SqlServer => new SqlParameter("@SiteId", siteId),
            RvtDatabaseProvider.Postgres => new NpgsqlParameter("@SiteId", siteId),
            _ => throw new InvalidOperationException($"Unsupported database provider '{connectionFactory.Provider}'.")
        };
    }
}
