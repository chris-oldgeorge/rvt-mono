using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Raises battery caution/alert notifications from monitor battery charge levels.
public sealed class NotifyBatteryLevelsHandler
{
    private const int _batteryLevelPercentCaution = 20;
    private const int _batteryLevelPercentAlert = 10;
    private const string _batteryLevel = "Battery level";

    private readonly SvantekMonitorReader _monitorReader;
    private readonly ISvantekRuleQueries _ruleQueries;
    private readonly ISvantekMonitorCommands _monitorCommands;
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor;

    public NotifyBatteryLevelsHandler(
        SvantekMonitorReader monitorReader,
        ISvantekRuleQueries ruleQueries,
        ISvantekMonitorCommands monitorCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        _monitorReader = monitorReader;
        _ruleQueries = ruleQueries;
        _monitorCommands = monitorCommands;
        _operationalCommands = operationalCommands;
        _ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        List<NoiseMonitorReadDto> monitors = await _monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        SvantekFailureCollector failures = new(_operationalCommands);

        foreach (NoiseMonitorReadDto monitor in monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ProcessMonitorAsync(monitor, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Capture($"NotifyBatteryLevels monitor {monitor.SerialId}", exception, cancellationToken);
            }
        }

        failures.ThrowIfAny("NotifyBatteryLevels");
    }

    private async Task ProcessMonitorAsync(
        NoiseMonitorReadDto monitor,
        CancellationToken cancellationToken)
    {
        int batteryLevel = monitor.BatteryCharge;
        if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
        {
            RvtLogger.Logger.LogDebug(
                "NotifyBatteryLevels battery level={BatteryLevel} for serialId={SerialId} status={BatteryStatus}",
                batteryLevel,
                monitor.SerialId,
                monitor.BatteryStatus);
        }

        if (batteryLevel <= _batteryLevelPercentAlert)
        {
            if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.BatteryAlert)
            {
                await ProcessBatteryAlertAsync(
                    batteryLevel,
                    monitor,
                    _batteryLevelPercentAlert,
                    AlertType.BatteryAlert,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else if (batteryLevel <= _batteryLevelPercentCaution)
        {
            if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.BatteryCaution)
            {
                await ProcessBatteryAlertAsync(
                    batteryLevel,
                    monitor,
                    _batteryLevelPercentCaution,
                    AlertType.BatteryCaution,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.Off)
        {
            await _monitorCommands.SetMonitorBatteryStatusAsync(
                monitor.Id,
                batteryStatus: 0,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessBatteryAlertAsync(
        int batteryLevel,
        NoiseMonitorReadDto monitor,
        int alertLevel,
        AlertType alertType,
        CancellationToken cancellationToken)
    {
        // Signal first and latch the status gate only after the durable stack
        // accepted the alert; latching first would permanently suppress the
        // alert when AcceptAsync fails transiently (the offline handler's order).
        await _ruleProcessor.SignalAlertAsync(
            monitor.SerialId,
            DateTimeUtil.TruncateMillis(DateTime.UtcNow),
            alertLevel,
            0,
            batteryLevel,
            alertType,
            _batteryLevel,
            cancellationToken).ConfigureAwait(false);
        byte status = (byte)(alertType == AlertType.BatteryAlert ? 1 : 2);
        await _monitorCommands.SetMonitorBatteryStatusAsync(
            monitor.Id,
            status,
            cancellationToken).ConfigureAwait(false);
    }
}
