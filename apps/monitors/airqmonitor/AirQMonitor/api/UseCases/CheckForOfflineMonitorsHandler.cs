using System.Globalization;
using AirQ.Api.Db;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace AirQ.Api.UseCases
{
    // Summary: Marks monitors offline from rule cutoffs and alerts contacts on transitions.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApiMonitors).
    public class CheckForOfflineMonitorsHandler
    {
        private readonly IAirQRuleQueries _ruleQueries;
        private readonly AirQMonitorReader _monitorReader;
        private readonly IAirQMonitorCommands _monitorCommands;
        private readonly IAirQOperationalCommands _operationalCommands;
        private readonly AirQRuleProcessor _ruleProcessor;

        public CheckForOfflineMonitorsHandler(
            IAirQRuleQueries ruleQueries,
            AirQMonitorReader monitorReader,
            IAirQMonitorCommands monitorCommands,
            IAirQOperationalCommands operationalCommands,
            AirQRuleProcessor ruleProcessor)
        {
            _ruleQueries = ruleQueries;
            _monitorReader = monitorReader;
            _monitorCommands = monitorCommands;
            _operationalCommands = operationalCommands;
            _ruleProcessor = ruleProcessor;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<RvtAlertRuleDto> rules = _ruleQueries.ReadRules(null);

            DateTime utcNow = DateTime.UtcNow;
            // Each monitor is an independent unit: one AcceptAsync or write
            // failure on monitor 3 of 200 must not leave 4-200 unevaluated and
            // unrecorded. Failures are captured per monitor and rethrown as an
            // aggregate so the job still fails visibly (Svantek's shape).
            List<Exception> failures = [];
            foreach (RvtAlertRuleDto rule in rules)
            {
                if (RuleConstants.OFFLINE_RULE.Equals(rule.Field))
                {
                    DateTime cutOff = utcNow.Subtract(new TimeSpan(hours: 0, minutes: 0, seconds: rule.AveragingPeriod));
                    DateTime offlineDateTime = DateTimeUtil.TruncateMillis(utcNow.AddSeconds(-rule.AveragingPeriod));
                    List<NoiseMonitorDto> monitors = _monitorReader.ReadMonitors(null);

                    foreach (NoiseMonitorDto monitor in monitors!)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            await CheckMonitorAsync(monitor, rule, cutOff, offlineDateTime, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            failures.Add(e);
                            try
                            {
                                // Recording is best-effort: a database outage while
                                // writing the error row must not replace or swallow
                                // the original failure (MyAtm's collector semantics).
                                _operationalCommands.HandleException(
                                    string.Format(CultureInfo.InvariantCulture, "CheckForOfflineMonitors SerialId={0}", monitor.SerialId),
                                    e);
                            }
                            catch (Exception recordingException)
                            {
                                failures.Add(recordingException);
                            }
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("One or more AirQ offline checks failed.", failures);
            }
        }

        private async Task CheckMonitorAsync(
            NoiseMonitorDto monitor,
            RvtAlertRuleDto rule,
            DateTime cutOff,
            DateTime offlineDateTime,
            CancellationToken cancellationToken)
        {
            if (monitor.Offline)
            {
                if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                {
                    RvtLogger.Logger.LogDebug("Monitor serialId = {Value1} is already offline lastDataTime={Value2}",
                        monitor.SerialId, monitor.LastDataTime);
                }
                return;
            }

            DateTime lastDataTime = monitor.LastDataTime != null ? DateTimeUtil.TruncateMillis((DateTime)monitor.LastDataTime!).ToUniversalTime() : DateTimeUtil.JAN1_1970;
            double diffInSeconds = monitor.LastDataTime != null ? offlineDateTime.Subtract(lastDataTime).TotalSeconds : 0;

            if (lastDataTime < cutOff)
            {
                if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                {
                    RvtLogger.Logger.LogInformation("Device serialId = {Value1} Data has not been recieved marking as offline", monitor.SerialId);
                }
                await _ruleProcessor.SignalAlertAsync(serialId: monitor.SerialId!,
                                        alertTime: DateTime.UtcNow,
                                        limitOn: 0,
                                        averagingPeriod: rule.AveragingPeriod,
                                        level: diffInSeconds,
                                        alertType: AlertType.Offline,
                                        field: rule.Field,
                                        cancellationToken: cancellationToken);
                monitor.Offline = true;
            }
            else
            {
                if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                {
                    RvtLogger.Logger.LogDebug("Device serialId = {Value1} Data has been recieved marking as online", monitor.SerialId);
                }
                monitor.Offline = false;
            }
            _monitorCommands.SetMonitorOffline(monitor.Id, monitor.Offline);
        }
    }
}
