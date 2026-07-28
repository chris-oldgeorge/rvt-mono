using MyAtm.Api.Db;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Detects offline/online edges using elapsed active-site time and atomic alert commits.
public sealed class CheckForOfflineMonitorsHandler(
    IMyAtmRuleQueries ruleQueries,
    MyAtmMonitorReader monitorReader,
    IMyAtmSiteScheduleQueries siteScheduleQueries,
    IMyAtmAlertCommitCommands alertCommitCommands,
    IMyAtmOperationalCommands operationalCommands,
    MyAtmRuleProcessor ruleProcessor,
    TimeProvider timeProvider)
{
    private readonly IMyAtmRuleQueries ruleQueries = ruleQueries;
    private readonly MyAtmMonitorReader monitorReader = monitorReader;
    private readonly IMyAtmSiteScheduleQueries siteScheduleQueries = siteScheduleQueries;
    private readonly IMyAtmAlertCommitCommands alertCommitCommands = alertCommitCommands;
    private readonly IMyAtmOperationalCommands operationalCommands = operationalCommands;
    private readonly MyAtmRuleProcessor ruleProcessor = ruleProcessor;
    private readonly TimeProvider timeProvider = timeProvider;

    public async Task RunAsync(int customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        List<RvtAlertRuleDto> rules = [.. (ruleQueries.ReadRules(null) ?? []).Where(rule => RuleConstants.OFFLINE_RULE.Equals(rule.Field))];
        List<DustMonitorDto> monitors = monitorReader.ReadMonitors(customerId) ?? [];
        MyAtmFailureCollector failures = new(operationalCommands);

        foreach (DustMonitorDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (RvtAlertRuleDto rule in rules)
                {
                    DateTime cutoff = utcNow.AddSeconds(-rule.AveragingPeriod);
                    DateTime offlineDateTime = DateTimeUtil.TruncateMillis(cutoff);
                    DateTime lastDataTime = monitor.LastDataTime1Min.HasValue
                        ? DateTimeUtil.AsUtc(DateTimeUtil.TruncateMillis(monitor.LastDataTime1Min.Value))
                        : MyAtmApi.JAN1_1970;

                    if (lastDataTime >= cutoff)
                    {
                        await RecoverIfOfflineAsync(monitor, utcNow, cancellationToken);
                        continue;
                    }

                    if (!TryResolveTimeZone(monitor.TimeZone, out TimeZoneInfo? siteTimeZone))
                    {
                        throw new InvalidOperationException("Monitor timezone is missing or invalid.");
                    }

                    MyAtmSiteSchedule schedule = siteScheduleQueries.ReadSiteSchedule(monitor.Id);
                    TimeSpan activeDuration = MyAtmSiteActiveDurationCalculator.Between(
                        schedule,
                        lastDataTime,
                        utcNow,
                        siteTimeZone);
                    if (activeDuration > TimeSpan.FromSeconds(rule.AveragingPeriod))
                    {
                        if (!monitor.Offline)
                        {
                            MyAtmAlertCommit commit = ruleProcessor.CreateOfflineCommit(
                                monitor,
                                rule,
                                offlineDateTime.Subtract(lastDataTime).TotalSeconds,
                                lastDataTime,
                                utcNow);
                            MyAtmAlertCommitResult result = await alertCommitCommands.CommitAlertAsync(commit, cancellationToken);
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

        MyAtmAlertCommitResult result = await alertCommitCommands.CommitAlertAsync(
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
