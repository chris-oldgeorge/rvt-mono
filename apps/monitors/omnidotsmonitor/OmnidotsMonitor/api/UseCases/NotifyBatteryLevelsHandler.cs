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
        private static readonly int _batteryLevelPercentCaution = 20;
        private static readonly int _batteryLevelPercentAlert = 10;
        private static readonly string _batteryLevel = "Battery level";

        private readonly OmnidotsMonitorReader _monitorReader;
        private readonly IOmnidotsMonitorCommands _monitorCommands;
        private readonly IAlertIngressPort _alertIngress;

        public NotifyBatteryLevelsHandler(
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            IAlertIngressPort alertIngress)
        {
            _monitorReader = monitorReader;
            _monitorCommands = monitorCommands;
            _alertIngress = alertIngress;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();

            foreach (VibrationMonitorDto monitor in monitors)
            {
                if (monitor.Sensor != null)
                {
                    int batteryLevel = monitor.Sensor!.BatteryCharge;
                    if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                    {
                        RvtLogger.Logger.LogDebug("NotifyBatteryLevels Battery level={Value1} for serialId={Value2} status={Value3}", batteryLevel, monitor.SerialId!, monitor.BatteryStatus!);
                    }

                    if (batteryLevel < 0) // -1 means there is no valid value for battery level so ignore
                    {
                        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                        {
                            RvtLogger.Logger.LogInformation("NotifyBatteryLevels Battery data missing level={Value1} for serialId={Value2} ", batteryLevel, monitor.SerialId!);
                        }
                    }
                    else if (batteryLevel <= _batteryLevelPercentAlert)
                    {
                        if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryAlert)
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing ALERT because monitor serialId={Value1} is already at BATTERY ALERT",
                                monitor.SerialId!);
                            }
                            continue;
                        }

                        if (RvtLogger.Logger.IsEnabled(LogLevel.Warning))
                        {
                            RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery ALERT level={Value1} for serialId={Value2} below alert level={Value3}",
                            batteryLevel, monitor.SerialId!, _batteryLevelPercentAlert);
                        }
                        await ProcessBatteryAlertAsync(batteryLevel, monitor, _batteryLevelPercentAlert, AlertType.BatteryAlert, cancellationToken);
                    }
                    else if (batteryLevel <= _batteryLevelPercentCaution)
                    {

                        if (monitor.BatteryStatus == OmnidotsApi.BatteryAlertType.BatteryCaution)
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing CAUTION because monitor serialId={Value1}  is already at BATTERY CAUTION",
                                monitor.SerialId!);
                            }
                            continue;
                        }

                        if (RvtLogger.Logger.IsEnabled(LogLevel.Warning))
                        {
                            RvtLogger.Logger.LogWarning("NotifyBatteryLevels Battery CAUTION level={Value1} for serialId={Value2} below alert level={Value3}",
                            batteryLevel, monitor.SerialId!, _batteryLevelPercentCaution);
                        }
                        await ProcessBatteryAlertAsync(batteryLevel, monitor, _batteryLevelPercentCaution, AlertType.BatteryCaution, cancellationToken);

                    }
                    else
                    {
                        if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
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
                    if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                    {
                        RvtLogger.Logger.LogDebug("No sensor attached to measuring point serialId={Value1}", monitor.SerialId);
                    }
                }

            }

        }

        private Task<AlertIngressResult> ProcessBatteryAlertAsync(
            int batteryLevel,
            VibrationMonitorDto monitor,
            int alertLevel,
            AlertType alertType,
            CancellationToken cancellationToken)
        {
            _monitorCommands.SetMonitorBatteryStatus(monitor.Id, (byte)(alertType == AlertType.BatteryAlert ? 1 : 2));  //1 for alert and 2 for Caution
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
                    Field: _batteryLevel,
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
