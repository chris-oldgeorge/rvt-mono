using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;
using Svantek.Model.Dto;

namespace Svantek.Api.UseCases;

// Summary: Raises battery caution/alert notifications from monitor battery charge levels.
public sealed class NotifyBatteryLevelsHandler(
    SvantekMonitorReader monitorReader,
    ISvantekRuleQueries ruleQueries,
    ISvantekMonitorCommands monitorCommands,
    ISvantekOperationalCommands operationalCommands,
    SvantekRuleProcessor ruleProcessor)
{
    private const int _batteryLevelPercentCaution = 20;
    private const int _batteryLevelPercentAlert = 10;
    private const string _batteryLevel = "Battery level";

    private readonly SvantekMonitorReader _monitorReader = monitorReader;
    private readonly ISvantekRuleQueries _ruleQueries = ruleQueries;
    private readonly ISvantekMonitorCommands _monitorCommands = monitorCommands;
    private readonly ISvantekOperationalCommands _operationalCommands = operationalCommands;
    private readonly SvantekRuleProcessor _ruleProcessor = ruleProcessor;

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
                failures.Capture($"NotifyBatteryLevels monitor {monitor.SerialId}", exception);
            }
        }

        failures.ThrowIfAny("NotifyBatteryLevels");
    }

    private async Task ProcessMonitorAsync(
        NoiseMonitorReadDto monitor,
        CancellationToken cancellationToken)
    {
        int batteryLevel = monitor.BatteryCharge;
        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
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
        byte status = (byte)(alertType == AlertType.BatteryAlert ? 1 : 2);
        await _monitorCommands.SetMonitorBatteryStatusAsync(
            monitor.Id,
            status,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        List<Rvt.Monitor.Common.Rules.RvtContactDto> contacts = _ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
        _ruleProcessor.ProcessAlertForContacts(
            monitor.FleetNr,
            monitor.SerialId,
            DateTimeUtil.TruncateMillis(DateTime.UtcNow),
            alertLevel,
            0,
            batteryLevel,
            alertType,
            _batteryLevel,
            monitor.Id,
            contacts);
    }
}
