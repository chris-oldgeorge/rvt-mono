using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Evaluates each completed aggregate period and persists its state transition through one atomic commit.
public sealed class ProcessDustLevelsHandler(
    IMyAtmMonitorQueries monitorQueries,
    IMyAtmRuleQueries ruleQueries,
    IMyAtmAlertCommitCommands alertCommitCommands,
    IMyAtmOperationalCommands operationalCommands,
    MyAtmRuleProcessor ruleProcessor,
    TimeProvider timeProvider,
    bool testLocal)
{
    private readonly IMyAtmMonitorQueries _monitorQueries = monitorQueries;
    private readonly IMyAtmRuleQueries _ruleQueries = ruleQueries;
    private readonly IMyAtmAlertCommitCommands _alertCommitCommands = alertCommitCommands;
    private readonly IMyAtmOperationalCommands _operationalCommands = operationalCommands;
    private readonly MyAtmRuleProcessor _ruleProcessor = ruleProcessor;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly bool _testLocal = testLocal;

    public async Task RunAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement
    {
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        List<RvtAlertRuleDto> rules = [.. _ruleQueries.ReadRules(period).OrderBy(rule => rule.AlertType)];
        MyAtmFailureCollector failures = new(_operationalCommands);
        foreach (RvtAlertRuleDto rule in rules)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rule.IsDeleted)
                {
                    if (rule.IsActive)
                    {
                        MyAtmAlertCommitResult result = await _alertCommitCommands.CommitAlertAsync(
                            _ruleProcessor.CreateDeletedRuleDeactivationCommit(rule, utcNow),
                            cancellationToken);
                        if (result.Applied)
                        {
                            rule.IsActive = false;
                        }
                    }

                    continue;
                }

                if (rule.SerialId == null || (_testLocal && !MyAtmTestLocalMonitorFilter.IsTargetSerial(rule.SerialId)))
                {
                    continue;
                }

                DustMonitorDto? monitor = _monitorQueries.ReadMonitor(customerId, rule.SerialId);
                if (monitor == null || (_testLocal && !MyAtmTestLocalMonitorFilter.IsTargetReadMonitor(monitor)))
                {
                    continue;
                }

                while (true)
                {
                    DateTime lastProcessed = DateTimeUtil.AsUtc(rule.Accessed ?? rule.Created);
                    if (lastProcessed < utcNow.AddDays(-7))
                    {
                        lastProcessed = utcNow.AddDays(-7);
                    }

                    DateTime start = DateTimeUtil.AsUtc(DateTimeUtil.GetNearestPeriodBlock(lastProcessed, rule.AveragingPeriod));
                    DateTime end = start.AddSeconds(rule.AveragingPeriod);
                    if (end > utcNow || DateTimeUtil.AsUtc(monitor.LastDataTime1Min) < end)
                    {
                        break;
                    }

                    string normalizedField = MyAtmAlertTransitionEvaluator.NormalizeField(rule.Field);
                    bool alertForFieldIsActive = rule.AlertType == Rvt.Monitor.Common.Notifications.AlertType.Caution &&
                        rules.Any(other =>
                            other.AlertType == Rvt.Monitor.Common.Notifications.AlertType.Alert &&
                            MyAtmAlertTransitionEvaluator.NormalizeField(other.Field) == normalizedField && other.IsActive);
                    double? level = !rule.RuleActiveTime.IsActive(end)
                        ? null
                        : _ruleQueries.GetAverageDustLevel(rule.SerialId, rule.Field, start, end);
                    MyAtmAlertCommit commit = _ruleProcessor.CreateAggregateCommit(monitor, rule, level, end, alertForFieldIsActive, utcNow);
                    MyAtmAlertCommitResult result = await _alertCommitCommands.CommitAlertAsync(commit, cancellationToken);
                    if (!result.Applied)
                    {
                        break;
                    }

                    rule.IsActive = commit.RuleStateMutations[0].IsActive;
                    rule.Accessed = end;
                }
            }
            catch (Exception exception)
            {
                failures.Capture(
                    $"ProcessDustLevels RuleId={rule.RuleId} SerialId={rule.SerialId ?? "none"}",
                    exception,
                    cancellationToken);
            }
        }

        failures.ThrowIfAny("ProcessDustLevels");
    }
}
