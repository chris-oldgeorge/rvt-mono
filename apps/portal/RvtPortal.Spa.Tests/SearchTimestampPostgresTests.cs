// File summary: Verifies the portal's PostgreSQL telemetry timestamp boundary and complete SampleTime store-type contract.

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
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
            ["NoiseLevelSiteAvg"] = "timestamp without time zone",
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

    [Fact]
    // Function summary: Verifies the EF view metadata and checked-in PostgreSQL definitions agree on UTC-naive aggregate timestamps.
    public void SearchModel_AggregateViewMappings_MatchCheckedInPostgresDefinitions()
    {
        var options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new RVTSearchContext(options);
        var sql = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "portal",
            "database",
            "postgres",
            "post-load",
            "03_views_and_routines.sql"));
        var timestampViews = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["my_atm_dust_level_8_hour_avg"] = typeof(MyAtmDustLevel8hourAvg),
            ["noise_level_1_hour_avg"] = typeof(NoiseLevel1hourAvg),
            ["noise_level_site_avg"] = typeof(NoiseLevelSiteAvg),
            ["omnidots_peak_level_1_min"] = typeof(OmnidotsPeakLevel1min),
            ["omnidots_peak_level_5_min"] = typeof(OmnidotsPeakLevel5min),
            ["omnidots_peak_level_15_min"] = typeof(OmnidotsPeakLevel15min),
            ["omnidots_peak_level_20_min"] = typeof(OmnidotsPeakLevel20min)
        };

        foreach (var (viewName, entityType) in timestampViews)
        {
            var entity = context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
            Assert.Equal(viewName, entity.GetViewName());
            Assert.Equal("timestamp without time zone", entity.FindProperty("SampleTime")?.GetColumnType());
        }

        foreach (var viewName in new[]
                 {
                     "air_q_noise_level_1_hour_avg",
                     "noise_level_1_hour_avg",
                     "my_atm_dust_level_8_hour_avg",
                     "omnidots_peak_level_1_min",
                     "omnidots_peak_level_5_min",
                     "omnidots_peak_level_15_min",
                     "omnidots_peak_level_20_min"
                 })
        {
            var definition = ExtractViewDefinition(sql, viewName);
            Assert.Contains("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'", definition, StringComparison.OrdinalIgnoreCase);
            var compactDefinition = definition
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("\t", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal);
            Assert.DoesNotContain(",CURRENT_TIMESTAMP)", compactDefinition, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    // Function summary: Verifies the checked-in model snapshot carries the same SampleTime types as the runtime PostgreSQL model.
    public void SearchModelSnapshot_SampleTimeMappings_MatchRuntimeModel()
    {
        var snapshot = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "portal",
            "RVT.DataAccess",
            "Migrations",
            "Search",
            "RVTSearchContextModelSnapshot.cs"));

        foreach (var (entityName, storeType) in ApprovedSampleTimeStoreTypes)
        {
            var entityBlock = ExtractSnapshotEntityBlock(snapshot, entityName);
            Assert.Matches(
                $@"Property<DateTime\??>\(""SampleTime""\)\s*\.HasColumnType\(""{Regex.Escape(storeType)}""\)",
                entityBlock);
        }
    }

    [RequiresPostgresFact]
    // Function summary: Inspects and queries every timestamp view affected by the PostgreSQL UTC-naive aggregate contract.
    public async Task AggregateViews_HaveExpectedProviderTypesAndAcceptUtcNaiveBounds()
    {
        var expectedViewTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["air_q_noise_level_1_hour_avg"] = "timestamp without time zone",
            ["air_q_noise_level_site_avg"] = "timestamp without time zone",
            ["my_atm_dust_level_8_hour_avg"] = "timestamp without time zone",
            ["noise_level_1_hour_avg"] = "timestamp without time zone",
            ["noise_level_site_avg"] = "timestamp without time zone",
            ["omnidots_peak_level_1_min"] = "timestamp without time zone",
            ["omnidots_peak_level_5_min"] = "timestamp without time zone",
            ["omnidots_peak_level_15_min"] = "timestamp without time zone",
            ["omnidots_peak_level_20_min"] = "timestamp without time zone",
            ["noise_level_1_day_avg"] = "date",
            ["omnidots_peak_level_1_day_peak"] = "date"
        };
        var connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        var searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new RVTSearchContext(searchOptions);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        foreach (var (viewName, expectedType) in expectedViewTypes)
        {
            await using var metadata = connection.CreateCommand();
            metadata.CommandText = """
                SELECT data_type
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @view_name
                  AND column_name = 'sample_time'
                """;
            var viewParameter = metadata.CreateParameter();
            viewParameter.ParameterName = "view_name";
            viewParameter.Value = viewName;
            metadata.Parameters.Add(viewParameter);
            Assert.Equal(expectedType, await metadata.ExecuteScalarAsync());

            await using var query = connection.CreateCommand();
            query.CommandText = $"""
                SELECT sample_time
                FROM public.{viewName}
                WHERE sample_time >= @from_date
                  AND sample_time <= @to_date
                LIMIT 1
                """;
            var fromParameter = query.CreateParameter();
            fromParameter.ParameterName = "from_date";
            fromParameter.Value = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
            query.Parameters.Add(fromParameter);
            var toParameter = query.CreateParameter();
            toParameter.ParameterName = "to_date";
            toParameter.Value = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Unspecified);
            query.Parameters.Add(toParameter);
            await query.ExecuteScalarAsync();
        }
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

    private static string ExtractViewDefinition(string sql, string viewName)
    {
        var match = Regex.Match(
            sql,
            $@"CREATE\s+OR\s+REPLACE\s+VIEW\s+public\.{Regex.Escape(viewName)}\s+AS(?<body>.*?);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(match.Success, $"View '{viewName}' was not found in the checked-in PostgreSQL script.");
        return match.Value;
    }

    private static string ExtractSnapshotEntityBlock(string snapshot, string entityName)
    {
        var match = Regex.Match(
            snapshot,
            $@"modelBuilder\.Entity\(""[^""]*\.{Regex.Escape(entityName)}"",\s*b\s*=>\s*\{{(?<body>.*?)\n\s*\}}\);",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"Entity '{entityName}' was not found in the search model snapshot.");
        return match.Value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Rvt.Mono.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
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
