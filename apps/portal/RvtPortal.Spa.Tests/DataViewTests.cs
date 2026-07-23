// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-26 pending Added moved-monitor trace ownership-window regressions.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.BusinessLogic;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Application.Monitors;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Ports.Persistence;
using RVT.Entities.Querying;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class DataViewTests
{
    private const string AdminEmail = "data.admin@rvt.test";
    private const string CompanyUserEmail = "data.company@rvt.test";
    private const string Password = "P8sSw0rd9$";
    private const int GridPageSize = 2;
    private const double VibrationAlertLimitOn = 5;

    [Fact]
    // Function summary: Handles the data grid and csv download return dust rows with paging and escaped csv workflow for this module.
    public async Task DataGridAndCsvDownload_ReturnDustRowsWithPagingAndEscapedCsv()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);

        var client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        var grid = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.DustDeploymentId}/grid?filterOption=60&page=1&pageSize={GridPageSize}&sort=sampleTime&sortDir=Descending");
        Assert.Equal("Dust", grid.RootElement.GetProperty("monitorType").GetString());
        Assert.Equal("60", grid.RootElement.GetProperty("filterOption").GetString());
        Assert.Equal(GridPageSize, grid.RootElement.GetProperty("rows").GetArrayLength());
        Assert.Equal("PM10", grid.RootElement.GetProperty("columns")[3].GetProperty("label").GetString());
        AssertApproximately(FakeMonitorDataSource.PeakDustPm10, grid.RootElement.GetProperty("rows")[0].GetProperty("values").GetProperty("pm10").GetDouble());
        Assert.Equal("SampleTime", dataSource.LastGridSort);
        Assert.Equal(OrderByDirectionEnum.Descending, dataSource.LastGridSortDirection);

        var download = await client.GetAsync(
            $"/api/data/deployments/{ids.DustDeploymentId}/download?filterOption=60&fromDate={ids.Today:yyyy-MM-ddTHH:mm:ss}&toDate={ids.Today.AddHours(3):yyyy-MM-ddTHH:mm:ss}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.StartsWith("text/csv", download.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Air Quality Levels at Dust Monitor DATA-DUST", download.Content.Headers.ContentDisposition?.FileNameStar);
        var csv = await download.Content.ReadAsStringAsync();
        Assert.StartsWith("Date,Pm1,Pm2.5,Pm10,PmTotal", csv);
        Assert.Contains(FakeMonitorDataSource.PeakDustPm10.ToString("0.00", CultureInfo.InvariantCulture), csv);
        Assert.Equal(4, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    // Function summary: Verifies database-style unspecified telemetry is restored to UTC before API JSON serialization.
    public async Task DataGrid_RestoresDatabaseTimestampAsUtcJson()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        dataSource.AddDustData(
            ids.DustDeploymentId,
            Monitor(Guid.NewGuid(), "DATA-DUST", "SER-DATA-D", MonitorTypeEnum.Dust, ids.Today),
            DateTime.SpecifyKind(ids.Today, DateTimeKind.Unspecified));

        var client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        var grid = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.DustDeploymentId}/grid?filterOption=60&page=1&pageSize={GridPageSize}&sort=sampleTime&sortDir=Descending");

        Assert.Equal(
            "2026-05-24T08:30:00Z",
            grid.RootElement.GetProperty("rows")[0].GetProperty("sampleTime").GetString());
    }

    [Fact]
    // Function summary: Verifies UTC application bounds keep their ticks and become unspecified only at the search database boundary.
    public async Task MonitorService_TimeSeriesBounds_AreUnspecifiedAtDatabaseBoundary()
    {
        var reader = new RecordingSearchQueryReader();
        var service = new MonitorService(null!, null!, null!, reader, null!, null!, null!, null!);
        var from = new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc);
        var to = from.AddHours(1);

        await service.GetAirQnoiseLevels("TEST-UTC-BOUND", from, to);

        var bounds = reader.LastFilters!
            .OfType<SingleFilter>()
            .Where(filter => filter.PropertyName == "SampleTime")
            .Select(filter => Assert.IsType<DateTime>(filter.Value))
            .ToArray();
        Assert.Equal([from.Ticks, to.Ticks], bounds.Select(bound => bound.Ticks));
        Assert.All(bounds, bound => Assert.Equal(DateTimeKind.Unspecified, bound.Kind));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    // Function summary: Verifies the search boundary rejects application timestamp bounds that are not UTC.
    public async Task MonitorService_TimeSeriesBounds_RejectNonUtcInputs(DateTimeKind kind)
    {
        var reader = new RecordingSearchQueryReader();
        var service = new MonitorService(null!, null!, null!, reader, null!, null!, null!, null!);
        var from = DateTime.SpecifyKind(new DateTime(2026, 7, 1, 14, 0, 0), kind);
        var to = DateTime.SpecifyKind(new DateTime(2026, 7, 1, 15, 0, 0), kind);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAirQnoiseLevels("TEST-NON-UTC-BOUND", from, to));

        Assert.Equal("value", error.ParamName);
        Assert.Contains("must be UTC", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a read that stopped at its row bound is reported as truncated, not as complete.
    public async Task CappedRead_IsReportedAsTruncated()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);

        // Re-register the dust data as a read that hit its bound and stopped short.
        var dustMonitor = Monitor(Guid.NewGuid(), "DATA-DUST", "SER-DATA-D", MonitorTypeEnum.Dust, ids.Today);
        dataSource.AddDustData(ids.DustDeploymentId, dustMonitor, ids.Today, hasMore: true);

        var client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        var grid = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.DustDeploymentId}/grid?filterOption=60&page=1&pageSize={GridPageSize}&sort=sampleTime&sortDir=Descending");
        var download = await client.GetAsync(
            $"/api/data/deployments/{ids.DustDeploymentId}/download?filterOption=60&fromDate={ids.Today:yyyy-MM-ddTHH:mm:ss}&toDate={ids.Today.AddHours(3):yyyy-MM-ddTHH:mm:ss}");

        // The capped result must not look complete: JSON carries a flag, the CSV carries a header.
        Assert.True(grid.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("true", Assert.Single(download.Headers.GetValues(DataController.TruncatedHeader)));
    }

    [Fact]
    // Function summary: Verifies a complete read is not flagged as truncated.
    public async Task CompleteRead_IsNotReportedAsTruncated()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);

        var client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        var grid = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.DustDeploymentId}/grid?filterOption=60&page=1&pageSize={GridPageSize}&sort=sampleTime&sortDir=Descending");
        var download = await client.GetAsync(
            $"/api/data/deployments/{ids.DustDeploymentId}/download?filterOption=60&fromDate={ids.Today:yyyy-MM-ddTHH:mm:ss}&toDate={ids.Today.AddHours(3):yyyy-MM-ddTHH:mm:ss}");

        Assert.False(grid.RootElement.GetProperty("truncated").GetBoolean());
        Assert.False(download.Headers.Contains(DataController.TruncatedHeader));
    }

    [Fact]
    // Function summary: Handles the graph returns vibration frequency series and alert thresholds workflow for this module.
    public async Task Graph_ReturnsVibrationFrequencySeriesAndAlertThresholds()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);

        var client = CreateClient(clientFactory);
        await LoginAsync(client, AdminEmail, Password);

        var graph = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.VibrationDeploymentId}/graph?filterOption=frequency&fromDate={ids.Today:yyyy-MM-ddTHH:mm:ss}&toDate={ids.Today.AddHours(1):yyyy-MM-ddTHH:mm:ss}");

        Assert.Equal("Vibration", graph.RootElement.GetProperty("monitorType").GetString());
        Assert.Equal("Frequency (Hz)", graph.RootElement.GetProperty("xAxisLabel").GetString());
        Assert.True(graph.RootElement.GetProperty("xAxisNumeric").GetBoolean());
        Assert.Equal(3, graph.RootElement.GetProperty("datasets").GetArrayLength());
        Assert.Equal("Xvtop", graph.RootElement.GetProperty("datasets")[0].GetProperty("label").GetString());
        AssertApproximately(FakeMonitorDataSource.PeakFrequencyHz, graph.RootElement.GetProperty("datasets")[0].GetProperty("points")[1].GetProperty("x").GetDouble());
        AssertApproximately(FakeMonitorDataSource.PeakFrequencyXVtop, graph.RootElement.GetProperty("datasets")[0].GetProperty("points")[1].GetProperty("y").GetDouble());
        var threshold = Assert.Single(graph.RootElement.GetProperty("thresholds").EnumerateArray(), threshold =>
            threshold.GetProperty("field").GetString() == "Xvtop" &&
            threshold.GetProperty("alertType").GetString() == "Alert");
        AssertApproximately(VibrationAlertLimitOn, threshold.GetProperty("limitOn").GetDouble());
    }

    [Fact]
    // Function summary: Handles the traces return scoped list detail and csv download workflow for this module.
    public async Task Traces_ReturnScopedListDetailAndCsvDownload()
    {
        var dataSource = new FakeMonitorDataSource();
        using var factory = new SpaTestApplicationFactory();
        using var clientFactory = CreateClientFactory(factory, dataSource);
        var ids = await SeedDataViewScenarioAsync(factory, dataSource);
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: ids.CompanyId);
        await AssignUserToSiteAsync(factory, companyUser.Id, ids.SiteId);

        var client = CreateClient(clientFactory);
        await LoginAsync(client, CompanyUserEmail, Password);

        var list = await GetJsonAsync(client, $"/api/data/deployments/{ids.VibrationDeploymentId}/traces");
        var detail = await GetJsonAsync(client, $"/api/data/deployments/{ids.VibrationDeploymentId}/traces/{ids.TraceId}");
        var download = await client.GetAsync($"/api/data/deployments/{ids.VibrationDeploymentId}/traces/{ids.TraceId}/download");
        var hidden = await client.GetAsync($"/api/data/deployments/{ids.HiddenDeploymentId}/traces");
        var wideList = await GetJsonAsync(
            client,
            $"/api/data/deployments/{ids.VibrationDeploymentId}/traces?fromDate={ids.Today.AddDays(-10):yyyy-MM-ddTHH:mm:ss}&toDate={ids.Today.AddHours(1):yyyy-MM-ddTHH:mm:ss}");
        var oldDetail = await client.GetAsync($"/api/data/deployments/{ids.VibrationDeploymentId}/traces/{ids.OldTraceId}");

        Assert.Single(list.RootElement.GetProperty("traces").EnumerateArray());
        Assert.Equal(ids.TraceId, list.RootElement.GetProperty("traces")[0].GetProperty("id").GetGuid());
        Assert.Single(wideList.RootElement.GetProperty("traces").EnumerateArray());
        Assert.Equal(ids.TraceId, wideList.RootElement.GetProperty("traces")[0].GetProperty("id").GetGuid());
        Assert.Equal(ids.TraceId, detail.RootElement.GetProperty("traceId").GetGuid());
        Assert.Equal(3, detail.RootElement.GetProperty("samples").GetArrayLength());
        AssertApproximately(FakeMonitorDataSource.SecondTraceY, detail.RootElement.GetProperty("samples")[1].GetProperty("y").GetDouble());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("X,Y,Z", await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldDetail.StatusCode);
    }

    [Fact]
    // Function summary: Verifies vibration trace reads use the mapped EF trace entity rather than the unmapped legacy DTO.
    public async Task GetVibrationTraces_ReadsMappedTraceRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseSqlite(connection)
            .Options;
        await using var searchContext = new RVTSearchContext(options);
        await searchContext.Database.EnsureCreatedAsync();
        var traceId = Guid.NewGuid();
        searchContext.OmnidotsTracesIndices.Add(new OmnidotsTracesIndex
        {
            Id = traceId,
            SerialId = "TRACE-MAPPED",
            StartTime = DateTime.UnixEpoch,
            EndTime = DateTime.UnixEpoch.AddMinutes(1)
        });
        await searchContext.SaveChangesAsync();
        await searchContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO omnidots_trace (omnidots_trace_index_id, x, y, z)
            VALUES ({traceId}, {0.1}, {0.2}, {0.3})
            """);
        var service = new MonitorService(null!, null!, null!, null!, searchContext, null!, null!, null!);

        var result = await service.GetVibrationTraces(traceId);

        var trace = Assert.Single(result.Value);
        Assert.Equal(traceId, trace.TraceId);
        Assert.Equal(0.2, trace.Y);
    }

    // Function summary: Creates client factory data for the current workflow.
    private static WebApplicationFactory<Program> CreateClientFactory(SpaTestApplicationFactory factory, FakeMonitorDataSource dataSource)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMonitorDataSource>();
                services.AddSingleton<IMonitorDataSource>(dataSource);
            });
        });
    }

    // Function summary: Retrieves json data for callers.
    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    // Function summary: Verifies floating-point API values with a small tolerance.
    private static void AssertApproximately(double expected, double actual)
    {
        Assert.InRange(actual, expected - 0.000001, expected + 0.000001);
    }

    // Function summary: Initializes data view scenario state required by the application.
    private static async Task<DataViewScenarioIds> SeedDataViewScenarioAsync(SpaTestApplicationFactory factory, FakeMonitorDataSource dataSource)
    {
        var companyId = Guid.NewGuid();
        var hiddenCompanyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var hiddenSiteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var hiddenContractId = Guid.NewGuid();
        var dustMonitorId = Guid.NewGuid();
        var vibrationMonitorId = Guid.NewGuid();
        var hiddenMonitorId = Guid.NewGuid();
        var dustDeploymentId = Guid.NewGuid();
        var vibrationDeploymentId = Guid.NewGuid();
        var hiddenDeploymentId = Guid.NewGuid();
        var traceId = Guid.NewGuid();
        var oldTraceId = Guid.NewGuid();
        var endBoundaryTraceId = Guid.NewGuid();
        var today = new DateTime(2026, 5, 24, 8, 0, 0, DateTimeKind.Utc);

        var dustMonitor = Monitor(dustMonitorId, "DATA-DUST", "SER-DATA-D", MonitorTypeEnum.Dust, today);
        var vibrationMonitor = Monitor(vibrationMonitorId, "DATA-VIBE", "SER-DATA-V", MonitorTypeEnum.Vibration, today);
        var hiddenMonitor = Monitor(hiddenMonitorId, "DATA-HIDDEN", "SER-DATA-H", MonitorTypeEnum.Vibration, today);

        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Data View Company", Contracts = [] },
            new Company { Id = hiddenCompanyId, CompanyName = "Hidden Data View Company", Contracts = [] },
            new Site { Id = siteId, SiteName = "Data View Site", CreateDate = today.AddDays(-10), Contracts = [] },
            new Site { Id = hiddenSiteId, SiteName = "Hidden Data View Site", CreateDate = today.AddDays(-10), Contracts = [] },
            new Contract { Id = contractId, ContractNumber = "DATA-CON-001", CompanyId = companyId, SiteiD = siteId, OnHireDate = today.AddDays(-8) },
            new Contract { Id = hiddenContractId, ContractNumber = "DATA-CON-002", CompanyId = hiddenCompanyId, SiteiD = hiddenSiteId, OnHireDate = today.AddDays(-8) },
            dustMonitor,
            vibrationMonitor,
            hiddenMonitor,
            new Deployment { Id = dustDeploymentId, ContractId = contractId, MonitorId = dustMonitorId, StartDate = today.AddDays(-3) },
            new Deployment { Id = vibrationDeploymentId, ContractId = contractId, MonitorId = vibrationMonitorId, StartDate = today.AddDays(-3), EndDate = today.AddHours(1) },
            new Deployment { Id = hiddenDeploymentId, ContractId = hiddenContractId, MonitorId = hiddenMonitorId, StartDate = today.AddDays(-3) },
            new Alertlevel
            {
                Id = Guid.NewGuid(),
                MonitorId = vibrationMonitorId,
                SerialId = vibrationMonitor.SerialId,
                AlertField = "Xvtop",
                AlertType = AlertTypeEnum.Alert,
                LimitOn = VibrationAlertLimitOn,
                LimitOff = 4.5,
                AveragingPeriod = (int)AveragingPeriodsVibrationEnum._1_min,
                IsActive = true,
                Weekdays = true,
                Saturdays = true,
                Sundays = true
            });

        dataSource.AddDustData(dustDeploymentId, dustMonitor, today);
        dataSource.AddVibrationFrequencyData(vibrationDeploymentId, vibrationMonitor, today);
        dataSource.AddTraceData(vibrationDeploymentId, vibrationMonitor, traceId, today);
        dataSource.AddTraceDataAt(vibrationMonitor, oldTraceId, today.AddDays(-6));
        dataSource.AddTraceDataAt(vibrationMonitor, endBoundaryTraceId, today.AddHours(1));

        return new DataViewScenarioIds(companyId, siteId, dustDeploymentId, vibrationDeploymentId, hiddenDeploymentId, traceId, oldTraceId, today);
    }

    // Function summary: Handles the assign user to site workflow for this module.
    private static async Task AssignUserToSiteAsync(SpaTestApplicationFactory factory, string userId, Guid siteId)
    {
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: siteId, userId: Guid.Parse(userId), startDate: DateTime.UtcNow.AddDays(-1)));
    }

    // Function summary: Handles the monitor workflow for this module.
    private static RVT.Entities.Monitor Monitor(Guid id, string fleetNumber, string serialId, MonitorTypeEnum type, DateTime today)
    {
        var monitor = TestData.Monitor(type, id: id, fleetNr: fleetNumber, serialId: serialId);
        monitor.ListedAtTime = today.AddDays(-30);
        monitor.LastDataTime15Min = today;
        return monitor;
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });
    }

    // Function summary: Handles the data view scenario ids workflow for this module.
    private sealed record DataViewScenarioIds(
        Guid CompanyId,
        Guid SiteId,
        Guid DustDeploymentId,
        Guid VibrationDeploymentId,
        Guid HiddenDeploymentId,
        Guid TraceId,
        Guid OldTraceId,
        DateTime Today);
}

internal sealed class FakeMonitorDataSource : IMonitorDataSource
{
    // Probe values that DataViewTests assert against; declared here so the seeded sample data and the
    // assertions share a single source of truth instead of independently repeating the same literals.
    public const double PeakDustPm10 = 24.5;
    public const double PeakFrequencyHz = 0.25;
    public const double PeakFrequencyXVtop = 1.2;
    public const double SecondTraceY = 0.2;

    private readonly Dictionary<Guid, Func<int?, MonitorData>> dataByDeployment = [];
    // Function summary: Handles the guid workflow for this module.
    private readonly Dictionary<Guid, (RVT.Entities.Monitor Monitor, OmnidotsTracesIndex Index, List<OmnidotsTrace> Samples)> traces = [];

    public string? LastGridSort { get; private set; }
    public OrderByDirectionEnum? LastGridSortDirection { get; private set; }
    public DeploymentDataQuery? LastDeploymentRequest { get; private set; }

    // Function summary: Retrieves deployment data data for callers.
    public Task<MonitorData> GetDeploymentDataAsync(DeploymentDataQuery request)
    {
        LastDeploymentRequest = request;
        LastGridSort = request.Sort;
        LastGridSortDirection = request.SortDir;

        if (request.TraceId is not null)
        {
            var trace = traces[(Guid)request.TraceId];
            return Task.FromResult(new MonitorData
            {
                Monitor = trace.Monitor,
                MinDate = trace.Index.StartTime,
                MaxDate = trace.Index.EndTime,
                FromDate = trace.Index.StartTime,
                ToDate = trace.Index.EndTime,
                FilterOptions = [],
                VibrationTraces = new SearchQueryResult<OmnidotsTrace>(true, "", trace.Samples, trace.Samples.Count, "")
            });
        }

        return Task.FromResult(dataByDeployment[request.DeploymentId](request.Page));
    }

    // Function summary: Retrieves trace indexes data for callers.
    public Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(string serialId, DateTime fromDate, DateTime toDate)
    {
        IReadOnlyList<OmnidotsTracesIndex> indexes = traces.Values
            .Where(trace => trace.Monitor.SerialId == serialId && trace.Index.StartTime >= fromDate && trace.Index.StartTime < toDate)
            .Select(trace => trace.Index)
            .ToList();
        return Task.FromResult(indexes);
    }

    // Function summary: Retrieves trace index data for callers.
    public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId)
    {
        return Task.FromResult(traces.TryGetValue(traceId, out var trace) ? trace.Index : null);
    }

    // Function summary: Registers dust data for the current workflow.
    public void AddDustData(Guid deploymentId, RVT.Entities.Monitor monitor, DateTime today, bool hasMore = false)
    {
        dataByDeployment[deploymentId] = (page) =>
        {
            var rows = new List<MyAtmDustLevel>
            {
                new() { SerialId = monitor.SerialId, Avrg = 60, SampleTime = today.AddMinutes(30), Pm1 = 4.1, Pm25 = 10.2, Pm10 = PeakDustPm10, PmTotal = 31.2 },
                new() { SerialId = monitor.SerialId, Avrg = 60, SampleTime = today.AddMinutes(15), Pm1 = 3.1, Pm25 = 9.2, Pm10 = 21.2, PmTotal = 28.8 },
                new() { SerialId = monitor.SerialId, Avrg = 60, SampleTime = today, Pm1 = 2.1, Pm25 = 8.2, Pm10 = 20.2, PmTotal = 27.8 }
            };
            var values = page is null ? rows : rows.Take(2).ToList();
            return new MonitorData
            {
                Monitor = monitor,
                MinDate = today.AddDays(-3),
                MaxDate = today.AddDays(1),
                FromDate = today,
                ToDate = today.AddHours(3),
                FilterOption = "60",
                FilterOptions = new Dictionary<string, string> { ["60"] = "All Readings", ["900"] = "15 Min Averages" },
                DustLevels = new SearchQueryResult<MyAtmDustLevel>(true, "", values, rows.Count, "") { HasMore = hasMore }
            };
        };
    }

    // Function summary: Registers vibration frequency data for the current workflow.
    public void AddVibrationFrequencyData(Guid deploymentId, RVT.Entities.Monitor monitor, DateTime today)
    {
        dataByDeployment[deploymentId] = (_) => new MonitorData
        {
            Monitor = monitor,
            MinDate = today.AddDays(-3),
            MaxDate = today.AddDays(1),
            FromDate = today,
            ToDate = today.AddHours(1),
            FilterOption = "frequency",
            FilterOptions = new Dictionary<string, string> { ["time"] = "Over Time", ["frequency"] = "By Frequency" },
            VibrationFrequencyMagnitudes =
            [
                new OmnidotsFrequencyMagnitudes { Frequency = 0, XVtop = 0.6, YVtop = 0.2, ZVtop = 0.1 },
                new OmnidotsFrequencyMagnitudes { Frequency = PeakFrequencyHz, XVtop = PeakFrequencyXVtop, YVtop = 0.4, ZVtop = 0.3 }
            ]
        };
    }

    // Function summary: Registers trace data for the current workflow.
    public void AddTraceData(Guid deploymentId, RVT.Entities.Monitor monitor, Guid traceId, DateTime today)
    {
        AddTraceDataAt(monitor, traceId, today.AddMinutes(10));

        if (!dataByDeployment.ContainsKey(deploymentId))
        {
            AddVibrationFrequencyData(deploymentId, monitor, today);
        }
    }

    // Function summary: Registers trace data at a specific timestamp for ownership-window tests.
    public void AddTraceDataAt(RVT.Entities.Monitor monitor, Guid traceId, DateTime startTime)
    {
        traces[traceId] = (
            monitor,
            new OmnidotsTracesIndex { Id = traceId, SerialId = monitor.SerialId, StartTime = startTime, EndTime = startTime.AddMinutes(1) },
            [
                new OmnidotsTrace { TraceId = traceId, X = 0.1, Y = 0.1, Z = 0.1 },
                new OmnidotsTrace { TraceId = traceId, X = 0.2, Y = SecondTraceY, Z = 0.3 },
                new OmnidotsTrace { TraceId = traceId, X = 0.3, Y = 0.4, Z = 0.5 }
            ]);
    }
}

internal sealed class RecordingSearchQueryReader : ISearchQueryReader
{
    public List<Filter>? LastFilters { get; private set; }

    public Task<SearchQueryResult<TResult>> ReadFilteredAsync<TSource, TResult>(
        List<Filter> whereFilter,
        OrderByProperty[] orderBy,
        int maximumRecords,
        Paging pagedata,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        LastFilters = whereFilter;
        return Task.FromResult(new SearchQueryResult<TResult>(true, string.Empty, [], 0, string.Empty));
    }
}
