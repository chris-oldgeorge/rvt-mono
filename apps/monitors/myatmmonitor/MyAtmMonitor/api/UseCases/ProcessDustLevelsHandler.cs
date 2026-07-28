using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace MyAtm.Api.UseCases;

// Evaluates each completed aggregate period and persists its state transition through one atomic commit.
public sealed class ProcessDustLevelsHandler
{
    private readonly IMyAtmMonitorQueries monitorQueries;
    private readonly IMyAtmRuleQueries ruleQueries;
    private readonly IMyAtmAlertCommitCommands alertCommitCommands;
    private readonly IMyAtmOperationalCommands operationalCommands;
    private readonly MyAtmRuleProcessor ruleProcessor;
    private readonly TimeProvider timeProvider;
    private readonly bool testLocal;

    public ProcessDustLevelsHandler(
        IMyAtmMonitorQueries monitorQueries,
        IMyAtmRuleQueries ruleQueries,
        IMyAtmAlertCommitCommands alertCommitCommands,
        IMyAtmOperationalCommands operationalCommands,
        MyAtmRuleProcessor ruleProcessor,
        TimeProvider timeProvider,
        bool testLocal)
    {
        this.monitorQueries = monitorQueries;
        this.ruleQueries = ruleQueries;
        this.alertCommitCommands = alertCommitCommands;
        this.operationalCommands = operationalCommands;
        this.ruleProcessor = ruleProcessor;
        this.timeProvider = timeProvider;
        this.testLocal = testLocal;
    }

    public async Task RunAsync<T>(int customerId, Period period, CancellationToken cancellationToken = default)
        where T : BaseDeviceMeasurement
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        List<RvtAlertRuleDto> rules = ruleQueries.ReadRules(period).OrderBy(rule => rule.AlertType).ToList();
        MyAtmFailureCollector failures = new MyAtmFailureCollector(operationalCommands);
        foreach (RvtAlertRuleDto rule in rules)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rule.IsDeleted)
                {
                    if (rule.IsActive)
                    {
                        MyAtmAlertCommitResult result = await alertCommitCommands.CommitAlertAsync(
                            ruleProcessor.CreateDeletedRuleDeactivationCommit(rule, utcNow),
                            cancellationToken);
                        if (result.Applied)
                        {
                            rule.IsActive = false;
                        }
                    }

                    continue;
                }

                if (rule.SerialId == null || (testLocal && !MyAtmTestLocalMonitorFilter.IsTargetSerial(rule.SerialId)))
                {
                    continue;
                }

                DustMonitorDto? monitor = monitorQueries.ReadMonitor(customerId, rule.SerialId);
                if (monitor == null || (testLocal && !MyAtmTestLocalMonitorFilter.IsTargetReadMonitor(monitor)))
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
                        : ruleQueries.GetAverageDustLevel(rule.SerialId, rule.Field, start, end);
                    MyAtmAlertCommit commit = ruleProcessor.CreateAggregateCommit(monitor, rule, level, end, alertForFieldIsActive, utcNow);
                    MyAtmAlertCommitResult result = await alertCommitCommands.CommitAlertAsync(commit, cancellationToken);
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
