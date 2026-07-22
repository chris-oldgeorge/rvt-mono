using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Mqtt;

namespace Rvt.Monitor.Common.Alerts;

public sealed class MqttAlertDeliveryAdapter(IMonitorEventPublisher publisher) : IAlertDeliveryAdapter
{
    public string Kind => AlertDeliveryAdapterValidation.MqttKind;

    public async Task<AlertDeliveryAudit?> DeliverAsync(
        ClaimedAlertDelivery delivery,
        CancellationToken cancellationToken)
    {
        var envelope = AlertDeliveryAdapterValidation.ReadEnvelope(
            delivery,
            Kind,
            destination => string.Equals(destination, "alert", StringComparison.Ordinal));
        await publisher.PublishAlertAsync(
                envelope.Timestamp,
                envelope.SerialId,
                envelope.Message,
                envelope.CustomerId)
            .WaitAsync(cancellationToken);
        return null;
    }
}
