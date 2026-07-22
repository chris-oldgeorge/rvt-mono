using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportingMonitor.Api.Db.EntityFramework;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace ReportingMonitor.Api.Db;

public sealed class ReportingDbClient(ReportingMonitorContext context) :
    IReportingRuleQueries,
    IReportingDataQueries,
    IReportingGenerationLocks,
    IReportingGenerationCommands,
    IReportingHealthQueries
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);

    public async Task<RuleGenerationLock?> TryAcquireAsync(
        Guid reportRuleId,
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select pg_try_advisory_lock(hashtextextended(@lock_key, @lock_seed));";
            AddTextParameter(command, "lock_key", GenerationLockKey(reportRuleId, period));
            AddInt64Parameter(command, "lock_seed", 0);

            var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("PostgreSQL advisory lock query returned no result."));
            if (!acquired)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
                return null;
            }
        }
        catch
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            throw;
        }

        var released = 0;
        return new RuleGenerationLock(async () =>
        {
            if (Interlocked.Exchange(ref released, 1) != 0)
            {
                return;
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "select pg_advisory_unlock(hashtextextended(@lock_key, @lock_seed));";
                AddTextParameter(command, "lock_key", GenerationLockKey(reportRuleId, period));
                AddInt64Parameter(command, "lock_seed", 0);
                await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        });
    }

    public async Task<GeneratedReport> SaveGeneratedReportAsync(
        GeneratedReportSaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SaveGeneratedReportAttemptAsync(request, reloadOneTimeRule: false, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (request.OneTimeReportRule is not null && IsHiddenOneTimeRuleConflict(exception))
        {
            // The competing transaction has committed the hidden rule. Start a clean EF transaction so the
            // subsequent read observes that winner and all report metadata remains in one atomic write.
            context.ChangeTracker.Clear();
            return await SaveGeneratedReportAttemptAsync(request, reloadOneTimeRule: true, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<GeneratedReport> SaveGeneratedReportAttemptAsync(
        GeneratedReportSaveRequest request,
        bool reloadOneTimeRule,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var reportRuleId = await ResolveReportRuleIdAsync(request, reloadOneTimeRule, cancellationToken).ConfigureAwait(false);
            var report = new ReportEntity
            {
                Id = Guid.NewGuid(),
                SiteId = request.SiteId,
                ReportRuleId = reportRuleId,
                Frequency = (int)request.Frequency,
                ReportDate = request.GeneratedAtUtc,
                ReportFrom = request.PeriodStartUtc,
                ReportTo = request.PeriodEndUtc,
                ReportLink = request.ReportUri.ToString()
            };
            context.Reports.Add(report);
            context.ReportSends.AddRange(request.Deliveries.Select(delivery => new ReportSentEntity
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                SendTime = delivery.SentAtUtc,
                Address = delivery.RecipientEmail,
                ErrorMessage = delivery.ErrorMessage
            }));

            if (request.UpdateLastGenerated)
            {
                var rule = await context.ReportRules
                    .SingleAsync(row => row.Id == reportRuleId, cancellationToken)
                    .ConfigureAwait(false);
                rule.LastGenerated = request.GeneratedAtUtc;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new GeneratedReport(report.Id, reportRuleId, request.ReportUri, request.PeriodStartUtc, request.PeriodEndUtc);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<IReadOnlyList<ReportRule>> GetDueReportRulesAsync(
        DateTimeOffset maxLastGeneratedUtc,
        CancellationToken cancellationToken)
    {
        var rules = await (
                from rule in context.ReportRules.AsNoTracking()
                join site in context.SiteSearchRows.AsNoTracking() on rule.SiteId equals site.Id
                where !site.Archived &&
                      !rule.Deleted &&
                      !rule.IsHiddenSystemRule &&
                      rule.Frequency != (int)FrequencyType.Off &&
                      rule.Frequency != (int)FrequencyType.OneTime &&
                      (rule.LastGenerated == null || rule.LastGenerated < maxLastGeneratedUtc)
                orderby rule.Id
                select rule)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await MapRulesAsync(rules, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReportRule?> GetReportRuleAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        var rule = await (
                from row in context.ReportRules.AsNoTracking()
                join site in context.SiteSearchRows.AsNoTracking() on row.SiteId equals site.Id
                where row.Id == reportRuleId && !row.Deleted && !site.Archived
                select row)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rule is null)
        {
            return null;
        }

        return (await MapRulesAsync([rule], cancellationToken).ConfigureAwait(false)).Single();
    }

    public async Task<SiteReportData> LoadSiteReportDataAsync(
        Guid siteId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (fromUtc >= toUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(fromUtc), "The report start must be before the report end.");
        }

        var site = await context.SiteSearchRows
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == siteId && !row.Archived, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Site {siteId} was not found.");

        var ownershipRows = await (
                from monitor in context.MonitorReportRows.AsNoTracking()
                join deployment in context.ReportingDeploymentRows.AsNoTracking()
                    on monitor.DeploymentId equals (Guid?)deployment.Id into deployments
                from deployment in deployments.DefaultIfEmpty()
                join contract in context.ReportingContractRows.AsNoTracking()
                    on (Guid?)deployment.ContractId equals (Guid?)contract.Id into contracts
                from contract in contracts.DefaultIfEmpty()
                where monitor.SiteId == siteId
                select new { Monitor = monitor, Contract = contract })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var monitorWindows = ownershipRows
            .Select(row => new MonitorWindow(
                row.Monitor,
                EffectiveFrom(row.Monitor.StartDate, row.Contract?.OnHireDate, fromUtc),
                EffectiveTo(row.Monitor.EndDate, row.Contract?.OffHireDate, toUtc)))
            .Where(item => item.EffectiveFrom < item.EffectiveTo)
            .OrderBy(item => item.Row.TypeOfMonitor)
            .ThenBy(item => item.Row.FleetNumber)
            .ThenBy(item => item.Row.SerialId, StringComparer.Ordinal)
            .ToArray();

        if (monitorWindows.Length == 0)
        {
            return MapSite(site, []);
        }

        var serialIds = monitorWindows.Select(static item => item.Row.SerialId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var monitorIds = monitorWindows.Select(static item => item.Row.Id).ToArray();
        var telemetry = await ReadTelemetryAsync(serialIds, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var notifications = await context.ReportingNotificationRows
            .AsNoTracking()
            .Where(row => monitorIds.Contains(row.MonitorId) && row.NotificationTime >= fromUtc && row.NotificationTime < toUtc)
            .OrderBy(row => row.NotificationTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var alertRules = await context.ReportingAlertRuleRows
            .AsNoTracking()
            .Where(row => row.MonitorId != null && monitorIds.Contains(row.MonitorId.Value) && row.IsActive && !row.IsDeleted)
            .OrderBy(row => row.AlertType)
            .ThenBy(row => row.AlertField)
            .ThenBy(row => row.LimitOn)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var monitors = monitorWindows.Select(window => MapMonitor(window, telemetry, notifications, alertRules)).ToArray();
        return MapSite(site, monitors);
    }

    private async Task<IReadOnlyList<ReportRule>> MapRulesAsync(
        IReadOnlyList<ReportRuleEntity> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return [];
        }

        var ruleIds = rules.Select(static rule => rule.Id).ToArray();
        var recipientRows = await context.ReportRecipientRows
            .AsNoTracking()
            .Where(row => ruleIds.Contains(row.ReportRuleId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var recipientUserIds = recipientRows.Select(static row => row.UserId.ToString()).Distinct(StringComparer.Ordinal).ToArray();
        var users = await context.Users
            .AsNoTracking()
            .Where(user => recipientUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var emailByUserId = users.ToDictionary(static user => user.Id, static user => user.Email, StringComparer.Ordinal);
        var recipientsByRule = recipientRows
            .Where(row => emailByUserId.ContainsKey(row.UserId.ToString()))
            .GroupBy(static row => row.ReportRuleId)
            .ToDictionary(
                static group => group.Key,
                group => (IReadOnlyList<ReportRecipient>)group
                    .OrderBy(static row => row.UserId)
                    .Select(row => new ReportRecipient(row.UserId, emailByUserId[row.UserId.ToString()]))
                    .ToArray());

        return rules.Select(rule => ReportingDbMapper.ToReportRule(
                rule,
                recipientsByRule.TryGetValue(rule.Id, out var recipients) ? recipients : []))
            .ToArray();
    }

    private async Task<Guid> ResolveReportRuleIdAsync(
        GeneratedReportSaveRequest request,
        bool reloadOneTimeRule,
        CancellationToken cancellationToken)
    {
        if (request.OneTimeReportRule is null)
        {
            return request.ReportRuleId ?? throw new InvalidOperationException("A generated report must have a report rule.");
        }

        var rule = await context.ReportRules.SingleOrDefaultAsync(
                row => row.SiteId == request.SiteId &&
                       row.Frequency == (int)FrequencyType.OneTime &&
                       row.IsHiddenSystemRule,
                cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            if (reloadOneTimeRule)
            {
                throw new InvalidOperationException("The concurrently-created hidden one-time report rule could not be reloaded.");
            }

            rule = new ReportRuleEntity
            {
                Id = Guid.NewGuid(),
                SiteId = request.SiteId,
                UserId = request.OneTimeReportRule.RequestedByUserId,
                Frequency = (int)FrequencyType.OneTime,
                IsHiddenSystemRule = true
            };
            context.ReportRules.Add(rule);
        }

        rule.Deleted = false;
        rule.ReportName = request.OneTimeReportRule.ReportName;
        return rule.Id;
    }

    private static bool IsHiddenOneTimeRuleConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_report_rule_hidden_one_time_per_site"
        };

    private static string GenerationLockKey(Guid reportRuleId, ReportPeriod period) => string.Join(
        '|',
        reportRuleId.ToString("D"),
        ((int)period.Frequency).ToString(CultureInfo.InvariantCulture),
        period.StartUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        period.EndUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static void AddTextParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddInt64Parameter(DbCommand command, string name, long value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Int64;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<Telemetry> ReadTelemetryAsync(
        IReadOnlyCollection<string> serialIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var dustHourly = await context.DustHourlyAverageRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.AveragingPeriodSeconds == 3600 && row.Pm10 != null && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var dustDaily = await context.DustDailyAverageRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.Pm10 != null && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var noiseHourly = await context.NoiseHourlyAverageRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.Laeq != null && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var noiseDaily = await context.NoiseDailyAverageRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.Laeq != null && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var noiseSite = await context.NoiseSiteAverageRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.Laeq != null && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var vibrationDailyPeak = await context.VibrationDailyPeakRows.AsNoTracking()
            .Where(row => serialIds.Contains(row.SerialId) && row.SampleTime >= fromUtc && row.SampleTime < toUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new Telemetry(
            PointsBySerial(dustHourly, static row => row.SerialId, static row => row.Pm10!.Value, static row => row.SampleTime),
            PointsBySerial(dustDaily, static row => row.SerialId, static row => row.Pm10!.Value, static row => row.SampleTime),
            PointsBySerial(noiseHourly, static row => row.SerialId, static row => row.Laeq!.Value, static row => row.SampleTime),
            PointsBySerial(noiseDaily, static row => row.SerialId, static row => row.Laeq!.Value, static row => row.SampleTime),
            PointsBySerial(noiseSite, static row => row.SerialId, static row => row.Laeq!.Value, static row => row.SampleTime),
            PointsBySerial(vibrationDailyPeak, static row => row.SerialId, static row => Math.Max(row.XVtop ?? 0d, Math.Max(row.YVtop ?? 0d, row.ZVtop ?? 0d)), static row => row.SampleTime));
    }

    private static MonitorReportData MapMonitor(
        MonitorWindow monitor,
        Telemetry telemetry,
        IReadOnlyList<ReportingNotificationRow> notificationRows,
        IReadOnlyList<ReportingAlertRuleRow> alertRuleRows)
    {
        var notifications = notificationRows
            .Where(row => row.MonitorId == monitor.Row.Id && IsWithinWindow(monitor, row.NotificationTime))
            .OrderBy(row => row.NotificationTime)
            .Select(row => new NotificationData(
                (AlertType)row.AlertType,
                row.NotificationTime,
                row.AlertField,
                ToDecimal(row.LimitOn),
                ToDecimal(row.Level),
                row.AveragingPeriod,
                row.ClosedTime,
                EmptyToNull(row.ClosedByNote),
                null))
            .ToArray();
        var prototype = new MonitorReportData { TypeOfMonitor = (MonitorType)monitor.Row.TypeOfMonitor };
        var alertRules = alertRuleRows
            .Where(row => row.MonitorId == monitor.Row.Id)
            .Select(row =>
            {
                var threshold = ToDecimal(row.LimitOn);
                var matchingAveragingPeriod = row.AveragingPeriod;
                int? displayAveragingPeriod = prototype.TypeOfMonitor == MonitorType.Vibration
                    ? null
                    : matchingAveragingPeriod;
                var matchingNotifications = notifications.Where(notification =>
                    notification.AlertType == (AlertType)row.AlertType &&
                    string.Equals(notification.Field, row.AlertField, StringComparison.Ordinal) &&
                    notification.Threshold == threshold &&
                    notification.AveragingPeriodSeconds == matchingAveragingPeriod).ToArray();
                return new AlertRuleData(
                    (AlertType)row.AlertType,
                    row.AlertField,
                    threshold,
                    displayAveragingPeriod,
                    prototype.Unit,
                    FormatAlertField(row.AlertField),
                    matchingNotifications.Length,
                    LatestClosedNote(matchingNotifications));
            })
            .ToArray();

        return new MonitorReportData
        {
            Id = monitor.Row.Id,
            Active = monitor.Row.Active,
            DeploymentId = monitor.Row.DeploymentId,
            FleetNumber = monitor.Row.FleetNumber,
            SerialId = monitor.Row.SerialId,
            TypeOfMonitor = prototype.TypeOfMonitor,
            Offline = monitor.Row.Offline,
            HasAlerts = monitor.Row.Alerts,
            HasCautions = monitor.Row.Cautions,
            Latitude = monitor.Row.Latitude is null ? null : Convert.ToSingle(monitor.Row.Latitude.Value),
            Longitude = monitor.Row.Longitude is null ? null : Convert.ToSingle(monitor.Row.Longitude.Value),
            StartDate = monitor.Row.StartDate,
            EndDate = monitor.Row.EndDate,
            EffectiveFrom = monitor.EffectiveFrom,
            EffectiveTo = monitor.EffectiveTo,
            What3Words = monitor.Row.What3Words,
            LastDataTime = monitor.Row.LastDataTime,
            Location = monitor.Row.Location,
            CalibrationDate = monitor.Row.CalibrationDate,
            Notifications = notifications,
            AlertRules = alertRules,
            DustHourlyAverage = PointsForMonitor(telemetry.DustHourly, monitor),
            DustDailyAverage = PointsForMonitor(telemetry.DustDaily, monitor),
            NoiseHourlyAverage = PointsForMonitor(telemetry.NoiseHourly, monitor),
            NoiseDailyAverage = PointsForMonitor(telemetry.NoiseDaily, monitor),
            NoiseSiteAverage = PointsForMonitor(telemetry.NoiseSite, monitor),
            VibrationDailyPeak = PointsForMonitor(telemetry.VibrationDailyPeak, monitor)
        };
    }

    private static SiteReportData MapSite(SiteSearchRow row, IReadOnlyList<MonitorReportData> monitors) => new()
    {
        Id = row.Id,
        SiteName = row.SiteName,
        CreateDate = row.CreateDate,
        AddressLine1 = row.AddressLine1,
        AddressLine2 = row.AddressLine2,
        Postcode = row.Postcode,
        City = row.City,
        County = row.County,
        Contracts = row.Contracts,
        CompanyName = row.CompanyName,
        CompanyId = row.CompanyId,
        Monitors = monitors
    };

    private static DateTimeOffset EffectiveFrom(
        DateTimeOffset? monitorStartUtc,
        DateTimeOffset? contractOnHireUtc,
        DateTimeOffset fromUtc) => new[] { monitorStartUtc, contractOnHireUtc, fromUtc }
        .Where(static value => value.HasValue)
        .Select(static value => value!.Value)
        .Max();

    private static DateTimeOffset EffectiveTo(
        DateTimeOffset? monitorEndUtc,
        DateTimeOffset? contractOffHireUtc,
        DateTimeOffset toUtc) => new[] { monitorEndUtc, InclusiveContractOffHire(contractOffHireUtc), toUtc }
        .Where(static value => value.HasValue)
        .Select(static value => value!.Value)
        .Min();

    private static DateTimeOffset? InclusiveContractOffHire(DateTimeOffset? contractOffHireUtc)
    {
        if (contractOffHireUtc is not { } offHireUtc || offHireUtc.TimeOfDay != TimeSpan.Zero)
        {
            return contractOffHireUtc;
        }

        return offHireUtc.AddDays(1);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> PointsBySerial<T>(
        IEnumerable<T> rows,
        Func<T, string> serialId,
        Func<T, double> value,
        Func<T, DateTimeOffset> measuredAt)
        where T : class
    {
        return rows.GroupBy(serialId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                group => (IReadOnlyList<MeasurementPoint>)group
                    .OrderBy(measuredAt)
                    .Select(row => new MeasurementPoint(measuredAt(row), ToDecimal(value(row))))
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<MeasurementPoint> PointsForMonitor(
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> pointsBySerial,
        MonitorWindow monitor) =>
        pointsBySerial.TryGetValue(monitor.Row.SerialId, out var points)
            ? points.Where(point => IsWithinWindow(monitor, point.MeasuredAt)).ToArray()
            : [];

    private static bool IsWithinWindow(MonitorWindow monitor, DateTimeOffset value) =>
        value >= monitor.EffectiveFrom && value < monitor.EffectiveTo;

    private static decimal ToDecimal(double value) => Convert.ToDecimal(value, provider: null);

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? LatestClosedNote(IReadOnlyList<NotificationData> notifications) => notifications
        .Where(notification => notification.ClosedAt is not null && !string.IsNullOrWhiteSpace(notification.ClosedNote))
        .OrderByDescending(notification => notification.ClosedAt)
        .ThenByDescending(notification => notification.CreatedAt)
        .Select(notification => notification.ClosedNote)
        .FirstOrDefault();

    private static string FormatAlertField(string field) => string.IsNullOrWhiteSpace(field)
        ? "Alert"
        : string.Concat(field.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(field[index - 1])
                ? $" {character}"
                : character.ToString()));

    private sealed record MonitorWindow(MonitorReportRow Row, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveTo);

    private sealed record Telemetry(
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> DustHourly,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> DustDaily,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> NoiseHourly,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> NoiseDaily,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> NoiseSite,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementPoint>> VibrationDailyPeak);
}
