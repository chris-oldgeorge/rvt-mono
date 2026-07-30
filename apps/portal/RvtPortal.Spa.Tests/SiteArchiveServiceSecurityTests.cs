// File summary: Covers canonical PostgreSQL SQL and parameterization behavior for site archive exports.
// Major updates:
// - 2026-07-30 pending Replaced the timing-based no-request race with a deterministic completed-task assertion.
// - 2026-07-25 pending Replaced provider-dialect coverage with canonical PostgreSQL archive and site-write guards.
// - 2026-07-25 pending Added stable URL canonicalization and effective-port cleanup coverage.
// - 2026-07-09 pending Added guardrails for split archive SQL, unique workspaces, and streaming CSV output.
// - 2026-07-05 pending Replaced source-text archive SQL checks with reflection-backed behavior tests.
// - 2026-06-08 pending Added archive export SQL injection regression coverage.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using RVT.DataAccess.Context;
using RvtPortal.Application.Sites.Ports;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Adapters.Sites;
using RvtPortal.Spa.Adapters.Storage;

using RvtPortal.Spa.Tests.Support;
namespace RvtPortal.Spa.Tests;

public sealed class SiteArchiveServiceSecurityTests
{
    [Fact]
    public async Task SiteArchiveAdapter_MapsExportFailureWithoutSwallowingCancellation()
    {
        IOException exportFailure = new("upload failed");
        RecordingLogger<SiteArchiveAdapter> failureLogger = new();
        SiteArchiveAdapter failureAdapter = new(
            new ThrowingSiteArchiveService(exportFailure),
            failureLogger);
        Guid siteId = Guid.NewGuid();

        SiteArchiveExportResult failure = await failureAdapter.ExportAsync(
            siteId,
            CancellationToken.None);
        RecordedLogEntry entry = Assert.Single(failureLogger.Entries);

        Assert.False(failure.Succeeded);
        Assert.Null(failure.ArchiveUrl);
        Assert.Equal(
            "The site archive could not be created, so the site was not archived. Please try again.",
            failure.ErrorMessage);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exportFailure, entry.Exception);
        Assert.Contains(siteId.ToString(), entry.Message, StringComparison.Ordinal);

