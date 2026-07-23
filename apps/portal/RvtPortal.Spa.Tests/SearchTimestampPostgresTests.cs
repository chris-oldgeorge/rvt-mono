// File summary: Verifies the portal's PostgreSQL telemetry timestamp boundary and complete SampleTime store-type contract.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Data;
using RvtPortal.Spa.Application.Monitors;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class SearchTimestampPostgresTests
{
    private static readonly IReadOnlyDictionary<string, string> ApprovedSampleTimeStoreTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MyAtmDustLevel"] = "timestamp without time zone",
            ["MyAtmDustLevel8hourAvg"] = "timestamp without time zone",
            ["NoiseLevel15minAvg"] = "timestamp without time zone",
            ["NoiseLevel1dayAvg"] = "date",
            ["NoiseLevel1hourAvg"] = "timestamp without time zone",
            ["NoiseLevelSiteAvg"] = "date",
            ["OmnidotsPeakLevel"] = "timestamp without time zone",
            ["OmnidotsPeakLevel15min"] = "timestamp without time zone",
            ["OmnidotsPeakLevel1dayPeak"] = "date",
            ["OmnidotsPeakLevel1min"] = "timestamp without time zone",
            ["OmnidotsPeakLevel20min"] = "timestamp without time zone",
            ["OmnidotsPeakLevel5min"] = "timestamp without time zone"
        };

    [Fact]
    // Function summary: Enumerates every search SampleTime property and compares its PostgreSQL store type to the approved table.
    public void SearchModel_SampleTimeMappings_MatchApprovedPostgresContract()
    {
        var options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new RVTSearchContext(options);

        var actual = context.Model.GetEntityTypes()
            .Select(entity => (Entity: entity, Property: entity.FindProperty("SampleTime")))
            .Where(item => item.Property is not null)
            .ToDictionary(
                item => item.Entity.ClrType.Name,
                item => item.Property!.GetColumnType(),
                StringComparer.Ordinal);

        Assert.Equal(ApprovedSampleTimeStoreTypes, actual);
    }

    [RequiresPostgresFact]
    // Function summary: Inserts timestamp-without-zone telemetry, queries it with UTC bounds, and verifies API JSON restores Z.
    public async Task DustTelemetry_UtcBounds_QuerySuccessfullyAndReturnUtcJson()
    {
        var connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        var searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var searchContext = new RVTSearchContext(searchOptions);
        await using var transaction = await searchContext.Database.BeginTransactionAsync();
        var serialId = $"T5{Guid.NewGuid():N}"[..22];
        var databaseTimestamp = new DateTime(2026, 7, 1, 14, 30, 0, DateTimeKind.Unspecified);
        await searchContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.my_atm_dust_level
                (serial_id, avrg, sample_time, pm_1, pm_2_5, pm_10, pm_total)
            VALUES
                ({serialId}, {60}, {databaseTimestamp}, {1.0}, {2.0}, {3.0}, {4.0})
            """);

        var monitor = new RVT.Entities.Monitor
        {
            Id = Guid.NewGuid(),
            SerialId = serialId,
            FleetNr = "T5-UTC",
            Manufacturer = "Test",
            Model = "Test",
            FirmwareVersion = "0",
            TypeOfMonitor = MonitorTypeEnum.Dust,
            ListedAtTime = DateTime.UnixEpoch
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "T5-UTC-CONTRACT",
            CompanyId = Guid.NewGuid(),
            OnHireDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            MonitorId = monitor.Id,
            Monitor = monitor,
            ContractId = contract.Id,
            Contract = contract,
            StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await using var domainContext = CreateDomainContext(deployment);
        var monitorService = new MonitorService(
            null!,
            null!,
            null!,
            new SearchQueryReader(searchContext),
            searchContext,
            null!,
            null!,
            null!);
        var dataSource = new PostgresDustDataSource(monitorService, monitor);
        var application = new DataApplicationService(domainContext, dataSource);

        var result = await application.GetGridAsync(
            deployment.Id,
            new MonitorDataGridRequest
            {
                FilterOption = "60",
                FromDate = new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc),
                Page = 1,
                PageSize = 10
            },
            new DataViewActor(null, IsAdmin: true, IsCompanyUser: false),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Null(result.Failure);
        Assert.Contains("\"sampleTime\":\"2026-07-01T14:30:00Z\"", json, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    // Function summary: Creates an isolated domain model that supplies deployment visibility to the API application service.
    private static RVTDbContext CreateDomainContext(Deployment deployment)
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase($"timestamp-contract-{Guid.NewGuid():N}")
            .Options;
        var context = new RVTDbContext(options);
        context.Deployments.Add(deployment);
        context.SaveChanges();
        return context;
    }

    private sealed class PostgresDustDataSource : IMonitorDataSource
    {
        private readonly IMonitorService monitorService;
        private readonly RVT.Entities.Monitor monitor;

        public PostgresDustDataSource(IMonitorService monitorService, RVT.Entities.Monitor monitor)
        {
            this.monitorService = monitorService;
            this.monitor = monitor;
        }

        public async Task<MonitorData> GetDeploymentDataAsync(DeploymentDataQuery request)
        {
            var levels = await monitorService.GetMyAtmDustLevels(
                monitor.SerialId,
                request.FromDate!.Value,
                request.ToDate!.Value,
                60,
                request.Page,
                request.PageSize,
                request.Sort,
                request.SortDir);
            return new MonitorData
            {
                Monitor = monitor,
                MinDate = request.FromDate.Value,
                MaxDate = request.ToDate.Value,
                FromDate = request.FromDate.Value,
                ToDate = request.ToDate.Value,
                FilterOption = "60",
                DustLevels = levels
            };
        }

        public Task<IReadOnlyList<RVT.DataAccess.EntityModels.Models.OmnidotsTracesIndex>> GetTraceIndexesAsync(
            string serialId,
            DateTime fromDate,
            DateTime toDate)
        {
            return Task.FromResult<IReadOnlyList<RVT.DataAccess.EntityModels.Models.OmnidotsTracesIndex>>([]);
        }

        public Task<RVT.DataAccess.EntityModels.Models.OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId)
        {
            return Task.FromResult<RVT.DataAccess.EntityModels.Models.OmnidotsTracesIndex?>(null);
        }
    }
}
