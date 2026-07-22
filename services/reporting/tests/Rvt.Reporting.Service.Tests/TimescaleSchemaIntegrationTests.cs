using Npgsql;
using Rvt.Reporting.Data.Postgres;

namespace Rvt.Reporting.Service.Tests;

/// <summary>
/// Validates the reporting service database contract against a real Timescale schema when explicitly configured.
/// Major updates: 2026-06-25 added gated Timescale prerequisite checks for reporting SQL cutover; added notification closed-note schema and repository coverage; 2026-06-26 added monitor ownership-window SQL guard.
/// </summary>
public sealed class TimescaleSchemaIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "RVT_REPORTING_TIMESCALE_TEST_CONNECTION";

    [Fact]
    public void PrerequisiteScriptDocumentsIdempotentDatabaseChanges()
    {
        var sql = File.ReadAllText(FindRepositoryPath("database/postgres/reporting_service_prerequisites_20260625.sql"));
        var normalized = NormalizeSql(sql);

        Assert.Contains("create extension if not exists pgcrypto", normalized, StringComparison.Ordinal);
        Assert.Contains("alter table report_rule add column if not exists is_hidden_system_rule boolean not null default false", normalized, StringComparison.Ordinal);
        Assert.Contains("create unique index if not exists ux_report_rule_hidden_one_time_per_site", normalized, StringComparison.Ordinal);
        Assert.Contains("on report_rule (site_id, frequency, is_hidden_system_rule)", normalized, StringComparison.Ordinal);
        Assert.Contains("where is_hidden_system_rule = true and frequency = 5", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryScopesMonitorBoundDataToEffectiveOwnershipWindows()
    {
        var source = File.ReadAllText(FindRepositoryPath("src/Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs"));
        var normalized = NormalizeSql(source);

        Assert.Contains("effective_from", normalized, StringComparison.Ordinal);
        Assert.Contains("effective_to", normalized, StringComparison.Ordinal);
        Assert.Contains("contract c", normalized, StringComparison.Ordinal);
        Assert.Contains("pointsformonitor", normalized, StringComparison.Ordinal);
        Assert.Contains("iswithinmonitorwindow", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("pointsforserial(", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealTimescaleSchemaSatisfiesReportingSqlPrerequisites()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = CancellationToken.None;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        Assert.True(await ScalarAsync<bool>(connection, "select gen_random_uuid() is not null", cancellationToken));
        Assert.True(await HiddenOneTimeRuleIndexExistsAsync(connection, cancellationToken));

        foreach (var requiredObject in RequiredObjects)
        {
            var columns = await ReadColumnsAsync(connection, requiredObject.Key, cancellationToken);
            foreach (var column in requiredObject.Value)
            {
                Assert.Contains(column, columns);
            }
        }
    }

    [Fact]
    public async Task RepositoryCanLoadSiteProjectionFromRealTimescaleSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = CancellationToken.None;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var siteId = await ReadFirstActiveSiteIdAsync(connection, cancellationToken);
        if (siteId is null)
        {
            return;
        }

        var repository = new PostgresReportingRepository(dataSource);
        var site = await repository.LoadSiteReportDataAsync(
            siteId.Value,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            cancellationToken);

        Assert.Equal(siteId.Value, site.Id);
        Assert.False(string.IsNullOrWhiteSpace(site.SiteName));
    }

    [Fact]
    public async Task RepositoryLoadsAlertRulesWithThresholdMatchedTriggeredCountsFromRealTimescaleSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = CancellationToken.None;
        var fromUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.Zero);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var monitor = await ReadFirstReportMonitorAsync(connection, fromUtc, toUtc, cancellationToken);
        if (monitor is null)
        {
            return;
        }

        var ruleId = Guid.NewGuid();
        const string alertField = "CodexTriggeredThresholdTest";
        try
        {
            await InsertAlertRuleAsync(connection, ruleId, monitor.Value.MonitorId, alertField, 42.5, 60, AlertType: 0, cancellationToken);
            await InsertNotificationAsync(connection, monitor.Value.MonitorId, alertField, 42.5, 60, AlertType: 0, fromUtc.AddHours(1), cancellationToken, closedNote: "First investigation");
            await InsertNotificationAsync(connection, monitor.Value.MonitorId, alertField, 42.5, 60, AlertType: 0, toUtc.AddHours(-1), cancellationToken, closedNote: "Latest investigation");
            await InsertNotificationAsync(connection, monitor.Value.MonitorId, alertField, 43.5, 60, AlertType: 0, fromUtc.AddHours(2), cancellationToken);
            await InsertNotificationAsync(connection, monitor.Value.MonitorId, alertField, 42.5, 60, AlertType: 0, fromUtc.AddDays(-1), cancellationToken);

            var repository = new PostgresReportingRepository(dataSource);
            var site = await repository.LoadSiteReportDataAsync(monitor.Value.SiteId, fromUtc, toUtc, cancellationToken);

            var seededMonitor = Assert.Single(site.Monitors, item => item.Id == monitor.Value.MonitorId);
            var seededRule = Assert.Single(seededMonitor.AlertRules, rule => rule.Field == alertField);
            Assert.Equal(42.5m, seededRule.Threshold);
            Assert.Equal(2, seededRule.TriggeredCount);
            Assert.Equal("Latest investigation", seededRule.LatestClosedNote);
            Assert.Equal(3, seededMonitor.Notifications.Count(notification => notification.Field == alertField));
            Assert.Contains(seededMonitor.Notifications, notification => notification.Field == alertField && notification.ClosedNote == "Latest investigation");
        }
        finally
        {
            await DeleteNotificationsAsync(connection, monitor.Value.MonitorId, alertField, cancellationToken);
            await DeleteAlertRuleAsync(connection, ruleId, cancellationToken);
        }
    }

    [Fact]
    public async Task RepositoryLoadsReportPeriodAveragePointsFromRealTimescaleSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = CancellationToken.None;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var expected = await ReadFirstNoiseAveragePointAsync(connection, cancellationToken);
        if (expected is null)
        {
            return;
        }

        var fromUtc = expected.Value.SampleTime.AddDays(-1);
        var toUtc = expected.Value.SampleTime.AddDays(1);
        var repository = new PostgresReportingRepository(dataSource);
        var site = await repository.LoadSiteReportDataAsync(expected.Value.SiteId, fromUtc, toUtc, cancellationToken);

        var monitor = Assert.Single(site.Monitors, item => item.Id == expected.Value.MonitorId);
        Assert.Contains(monitor.NoiseDailyAverage, point =>
            point.MeasuredAt == expected.Value.SampleTime &&
            Math.Abs(point.Value - expected.Value.Laeq) < 0.0001m);
    }

    private static readonly IReadOnlyDictionary<string, string[]> RequiredObjects = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["site_search"] =
        [
            "id", "site_name", "create_date", "address_line_1", "address_line_2", "postcode", "city", "county",
            "contracts", "company_name", "company_id", "archived"
        ],
        ["monitor_report"] =
        [
            "id", "site_id", "active", "deployment_id", "fleet_row_count", "serial_id", "type_of_monitor", "off_line",
            "alerts", "cautions", "latitude", "longitude", "start_date", "end_date", "what_3_words",
            "last_data_time", "location", "calibration_date"
        ],
        ["deployment"] = ["id", "contract_id"],
        ["contract"] = ["id", "on_hire_date", "off_hire_date"],
        ["report_rule"] =
        [
            "id", "site_id", "user_id", "frequency", "day_of_week", "day_of_month", "last_generated",
            "report_name", "is_hidden_system_rule", "deleted"
        ],
        ["report"] = ["id", "site_id", "report_rule_id", "frequency", "report_date", "report_from", "report_to", "report_link"],
        ["report_sent"] = ["id", "report_id", "send_time", "address", "error_message"],
        ["report_user"] = ["report_rule_id", "user_id"],
        ["rvt_alert_rule"] = ["id", "monitor_id", "alert_type", "alert_field", "limit_on", "averaging_period", "is_active", "is_deleted"],
        ["notification"] = ["id", "monitor_id", "alert_type", "alert_field", "limit_on", "averaging_period", "level", "notification_time", "closed_time", "closed_note"],
        ["noise_level_1_hour_avg"] = ["serial_id", "sample_time", "laeq"],
        ["noise_level_1_day_avg"] = ["serial_id", "sample_time", "laeq"],
        ["noise_level_site_avg"] = ["serial_id", "sample_time", "laeq"],
        ["my_atm_dust_level"] = ["serial_id", "avrg", "sample_time", "pm_10"],
        ["my_atm_dust_level_1_day_avg"] = ["serial_id", "sample_time", "pm_10"],
        ["omnidots_peak_level_1_day_peak"] = ["serial_id", "sample_time", "x_vtop", "y_vtop", "z_vtop"],
        ["AspNetUsers"] = ["Id", "Email"]
    };

    private static async Task<HashSet<string>> ReadColumnsAsync(NpgsqlConnection connection, string objectName, CancellationToken cancellationToken)
    {
        const string sql = """
            select column_name
            from information_schema.columns
            where table_schema = 'public' and table_name = @object_name
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("object_name", objectName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<Guid?> ReadFirstActiveSiteIdAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "select id from site_search where coalesce(archived, false) = false order by site_name limit 1";
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is Guid siteId ? siteId : null;
    }

    private static async Task<(Guid SiteId, Guid MonitorId)?> ReadFirstReportMonitorAsync(NpgsqlConnection connection, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        const string sql = """
            select m.site_id, m.id
            from monitor_report m
            inner join site_search s on s.id = m.site_id
            where coalesce(s.archived, false) = false
              and (m.start_date is null or m.start_date <= @to_utc)
              and (m.end_date is null or m.end_date >= @from_utc)
            order by m.site_name, m.serial_id
            limit 1
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("from_utc", fromUtc.UtcDateTime);
        command.Parameters.AddWithValue("to_utc", toUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<(Guid SiteId, Guid MonitorId, DateTimeOffset SampleTime, decimal Laeq)?> ReadFirstNoiseAveragePointAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select m.site_id, m.id, a.sample_time, a.laeq
            from monitor_report m
            inner join site_search s on s.id = m.site_id
            inner join noise_level_1_day_avg a on a.serial_id = m.serial_id
            where coalesce(s.archived, false) = false
              and m.type_of_monitor = 1
              and a.laeq is not null
              and (m.start_date is null or m.start_date <= a.sample_time)
              and (m.end_date is null or m.end_date >= a.sample_time)
            order by a.sample_time desc, m.site_name, m.serial_id
            limit 1
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetGuid(0), reader.GetGuid(1), ReadUtcDateTimeOffset(reader, 2), Convert.ToDecimal(reader.GetDouble(3), provider: null));
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(NpgsqlDataReader reader, int ordinal)
    {
        var value = DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        return new DateTimeOffset(value);
    }

    private static async Task InsertAlertRuleAsync(NpgsqlConnection connection, Guid ruleId, Guid monitorId, string alertField, double threshold, int averagingPeriod, int AlertType, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into rvt_alert_rule (
                id, monitor_id, alert_field, limit_on, limit_off, alert_type, is_active,
                averaging_period, weekdays, saturdays, sundays, start_time, end_time, is_deleted, created, accessed
            )
            values (
                @id, @monitor_id, @alert_field, @limit_on, @limit_off, @alert_type, true,
                @averaging_period, true, true, true, '00:00:00', '23:59:59', false, now(), now()
            )
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", ruleId);
        command.Parameters.AddWithValue("monitor_id", monitorId);
        command.Parameters.AddWithValue("alert_field", alertField);
        command.Parameters.AddWithValue("limit_on", threshold);
        command.Parameters.AddWithValue("limit_off", threshold - 1);
        command.Parameters.AddWithValue("alert_type", AlertType);
        command.Parameters.AddWithValue("averaging_period", averagingPeriod);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertNotificationAsync(
        NpgsqlConnection connection,
        Guid monitorId,
        string alertField,
        double threshold,
        int averagingPeriod,
        int AlertType,
        DateTimeOffset notificationTime,
        CancellationToken cancellationToken,
        string? closedNote = null)
    {
        const string sql = """
            insert into notification (
                id, monitor_id, alert_field, limit_on, averaging_period, alert_type, level, notification_time, closed_time, closed_note
            )
            values (gen_random_uuid(), @monitor_id, @alert_field, @limit_on, @averaging_period, @alert_type, @level, @notification_time, @closed_time, @closed_note)
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("monitor_id", monitorId);
        command.Parameters.AddWithValue("alert_field", alertField);
        command.Parameters.AddWithValue("limit_on", threshold);
        command.Parameters.AddWithValue("averaging_period", averagingPeriod);
        command.Parameters.AddWithValue("alert_type", AlertType);
        command.Parameters.AddWithValue("level", threshold + 1);
        command.Parameters.AddWithValue("notification_time", notificationTime.UtcDateTime);
        command.Parameters.AddWithValue("closed_time", closedNote is null ? DBNull.Value : notificationTime.AddHours(1).UtcDateTime);
        command.Parameters.AddWithValue("closed_note", closedNote is null ? DBNull.Value : closedNote);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteNotificationsAsync(NpgsqlConnection connection, Guid monitorId, string alertField, CancellationToken cancellationToken)
    {
        const string sql = "delete from notification where monitor_id = @monitor_id and alert_field = @alert_field";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("monitor_id", monitorId);
        command.Parameters.AddWithValue("alert_field", alertField);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteAlertRuleAsync(NpgsqlConnection connection, Guid ruleId, CancellationToken cancellationToken)
    {
        const string sql = "delete from rvt_alert_rule where id = @id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", ruleId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> HiddenOneTimeRuleIndexExistsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            select exists (
                select 1
                from pg_indexes
                where schemaname = 'public'
                  and indexname = 'ux_report_rule_hidden_one_time_per_site'
                  and indexdef ilike '%on public.report_rule%'
                  and indexdef ilike '%site_id%'
                  and indexdef ilike '%frequency%'
                  and indexdef ilike '%is_hidden_system_rule%'
                  and indexdef ilike '%where%'
                  and indexdef ilike '%is_hidden_system_rule = true%'
                  and indexdef ilike '%frequency = 5%'
            )
            """;

        return await ScalarAsync<bool>(connection, sql, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is T typed ? typed : throw new InvalidOperationException($"Expected scalar result of type {typeof(T).Name}.");
    }

    private static string FindRepositoryPath(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(current.FullName, "Rvt.Reporting.New.slnx")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}.");
    }

    private static string NormalizeSql(string sql) => string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