        SiteArchiveAdapter cancellationAdapter = new(
            new ThrowingSiteArchiveService(new OperationCanceledException()),
            new RecordingLogger<SiteArchiveAdapter>());
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cancellationAdapter.ExportAsync(
                Guid.NewGuid(),
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSupersededAsync_MatchingStableUrlDoesNotCallBlobStorage()
    {
        Guid siteId = Guid.NewGuid();
        SiteArchiveService service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site-archive.zip",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_PercentEncodedMatchingStableUrlDoesNotCallBlobStorage()
    {
        Guid siteId = Guid.NewGuid();
        SiteArchiveService service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site%2Darchive.zip",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_QueryEquivalentMatchingStableUrlDoesNotCallBlobStorage()
    {
        Guid siteId = Guid.NewGuid();
        SiteArchiveService service = CreateArchiveService(
            BlobConnectionString("http://127.0.0.1:1/archiveaccount"));
        using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(250));

        await service.DeleteSupersededAsync(
            siteId,
            $"http://127.0.0.1:1/archiveaccount/site-archives/{siteId:N}/site-archive.zip"
                + "?sv=2025-05-05&sr=b&sig=test-signature",
            timeout.Token);
    }

    [Fact]
    public async Task DeleteSupersededAsync_SameAccountAndContainerWithWrongEffectivePortFailsClosedWithoutDeleting()
    {
        using LoopbackBlobServer server = new();
        Guid siteId = Guid.NewGuid();
        SiteArchiveService service = CreateArchiveService(
            BlobConnectionString($"{server.Endpoint}/archiveaccount"));
        Uri candidateUri = new(
            $"{server.Endpoint}/archiveaccount/site-archives/{siteId:N}/site-archive.zip");
        int wrongPort = candidateUri.Port == 65535
            ? candidateUri.Port - 1
            : candidateUri.Port + 1;
        string durableArchiveUrl = new UriBuilder(candidateUri)
        {
            Port = wrongPort
        }.Uri.AbsoluteUri;

        Exception exception = await Record.ExceptionAsync(
            () => service.DeleteSupersededAsync(
                siteId,
                durableArchiveUrl,
                CancellationToken.None));

        // Deterministic negative assertion: the delete awaits any blob call before returning, so once it
        // has failed closed, a request that was never started can never arrive - no timing window needed.
        Assert.Multiple(
            () => Assert.IsType<InvalidOperationException>(exception),
            () => Assert.False(server.Request.IsCompleted, "The fail-closed path must not reach blob storage."));
    }

    [Theory]
    [InlineData("not-an-absolute-url")]
    [InlineData("file:///tmp/site-archive.zip")]
    [InlineData("https://archiveaccount.blob.core.windows.net/site-archives/legacy.zip#fragment")]
    public async Task SiteArchiveAdapter_MapsUnverifiableDurableUrlToCleanupFailure(
        string durableArchiveUrl)
    {
        RecordingLogger<SiteArchiveAdapter> logger = new();
        SiteArchiveAdapter adapter = new(
            CreateArchiveService(BlobConnectionString()),
            logger);

        SiteArchiveCleanupResult result = await adapter.CleanupSupersededAsync(
            Guid.NewGuid(),
            durableArchiveUrl,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Single(logger.Entries);
    }

    [Theory]
    [InlineData("https://otheraccount.blob.core.windows.net/site-archives/legacy.zip")]
    [InlineData("https://archiveaccount.blob.core.windows.net/other-container/legacy.zip")]
    public async Task DeleteSupersededAsync_WrongAccountOrContainerFailsClosed(
        string durableArchiveUrl)
    {
        SiteArchiveService service = CreateArchiveService(BlobConnectionString());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteSupersededAsync(
                Guid.NewGuid(),
                durableArchiveUrl,
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSupersededAsync_LegacyUrlDeletesOnlyDerivedStableCandidateWithSnapshots()
    {
        using LoopbackBlobServer server = new();
        Guid siteId = Guid.NewGuid();
        SiteArchiveService service = CreateArchiveService(
            BlobConnectionString($"{server.Endpoint}/archiveaccount"));
        string durableArchiveUrl =
            $"{server.Endpoint}/archiveaccount/site-archives/legacy/archive.zip";

        await service.DeleteSupersededAsync(
            siteId,
            durableArchiveUrl,
            CancellationToken.None);
        BlobRequest request = await server.Request;

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
        string source = File.ReadAllText(Path.Combine(
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

    [Fact]
    // Function summary: Verifies archive queries receive an Npgsql parameter instead of an interpolated site id.
    public void SiteArchiveService_CreatesNpgsqlSiteIdParameter()
    {
        Guid siteId = Guid.NewGuid();
        SiteArchiveQueryExecutor service = new(NoOpDomainContext());

        NpgsqlParameter parameter = service.CreateSiteIdParameter(siteId);

        Assert.Equal("@SiteId", parameter.ParameterName);
        Assert.Equal(siteId, parameter.Value);
        Assert.IsType<NpgsqlParameter>(parameter);
    }

    [Fact]
    // Function summary: Verifies the archive catalog supplies one canonical public-schema PostgreSQL SQL definition.
    public void SiteArchiveQueryCatalog_ProvidesCanonicalPostgresArchiveSql()
    {
        SiteArchiveQueryCatalog catalog = new();

        string sql = FirstExportSql(catalog);
        string allSql = AllExportSql(catalog);

        Assert.Contains("\"public\".\"deployment\"", sql, StringComparison.Ordinal);
        Assert.Contains("s.id = @SiteId", sql, StringComparison.Ordinal);
        Assert.Contains("now()", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"public\".\"report\"", catalog.ReportLinksSql, StringComparison.Ordinal);
        Assert.DoesNotContain("dbo", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("getdate()", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('[', allSql);
    }

    [Fact]
    // Function summary: Verifies both atomic site writes use PostgreSQL ON CONFLICT.
    public void EfSiteWriteAdapter_UsesPostgreSqlOnConflict()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "RvtPortal.Spa",
            "Adapters",
            "Sites",
            "EfSiteWriteAdapter.cs"));

        Assert.Contains("ON CONFLICT (\"site_id\") DO NOTHING", source, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (\"site_user_id\") DO UPDATE", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDLOCK", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HOLDLOCK", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@@ROWCOUNT", source, StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Builds a domain context the archive executor only needs to hold, never to query.
    private static RVTDbContext NoOpDomainContext()
    {
        return new RVTDbContext(TestDbContexts.InMemory<RVTDbContext>());
    }

    private static SiteArchiveService CreateArchiveService(string blobConnectionString)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:blobConnectionString"] = blobConnectionString,
                ["BlobStorage:ArchiveContainer"] = "site-archives"
            })
            .Build();
        return new SiteArchiveService(
            new SiteArchiveQueryCatalog(),
            new SiteArchiveQueryExecutor(NoOpDomainContext()),
            new SiteArchiveCsvWriter(),
            new SiteArchiveWorkspaceFactory(),
            new BlobStorageClientFactory(configuration),
            configuration);
    }

    private static string BlobConnectionString(
        string? blobEndpoint = null)
    {
        string endpoint = blobEndpoint is null
            ? string.Empty
            : $";BlobEndpoint={blobEndpoint}";
        return "DefaultEndpointsProtocol=https"
            + ";AccountName=archiveaccount"
            + $";AccountKey={Convert.ToBase64String(new byte[64])}"
            + ";EndpointSuffix=core.windows.net"
            + endpoint;
    }

    // Function summary: Reads the first configured CSV export SQL from the query catalog.
    private static string FirstExportSql(ISiteArchiveQueryCatalog catalog)
    {
        return catalog.CsvExports.First().Sql;
    }

    // Function summary: Reads all configured CSV export SQL from the query catalog.
    private static string AllExportSql(ISiteArchiveQueryCatalog catalog)
    {
        return string.Join(Environment.NewLine, catalog.CsvExports.Select(export => export.Sql));
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
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
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
            using TcpClient client = await listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            string requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("Blob request line was missing.");
            string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

            byte[] response = Encoding.ASCII.GetBytes(
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
