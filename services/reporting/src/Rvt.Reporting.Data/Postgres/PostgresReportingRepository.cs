using Npgsql;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace Rvt.Reporting.Data.Postgres;

/// <summary>
/// Postgres/Timescale implementation of the reporting repository using parameterized canonical SQL.
/// Major updates: 2026-06-24 initial SQL Server-to-Postgres reporting port; 2026-06-25 loads alert rules with report-period triggered counts; 2026-06-25 hydrates report-period average graph points; 2026-06-25 hydrates report-period notifications and closed alert notes; 2026-06-26 scopes monitor-bound report data to effective contract windows.
/// </summary>
public sealed class PostgresReportingRepository : IReportingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReportingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("select 1", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, provider: null) == 1;
    }

    public async Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(DateTimeOffset maxLastGeneratedUtc, CancellationToken cancellationToken)
    {
        const string sql = """
            select r.id, r.site_id, r.user_id, r.frequency, r.day_of_week, r.day_of_month,
                   r.last_generated, r.report_name, coalesce(r.is_hidden_system_rule, false)
            from report_rule r
            inner join site s on s.id = r.site_id
            where coalesce(r.deleted, false) = false
              and coalesce(s.archived, false) = false
              and coalesce(r.is_hidden_system_rule, false) = false
              and r.frequency <> @one_time_frequency
              and (r.last_generated < @max_last_generated or r.last_generated is null)
            """;

        var rules = await ReadRulesAsync(sql, command =>
        {
            command.Parameters.AddWithValue("one_time_frequency", (int)FrequencyType.OneTime);
            command.Parameters.AddWithValue("max_last_generated", maxLastGeneratedUtc.UtcDateTime.Date);
        }, cancellationToken).ConfigureAwait(false);

        return await AttachRecipientsAsync(rules, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        const string sql = """
            select r.id, r.site_id, r.user_id, r.frequency, r.day_of_week, r.day_of_month,
                   r.last_generated, r.report_name, coalesce(r.is_hidden_system_rule, false)
            from report_rule r
            inner join site s on s.id = r.site_id
            where coalesce(r.deleted, false) = false
              and coalesce(s.archived, false) = false
              and r.id = @report_rule_id
            """;

        var rules = await ReadRulesAsync(sql, command => command.Parameters.AddWithValue("report_rule_id", reportRuleId), cancellationToken).ConfigureAwait(false);
        return (await AttachRecipientsAsync(rules, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
    }

    public async Task<Guid> GetOrCreateOneTimeReportRuleAsync(Guid siteId, Guid requestedByUserId, string reportName, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into report_rule (id, site_id, user_id, frequency, report_name, is_hidden_system_rule, deleted)
            values (gen_random_uuid(), @site_id, @user_id, @frequency, @report_name, true, false)
            on conflict (site_id, frequency, is_hidden_system_rule)
            where is_hidden_system_rule = true and frequency = 5
            do update set report_name = excluded.report_name
            returning id
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("site_id", siteId);
        command.Parameters.AddWithValue("user_id", requestedByUserId);
        command.Parameters.AddWithValue("frequency", (int)FrequencyType.OneTime);
        command.Parameters.AddWithValue("report_name", reportName);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is Guid id ? id : throw new InvalidOperationException("Failed to create one-time report rule.");
    }

    public async Task<SiteReportData> LoadSiteReportDataAsync(Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        const string siteSql = """
            select id, site_name, create_date, address_line_1, address_line_2, postcode,
                   city, county, contracts, company_name, company_id
            from site_search
            where id = @site_id and coalesce(archived, false) = false
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var siteCommand = new NpgsqlCommand(siteSql, connection);
        siteCommand.Parameters.AddWithValue("site_id", siteId);

        await using var reader = await siteCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Site {siteId} was not found or is archived.");
        }

        var site = new SiteReportData
        {
            Id = reader.GetGuid(0),
            SiteName = reader.GetString(1),
            CreateDate = ReadDateTimeOffset(reader, 2),
            AddressLine1 = ReadNullableString(reader, 3),
            AddressLine2 = ReadNullableString(reader, 4),
            Postcode = ReadNullableString(reader, 5),
            City = ReadNullableString(reader, 6),
            County = ReadNullableString(reader, 7),
            Contracts = ReadNullableString(reader, 8),
            CompanyName = ReadNullableString(reader, 9),
            CompanyId = reader.IsDBNull(10) ? null : reader.GetGuid(10)
        };

        await reader.CloseAsync().ConfigureAwait(false);
        var monitors = await ReadMonitorDataAsync(connection, siteId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var monitorsWithAverages = await AttachAveragePointsAsync(connection, monitors, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var monitorsWithNotifications = await AttachNotificationsAsync(connection, monitorsWithAverages, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        return site with { Monitors = await AttachAlertRulesAsync(connection, monitorsWithNotifications, fromUtc, toUtc, cancellationToken).ConfigureAwait(false) };
    }

    public async Task<RuleGenerationLock?> TryAcquireGenerationLockAsync(Guid reportRuleId, ReportPeriod period, CancellationToken cancellationToken)
    {
        const string sql = "select pg_try_advisory_lock(hashtextextended(@lock_key, 0))";
        var lockKey = $"{reportRuleId:N}:{period.StartUtc:O}:{period.EndUtc:O}:{(int)period.Frequency}";

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lock_key", lockKey);
        var acquired = (bool?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) == true;

        if (!acquired)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        return new RuleGenerationLock(async () =>
        {
            await using var releaseCommand = new NpgsqlCommand("select pg_advisory_unlock(hashtextextended(@lock_key, 0))", connection);
            releaseCommand.Parameters.AddWithValue("lock_key", lockKey);
            await releaseCommand.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        });
    }

    public async Task<GeneratedReport> InsertReportAsync(Guid siteId, Guid reportRuleId, FrequencyType frequency, DateTimeOffset generatedAtUtc, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, Uri reportUri, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into report (id, site_id, report_rule_id, frequency, report_date, report_from, report_to, report_link)
            values (gen_random_uuid(), @site_id, @report_rule_id, @frequency, @generated_at, @period_start, @period_end, @report_uri)
            returning id
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("site_id", siteId);
        command.Parameters.AddWithValue("report_rule_id", reportRuleId);
        command.Parameters.AddWithValue("frequency", (int)frequency);
        command.Parameters.AddWithValue("generated_at", generatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("period_start", periodStartUtc.UtcDateTime);
        command.Parameters.AddWithValue("period_end", periodEndUtc.UtcDateTime);
        command.Parameters.AddWithValue("report_uri", reportUri.ToString());

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var reportId = result is Guid id ? id : throw new InvalidOperationException("Failed to insert report row.");
        return new GeneratedReport(reportId, reportRuleId, reportUri, periodStartUtc, periodEndUtc);
    }

    public async Task InsertReportSentAsync(Guid reportId, DateTimeOffset sentAtUtc, string recipientEmail, string statusMessage, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into report_sent (id, report_id, send_time, address, error_message)
            values (gen_random_uuid(), @report_id, @sent_at, @recipient_email, @status_message)
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("report_id", reportId);
        command.Parameters.AddWithValue("sent_at", sentAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("recipient_email", recipientEmail);
        command.Parameters.AddWithValue("status_message", statusMessage);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateReportRuleLastGeneratedAsync(Guid reportRuleId, DateTimeOffset generatedAtUtc, CancellationToken cancellationToken)
    {
        const string sql = "update report_rule set last_generated = @last_generated where id = @report_rule_id";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("report_rule_id", reportRuleId);
        command.Parameters.AddWithValue("last_generated", generatedAtUtc.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ReportRule>> ReadRulesAsync(string sql, Action<NpgsqlCommand> configure, CancellationToken cancellationToken)
    {
        var rules = new List<ReportRule>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        configure(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rules.Add(new ReportRule
            {
                Id = reader.GetGuid(0),
                SiteId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                Frequency = (FrequencyType)reader.GetInt32(3),
                DayOfWeek = reader.IsDBNull(4) ? null : (DayOfWeek)reader.GetInt32(4),
                DayOfMonth = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                LastGenerated = ReadNullableDateTimeOffset(reader, 6),
                ReportName = ReadNullableString(reader, 7),
                IsHiddenSystemRule = reader.GetBoolean(8)
            });
        }

        return rules;
    }

    private async Task<IReadOnlyList<ReportRule>> AttachRecipientsAsync(IReadOnlyList<ReportRule> rules, CancellationToken cancellationToken)
    {
        var hydrated = new List<ReportRule>();
        foreach (var rule in rules)
        {
            hydrated.Add(rule with { Recipients = await ReadRecipientsAsync(rule.Id, cancellationToken).ConfigureAwait(false) });
        }

        return hydrated;
    }

    private async Task<IReadOnlyList<ReportRecipient>> ReadRecipientsAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        const string sql = """
            select ru.user_id, u."Email"
            from report_user ru
            inner join "AspNetUsers" u on u."Id" = ru.user_id::text
            where ru.report_rule_id = @report_rule_id
            """;

        var recipients = new List<ReportRecipient>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("report_rule_id", reportRuleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recipients.Add(new ReportRecipient(reader.GetGuid(0), reader.GetString(1)));
        }

        return recipients;
    }

    private static async Task<IReadOnlyList<MonitorReportData>> ReadMonitorDataAsync(NpgsqlConnection connection, Guid siteId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        const string sql = """
            with monitor_windows as (
                select m.id, m.active, m.deployment_id, m.fleet_row_count, m.serial_id, m.type_of_monitor,
                       m.off_line, m.alerts, m.cautions, m.latitude, m.longitude, m.start_date,
                       m.end_date, m.what_3_words, m.last_data_time, m.location, m.calibration_date,
                       greatest(coalesce(m.start_date, @from_utc), coalesce(c.on_hire_date, @from_utc), @from_utc) as effective_from,
                       least(
                           coalesce(m.end_date, @to_utc),
                           coalesce(
                               case
                                   when c.off_hire_date is not null and c.off_hire_date::time = time '00:00:00'
                                       then c.off_hire_date + interval '1 day'
                                   else c.off_hire_date
                               end,
                               @to_utc),
                           @to_utc) as effective_to
                from monitor_report m
                left join deployment d on d.id = m.deployment_id
                left join contract c on c.id = d.contract_id
                where m.site_id = @site_id
            )
            select id, active, deployment_id, fleet_row_count, serial_id, type_of_monitor,
                   off_line, alerts, cautions, latitude, longitude, start_date,
                   end_date, what_3_words, last_data_time, location, calibration_date,
                   effective_from, effective_to
            from monitor_windows
            where effective_from < effective_to
            order by type_of_monitor, fleet_row_count, serial_id
            """;

        var monitors = new List<MonitorReportData>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("site_id", siteId);
        command.Parameters.AddWithValue("from_utc", fromUtc.UtcDateTime);
        command.Parameters.AddWithValue("to_utc", toUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            monitors.Add(new MonitorReportData
            {
                Id = reader.GetGuid(0),
                Active = reader.GetBoolean(1),
                DeploymentId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                FleetNumber = ReadNullableString(reader, 3),
                SerialId = reader.GetString(4),
                TypeOfMonitor = (MonitorType)reader.GetInt32(5),
                Offline = reader.GetBoolean(6),
                HasAlerts = reader.GetBoolean(7),
                HasCautions = reader.GetBoolean(8),
                Latitude = ReadNullableFloat(reader, 9),
                Longitude = ReadNullableFloat(reader, 10),
                StartDate = ReadNullableDateTimeOffset(reader, 11),
                EndDate = ReadNullableDateTimeOffset(reader, 12),
                What3Words = ReadNullableString(reader, 13),
                LastDataTime = ReadNullableDateTimeOffset(reader, 14),
                Location = ReadNullableString(reader, 15),
                CalibrationDate = ReadNullableDateTimeOffset(reader, 16),
                EffectiveFrom = ReadDateTimeOffset(reader, 17),
                EffectiveTo = ReadDateTimeOffset(reader, 18)
            });
        }

        return monitors;
    }

    private static async Task<IReadOnlyList<MonitorReportData>> AttachAlertRulesAsync(
        NpgsqlConnection connection,
        IReadOnlyList<MonitorReportData> monitors,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (monitors.Count == 0)
        {
            return monitors;
        }

        var rulesByMonitor = await ReadAlertRulesAsync(connection, monitors, cancellationToken).ConfigureAwait(false);
        return monitors
            .Select(monitor => monitor with
            {
                AlertRules = rulesByMonitor.TryGetValue(monitor.Id, out var rules)
                    ? rules
                    : []
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<MonitorReportData>> AttachAveragePointsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<MonitorReportData> monitors,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (monitors.Count == 0)
        {
            return monitors;
        }

        var serials = monitors.Select(static monitor => monitor.SerialId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var dustHourly = await ReadAveragePointsBySerialAsync(connection, "my_atm_dust_level", "pm_10", serials, fromUtc, toUtc, cancellationToken, "and avrg = 3600").ConfigureAwait(false);
        var dustDaily = await ReadAveragePointsBySerialAsync(connection, "my_atm_dust_level_1_day_avg", "pm_10", serials, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var noiseHourly = await ReadAveragePointsBySerialAsync(connection, "noise_level_1_hour_avg", "laeq", serials, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var noiseDaily = await ReadAveragePointsBySerialAsync(connection, "noise_level_1_day_avg", "laeq", serials, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var noiseSite = await ReadAveragePointsBySerialAsync(connection, "noise_level_site_avg", "laeq", serials, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var vibrationDailyPeak = await ReadAveragePointsBySerialAsync(
            connection,
            "omnidots_peak_level_1_day_peak",
            "greatest(coalesce(x_vtop, 0), coalesce(y_vtop, 0), coalesce(z_vtop, 0))",
            serials,
            fromUtc,
            toUtc,
            cancellationToken).ConfigureAwait(false);

        return monitors
            .Select(monitor => monitor with
            {
                DustHourlyAverage = PointsForMonitor(dustHourly, monitor),
                DustDailyAverage = PointsForMonitor(dustDaily, monitor),
                NoiseHourlyAverage = PointsForMonitor(noiseHourly, monitor),
                NoiseDailyAverage = PointsForMonitor(noiseDaily, monitor),
                NoiseSiteAverage = PointsForMonitor(noiseSite, monitor),
                VibrationDailyPeak = PointsForMonitor(vibrationDailyPeak, monitor)
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<MonitorReportData>> AttachNotificationsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<MonitorReportData> monitors,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (monitors.Count == 0)
        {
            return monitors;
        }

        const string sql = """
            select monitor_id,
                   alert_type,
                   notification_time,
                   alert_field,
                   limit_on,
                   level,
                   averaging_period,
                   closed_time,
                   closed_note
            from notification
            where monitor_id = any(@monitor_ids)
              and notification_time >= @from_utc
              and notification_time < @to_utc
            order by monitor_id, notification_time
            """;

        var notificationsByMonitor = new Dictionary<Guid, List<NotificationData>>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("monitor_ids", monitors.Select(static monitor => monitor.Id).ToArray());
        command.Parameters.AddWithValue("from_utc", fromUtc.UtcDateTime);
        command.Parameters.AddWithValue("to_utc", toUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var monitorId = reader.GetGuid(0);
            if (!notificationsByMonitor.TryGetValue(monitorId, out var notifications))
            {
                notifications = [];
                notificationsByMonitor[monitorId] = notifications;
            }

            notifications.Add(new NotificationData(
                (AlertType)reader.GetInt32(1),
                ReadDateTimeOffset(reader, 2),
                reader.GetString(3),
                Convert.ToDecimal(reader.GetDouble(4), provider: null),
                Convert.ToDecimal(reader.GetDouble(5), provider: null),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ReadNullableDateTimeOffset(reader, 7),
                EmptyToNull(ReadNullableString(reader, 8)),
                null));
        }

        return monitors
            .Select(monitor => monitor with
            {
                Notifications = notificationsByMonitor.TryGetValue(monitor.Id, out var notifications)
                    ? notifications.Where(notification => IsWithinMonitorWindow(monitor, notification.CreatedAt)).ToArray()
                    : []
            })
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>>> ReadAveragePointsBySerialAsync(
        NpgsqlConnection connection,
        string tableName,
        string valueExpression,
        IReadOnlyList<string> serials,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        string extraPredicate = "")
    {
        var sql = $"""
            select serial_id, sample_time, {valueExpression} as value
            from {tableName}
            where serial_id = any(@serial_ids)
              and sample_time >= @from_utc
              and sample_time < @to_utc
              and {valueExpression} is not null
              {extraPredicate}
            order by serial_id, sample_time
            """;

        var pointsBySerial = new Dictionary<string, List<MeasurementPoint>>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("serial_ids", serials.ToArray());
        command.Parameters.AddWithValue("from_utc", fromUtc.UtcDateTime);
        command.Parameters.AddWithValue("to_utc", toUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var serialId = reader.GetString(0);
            if (!pointsBySerial.TryGetValue(serialId, out var points))
            {
                points = [];
                pointsBySerial[serialId] = points;
            }

            points.Add(new MeasurementPoint(ReadDateTimeOffset(reader, 1), Convert.ToDecimal(reader.GetValue(2), provider: null)));
        }

        return pointsBySerial.ToDictionary(static item => item.Key, static item => (IReadOnlyList<MeasurementPoint>)item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static MeasurementPoint[] PointsForMonitor(IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> pointsBySerial, MonitorReportData monitor)
    {
        return pointsBySerial.TryGetValue(monitor.SerialId, out var points)
            ? points.Where(point => IsWithinMonitorWindow(monitor, point.MeasuredAt)).ToArray()
            : [];
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<AlertRuleData>>> ReadAlertRulesAsync(
        NpgsqlConnection connection,
        IReadOnlyList<MonitorReportData> monitors,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select ar.monitor_id,
                   ar.alert_type,
                   ar.alert_field,
                   ar.limit_on,
                   ar.averaging_period
            from rvt_alert_rule ar
            where ar.monitor_id = any(@monitor_ids)
              and coalesce(ar.is_active, true) = true
              and coalesce(ar.is_deleted, false) = false
            order by ar.alert_type, ar.alert_field, ar.limit_on
            """;

        var monitorById = monitors.ToDictionary(static monitor => monitor.Id);
        var rulesByMonitor = new Dictionary<Guid, List<AlertRuleData>>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("monitor_ids", monitors.Select(static monitor => monitor.Id).ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var monitorId = reader.GetGuid(0);
            var field = reader.GetString(2);
            var threshold = Convert.ToDecimal(reader.GetDouble(3), provider: null);
            int? averagingPeriod = reader.IsDBNull(4) ? null : reader.GetInt32(4);
            var monitor = monitorById[monitorId];
            var matchingNotifications = monitor.Notifications
                .Where(notification => NotificationMatchesRule(notification, (AlertType)reader.GetInt32(1), field, threshold, averagingPeriod))
                .ToArray();
            var rule = new AlertRuleData(
                (AlertType)reader.GetInt32(1),
                field,
                threshold,
                monitor.TypeOfMonitor == MonitorType.Vibration ? null : averagingPeriod,
                monitor.Unit,
                FormatAlertField(field),
                matchingNotifications.Length,
                LatestClosedNote(matchingNotifications));

            if (!rulesByMonitor.TryGetValue(monitorId, out var monitorRules))
            {
                monitorRules = [];
                rulesByMonitor[monitorId] = monitorRules;
            }

            monitorRules.Add(rule);
        }

        return rulesByMonitor.ToDictionary(static item => item.Key, static item => (IReadOnlyList<AlertRuleData>)item.Value);
    }

    private static bool IsWithinMonitorWindow(MonitorReportData monitor, DateTimeOffset timestamp)
    {
        return timestamp >= monitor.EffectiveFrom && timestamp < monitor.EffectiveTo;
    }

    private static bool NotificationMatchesRule(NotificationData notification, AlertType alertType, string field, decimal threshold, int? averagingPeriod)
    {
        return notification.AlertType == alertType
            && string.Equals(notification.Field, field, StringComparison.Ordinal)
            && notification.Threshold == threshold
            && notification.AveragingPeriodSeconds == averagingPeriod;
    }

    private static string? LatestClosedNote(IReadOnlyList<NotificationData> notifications)
    {
        return notifications
            .Where(static notification => notification.ClosedAt is not null && !string.IsNullOrWhiteSpace(notification.ClosedNote))
            .OrderByDescending(static notification => notification.ClosedAt)
            .ThenByDescending(static notification => notification.CreatedAt)
            .Select(static notification => notification.ClosedNote)
            .FirstOrDefault();
    }

    private static string FormatAlertField(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return "Alert";
        }

        return string.Concat(field.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(field[index - 1])
                ? $" {character}"
                : character.ToString()));
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static float? ReadNullableFloat(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToSingle(reader.GetDouble(ordinal), provider: null);

    private static DateTimeOffset? ReadNullableDateTimeOffset(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : ReadDateTimeOffset(reader, ordinal);

    private static DateTimeOffset ReadDateTimeOffset(NpgsqlDataReader reader, int ordinal)
    {
        var value = DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        return new DateTimeOffset(value);
    }
}
