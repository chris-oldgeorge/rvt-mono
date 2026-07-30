using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Mqtt;

namespace Rvt.Monitor.Common.Alerts;

public sealed class MqttAlertDeliveryAdapter(
    IMonitorEventPublisher publisher,
    MqttOptions options) : IAlertDeliveryAdapter
{
    public string Kind => AlertDeliveryAdapterValidation.MqttKind;

    public async Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken)
    {
        AlertDeliveryEnvelope envelope = AlertDeliveryAdapterValidation.ReadEnvelope(
            delivery,
            Kind,
            destination => string.Equals(destination, "alert", StringComparison.Ordinal));

        // A disabled broker used to publish nothing and report success, so the
        // row completed without anything being sent. Rows planned before the
        // channel was disabled now dead-letter visibly instead.
        if (!options.Enabled)
        {
            throw new MqttAlertDeliveryException(DeliveryFailureKind.Configuration, "disabled");
        }

        await publisher.PublishAlertAsync(
            envelope.Timestamp,
            envelope.SerialId,
            envelope.Message,
            envelope.CustomerId,
            cancellationToken);
        return null;
    }
}
