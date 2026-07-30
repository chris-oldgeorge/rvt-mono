// File summary: Covers the Omnidots vibration vendor gateway adapter and the command handler that consumes it.
// Major updates:
// - 2026-07-15 pending Migrated from the retired OmnidotsVibrationApiService to the IVibrationVendorGateway port/adapter.

using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Ports.Vendors;
using RvtPortal.Spa.Adapters.Vendors;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.AlertLevels;
using RvtPortal.Spa.Tests.Support;
namespace RvtPortal.Spa.Tests;

public sealed class OmnidotsVibrationGatewayTests
{
    private const string AdapterUrl = "https://adapter.rvt.test/configure";
    private const string AdapterSecret = "adapter-secret";

    // --- Adapter tests (the vendor HTTP integration; payload assertions are the regression net) ---

    [Fact]
    // Function summary: Verifies alert-level updates post the exact adapter payload to the configured URL.
    public async Task UpdateAlertLevelsAsync_PostsConfiguredPayloadAndReportsSuccess()
    {
        RecordingHttpMessageHandler handler = new(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        OmnidotsVibrationGateway gateway = new(new HttpClient(handler), CreateOptions());

        const string serialId = "SER-001";
        const double alertLevel = 12.5;
        const double cautionLevel = 7.25;

        VendorSyncResult result = await gateway.UpdateAlertLevelsAsync(serialId, alertLevel, cautionLevel, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(AdapterUrl, handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Contains($"\"secret\":\"{AdapterSecret}\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains($"\"serialid\":\"{serialId}\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains($"\"level_caution\":{cautionLevel.ToString(CultureInfo.InvariantCulture)}", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains($"\"level_alert\":{alertLevel.ToString(CultureInfo.InvariantCulture)}", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a missing adapter secret fails fast without any HTTP call.
    public async Task UpdateAlertLevelsAsync_WithoutSecret_FailsWithoutSendingRequest()
    {
        RecordingHttpMessageHandler handler = new(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        OmnidotsVibrationGateway gateway = new(
            new HttpClient(handler),
            Options.Create(new OmnidotsAdapterOptions { Url = AdapterUrl, Secret = null }));

        VendorSyncResult result = await gateway.UpdateAlertLevelsAsync("SER-001", 12.5, 7.25, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Omnidots adapter secret is not configured.", result.Error);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    // Function summary: Verifies a non-success vendor response is typed without reflecting a potentially sensitive body.
    public async Task UpdateAlertLevelsAsync_OnErrorResponse_ReturnsSafeStatusOnly()
    {
        HttpResponseMessage response = new(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new StringContent("vendor rejected the level secret=adapter-secret")
        };
        OmnidotsVibrationGateway gateway = new(new HttpClient(new RecordingHttpMessageHandler(response)), CreateOptions());

        VendorSyncResult result = await gateway.UpdateAlertLevelsAsync("SER-001", 12.5, 7.25, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Omnidots adapter returned HTTP 400.", result.Error);
    }

    [Fact]
    // Function summary: Verifies invalid endpoint configuration fails without issuing an outbound request.
    public async Task UpdateAlertLevelsAsync_WithInvalidUrl_FailsWithoutSendingRequest()
    {
        RecordingHttpMessageHandler handler = new(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        OmnidotsVibrationGateway gateway = new(
            new HttpClient(handler),
            Options.Create(new OmnidotsAdapterOptions { Url = "not a URL", Secret = AdapterSecret }));

        VendorSyncResult result = await gateway.UpdateAlertLevelsAsync("SER-001", 12.5, 7.25, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Omnidots adapter URL is invalid.", result.Error);
        Assert.Equal(0, handler.RequestCount);
    }

    // --- Handler tests (the command consuming the port; success proceeds, failure surfaces the vendor error) ---

    [Fact]
    // Function summary: Verifies a successful vendor sync lets the handler persist both vibration levels.
    public async Task Handler_OnVendorSuccess_PersistsLevelsAndReportsSync()
    {
        Guid monitorId = Guid.NewGuid();
        await using RVTDbContext context = await CreateVibrationMonitorContextAsync(monitorId);
        FakeVibrationVendorGateway gateway = new(VendorSyncResult.Success());
        UpdateVibrationAlertLevelsCommandHandler handler = new(context, gateway, ProductionEnvironment());

        VibrationAlertLevelCommandResult result = await handler.Handle(
            new UpdateVibrationAlertLevelsCommand(monitorId, new VibrationAlertLevelMutationRequest { AlertLevel = 12.5, CautionLevel = 7.25 }),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Response);
        Assert.True(result.Response.ExternalSyncAttempted);
        Assert.True(result.Response.ExternalSyncSucceeded);
        Assert.Equal(2, result.Response.AlertLevels.Count);
        // The handler queues the levels for the transaction pipeline to persist; it does not SaveChanges itself.
        Assert.Equal(2, context.ChangeTracker.Entries().Count(entry => entry.State == EntityState.Added));
        Assert.Equal("VIB-001", gateway.LastSerialId);
        Assert.Equal(12.5, gateway.LastAlertLevel);
        Assert.Equal(7.25, gateway.LastCautionLevel);
    }

    [Fact]
    // Function summary: Verifies a failed vendor sync surfaces the vendor error and writes no levels.
    public async Task Handler_OnVendorFailure_SurfacesErrorAndWritesNothing()
    {
        Guid monitorId = Guid.NewGuid();
        await using RVTDbContext context = await CreateVibrationMonitorContextAsync(monitorId);
        FakeVibrationVendorGateway gateway = new(VendorSyncResult.Failure("vendor boom"));
        UpdateVibrationAlertLevelsCommandHandler handler = new(context, gateway, ProductionEnvironment());

        VibrationAlertLevelCommandResult result = await handler.Handle(
            new UpdateVibrationAlertLevelsCommand(monitorId, new VibrationAlertLevelMutationRequest { AlertLevel = 12.5, CautionLevel = 7.25 }),
            CancellationToken.None);

        Assert.Null(result.Response);
        KeyValuePair<string, string[]> error = Assert.Single(result.Errors);
        Assert.Contains("vendor boom", string.Join(" ", error.Value), StringComparison.Ordinal);
        Assert.Equal(0, await context.RvtAlertRules.CountAsync());
    }

    // --- Helpers ---

    private static IOptions<OmnidotsAdapterOptions> CreateOptions()
    {
        return Options.Create(new OmnidotsAdapterOptions { Url = AdapterUrl, Secret = AdapterSecret });
    }

    private static async Task<RVTDbContext> CreateVibrationMonitorContextAsync(Guid monitorId)
    {
        DbContextOptions<RVTDbContext> options = TestDbContexts.InMemory<RVTDbContext>($"vibration-gateway-{Guid.NewGuid():N}");
        RVTDbContext context = new(options);
        context.MonitorsList.Add(TestData.Monitor(MonitorTypeEnum.Vibration, id: monitorId, serialId: "VIB-001"));
        await context.SaveChangesAsync();
        return context;
    }

    private static IWebHostEnvironment ProductionEnvironment()
    {
        return new StubWebHostEnvironment { EnvironmentName = "Production" };
    }

    private sealed class FakeVibrationVendorGateway : IVibrationVendorGateway
    {
        private readonly VendorSyncResult result;

        public FakeVibrationVendorGateway(VendorSyncResult result)
        {
            this.result = result;
        }

        public string? LastSerialId { get; private set; }
        public double LastAlertLevel { get; private set; }
        public double LastCautionLevel { get; private set; }

        // Function summary: Records the requested vendor sync and returns the preconfigured result.
        public Task<VendorSyncResult> UpdateAlertLevelsAsync(string serialId, double alertLevel, double cautionLevel, CancellationToken cancellationToken)
        {
            LastSerialId = serialId;
            LastAlertLevel = alertLevel;
            LastCautionLevel = cautionLevel;
            return Task.FromResult(result);
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "RvtPortal.Spa.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        public int RequestCount { get; private set; }

        // Function summary: Initializes this type with the response returned to callers.
        public RecordingHttpMessageHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        // Function summary: Records outbound HTTP request details for assertions.
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            LastBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }
}
