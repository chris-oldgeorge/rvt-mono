using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.UseCases;

// Summary: Raises battery caution/alert notifications from measuring-point battery charge levels.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiBattery).
public class NotifyBatteryLevelsHandler(
    OmnidotsMonitorReader monitorReader,
    IOmnidotsMonitorCommands monitorCommands,
    OmnidotsRuleProcessor ruleProcessor)
{
    private static readonly int _batteryLevelPercentCaution = 20;
    private static readonly int _batteryLevelPercentAlert = 10;
    private static readonly string _batteryLevel = "Battery level";

    private readonly OmnidotsMonitorReader _monitorReader = monitorReader;
    private readonly IOmnidotsMonitorCommands _monitorCommands = monitorCommands;
    private readonly OmnidotsRuleProcessor _ruleProcessor = ruleProcessor;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();

        foreach (VibrationMonitorDto monitor in monitors)
        {
            if (monitor.Sensor != null)
            {
                int batteryLevel = monitor.Sensor!.BatteryCharge;
                if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("NotifyBatteryLevels Battery level={Value1} for serialId={Value2} status={Value3}", batteryLevel, monitor.SerialId!, monitor.BatteryStatus!); }

                if (batteryLevel < 0) // -1 means there is no valid value for battery level so ignore
                {
                    if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information)) { RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery data missing level={Value1} for serialId={Value2} ", batteryLevel, monitor.SerialId!); }
                }
                else if (batteryLevel <= _batteryLevelPercentAlert)
                {
                    if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryAlert)
                    {
                        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                        {
                            RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing ALERT because monitor serialId={Value1} is already at BATTERY ALERT",
                        monitor.SerialId!);
                        }
                        continue;
                    }

                    RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery ALERT level={Value1} for serialId={Value2} below alert level={Value3}",
                    batteryLevel, monitor.SerialId!, _batteryLevelPercentAlert);
                    ProcessBatteryAlert(batteryLevel, monitor, _batteryLevelPercentAlert, AlertType.BatteryAlert);
                }
                else if (batteryLevel <= _batteryLevelPercentCaution)
                {

                    if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryCaution)
                    {
                        if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                        {
                            RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing CAUTION because monitor serialId={Value1}  is already at BATTERY CAUTION",
                        monitor.SerialId!);
                        }
                        continue;
                    }

                    RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery CAUTION level={Value1} for serialId={Value2} below alert level={Value3}",
                    batteryLevel, monitor.SerialId!, _batteryLevelPercentCaution);
                    ProcessBatteryAlert(batteryLevel, monitor, _batteryLevelPercentCaution, AlertType.BatteryCaution);

                }
                else
                {
                    if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery OK level={Value1} for serialId={Value2} is above caution level={Value3}",
                    batteryLevel, monitor.SerialId!, _batteryLevelPercentCaution);
                    }
                    if (monitor.BatteryStatus != OmnidotsApi.BatteryAlertType.Off)
                    {
                        _monitorCommands.SetMonitorBatteryStatus(monitor.Id, 0);
                    }
                }
            }
            else
            {
                if (RvtLogger.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) { RvtLogger.Logger.LogDebug("No sensor attached to measuring point serialId={Value1}", monitor.SerialId); }
            }

        }

        return Task.CompletedTask;
    }

    private void ProcessBatteryAlert(int batteryLevel, VibrationMonitorDto monitor, int alertLevel, AlertType alertType)
    {
        _monitorCommands.SetMonitorBatteryStatus(monitor.Id, (byte)(alertType == AlertType.BatteryAlert ? 1 : 2));  //1 for alert and 2 for Caution
        DateTime createdTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow);

        NotificationDto notification = new(id: Guid.NewGuid(),
            notificationTime: createdTime,
            limitOn: alertLevel,
            averagingPeriod: 0,
            level: batteryLevel,
            closedTime: null,
            closedByUser: null,
            alertType: alertType,
            alertField: _batteryLevel,
            monitorId: monitor.Id);

        _ruleProcessor.ProcessAlertForContacts(monitor, notification);

    }
}
