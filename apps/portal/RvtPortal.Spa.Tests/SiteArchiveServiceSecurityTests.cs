// File summary: Covers provider translation and parameterization behavior for site archive exports.
// Major updates:
// - 2026-07-09 pending Added guardrails for split archive SQL, unique workspaces, and streaming CSV output.
// - 2026-07-05 pending Replaced source-text archive SQL checks with reflection-backed behavior tests.
// - 2026-06-08 pending Added archive export SQL injection regression coverage.

using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using RvtPortal.Spa.Adapters.Archive;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Tests;

public sealed class SiteArchiveServiceSecurityTests
{
    [Fact]
    // Function summary: Verifies the archive orchestrator delegates SQL, temp-workspace, and streamed CSV concerns to dedicated components.
    public void SiteArchiveService_DoesNotOwnSqlTranslationFixedWorkspaceOrCsvMaterialization()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "RvtPortal.Spa",
            "Adapters",
            "Archive",
            "SiteArchiveService.cs"));

        Assert.DoesNotContain("Regex", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SiteArchiveFiles", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlQueryRaw", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToListAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StringBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new BlobServiceClient", source, StringComparison.Ordinal);
        Assert.Contains("IBlobStorageClientFactory", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RvtDatabaseProvider.SqlServer, "Microsoft.Data.SqlClient.SqlParameter")]
    [InlineData(RvtDatabaseProvider.Postgres, "Npgsql.NpgsqlParameter")]
    // Function summary: Verifies archive queries receive a provider-specific parameter instead of an interpolated site id.
    public void SiteArchiveService_CreatesProviderSpecificSiteIdParameter(
        RvtDatabaseProvider provider,
        string expectedParameterType)
    {
        var siteId = Guid.NewGuid();
        var service = CreateQueryExecutor(provider);

        var parameter = InvokePrivate<DbParameter>(service, "CreateSiteIdParameter", siteId);

        Assert.Equal("@SiteId", parameter.ParameterName);
        Assert.Equal(siteId, parameter.Value);
        Assert.Equal(expectedParameterType, parameter.GetType().FullName);
    }

    [Fact]
    // Function summary: Verifies SQL Server archive SQL is supplied by the query catalog without PostgreSQL syntax.
    public void SiteArchiveQueryCatalog_ProvidesSqlServerArchiveSql()
    {
        var catalog = CreateQueryCatalog(RvtDatabaseProvider.SqlServer);

        var sql = FirstExportSql(catalog);

        Assert.Contains("dbo.deployment", sql, StringComparison.Ordinal);
        Assert.Contains("s.id = @SiteId", sql, StringComparison.Ordinal);
        Assert.Contains("getdate()", AllExportSql(catalog), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public.deployment", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("now()", AllExportSql(catalog), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Verifies PostgreSQL archive SQL is supplied by the query catalog without regex translation.
    public void SiteArchiveQueryCatalog_ProvidesPostgresArchiveSql()
    {
        var catalog = CreateQueryCatalog(RvtDatabaseProvider.Postgres);

        var sql = FirstExportSql(catalog);
        var allSql = AllExportSql(catalog);

        Assert.Contains("public.deployment", sql, StringComparison.Ordinal);
        Assert.Contains("s.id = @SiteId", sql, StringComparison.Ordinal);
        Assert.Contains("now()", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[dbo]", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("getdate()", allSql, StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Creates the internal archive query executor with a provider-specific connection factory.
    private static object CreateQueryExecutor(RvtDatabaseProvider provider)
    {
        var serviceType = ArchiveType("RvtPortal.Spa.Adapters.Archive.SiteArchiveQueryExecutor");
        var factory = CreateConnectionFactory(provider);

        return Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [NoOpDomainContext(), factory],
            culture: null) ?? throw new InvalidOperationException("Could not create SiteArchiveQueryExecutor.");
    }

    // Function summary: Builds a domain context the archive executor only needs to hold, never to query.
    private static RVTDbContext NoOpDomainContext()
    {
        return new RVTDbContext(new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    // Function summary: Creates the internal archive query catalog with a provider-specific connection factory.
    private static object CreateQueryCatalog(RvtDatabaseProvider provider)
    {
        var serviceType = ArchiveType("RvtPortal.Spa.Adapters.Archive.SiteArchiveQueryCatalog");
        var factory = CreateConnectionFactory(provider);

        return Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [factory],
            culture: null) ?? throw new InvalidOperationException("Could not create SiteArchiveQueryCatalog.");
    }

    // Function summary: Resolves an internal archive type from the business assembly.
    private static Type ArchiveType(string typeName)
    {
        return typeof(BlobStorage).Assembly.GetType(
            typeName,
            throwOnError: true) ?? throw new InvalidOperationException("SiteArchiveService type not found.");
    }

    // Function summary: Creates a provider-specific database connection factory for archive component tests.
    private static RvtDatabaseConnectionFactory CreateConnectionFactory(RvtDatabaseProvider provider)
    {
        var connectionString = provider == RvtDatabaseProvider.SqlServer
            ? "Server=(local);Database=rvt;User Id=rvt;Password=rvt;TrustServerCertificate=True"
            : "Host=localhost;Database=rvt;Username=rvt;Password=rvt";
        return new RvtDatabaseConnectionFactory(new RvtDatabaseOptions
        {
            Provider = provider,
            ConnectionString = connectionString
        });
    }

    // Function summary: Reads the first configured CSV export SQL from the query catalog.
    private static string FirstExportSql(object catalog)
    {
        return ExportSql(catalog).First();
    }

    // Function summary: Reads all configured CSV export SQL from the query catalog.
    private static string AllExportSql(object catalog)
    {
        return string.Join(Environment.NewLine, ExportSql(catalog));
    }

    // Function summary: Enumerates SQL text from archive export descriptors.
    private static IEnumerable<string> ExportSql(object catalog)
    {
        var exports = (System.Collections.IEnumerable)(catalog.GetType().GetProperty("CsvExports")?.GetValue(catalog)
            ?? throw new InvalidOperationException("CsvExports property not found."));
        foreach (var export in exports)
        {
            yield return (string)(export.GetType().GetProperty("Sql")?.GetValue(export)
                ?? throw new InvalidOperationException("Sql property not found."));
        }
    }

    // Function summary: Invokes an archive component helper used by focused security tests.
    private static T InvokePrivate<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);

        return (T)(method.Invoke(target, args) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }
}
