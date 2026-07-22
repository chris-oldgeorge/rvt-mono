// File summary: Provides provider-aware SQL definitions for site archive CSV and report exports.
// Major updates:
// - 2026-07-09 pending Moved site archive SQL into a dedicated provider-aware query catalog.

using RVT.DataAccess.Configuration;

namespace RvtPortal.Spa.Adapters.Archive;

internal interface ISiteArchiveQueryCatalog
{
    IReadOnlyList<ArchiveCsvExport> CsvExports { get; }

    string ReportLinksSql { get; }
}

internal abstract record ArchiveCsvExport(string FileName, Type RowType, string Sql)
{
    // Function summary: Streams this export into its target CSV file.
    public abstract Task WriteAsync(
        ISiteArchiveQueryExecutor queryExecutor,
        ISiteArchiveCsvWriter csvWriter,
        string filesDirectory,
        Guid siteId,
        CancellationToken cancellationToken);
}

internal sealed record ArchiveCsvExport<T>(string ExportFileName, string ExportSql)
    : ArchiveCsvExport(ExportFileName, typeof(T), ExportSql)
    where T : class
{
    // Function summary: Streams typed query rows directly into the archive CSV writer.
    public override Task WriteAsync(
        ISiteArchiveQueryExecutor queryExecutor,
        ISiteArchiveCsvWriter csvWriter,
        string filesDirectory,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        return csvWriter.WriteAsync(
            Path.Combine(filesDirectory, FileName),
            queryExecutor.StreamAsync<T>(Sql, siteId, cancellationToken),
            cancellationToken);
    }
}

internal sealed class SiteArchiveQueryCatalog : ISiteArchiveQueryCatalog
{
    private readonly SiteArchiveSqlDialect sql;

    // Function summary: Initializes archive SQL definitions for the configured database provider.
    public SiteArchiveQueryCatalog(IRvtDatabaseConnectionFactory connectionFactory)
    {
        sql = SiteArchiveSqlDialect.For(connectionFactory.Provider);
        CsvExports =
        [
            Export<MonitorArchiveRow>("Monitors.csv", MonitorSql()),
            Export<BreachArchiveRow>("Breaches.csv", BreachSql()),
            Export<DustArchiveRow>("DustMonitorData.csv", DustSql()),
            Export<NoiseArchiveRow>("NoiseMonitorData.csv", NoiseSql("air_q_noise_level")),
            Export<NoiseArchiveRow>("NoiseMonitorDataS.csv", NoiseSql("svantek_noise_level")),
            Export<VibrationArchiveRow>("VibrationMonitorData.csv", VibrationSql()),
            Export<TraceListArchiveRow>("TraceList.csv", TraceListSql()),
            Export<TraceDataArchiveRow>("TraceData.csv", TraceDataSql())
        ];
        ReportLinksSql = $"SELECT report_link as \"ReportLink\" FROM {sql.Table("report")} WHERE site_id = @SiteId";
    }

    public IReadOnlyList<ArchiveCsvExport> CsvExports { get; }

    public string ReportLinksSql { get; }

    // Function summary: Creates a typed CSV export descriptor.
    private static ArchiveCsvExport Export<T>(string fileName, string query)
        where T : class
    {
        return new ArchiveCsvExport<T>(fileName, query);
    }

