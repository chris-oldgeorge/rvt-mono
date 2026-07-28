// File summary: Verifies the portal's PostgreSQL telemetry timestamp boundary and complete SampleTime store-type contract.

using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using RVT.DataAccess;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;
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
        DbContextOptions<RVTSearchContext> options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        using RVTSearchContext context = new(options);

        Dictionary<string, string> actual = context.Model.GetEntityTypes()
            .Select(entity => (Entity: entity, Property: entity.FindProperty("SampleTime")))
            .Where(item => item.Property is not null)
            .ToDictionary(
                item => item.Entity.ClrType.Name,
                item => item.Property!.GetColumnType(),
                StringComparer.Ordinal);

        Assert.Equal(ApprovedSampleTimeStoreTypes, actual);
    }

    [Fact]
    // Function summary: Verifies both trace-index bounds use the PostgreSQL UTC-naive timestamp contract in runtime and snapshot metadata.
    public void SearchModel_TraceIndexTimeMappings_MatchApprovedPostgresContract()
    {
        DbContextOptions<RVTSearchContext> options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        using RVTSearchContext context = new(options);
        IEntityType? entity = context.Model.FindEntityType(typeof(OmnidotsTracesIndex));
        Assert.NotNull(entity);
        Assert.Equal("timestamp without time zone", entity.FindProperty(nameof(OmnidotsTracesIndex.StartTime))?.GetColumnType());
        Assert.Equal("timestamp without time zone", entity.FindProperty(nameof(OmnidotsTracesIndex.EndTime))?.GetColumnType());

        string snapshot = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "portal",
            "RVT.DataAccess",
            "Migrations",
            "Search",
            "RVTSearchContextModelSnapshot.cs"));
        string entityBlock = ExtractSnapshotEntityBlock(snapshot, nameof(OmnidotsTracesIndex));
        Assert.Matches(
            @"Property<DateTime>\(""StartTime""\)\s*\.HasColumnType\(""timestamp without time zone""\)",
            entityBlock);
        Assert.Matches(
            @"Property<DateTime>\(""EndTime""\)\s*\.HasColumnType\(""timestamp without time zone""\)",
            entityBlock);
    }

    [Fact]
    // Function summary: Verifies the EF view metadata and checked-in PostgreSQL definitions agree on UTC-naive aggregate timestamps.
    public void SearchModel_AggregateViewMappings_MatchCheckedInPostgresDefinitions()
    {
        DbContextOptions<RVTSearchContext> options = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        using RVTSearchContext context = new(options);
        string sql = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "portal",
            "database",
            "postgres",
            "post-load",
            "03_views_and_routines.sql"));
        Dictionary<string, Type> timestampViews = new(StringComparer.Ordinal)
        {
            ["my_atm_dust_level_8_hour_avg"] = typeof(MyAtmDustLevel8hourAvg),
            ["noise_level_1_hour_avg"] = typeof(NoiseLevel1hourAvg),
            ["noise_level_site_avg"] = typeof(NoiseLevelSiteAvg),
            ["omnidots_peak_level_1_min"] = typeof(OmnidotsPeakLevel1min),
            ["omnidots_peak_level_5_min"] = typeof(OmnidotsPeakLevel5min),
            ["omnidots_peak_level_15_min"] = typeof(OmnidotsPeakLevel15min),
            ["omnidots_peak_level_20_min"] = typeof(OmnidotsPeakLevel20min)
        };

        foreach ((string? viewName, Type? entityType) in timestampViews)
        {
            IEntityType? entity = context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
            Assert.Equal(viewName, entity.GetViewName());
            Assert.Equal("timestamp without time zone", entity.FindProperty("SampleTime")?.GetColumnType());
        }

        foreach (string? viewName in new[]
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
            string definition = ExtractViewDefinition(sql, viewName);
            Assert.Contains("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'", definition, StringComparison.OrdinalIgnoreCase);
            string compactDefinition = definition
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("\t", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal);
            Assert.DoesNotContain(",CURRENT_TIMESTAMP)", compactDefinition, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                ")::timestampwithouttimezoneassample_time",
                compactDefinition,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    // Function summary: Verifies the checked-in model snapshot carries the same SampleTime types as the runtime PostgreSQL model.
    public void SearchModelSnapshot_SampleTimeMappings_MatchRuntimeModel()
    {
        string snapshot = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "apps",
            "portal",
            "RVT.DataAccess",
            "Migrations",
            "Search",
            "RVTSearchContextModelSnapshot.cs"));

        foreach ((string? entityName, string? storeType) in ApprovedSampleTimeStoreTypes)
        {
            string entityBlock = ExtractSnapshotEntityBlock(snapshot, entityName);
            Assert.Matches(
                $@"Property<DateTime\??>\(""SampleTime""\)\s*\.HasColumnType\(""{Regex.Escape(storeType)}""\)",
                entityBlock);
        }
    }

    [RequiresPostgresFact]
    // Function summary: Inspects and queries every timestamp view affected by the PostgreSQL UTC-naive aggregate contract.
    public async Task AggregateViews_HaveExpectedProviderTypesAndAcceptUtcNaiveBounds()
    {
        Dictionary<string, string> expectedViewTypes = new(StringComparer.Ordinal)
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
        string? connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using RVTSearchContext context = new(searchOptions);
        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        foreach ((string? viewName, string? expectedType) in expectedViewTypes)
        {
            await using DbCommand metadata = connection.CreateCommand();
            metadata.CommandText = """
                SELECT data_type
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @view_name
                  AND column_name = 'sample_time'
                """;
            DbParameter viewParameter = metadata.CreateParameter();
            viewParameter.ParameterName = "view_name";
            viewParameter.Value = viewName;
            metadata.Parameters.Add(viewParameter);
            Assert.Equal(expectedType, await metadata.ExecuteScalarAsync());

            await using DbCommand query = connection.CreateCommand();
            query.CommandText = $"""
                SELECT sample_time
                FROM public.{viewName}
                WHERE sample_time >= @from_date
                  AND sample_time <= @to_date
                LIMIT 1
                """;
            DbParameter fromParameter = query.CreateParameter();
            fromParameter.ParameterName = "from_date";
            fromParameter.Value = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
            query.Parameters.Add(fromParameter);
            DbParameter toParameter = query.CreateParameter();
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
        string? connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using RVTSearchContext searchContext = new(searchOptions);
        await using IDbContextTransaction transaction = await searchContext.Database.BeginTransactionAsync();
        string serialId = $"T5{Guid.NewGuid():N}"[..22];
        DateTime databaseTimestamp = new(2026, 7, 1, 14, 30, 0, DateTimeKind.Unspecified);
        NpgsqlParameter databaseTimestampParameter = new("sample_time", databaseTimestamp)
        {
            NpgsqlDbType = NpgsqlDbType.Timestamp
        };
        await searchContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.my_atm_dust_level
                (serial_id, avrg, sample_time, pm_1, pm_2_5, pm_10, pm_total)
            VALUES
                ({serialId}, {60}, {databaseTimestampParameter}, {1.0}, {2.0}, {3.0}, {4.0})
            """);

        RVT.Entities.Monitor monitor = new()
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
        Contract contract = new()
        {
            Id = Guid.NewGuid(),
            ContractNumber = "T5-UTC-CONTRACT",
            CompanyId = Guid.NewGuid(),
            OnHireDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        Deployment deployment = new()
        {
            Id = Guid.NewGuid(),
            MonitorId = monitor.Id,
            Monitor = monitor,
            ContractId = contract.Id,
            Contract = contract,
            StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await using RVTDbContext domainContext = CreateDomainContext(deployment);
        MonitorService monitorService = new(
            null!,
            null!,
            new SearchQueryReader(searchContext),
            searchContext,
            null!);
        PostgresDustDataSource dataSource = new(monitorService, monitor);
        DataApplicationService application = new(domainContext, dataSource);

        DataWorkflowResult<MonitorDataGridResponse> result = await application.GetGridAsync(
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
        string json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Null(result.Failure);
        Assert.Contains("\"sampleTime\":\"2026-07-01T14:30:00Z\"", json, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    [RequiresPostgresFact]
    // Function summary: Queries trace indexes with UTC bounds and verifies list/detail API values restore the UTC JSON contract.
    public async Task TraceIndexes_UtcBounds_QuerySuccessfullyAndReturnUtcJson()
    {
        string? connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using RVTSearchContext searchContext = new(searchOptions);
        await using IDbContextTransaction transaction = await searchContext.Database.BeginTransactionAsync();
        Guid traceId = Guid.NewGuid();
        string serialId = $"T5{Guid.NewGuid():N}"[..22];
        DateTime databaseStart = new(2026, 7, 1, 14, 30, 0, DateTimeKind.Unspecified);
        DateTime databaseEnd = databaseStart.AddMinutes(1);
        NpgsqlParameter databaseStartParameter = new("start_time", databaseStart)
        {
            NpgsqlDbType = NpgsqlDbType.Timestamp
        };
        NpgsqlParameter databaseEndParameter = new("end_time", databaseEnd)
        {
            NpgsqlDbType = NpgsqlDbType.Timestamp
        };
        await searchContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.omnidots_trace_index
                (id, serial_id, start_time, end_time)
            VALUES
                ({traceId}, {serialId}, {databaseStartParameter}, {databaseEndParameter})
            """);

        RVT.Entities.Monitor monitor = new()
        {
            Id = Guid.NewGuid(),
            SerialId = serialId,
            FleetNr = "T5-TRACE-UTC",
            Manufacturer = "Test",
            Model = "Test",
            FirmwareVersion = "0",
            TypeOfMonitor = MonitorTypeEnum.Vibration,
            ListedAtTime = DateTime.UnixEpoch
        };
        Contract contract = new()
        {
            Id = Guid.NewGuid(),
            ContractNumber = "T5-TRACE-UTC-CONTRACT",
            CompanyId = Guid.NewGuid(),
            OnHireDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        Deployment deployment = new()
        {
            Id = Guid.NewGuid(),
            MonitorId = monitor.Id,
            Monitor = monitor,
            ContractId = contract.Id,
            Contract = contract,
            StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await using RVTDbContext domainContext = CreateDomainContext(deployment);
        MonitorDataSource realDataSource = new(null!, searchContext, null!);
        PostgresTraceDataSource dataSource = new(realDataSource, monitor);
        DataApplicationService application = new(domainContext, dataSource);
        DataViewActor actor = new(null, IsAdmin: true, IsCompanyUser: false);

        DataWorkflowResult<TraceListResponse> list = await application.GetTracesAsync(
            deployment.Id,
            new TraceListRequest
            {
                FromDate = new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc)
            },
            actor,
            CancellationToken.None);
        DataWorkflowResult<TraceDetailResponse> detail = await application.GetTraceDetailAsync(
            deployment.Id,
            traceId,
            actor,
            CancellationToken.None);
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        string listJson = JsonSerializer.Serialize(list.Value, jsonOptions);
        string detailJson = JsonSerializer.Serialize(detail.Value, jsonOptions);

        Assert.Null(list.Failure);
        Assert.Null(detail.Failure);
        Assert.Contains("\"startTime\":\"2026-07-01T14:30:00Z\"", listJson, StringComparison.Ordinal);
        Assert.Contains("\"endTime\":\"2026-07-01T14:31:00Z\"", listJson, StringComparison.Ordinal);
        Assert.Contains("\"fromDate\":\"2026-07-01T14:30:00Z\"", detailJson, StringComparison.Ordinal);
        Assert.Contains("\"toDate\":\"2026-07-01T14:31:00Z\"", detailJson, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    // Function summary: Creates an isolated domain model that supplies deployment visibility to the API application service.
    private static RVTDbContext CreateDomainContext(Deployment deployment)
    {
        DbContextOptions<RVTDbContext> options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase($"timestamp-contract-{Guid.NewGuid():N}")
            .Options;
        RVTDbContext context = new(options);
        context.Deployments.Add(deployment);
        context.SaveChanges();
        return context;
    }

    private static string ExtractViewDefinition(string sql, string viewName)
    {
        Match match = Regex.Match(
            sql,
            $@"CREATE\s+OR\s+REPLACE\s+VIEW\s+public\.{Regex.Escape(viewName)}\s+AS(?<body>.*?);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(match.Success, $"View '{viewName}' was not found in the checked-in PostgreSQL script.");
        return match.Value;
    }

    private static string ExtractSnapshotEntityBlock(string snapshot, string entityName)
    {
        Match match = Regex.Match(
            snapshot,
            $@"modelBuilder\.Entity\(""[^""]*\.{Regex.Escape(entityName)}"",\s*b\s*=>\s*\{{(?<body>.*?)\n\s*\}}\);",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"Entity '{entityName}' was not found in the search model snapshot.");
        return match.Value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
            SearchQueryResult<MyAtmDustLevel> levels = await monitorService.GetMyAtmDustLevels(
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

        public Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
            string serialId,
            DateTime fromDate,
            DateTime toDate)
        {
            return Task.FromResult<IReadOnlyList<OmnidotsTracesIndex>>([]);
        }

        public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId)
        {
            return Task.FromResult<OmnidotsTracesIndex?>(null);
        }
    }

    private sealed class PostgresTraceDataSource : IMonitorDataSource
    {
        private readonly MonitorDataSource inner;
        private readonly RVT.Entities.Monitor monitor;

        public PostgresTraceDataSource(MonitorDataSource inner, RVT.Entities.Monitor monitor)
        {
            this.inner = inner;
            this.monitor = monitor;
        }

        public async Task<MonitorData> GetDeploymentDataAsync(DeploymentDataQuery request)
        {
            OmnidotsTracesIndex? index = await inner.GetTraceIndexAsync(request.TraceId!.Value);
            Assert.NotNull(index);
            return new MonitorData
            {
                Monitor = monitor,
                FromDate = index.StartTime,
                ToDate = index.EndTime,
                VibrationTraces = new SearchQueryResult<OmnidotsTrace>(true, string.Empty, [], 0, string.Empty)
            };
        }

        public Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
            string serialId,
            DateTime fromDate,
            DateTime toDate)
        {
            return inner.GetTraceIndexesAsync(serialId, fromDate, toDate);
        }

        public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId)
        {
            return inner.GetTraceIndexAsync(traceId);
        }
    }
}
