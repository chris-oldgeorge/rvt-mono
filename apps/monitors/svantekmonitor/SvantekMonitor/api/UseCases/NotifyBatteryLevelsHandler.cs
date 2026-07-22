using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;
using Svantek.Api.Db;
using SvantekMonitor.model.dto;

namespace Svantek.Api.UseCases;

// Summary: Raises battery caution/alert notifications from monitor battery charge levels.
public sealed class NotifyBatteryLevelsHandler
{
    private const int BatteryLevelPercentCaution = 20;
    private const int BatteryLevelPercentAlert = 10;
    private const string BatteryLevel = "Battery level";

    private readonly SvantekMonitorReader monitorReader;
    private readonly ISvantekRuleQueries ruleQueries;
    private readonly ISvantekMonitorCommands monitorCommands;
    private readonly ISvantekOperationalCommands operationalCommands;
    private readonly SvantekRuleProcessor ruleProcessor;

    public NotifyBatteryLevelsHandler(
        SvantekMonitorReader monitorReader,
        ISvantekRuleQueries ruleQueries,
        ISvantekMonitorCommands monitorCommands,
        ISvantekOperationalCommands operationalCommands,
        SvantekRuleProcessor ruleProcessor)
    {
        this.monitorReader = monitorReader;
        this.ruleQueries = ruleQueries;
        this.monitorCommands = monitorCommands;
        this.operationalCommands = operationalCommands;
        this.ruleProcessor = ruleProcessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var monitors = await monitorReader.ReadMonitorsAsync(
            lastDataTime: null,
            cancellationToken).ConfigureAwait(false);
        var failures = new SvantekFailureCollector(operationalCommands);

        foreach (var monitor in monitors)
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
        var batteryLevel = monitor.BatteryCharge;
        RvtLogger.Logger.LogDebug(
            "NotifyBatteryLevels battery level={BatteryLevel} for serialId={SerialId} status={BatteryStatus}",
            batteryLevel,
            monitor.SerialId,
            monitor.BatteryStatus);

        if (batteryLevel <= BatteryLevelPercentAlert)
        {
            if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.BatteryAlert)
            {
                await ProcessBatteryAlertAsync(
                    batteryLevel,
                    monitor,
                    BatteryLevelPercentAlert,
                    AlertType.BatteryAlert,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else if (batteryLevel <= BatteryLevelPercentCaution)
        {
            if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.BatteryCaution)
            {
                await ProcessBatteryAlertAsync(
                    batteryLevel,
                    monitor,
                    BatteryLevelPercentCaution,
                    AlertType.BatteryCaution,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else if (monitor.BatteryStatus != SvantekApi.BatteryAlertType.Off)
        {
            await monitorCommands.SetMonitorBatteryStatusAsync(
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
        var status = (byte)(alertType == AlertType.BatteryAlert ? 1 : 2);
        await monitorCommands.SetMonitorBatteryStatusAsync(
            monitor.Id,
            status,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var contacts = ruleQueries.ReadAlertContacts(monitor.Id, out Guid _);
        ruleProcessor.ProcessAlertForContacts(
            monitor.FleetNr,
            monitor.SerialId,
            DateTimeUtil.TruncateMillis(DateTime.UtcNow),
            alertLevel,
            0,
            batteryLevel,
            alertType,
            BatteryLevel,
            monitor.Id,
            contacts);
    }
}
