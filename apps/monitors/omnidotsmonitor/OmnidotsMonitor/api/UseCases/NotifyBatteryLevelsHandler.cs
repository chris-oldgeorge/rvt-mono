using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.UseCases
{
    // Summary: Raises battery caution/alert notifications from measuring-point battery charge levels.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiBattery).
    public class NotifyBatteryLevelsHandler
    {
        private static readonly int BATTERY_LEVEL_PERCENT_CAUTION = 20;
        private static readonly int BATTERY_LEVEL_PERCENT_ALERT = 10;
        private static readonly string BATTERY_LEVEL = "Battery level";

        private readonly OmnidotsMonitorReader monitorReader;
        private readonly IOmnidotsMonitorCommands monitorCommands;
        private readonly OmnidotsRuleProcessor ruleProcessor;

        public NotifyBatteryLevelsHandler(
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            OmnidotsRuleProcessor ruleProcessor)
        {
            this.monitorReader = monitorReader;
            this.monitorCommands = monitorCommands;
            this.ruleProcessor = ruleProcessor;
        }

        public void Run()
        {
            var monitors = monitorReader.ReadMonitors();

            foreach (var monitor in monitors)
            {
                if (monitor.Sensor != null)
                {
                    var batteryLevel = monitor.Sensor!.BatteryCharge;
                    RvtLogger.Logger.LogDebug("NotifyBatteryLevels Battery level={Value1} for serialId={Value2} status={Value3}", batteryLevel, monitor.SerialId!, monitor.BatteryStatus!);

                    if (batteryLevel < 0) // -1 means there is no valid value for battery level so ignore
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery data missing level={Value1} for serialId={Value2} ", batteryLevel, monitor.SerialId!);
                    }
                    else if (batteryLevel <= BATTERY_LEVEL_PERCENT_ALERT)
                    {
                        if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryAlert)
                        {
                            RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing ALERT because monitor serialId={Value1} is already at BATTERY ALERT",
                            monitor.SerialId!);
                            continue;
                        }

                        RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery ALERT level={Value1} for serialId={Value2} below alert level={Value3}",
                        batteryLevel, monitor.SerialId!, BATTERY_LEVEL_PERCENT_ALERT);
                        ProcessBatteryAlert(batteryLevel, monitor, BATTERY_LEVEL_PERCENT_ALERT, AlertType.BatteryAlert);
                    }
                    else if (batteryLevel <= BATTERY_LEVEL_PERCENT_CAUTION)
                    {

                        if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryCaution)
                        {
                            RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing CAUTION because monitor serialId={Value1}  is already at BATTERY CAUTION",
                            monitor.SerialId!);
                            continue;
                        }

                        RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery CAUTION level={Value1} for serialId={Value2} below alert level={Value3}",
                        batteryLevel, monitor.SerialId!, BATTERY_LEVEL_PERCENT_CAUTION);
                        ProcessBatteryAlert(batteryLevel, monitor, BATTERY_LEVEL_PERCENT_CAUTION, AlertType.BatteryCaution);

                    }
                    else
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery OK level={Value1} for serialId={Value2} is above caution level={Value3}",
                        batteryLevel, monitor.SerialId!, BATTERY_LEVEL_PERCENT_CAUTION);
                        if (monitor.BatteryStatus != OmnidotsApi.BatteryAlertType.Off)
                            monitorCommands.SetMonitorBatteryStatus(monitor.Id, 0);
                    }
                }
                else
                {
                    RvtLogger.Logger.LogDebug("No sensor attached to measuring point serialId={Value1}", monitor.SerialId);
                }

            }
        }

        private void ProcessBatteryAlert(int batteryLevel, VibrationMonitorDto monitor, int alertLevel, AlertType alertType)
        {
            monitorCommands.SetMonitorBatteryStatus(monitor.Id, (byte)(alertType == AlertType.BatteryAlert ? 1 : 2));  //1 for alert and 2 for Caution
            var createdTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow);

            var notification = new NotificationDto(id: Guid.NewGuid(),
                notificationTime: createdTime,
                limitOn: alertLevel,
                averagingPeriod: 0,
                level: batteryLevel,
                closedTime: null,
                closedByUser: null,
                alertType: alertType,
                alertField: BATTERY_LEVEL,
                monitorId: monitor.Id);

            ruleProcessor.ProcessAlertForContacts(monitor, notification);

        }
    }
}
