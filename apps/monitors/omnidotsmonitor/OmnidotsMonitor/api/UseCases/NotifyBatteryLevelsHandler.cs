using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Alerts;
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
        private readonly IAlertIngressPort _alertIngress;

        public NotifyBatteryLevelsHandler(
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            IAlertIngressPort alertIngress)
        {
            this.monitorReader = monitorReader;
            this.monitorCommands = monitorCommands;
            _alertIngress = alertIngress;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<VibrationMonitorDto> monitors = monitorReader.ReadMonitors();

            foreach (VibrationMonitorDto monitor in monitors)
            {
                if (monitor.Sensor != null)
                {
                    int batteryLevel = monitor.Sensor!.BatteryCharge;
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
                        await ProcessBatteryAlertAsync(batteryLevel, monitor, BATTERY_LEVEL_PERCENT_ALERT, AlertType.BatteryAlert, cancellationToken);
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
                        await ProcessBatteryAlertAsync(batteryLevel, monitor, BATTERY_LEVEL_PERCENT_CAUTION, AlertType.BatteryCaution, cancellationToken);

                    }
                    else
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery OK level={Value1} for serialId={Value2} is above caution level={Value3}",
                        batteryLevel, monitor.SerialId!, BATTERY_LEVEL_PERCENT_CAUTION);
                        if (monitor.BatteryStatus != OmnidotsApi.BatteryAlertType.Off)
                        {
                            monitorCommands.SetMonitorBatteryStatus(monitor.Id, 0);
                        }
                    }
                }
                else
                {
                    RvtLogger.Logger.LogDebug("No sensor attached to measuring point serialId={Value1}", monitor.SerialId);
                }

            }

        }

        private Task ProcessBatteryAlertAsync(
            int batteryLevel,
            VibrationMonitorDto monitor,
            int alertLevel,
            AlertType alertType,
            CancellationToken cancellationToken)
        {
            monitorCommands.SetMonitorBatteryStatus(monitor.Id, (byte)(alertType == AlertType.BatteryAlert ? 1 : 2));  //1 for alert and 2 for Caution
            DateTime createdTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow);

            // The durable stack writes the notification, plans per-contact
            // deliveries, and retries them; the status transition above gates
            // duplicates.
            return _alertIngress.AcceptAsync(
                new AlertSignal(
                    Source: "omnidots.battery",
                    SourceEventKey: $"{monitor.SerialId}:{alertType}:{createdTime:O}",
                    EventTime: createdTime,
                    SerialId: monitor.SerialId!,
                    AlertType: alertType,
                    Field: BATTERY_LEVEL,
                    Level: batteryLevel,
                    Limit: alertLevel,
                    AveragingPeriod: 0,
                    Message: $"Battery level {batteryLevel}% (limit {alertLevel}%)",
                    DeliveryChannels: AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms,
                    SuppressionWindow: TimeSpan.Zero),
                cancellationToken);
        }
    }
}
