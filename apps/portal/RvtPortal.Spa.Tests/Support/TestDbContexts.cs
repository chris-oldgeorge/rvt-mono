// File summary: The one place tests build DbContext options; 19 files hand-rolled this before.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RvtPortal.Spa.Tests.Support;

internal static class TestDbContexts
{
    /// <summary>
    /// Connection-string placeholder for model-only contexts. Tests that only
    /// inspect the EF model or generate migration SQL still need the Npgsql
    /// provider selected, but must never open a connection; a deliberately
    /// unresolvable host makes any accidental connect fail loudly.
    /// </summary>
    public const string ModelOnlyNpgsqlConnectionString =
        "Host=unused;Database=unused;Username=unused;Password=unused";

    public static DbContextOptions<TContext> Sqlite<TContext>(
        SqliteConnection connection,
        params IInterceptor[] interceptors)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlite(connection).AddInterceptors(interceptors).Options;

    public static DbContextOptions<TContext> Sqlite<TContext>(string connectionString)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlite(connectionString).Options;

    // The connection string is nullable to mirror UseNpgsql itself: integration
    // tests pass an environment variable that their fact attribute has already
    // checked for presence.
    public static DbContextOptions<TContext> Npgsql<TContext>(
        string? connectionString,
        params IInterceptor[] interceptors)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString).AddInterceptors(interceptors).Options;

    public static DbContextOptions<TContext> Npgsql<TContext>(DbConnection connection)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseNpgsql(connection).Options;

    public static DbContextOptions<TContext> ModelOnlyNpgsql<TContext>()
        where TContext : DbContext =>
        Npgsql<TContext>(ModelOnlyNpgsqlConnectionString);

    public static DbContextOptions<TContext> InMemory<TContext>(string? databaseName = null)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
}
