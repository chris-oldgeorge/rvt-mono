using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Rvt.Monitor.Common.Data;

// Summary: Centralizes SQL Server/PostgreSQL provider operations shared by monitor apps.
// Major updates:
// - 2026-06-12 Monitor Migration: moved duplicated monitor provider plumbing into Rvt.Monitor.Common.
// - 2026-06-18 Canonical Timescale pass: added provider SQL selector for PostgreSQL ON CONFLICT migrations.
// - 2026-06-18 Canonical Timescale pass: aligned PostgreSQL exception writes with error_log.
// - 2026-06-18 Canonical Timescale pass: canonicalized common read/report SQL identifiers for PostgreSQL.
public static class MonitorDb
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex BracketedIdentifierRegex = new(
        @"\[(?<identifier>[A-Za-z_][A-Za-z0-9_]*)\]",
        RegexOptions.Compiled,
        RegexTimeout);
    private static readonly Regex SafeSqlIdentifierRegex = new(
        @"^(\[[A-Za-z_][A-Za-z0-9_]*\]|[A-Za-z_][A-Za-z0-9_]*)(\.(\[[A-Za-z_][A-Za-z0-9_]*\]|[A-Za-z_][A-Za-z0-9_]*))*$",
        RegexOptions.Compiled,
        RegexTimeout);
    private static readonly Regex SqlServerBracketedSchemaRegex = new(
        @"\s*\[dbo\]\s*\.\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);
    private static readonly Regex SqlServerSchemaRegex = new(
        @"(?<![A-Za-z0-9_])dbo\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);
    private static readonly Regex DatePartDayOfWeekRegex = new(
        @"DATEPART\s*\(\s*dw\s*,\s*(?<parameter>@[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    private static readonly IReadOnlyDictionary<string, string> CommonIdentifierMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Id"] = "id",
            ["FleetNr"] = "fleet_row_count",
            ["SerialId"] = "serial_id",
            ["CustomerId"] = "customer_id",
            ["ListedAtTime"] = "listed_at_time",
            ["Model"] = "model",
            ["LocationId"] = "location_id",
            ["Latitude"] = "latitude",
            ["Longitude"] = "longitude",
            ["LocationAddress"] = "location_address",
            ["TimeZone"] = "time_zone",
            ["CustomerDisplayName"] = "customer_display_name",
            ["Manufacturer"] = "manufacturer",
            ["FirmwareVersion"] = "firmware_version",
            ["TypeOfMonitor"] = "type_of_monitor",
            ["Offline"] = "offline",
            ["BatteryStatus"] = "battery_status",
            ["LastDataTime1Min"] = "last_data_time_1_min",
            ["LastDataTime15Min"] = "last_data_time_15_min",
            ["LastDataTime1Hour"] = "last_data_time_1_hour",
            ["LastDataTime24Hour"] = "last_data_time_24_hour",
            ["CalibrationDate"] = "calibration_date",
            ["CalibrationDue"] = "calibration_due",
            ["MonitorId"] = "monitor_id",
            ["ContractId"] = "contract_id",
            ["SiteId"] = "site_id",
            ["SiteiD"] = "site_id",
            ["SiteName"] = "site_name",
            ["UserId"] = "user_id",
            ["SiteUserId"] = "site_user_id",
            ["NotificationId"] = "notification_id",
            ["NotificationTime"] = "notification_time",
            ["SendTime"] = "send_time",
            ["Address"] = "address",
            ["ErrorMessage"] = "error_message",
            ["AlertField"] = "alert_field",
            ["AlertType"] = "alert_type",
            ["LimitOn"] = "limit_on",
            ["LimitOff"] = "limit_off",
            ["AveragingPeriod"] = "averaging_period",
            ["Level"] = "level",
            ["Field"] = "field",
            ["CollectionTime"] = "collection_time",
            ["ClosedTime"] = "closed_time",
            ["ClosedByUser"] = "closed_by_user",
            ["IsActive"] = "is_active",
            ["IsDeleted"] = "is_deleted",
            ["Created"] = "created",
            ["Accessed"] = "accessed",
            ["Weekdays"] = "weekdays",
            ["Saturdays"] = "saturdays",
            ["Sundays"] = "sundays",
            ["StartTime"] = "start_time",
            ["EndTime"] = "end_time",
            ["SatStartTime"] = "sat_start_time",
            ["SatEndTime"] = "sat_end_time",
            ["SunStartTime"] = "sun_start_time",
            ["SunEndTime"] = "sun_end_time",
            ["StartDate"] = "start_date",
            ["EndDate"] = "end_date",
            ["Email"] = "email",
            ["SMS"] = "sms",
            ["PhoneNumber"] = "phone_number",
            ["UpdateTime"] = "update_time",
            ["Status"] = "status",
            ["ErrorCount"] = "error_count",
            ["BatteryVoltage"] = "battery_voltage",
            ["FilterChangeDate"] = "filter_change_date",
            ["PumpHours"] = "pump_hours",
            ["SampleTime"] = "sample_time",
            ["LAeq"] = "laeq",
            ["LAmax"] = "lamax",
            ["LA90"] = "la_90",
            ["LA10"] = "la_10",
            ["LCeq"] = "lceq",
            ["LCmax"] = "lcmax",
            ["LC90"] = "lc_90",
            ["LC10"] = "lc_10",
            ["NumberOfSamples"] = "number_of_samples",
            ["Avrg"] = "avrg",
            ["Pm1"] = "pm_1",
            ["Pm2_5"] = "pm_2_5",
            ["Pm10"] = "pm_10",
            ["PmTotal"] = "pm_total",
            ["Weather_t"] = "weather_t",
            ["Weather_p"] = "weather_p",
            ["Weather_rh"] = "weather_rh",
            ["Name"] = "name",
            ["Lastseen"] = "lastseen",
            ["BatteryCharge"] = "battery_charge",
            ["ConnectedUsing"] = "connected_using",
            ["Online"] = "online",
            ["MeasurementDuration"] = "measurement_duration",
            ["DataSaveLevel"] = "data_save_level",
            ["VdvEnabled"] = "vdv_enabled",
            ["VdvX"] = "vdv_x",
            ["VdvY"] = "vdv_y",
            ["VdvZ"] = "vdv_z",
            ["VdvPeriod"] = "vdv_period",
            ["TraceSaveLevel"] = "trace_save_level",
            ["TracePreTrigger"] = "trace_pre_trigger",
            ["TracePostTrigger"] = "trace_post_trigger",
            ["AlarmValue"] = "alarm_value",
            ["FlatLevel"] = "flat_level",
            ["DisableLed"] = "disable_led",
            ["LogFlushInterval"] = "log_flush_interval",
            ["GuideLine"] = "guide_line",
            ["BuildingLevel"] = "building_level",
            ["VectorEnabled"] = "vector_enabled",
            ["AtopEnabled"] = "atop_enabled",
            ["VtopEnabled"] = "vtop_enabled",
            ["XFdom"] = "x_fdom",
            ["XVtop"] = "x_vtop",
            ["XVtopOverflow"] = "x_vtop_overflow",
            ["YFdom"] = "y_fdom",
            ["YVtop"] = "y_vtop",
            ["YVtopOverflow"] = "y_vtop_overflow",
            ["ZFdom"] = "z_fdom",
            ["ZVtop"] = "z_vtop",
            ["ZVtopOverflow"] = "z_vtop_overflow"
        };

    private static readonly string[] BooleanColumns =
    [
        "offline",
        "email",
        "sms",
        "is_active",
        "is_deleted",
        "weekdays",
        "saturdays",
        "sundays",
        "vdv_enabled",
        "disable_led",
        "vector_enabled",
        "atop_enabled",
        "vtop_enabled",
        "online"
    ];

    private static readonly ISet<string> IdentityColumns =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Id",
            "Email",
            "PhoneNumber",
            "UserName"
        };

    public static MonitorDatabaseProvider ResolveProvider(string? primaryProvider, string? fallbackProvider)
    {
        var provider = FirstValue(primaryProvider, fallbackProvider, "postgresql").Trim().ToLowerInvariant();
        return provider switch
        {
            "sqlserver" or "mssql" => MonitorDatabaseProvider.SqlServer,
            "postgres" or "postgresql" or "timescale" or "timescaledb" => MonitorDatabaseProvider.PostgreSql,
            _ => throw new NotSupportedException($"Unsupported monitor database provider '{provider}'.")
        };
    }

    public static DbConnection OpenConnection(string connectionString, MonitorDbOptions options)
    {
        DbConnection connection = options.IsPostgreSql
            ? new NpgsqlConnection(connectionString)
            : new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public static DbCommand CreateCommand(string sql, DbConnection connection, MonitorDbOptions options)
    {
        var command = connection.CreateCommand();
        command.CommandText = RewriteSql(sql, options);
        return command;
    }

    public static string SelectProviderSql(string sqlServerSql, string postgreSql, MonitorDbOptions options)
    {
        return options.IsPostgreSql ? postgreSql : sqlServerSql;
    }

    public static void BulkInsert(string connectionString, string tableName, DataTable table, MonitorDbOptions options)
    {
        if (!options.IsPostgreSql)
        {
            using var bulkInsert = new SqlBulkCopy(connectionString)
            {
                DestinationTableName = RequireSafeSqlIdentifier(tableName, "bulk insert table")
            };
            bulkInsert.WriteToServer(table);
            return;
        }

        var columns = table.Columns
            .Cast<DataColumn>()
            .Select(column => RequireSafeSqlIdentifier(RewriteIdentifier(column.ColumnName), "bulk insert column"))
            .ToArray();
        var targetTable = RequireSafeSqlIdentifier(RewriteTableName(tableName, options), "bulk insert table");
        using var connection = (NpgsqlConnection)OpenConnection(connectionString, options);
        using var writer = connection.BeginBinaryImport(
            $"COPY {targetTable} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)");
        foreach (DataRow row in table.Rows)
        {
            writer.StartRow();
            foreach (DataColumn column in table.Columns)
            {
                writer.Write(row[column] == DBNull.Value ? null : row[column]);
            }
        }

        writer.Complete();
    }

    public static void WriteException(
        string connectionString,
        string tag,
        Exception exception,
        string serviceName,
        string serviceVersion,
        MonitorDbOptions options)
    {
        var sql = options.IsPostgreSql
            ? @"INSERT INTO error_log (host, source, message, level, stack_trace, variables, logged_at)
                VALUES (@Host, @Source, @Message, @Level, @StackTrace, @Variables, @LogTime);"
            : @"INSERT INTO dbo.ErrorMessages (Host, Source, Message, Level, StackTrace, Variables, LogTime)
                VALUES (@Host, @Source, @Message, @Level, @StackTrace, @Variables, @LogTime);";

        using var connection = OpenConnection(connectionString, options);
        using var command = CreateCommand(sql, connection, options);
        command.Parameters.AddWithValue("@Host", HostName(), options);
        command.Parameters.AddWithValue("@Source", serviceName + " " + serviceVersion, options);
        command.Parameters.AddWithValue("@Message", exception.Message, options);
        command.Parameters.AddWithValue("@Level", "Exception", options);
        command.Parameters.AddWithValue("@StackTrace", exception.StackTrace ?? "", options);
        command.Parameters.AddWithValue("@Variables", tag, options);
        command.Parameters.AddWithValue("@LogTime", DateTime.UtcNow, options);
        command.ExecuteNonQuery();
    }

    public static string RewriteSql(string sql, MonitorDbOptions options)
    {
        if (!options.IsPostgreSql)
        {
            return sql;
        }

        var rewritten = NormalizeSqlServerSyntax(sql);
        rewritten = ApplyIdentifierMap(rewritten, options.IdentifierMap);
        rewritten = ApplyIdentifierMap(rewritten, CommonIdentifierMap);
        rewritten = RewriteDatePartDayOfWeek(rewritten);
        return RewriteBooleanComparisons(rewritten);
    }

    public static string RewriteTableName(string tableName, MonitorDbOptions options)
    {
        var normalized = NormalizeSqlServerSyntax(tableName).Trim();
        return options.IdentifierMap.TryGetValue(normalized, out var mapped) ? mapped : RewriteIdentifier(normalized);
    }

    public static string RequireMappedSqlIdentifier(
        string identifier,
        IReadOnlyDictionary<string, string> allowedIdentifiers,
        string context)
    {
        if (!allowedIdentifiers.TryGetValue(identifier, out var mappedIdentifier))
        {
            throw new NotSupportedException($"Unsupported SQL identifier '{identifier}' for {context}.");
        }

        return RequireSafeSqlIdentifier(mappedIdentifier, context);
    }

    public static string RequireSafeSqlIdentifier(string identifier, string context)
    {
        if (!SafeSqlIdentifierRegex.IsMatch(identifier))
        {
            throw new InvalidOperationException($"Unsafe SQL identifier '{identifier}' for {context}.");
        }

        return identifier;
    }

    public static string RewriteIdentifier(string identifier)
    {
        if (identifier.StartsWith('"'))
        {
            return identifier;
        }

        return string.Concat(identifier.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "_" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
    }

    private static string FirstValue(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static string NormalizeSqlServerSyntax(string sql)
    {
        var normalized = SqlServerBracketedSchemaRegex.Replace(sql, " ");
        normalized = SqlServerSchemaRegex.Replace(normalized, string.Empty);
        return BracketedIdentifierRegex.Replace(normalized, match => match.Groups["identifier"].Value);
    }

    private static string ApplyIdentifierMap(string sql, IReadOnlyDictionary<string, string> identifierMap)
    {
        var rewritten = sql;
        foreach (var pair in identifierMap.OrderByDescending(pair => pair.Key.Length))
        {
            var pattern = $@"(?<![@A-Za-z0-9_]){Regex.Escape(pair.Key)}(?![A-Za-z0-9_])";
            rewritten = Regex.Replace(
                rewritten,
                pattern,
                match => IsIdentityAliasReference(rewritten, match.Index, pair.Key) ? match.Value : pair.Value,
                RegexOptions.None,
                RegexTimeout);
        }

        return rewritten;
    }

    private static bool IsIdentityAliasReference(string sql, int matchIndex, string identifier)
    {
        return IdentityColumns.Contains(identifier)
            && matchIndex >= 2
            && char.ToLowerInvariant(sql[matchIndex - 2]) == 'u'
            && sql[matchIndex - 1] == '.';
    }

    private static string RewriteDatePartDayOfWeek(string sql)
    {
        return DatePartDayOfWeekRegex.Replace(sql, match => $"(EXTRACT(DOW FROM {match.Groups["parameter"].Value}) + 1)");
    }

    private static string RewriteBooleanComparisons(string sql)
    {
        var rewritten = sql;
        foreach (var column in BooleanColumns)
        {
            rewritten = Regex.Replace(
                rewritten,
                $@"\b{Regex.Escape(column)}\s*=\s*1\b",
                $"{column} = TRUE",
                RegexOptions.IgnoreCase,
                RegexTimeout);
            rewritten = Regex.Replace(
                rewritten,
                $@"\b{Regex.Escape(column)}\s*=\s*0\b",
                $"{column} = FALSE",
                RegexOptions.IgnoreCase,
                RegexTimeout);
        }

        return rewritten;
    }

    private static string HostName()
    {
        var hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? Environment.MachineName;
        return hostName.Length > 100 ? hostName[..100] : hostName;
    }
}
