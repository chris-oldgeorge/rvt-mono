// File summary: Covers provider translation and parameterization behavior for site archive exports.
// Major updates:
// - 2026-07-25 pending Added stable URL canonicalization and effective-port cleanup coverage.
// - 2026-07-09 pending Added guardrails for split archive SQL, unique workspaces, and streaming CSV output.
// - 2026-07-05 pending Replaced source-text archive SQL checks with reflection-backed behavior tests.
// - 2026-06-08 pending Added archive export SQL injection regression coverage.

using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Adapters.Sites;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;

namespace RvtPortal.Spa.Tests;

public sealed class SiteArchiveServiceSecurityTests
{
    [Fact]
    public async Task SiteArchiveAdapter_MapsExportFailureWithoutSwallowingCancellation()
    {
        var failureAdapter = new SiteArchiveAdapter(
            new ThrowingSiteArchiveService(new IOException("upload failed")));

        var failure = await failureAdapter.ExportAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(failure.Succeeded);
        Assert.Null(failure.ArchiveUrl);
        Assert.Equal(
            "The site archive could not be created, so the site was not archived. Please try again.",
            failure.ErrorMessage);

        var cancellationAdapter = new SiteArchiveAdapter(
            new ThrowingSiteArchiveService(new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cancellationAdapter.ExportAsync(
                Guid.NewGuid(),
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSupersededAsync_MatchingStableUrlDoesNotCallBlobStorage()
    {
        var siteId = Guid.NewGuid();
        var service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site-archive.zip",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_PercentEncodedMatchingStableUrlDoesNotCallBlobStorage()
    {
        var siteId = Guid.NewGuid();
        var service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site%2Darchive.zip",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_QueryEquivalentMatchingStableUrlDoesNotCallBlobStorage()
    {
        var siteId = Guid.NewGuid();
        var service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site-archive.zip"
                + "?sv=2025-05-05&sr=b&sig=test-signature",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_SameAccountAndContainerWithWrongEffectivePortFailsClosedWithoutDeleting()
    {
        using var server = new LoopbackBlobServer();
        var siteId = Guid.NewGuid();
        var service = CreateArchiveService(
            BlobConnectionString($"{server.Endpoint}/archiveaccount"));
        var candidateUri = new Uri(
            $"{server.Endpoint}/archiveaccount/site-archives/{siteId:N}/site-archive.zip");
        var wrongPort = candidateUri.Port == 65535
            ? candidateUri.Port - 1
            : candidateUri.Port + 1;
        var durableArchiveUrl = new UriBuilder(candidateUri)
        {
            Port = wrongPort
        }.Uri.AbsoluteUri;

        var exception = await Record.ExceptionAsync(
            () => service.DeleteSupersededAsync(
                siteId,
                durableArchiveUrl,
                CancellationToken.None));
        var noRequestWindow = Task.Delay(TimeSpan.FromMilliseconds(250));
        var completedTask = await Task.WhenAny(server.Request, noRequestWindow);

        Assert.Multiple(
            () => Assert.IsType<InvalidOperationException>(exception),
            () => Assert.Same(noRequestWindow, completedTask));
    }

    [Theory]
    [InlineData("not-an-absolute-url")]
    [InlineData("file:///tmp/site-archive.zip")]
    [InlineData("https://archiveaccount.blob.core.windows.net/site-archives/legacy.zip#fragment")]
    public async Task SiteArchiveAdapter_MapsUnverifiableDurableUrlToCleanupFailure(
        string durableArchiveUrl)
    {
        var adapter = new SiteArchiveAdapter(
            CreateArchiveService(BlobConnectionString()));

        var result = await adapter.CleanupSupersededAsync(
            Guid.NewGuid(),
            durableArchiveUrl,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Theory]
    [InlineData("https://otheraccount.blob.core.windows.net/site-archives/legacy.zip")]
    [InlineData("https://archiveaccount.blob.core.windows.net/other-container/legacy.zip")]
    public async Task DeleteSupersededAsync_WrongAccountOrContainerFailsClosed(
        string durableArchiveUrl)
    {
        var service = CreateArchiveService(BlobConnectionString());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteSupersededAsync(
                Guid.NewGuid(),
                durableArchiveUrl,
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSupersededAsync_LegacyUrlDeletesOnlyDerivedStableCandidateWithSnapshots()
    {
        using var server = new LoopbackBlobServer();
        var siteId = Guid.NewGuid();
        var service = CreateArchiveService(
            BlobConnectionString($"{server.Endpoint}/archiveaccount"));
        var durableArchiveUrl =
            $"{server.Endpoint}/archiveaccount/site-archives/legacy/archive.zip";

        await service.DeleteSupersededAsync(
            siteId,
            durableArchiveUrl,
            CancellationToken.None);
        var request = await server.Request;

        Assert.Multiple(
            () => Assert.Equal(
                $"/archiveaccount/site-archives/{siteId:N}/site-archive.zip",
                request.Path),
            () => Assert.Equal("DELETE", request.Method),
            () => Assert.Equal("include", request.DeleteSnapshots),
            () => Assert.DoesNotContain(
                "/legacy/archive.zip",
                request.Path,
                StringComparison.Ordinal));
    }

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

    private static SiteArchiveService CreateArchiveService(string blobConnectionString)
    {
        var connectionFactory = CreateConnectionFactory(RvtDatabaseProvider.SqlServer);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:blobConnectionString"] = blobConnectionString,
                ["BlobStorage:ArchiveContainer"] = "site-archives"
            })
            .Build();
        return new SiteArchiveService(
            new SiteArchiveQueryCatalog(connectionFactory),
            new SiteArchiveQueryExecutor(NoOpDomainContext(), connectionFactory),
            new SiteArchiveCsvWriter(),
            new SiteArchiveWorkspaceFactory(),
            configuration);
    }

    private static string BlobConnectionString(
        string? blobEndpoint = null)
    {
        var endpoint = blobEndpoint is null
            ? string.Empty
            : $";BlobEndpoint={blobEndpoint}";
        return "DefaultEndpointsProtocol=https"
            + ";AccountName=archiveaccount"
            + $";AccountKey={Convert.ToBase64String(new byte[64])}"
            + ";EndpointSuffix=core.windows.net"
            + endpoint;
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

    private sealed class ThrowingSiteArchiveService(Exception exception)
        : ISiteArchiveService
    {
        public Task<string> Process(
            Guid siteId,
            CancellationToken cancellationToken) =>
            Task.FromException<string>(exception);

        public Task DeleteSupersededAsync(
            Guid siteId,
            string durableArchiveUrl,
            CancellationToken cancellationToken) =>
            Task.FromException(exception);
    }

    private sealed class LoopbackBlobServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);

        public LoopbackBlobServer()
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Endpoint = $"http://127.0.0.1:{port}";
            Request = ReceiveAsync();
        }

        public string Endpoint { get; }

        public Task<BlobRequest> Request { get; }

        public void Dispose()
        {
            listener.Stop();
        }

        private async Task<BlobRequest> ReceiveAsync()
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("Blob request line was missing.");
            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? deleteSnapshots = null;
            while (await reader.ReadLineAsync() is { Length: > 0 } header)
            {
                if (header.StartsWith(
                        "x-ms-delete-snapshots:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    deleteSnapshots = header[
                        (header.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
                }
            }

            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
            return new BlobRequest(parts[0], new Uri($"http://localhost{parts[1]}").AbsolutePath, deleteSnapshots);
        }
    }

    private sealed record BlobRequest(
        string Method,
        string Path,
        string? DeleteSnapshots);
}
