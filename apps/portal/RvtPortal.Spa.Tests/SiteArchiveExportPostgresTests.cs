// File summary: Runs every site-archive export statement against a seeded throwaway PostgreSQL schema.
// Major updates:
// - 2026-07-30 pending Added when the archive path was found to have no integration coverage at all.

using Npgsql;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// The nine hand-written archive statements had no integration coverage: the Spa test host replaces
/// <c>ISiteArchiveService</c> with a fake, so nothing ever executed them. These tests run the real catalog SQL
/// through the real executor and CSV writer against a throwaway schema seeded with one contract's worth of data.
/// <para>
/// The real <c>SiteArchiveService</c> is deliberately not used: past the SQL it only zips the CSVs and uploads
/// them to Azure blob storage, which is exactly the dependency the test host fakes away. Everything below the
/// upload - the catalog, the executor, the row types and the CSV writer - runs for real here.
/// </para>
/// <para>
/// The schema comes from the EF models (<see cref="SpaTestDatabase"/>) plus three relations no portal model
/// maps but the exports read. Because it is a throwaway schema reached through <c>SearchPath</c>, any statement
/// that pins a schema of its own reads the wrong tables and returns nothing.
/// </para>
/// </summary>
public sealed class SiteArchiveExportPostgresTests
{
    private static readonly DateTime _onHire = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A date-only off-hire, which is what <c>ContractCommands.AsUtcDate</c> stores for every contract.
    /// </summary>
    private static readonly DateTime _offHire = new(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime _midWindow = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Inside the final day of the contract. The whole-day off-hire rule includes it; comparing the raw
    /// off-hire value drops it, which is what every export used to do.
    /// </summary>
    private static readonly DateTime _finalDay = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime _afterEnd = new(2026, 5, 11, 0, 30, 0, DateTimeKind.Utc);

    [RequiresPostgresFact]
    // Function summary: Verifies every export runs against the seeded schema and keeps the contract's final day.
    public async Task Exports_ResolveThroughSearchPathAndCoverTheContractsFinalDay()
    {
        await using ArchiveSchemaFixture fixture = await ArchiveSchemaFixture.CreateAsync();
        Guid siteId = await fixture.SeedAsync();
        await using RVTDbContext context = fixture.CreateDomainContext();
        SiteArchiveQueryCatalog catalog = new();
        SiteArchiveQueryExecutor executor = new(context);
        SiteArchiveCsvWriter csvWriter = new();
        string workspace = fixture.CreateWorkspace();

        foreach (ArchiveCsvExport export in catalog.CsvExports)
        {
            await export.WriteAsync(executor, csvWriter, workspace, siteId, CancellationToken.None);
        }

        List<string> reportLinks = [];
        await foreach (ReportArchiveRow row in executor.StreamAsync<ReportArchiveRow>(
            catalog.ReportLinksSql,
            siteId,
            CancellationToken.None))
        {
            reportLinks.Add(row.ReportLink!);
        }

        // One monitor row, and for every time-bounded export the mid-window sample plus the final-day sample -
        // never the sample past the end. A raw off-hire comparison yields 1 instead of 2 for all seven.
        Assert.Multiple(
            () => Assert.Equal(1, DataRowCount(workspace, "Monitors.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "Breaches.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "DustMonitorData.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "NoiseMonitorData.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "NoiseMonitorDataS.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "VibrationMonitorData.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "TraceList.csv")),
            () => Assert.Equal(2, DataRowCount(workspace, "TraceData.csv")),
            () => Assert.Equal(["https://reports.example/site-report.pdf"], reportLinks));
    }

    [RequiresPostgresFact]
    /// <summary>
    /// The row-count test proves the final day survives; this one names the timestamps, so a future change that
    /// happens to return two rows for the wrong reason cannot pass it.
    /// </summary>
    // Function summary: Verifies the dust export returns exactly the in-window samples, final day included.
    public async Task DustExport_ReturnsTheFinalDaySampleAndNothingPastTheOwnershipEnd()
    {
        await using ArchiveSchemaFixture fixture = await ArchiveSchemaFixture.CreateAsync();
        Guid siteId = await fixture.SeedAsync();
        await using RVTDbContext context = fixture.CreateDomainContext();
        SiteArchiveQueryCatalog catalog = new();
        SiteArchiveQueryExecutor executor = new(context);
        string dustSql = catalog.CsvExports
            .Single(export => StringComparer.Ordinal.Equals(export.FileName, "DustMonitorData.csv"))
            .Sql;

        List<DateTime> sampleTimes = [];
        await foreach (DustArchiveRow row in executor.StreamAsync<DustArchiveRow>(
            dustSql,
            siteId,
            CancellationToken.None))
        {
            sampleTimes.Add(row.SampleTime!.Value);
        }

        Assert.Equal(
            [Unspecified(_finalDay), Unspecified(_midWindow)],
            sampleTimes);
    }

    // Function summary: Counts the data rows a written export holds, excluding its header line.
    private static int DataRowCount(string workspace, string fileName)
    {
        return File.ReadAllLines(Path.Combine(workspace, fileName)).Length - 1;
    }

    /// <summary>
    /// The measurement tables are <c>timestamp without time zone</c> in the canonical schema, so Npgsql both
    /// reads and writes them as <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    private static DateTime Unspecified(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// A throwaway schema carrying every relation the archive exports read. The EF models supply all but three:
    /// <c>air_q_noise_level</c>, <c>svantek_noise_level</c> and <c>report</c> are written by other services and
    /// mapped by no portal context, so their canonical shape is spelled out here.
    /// </summary>
    private sealed class ArchiveSchemaFixture : IAsyncDisposable
    {
        private readonly string _schemaName;
        private readonly string _scopedConnectionString;
        private readonly List<string> _workspaces = [];

        private ArchiveSchemaFixture(string schemaName, string scopedConnectionString)
        {
            _schemaName = schemaName;
            _scopedConnectionString = scopedConnectionString;
        }

        public static async Task<ArchiveSchemaFixture> CreateAsync()
        {
            string schemaName = $"site_archive_{Guid.NewGuid():N}";
            ArchiveSchemaFixture fixture = new(schemaName, SpaTestDatabase.CreateSchema(schemaName));

            await fixture.ExecuteAsync($"""
                CREATE TABLE {NoiseLevelColumns("air_q_noise_level")};
                CREATE TABLE {NoiseLevelColumns("svantek_noise_level")};
                CREATE TABLE "report"
                (
                    id uuid PRIMARY KEY,
                    site_id uuid NOT NULL,
                    report_link text NOT NULL
                );
                """);
            return fixture;
        }

        public RVTDbContext CreateDomainContext() =>
            new(TestDbContexts.Npgsql<RVTDbContext>(_scopedConnectionString));

        // Function summary: Creates a scratch directory for one run of the CSV exports.
        public string CreateWorkspace()
        {
            string workspace = Path.Combine(Path.GetTempPath(), _schemaName);
            Directory.CreateDirectory(workspace);
            _workspaces.Add(workspace);
            return workspace;
        }

        /// <summary>
        /// Seeds one site, one contract with a date-only off-hire, one deployment that outlives it, and - for
        /// every measurement relation plus notifications - a mid-window sample, a final-day sample, and a sample
        /// past the ownership end.
        /// </summary>
        public async Task<Guid> SeedAsync()
        {
            Guid siteId = Guid.NewGuid();
            Guid companyId = Guid.NewGuid();
            Guid contractId = Guid.NewGuid();
            Guid monitorId = Guid.NewGuid();
            string serialId = $"SER-{Guid.NewGuid():N}"[..24];

            await ExecuteAsync(
                """
                INSERT INTO "site" (id, site_name, create_date, archived)
                    VALUES (@siteId, 'Archive site', @onHire, false);
                INSERT INTO "company" (id, company_name) VALUES (@companyId, 'Archive company');
                INSERT INTO "monitor"
                    (id, serial_id, fleet_nr, manufacturer, model, firmware_version, type_of_monitor,
                     listed_at_time, archived)
                    VALUES (@monitorId, @serialId, 'FLEET-1', 'MyAtm', 'M1', '1.0', 0, @onHire, false);
                INSERT INTO "contract" (id, contract_number, on_hire_date, off_hire_date, company_id, site_id)
                    VALUES (@contractId, 'C-1', @onHire, @offHire, @companyId, @siteId);
                INSERT INTO "deployment"
                    (id, start_date, end_date, lat, lng, what_3_words, contract_id, monitor_id)
                    VALUES (@deploymentId, @onHire, NULL, 51.5, -0.12, 'one.two.three', @contractId, @monitorId);
                """,
                command =>
                {
                    command.Parameters.AddWithValue("siteId", siteId);
                    command.Parameters.AddWithValue("companyId", companyId);
                    command.Parameters.AddWithValue("contractId", contractId);
                    command.Parameters.AddWithValue("monitorId", monitorId);
                    command.Parameters.AddWithValue("deploymentId", Guid.NewGuid());
                    command.Parameters.AddWithValue("serialId", serialId);
                    command.Parameters.AddWithValue("onHire", _onHire);
                    command.Parameters.AddWithValue("offHire", _offHire);
                });

            await ExecuteAsync(
                """
                INSERT INTO "report" (id, site_id, report_link)
                    VALUES (@reportId, @siteId, 'https://reports.example/site-report.pdf');
                """,
                command =>
                {
                    command.Parameters.AddWithValue("reportId", Guid.NewGuid());
                    command.Parameters.AddWithValue("siteId", siteId);
                });

            foreach (DateTime sample in new[] { _midWindow, _finalDay, _afterEnd })
            {
                await SeedSampleAsync(monitorId, serialId, sample);
            }

            return siteId;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (string workspace in _workspaces)
            {
                Directory.Delete(workspace, recursive: true);
            }

            SpaTestDatabase.DropSchema(_schemaName, _scopedConnectionString);
            await Task.CompletedTask;
        }

        // Function summary: Returns the canonical column list for the two noise measurement relations.
        private static string NoiseLevelColumns(string tableName)
        {
            return $"""
                "{tableName}"
                (
                    serial_id character varying(32) NOT NULL,
                    sample_time timestamp without time zone NOT NULL,
                    laeq double precision,
                    lamax double precision,
                    la_90 double precision,
                    la_10 double precision,
                    lceq double precision,
                    lcmax double precision,
                    lc_90 double precision,
                    lc_10 double precision
                )
                """;
        }

        // Function summary: Seeds one notification and one row in every measurement relation at the given time.
        private async Task SeedSampleAsync(Guid monitorId, string serialId, DateTime sample)
        {
            Guid traceIndexId = Guid.NewGuid();
            await ExecuteAsync(
                """
                INSERT INTO "notification"
                    (id, monitor_id, alert_type, notification_time, limit_on, level, averaging_period,
                     alert_field, closed_time, closed_by_user, closed_note)
                    VALUES (@id, @monitorId, 0, @utcSample, 10, 12, 900, 'Pm10', NULL, NULL, NULL);
                INSERT INTO "my_atm_dust_level"
                    (serial_id, avrg, sample_time, pm_1, pm_2_5, pm_10, pm_total)
                    VALUES (@serialId, 900, @sample, 1, 2, 3, 6);
                INSERT INTO "air_q_noise_level"
                    (serial_id, sample_time, laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10)
                    VALUES (@serialId, @sample, 50, 60, 40, 55, 70, 80, 60, 75);
                INSERT INTO "svantek_noise_level"
                    (serial_id, sample_time, laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10)
                    VALUES (@serialId, @sample, 51, 61, 41, 56, 71, 81, 61, 76);
                INSERT INTO "omnidots_peak_level"
                    (serial_id, sample_time, x_fdom, x_vtop, x_vtop_overflow, y_fdom, y_vtop, y_vtop_overflow,
                     z_fdom, z_vtop, z_vtop_overflow)
                    VALUES (@serialId, @sample, 1, 2, 0, 3, 4, 0, 5, 6, 0);
                INSERT INTO "omnidots_trace_index" (id, serial_id, start_time, end_time)
                    VALUES (@traceIndexId, @serialId, @sample, @sample);
                INSERT INTO "omnidots_trace" (omnidots_trace_index_id, x, y, z)
                    VALUES (@traceIndexId, 1, 2, 3);
                """,
                command =>
                {
                    command.Parameters.AddWithValue("id", Guid.NewGuid());
                    command.Parameters.AddWithValue("traceIndexId", traceIndexId);
                    command.Parameters.AddWithValue("monitorId", monitorId);
                    command.Parameters.AddWithValue("serialId", serialId);
                    command.Parameters.AddWithValue("utcSample", sample);
                    command.Parameters.AddWithValue("sample", Unspecified(sample));
                });
        }

        // Function summary: Runs one statement batch on the schema-scoped connection.
        private async Task ExecuteAsync(string sql, Action<NpgsqlCommand>? bind = null)
        {
            await using NpgsqlConnection connection = new(_scopedConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            bind?.Invoke(command);
            await command.ExecuteNonQueryAsync();
        }
    }
}
