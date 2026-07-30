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
    // - 2026-07-30 Alert correctness: signal before latching the status gate; isolate per-monitor failures.
    public class NotifyBatteryLevelsHandler
    {
        private static readonly int _batteryLevelPercentCaution = 20;
        private static readonly int _batteryLevelPercentAlert = 10;
        private static readonly string _batteryLevel = "Battery level";

        private readonly OmnidotsMonitorReader _monitorReader;
        private readonly IOmnidotsMonitorCommands _monitorCommands;
        private readonly IOmnidotsOperationalCommands _operationalCommands;
        private readonly IAlertIngressPort _alertIngress;

        public NotifyBatteryLevelsHandler(
            OmnidotsMonitorReader monitorReader,
            IOmnidotsMonitorCommands monitorCommands,
            IOmnidotsOperationalCommands operationalCommands,
            IAlertIngressPort alertIngress)
        {
            _monitorReader = monitorReader;
            _monitorCommands = monitorCommands;
            _operationalCommands = operationalCommands;
            _alertIngress = alertIngress;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<VibrationMonitorDto> monitors = _monitorReader.ReadMonitors();

            List<OmnidotsMonitorFailure> failures = [];
            foreach (VibrationMonitorDto monitor in monitors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await ProcessMonitorAsync(monitor, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    string message = $"NotifyBatteryLevels serialId={monitor.SerialId}";
                    failures.Add(OmnidotsMonitorFailure.Record(
                        monitor.SerialId!,
                        exception,
                        () => _operationalCommands.HandleException(message, exception)));
                }
            }

            if (failures.Count > 0)
            {
                throw new OmnidotsImportException("NotifyBatteryLevels", failures);
            }
        }

        private async Task ProcessMonitorAsync(
            VibrationMonitorDto monitor,
            CancellationToken cancellationToken)
        {
            if (monitor.Sensor == null)
            {
                if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
                {
                    RvtLogger.Logger.LogDebug("No sensor attached to measuring point serialId={Value1}", monitor.SerialId);
                }
                return;
            }

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
                if (monitor.BatteryStatus == BatteryAlertType.BatteryAlert)
                {
                    if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing ALERT because monitor serialId={Value1} is already at BATTERY ALERT",
                        monitor.SerialId!);
                    }
                    return;
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
                if (monitor.BatteryStatus == BatteryAlertType.BatteryCaution)
                {
                    if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                    {
                        RvtLogger.Logger.LogInformation("NotifyBatteryLevels not notifing CAUTION because monitor serialId={Value1}  is already at BATTERY CAUTION",
                        monitor.SerialId!);
                    }
                    return;
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
                if (monitor.BatteryStatus != BatteryAlertType.Off)
                {
                    _monitorCommands.SetMonitorBatteryStatus(monitor.Id, 0);
                }
            }
        }

        private async Task ProcessBatteryAlertAsync(
            int batteryLevel,
            VibrationMonitorDto monitor,
            int alertLevel,
            AlertType alertType,
            CancellationToken cancellationToken)
        {
            DateTime createdTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow);

            // The durable stack writes the notification, plans per-contact
            // deliveries, and retries them; the status transition below gates
            // duplicates. Signal first and latch the gate only after the
            // signal is accepted, otherwise a transient AcceptAsync failure
            // would permanently suppress the alert (the offline handler's order).
            await _alertIngress.AcceptAsync(
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

            _monitorCommands.SetMonitorBatteryStatus(monitor.Id, (byte)(alertType == AlertType.BatteryAlert ? 1 : 2));  //1 for alert and 2 for Caution
        }
    }
}
