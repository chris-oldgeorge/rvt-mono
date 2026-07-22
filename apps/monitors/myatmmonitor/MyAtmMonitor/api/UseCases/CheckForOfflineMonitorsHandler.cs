using MyAtm.Api.Db;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Detects offline/online edges using elapsed active-site time and atomic alert commits.
public sealed class CheckForOfflineMonitorsHandler
{
    private readonly IMyAtmRuleQueries ruleQueries;
    private readonly MyAtmMonitorReader monitorReader;
    private readonly IMyAtmSiteScheduleQueries siteScheduleQueries;
    private readonly IMyAtmAlertCommitCommands alertCommitCommands;
    private readonly IMyAtmOperationalCommands operationalCommands;
    private readonly MyAtmRuleProcessor ruleProcessor;
    private readonly TimeProvider timeProvider;

    public CheckForOfflineMonitorsHandler(
        IMyAtmRuleQueries ruleQueries,
        MyAtmMonitorReader monitorReader,
        IMyAtmSiteScheduleQueries siteScheduleQueries,
        IMyAtmAlertCommitCommands alertCommitCommands,
        IMyAtmOperationalCommands operationalCommands,
        MyAtmRuleProcessor ruleProcessor,
        TimeProvider timeProvider)
    {
        this.ruleQueries = ruleQueries;
        this.monitorReader = monitorReader;
        this.siteScheduleQueries = siteScheduleQueries;
        this.alertCommitCommands = alertCommitCommands;
        this.operationalCommands = operationalCommands;
        this.ruleProcessor = ruleProcessor;
        this.timeProvider = timeProvider;
    }

    public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var rules = (ruleQueries.ReadRules(null) ?? [])
            .Where(rule => RuleConstants.OFFLINE_RULE.Equals(rule.Field))
            .ToList();
        var monitors = monitorReader.ReadMonitors(customerId) ?? [];
        var failures = new MyAtmFailureCollector(operationalCommands);

        foreach (var monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var rule in rules)
                {
                    var cutoff = utcNow.AddSeconds(-rule.AveragingPeriod);
                    var offlineDateTime = DateTimeUtil.TruncateMillis(cutoff);
                    var lastDataTime = monitor.LastDataTime1Min.HasValue
                        ? DateTimeUtil.AsUtc(DateTimeUtil.TruncateMillis(monitor.LastDataTime1Min.Value))
                        : MyAtmApi.JAN1_1970;

                    if (lastDataTime >= cutoff)
                    {
                        await RecoverIfOfflineAsync(monitor, utcNow, cancellationToken);
                        continue;
                    }

                    if (!TryResolveTimeZone(monitor.TimeZone, out var siteTimeZone))
                    {
                        throw new InvalidOperationException("Monitor timezone is missing or invalid.");
                    }

                    var schedule = siteScheduleQueries.ReadSiteSchedule(monitor.Id);
                    var activeDuration = MyAtmSiteActiveDurationCalculator.Between(
                        schedule,
                        lastDataTime,
                        utcNow,
                        siteTimeZone);
                    if (activeDuration > TimeSpan.FromSeconds(rule.AveragingPeriod))
                    {
                        if (!monitor.Offline)
                        {
                            var commit = ruleProcessor.CreateOfflineCommit(
                                monitor,
                                rule,
                                offlineDateTime.Subtract(lastDataTime).TotalSeconds,
                                lastDataTime,
                                utcNow);
                            var result = await alertCommitCommands.CommitAlertAsync(commit, cancellationToken);
                            if (result.Applied)
                            {
                                monitor.Offline = true;
                            }
                        }
                    }
                    else
                    {
                        await RecoverIfOfflineAsync(monitor, utcNow, cancellationToken);
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Capture(
                    $"CheckForOfflineMonitors serialId={monitor.SerialId}",
                    exception,
                    cancellationToken);
            }
        }

        failures.ThrowIfAny("CheckForOfflineMonitors");
    }

    private async Task RecoverIfOfflineAsync(
        MyAtm.Model.Dto.DustMonitorDto monitor,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!monitor.Offline)
        {
            return;
        }

        var result = await alertCommitCommands.CommitAlertAsync(
            ruleProcessor.CreateOnlineRecoveryCommit(monitor, utcNow),
            cancellationToken);
        if (result.Applied)
        {
            monitor.Offline = false;
        }
    }

    private static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        timeZone = null!;
        return false;
    }
}