    // Function summary: Builds the monitor metadata archive query.
    private string MonitorSql()
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   {MonitorTypeCase()} as "Type",
                   d.latitude as "Latitude",
                   d.longitude as "Longitude",
                   d.what_3_words as "What3words",
                   c.contract_number as "ContractNumber",
                   c.on_hire_date as "OnHireDate",
                   c.off_hire_date as "OffHireDate"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            WHERE s.id = @SiteId
            """;
    }

    // Function summary: Builds the breach and caution archive query.
    private string BreachSql()
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   {MonitorTypeCase()} as "Type",
                   CASE WHEN n.alert_type = 0 THEN 'Alert' WHEN n.alert_type = 1 THEN 'Caution' ELSE '-' END as "AlertType",
                   n.notification_time as "NotificationTime",
                   n.limit_on as "LimitOn",
                   n.level as "Level",
                   {PeriodCase("n.averaging_period")} as "Period",
                   n.alert_field as "Parameter",
                   n.closed_time as "ClosedTime",
                   n.closed_by_user as "ClosedByUser",
                   n.closed_note as "ClosedNote"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            LEFT JOIN {sql.Table("notification")} n ON n.monitor_id = d.monitor_id
                AND n.notification_time >= {EffectiveStartExpression()}
                AND n.notification_time < {EffectiveEndExpression()}
            WHERE n.alert_type < 2 AND s.id = @SiteId
            """;
    }

    // Function summary: Builds the dust measurement archive query.
    private string DustSql()
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   {PeriodCase("l.avrg")} as "Period",
                   l.sample_time as "SampleTime",
                   l.pm_1 as "Pm1",
                   l.pm_2_5 as "Pm2_5",
                   l.pm_10 as "Pm10",
                   l.pm_total as "PmTotal"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            RIGHT JOIN {sql.Table("my_atm_dust_level")} l ON l.serial_id = m.serial_id
                AND l.sample_time >= {EffectiveStartExpression()}
                AND l.sample_time < {EffectiveEndExpression()}
            WHERE s.id = @SiteId
            ORDER BY l.serial_id, l.sample_time DESC
            """;
    }

    // Function summary: Builds an AirQ/Svantek noise measurement archive query.
    private string NoiseSql(string tableName)
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   l.sample_time as "SampleTime",
                   l.laeq as "LAeq",
                   l.lamax as "LAmax",
                   l.la_90 as "LA90",
                   l.la_10 as "LA10",
                   l.lceq as "LCeq",
                   l.lcmax as "LCmax",
                   l.lc_90 as "LC90",
                   l.lc_10 as "LC10"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            RIGHT JOIN {sql.Table(tableName)} l ON l.serial_id = m.serial_id
                AND l.sample_time >= {EffectiveStartExpression()}
                AND l.sample_time < {EffectiveEndExpression()}
            WHERE s.id = @SiteId
            ORDER BY l.serial_id, l.sample_time DESC
            """;
    }

    // Function summary: Builds the vibration peak archive query.
    private string VibrationSql()
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   l.sample_time as "SampleTime",
                   l.x_fdom as "XFdom",
                   l.x_vtop as "XVtop",
                   l.x_vtop_overflow as "XVtopOverflow",
                   l.y_fdom as "YFdom",
                   l.y_vtop as "YVtop",
                   l.y_vtop_overflow as "YVtopOverflow",
                   l.z_fdom as "ZFdom",
                   l.z_vtop as "ZVtop",
                   l.z_vtop_overflow as "ZVtopOverflow"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            RIGHT JOIN {sql.Table("omnidots_peak_level")} l ON l.serial_id = m.serial_id
                AND l.sample_time >= {EffectiveStartExpression()}
                AND l.sample_time < {EffectiveEndExpression()}
            WHERE s.id = @SiteId
            ORDER BY l.serial_id, l.sample_time DESC
            """;
    }

    // Function summary: Builds the vibration trace index archive query.
    private string TraceListSql()
    {
        return $"""
            SELECT m.fleet_nr as "Monitor",
                   m.serial_id as "SerialId",
                   l.id as "TraceId",
                   l.start_time as "StartTime",
                   l.end_time as "EndTime"
            FROM {sql.Table("deployment")} d
            LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
            LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
            LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
            RIGHT JOIN {sql.Table("omnidots_trace_index")} l ON l.serial_id = m.serial_id
                AND l.start_time >= {EffectiveStartExpression()}
                AND l.start_time < {EffectiveEndExpression()}
            WHERE s.id = @SiteId
            ORDER BY l.serial_id, l.start_time DESC
            """;
    }

    // Function summary: Builds the vibration trace samples archive query.
    private string TraceDataSql()
    {
        return $"""
            SELECT t.omnidots_trace_index_id as "TraceId",
                   t.x as "X",
                   t.y as "Y",
                   t.z as "Z"
            FROM {sql.Table("omnidots_trace")} t
            WHERE t.omnidots_trace_index_id IN (
                SELECT l.id
                FROM {sql.Table("deployment")} d
                LEFT JOIN {sql.Table("contract")} c ON c.id = d.contract_id
                LEFT JOIN {sql.Table("site")} s ON s.id = c.site_id
                LEFT JOIN {sql.Table("monitor")} m ON m.id = d.monitor_id
                RIGHT JOIN {sql.Table("omnidots_trace_index")} l ON l.serial_id = m.serial_id
                    AND l.start_time >= {EffectiveStartExpression()}
                    AND l.start_time < {EffectiveEndExpression()}
                WHERE s.id = @SiteId
            )
            """;
    }

    // Function summary: Returns monitor type display SQL shared by archive exports.
    private static string MonitorTypeCase()
    {
        return "CASE WHEN m.type_of_monitor = 0 THEN 'Dust' WHEN m.type_of_monitor = 1 THEN 'Noise' WHEN m.type_of_monitor = 2 THEN 'Vibration' ELSE '-' END";
    }

    // Function summary: Returns averaging-period display SQL shared by archive exports.
    private static string PeriodCase(string column)
    {
        return $"CASE WHEN {column} = 0 THEN 'Site Average' WHEN {column} = 60 THEN '1 min' WHEN {column} = 900 THEN '15 min' WHEN {column} = 36000 THEN '1 hour' WHEN {column} = 28800 THEN '8 hour' WHEN {column} = 86400 THEN '1 day' ELSE '-' END";
    }

    // Function summary: Returns the effective monitor ownership start expression.
    private static string EffectiveStartExpression()
    {
        return "CASE WHEN c.on_hire_date IS NOT NULL AND c.on_hire_date > d.start_date THEN c.on_hire_date ELSE d.start_date END";
    }

    // Function summary: Returns the effective monitor ownership end expression.
    private string EffectiveEndExpression()
    {
        return $"CASE WHEN d.end_date IS NULL AND c.off_hire_date IS NULL THEN {sql.CurrentTimestamp} WHEN d.end_date IS NULL THEN c.off_hire_date WHEN c.off_hire_date IS NULL THEN d.end_date WHEN d.end_date < c.off_hire_date THEN d.end_date ELSE c.off_hire_date END";
    }
}

internal sealed class SiteArchiveSqlDialect
{
    private readonly RvtDatabaseProvider provider;

    // Function summary: Initializes provider-specific SQL rendering rules.
    private SiteArchiveSqlDialect(RvtDatabaseProvider provider)
    {
        this.provider = provider;
    }

    public string CurrentTimestamp => provider == RvtDatabaseProvider.Postgres ? "now()" : "getdate()";

    // Function summary: Creates a SQL dialect for the configured database provider.
    public static SiteArchiveSqlDialect For(RvtDatabaseProvider provider)
    {
        return provider switch
        {
            RvtDatabaseProvider.SqlServer or RvtDatabaseProvider.Postgres => new SiteArchiveSqlDialect(provider),
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };
    }

    // Function summary: Returns a provider-specific table reference for canonical RVT archive SQL.
    public string Table(string name)
    {
        return provider == RvtDatabaseProvider.Postgres
            ? $"public.{name}"
            : $"dbo.{name}";
    }
}
