using System.Text.Json;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Delivery;

internal static class DeliveryFixture
{
    internal static readonly Guid NotificationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid LeaseId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    internal static MonitorDeliveryPayloadV1 ValidPayload { get; } = new(
        NotificationId,
        new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
        "157206",
        null,
        "SV-1",
        AlertType.Alert,
        "LAeq",
        75,
        null);

    internal static MonitorDeliveryMessage Message(
        MonitorDeliveryKind kind = MonitorDeliveryKind.MqttAlert,
        string? payload = null,
        int attemptCount = 1,
        Guid? notificationId = null,
        Guid? leaseId = null) => new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MonitorDeliveryProducers.Svantek,
            notificationId ?? NotificationId,
            "notification:fixture-key",
            "delivery:fixture-key",
            kind,
            "alerts@example.test",
            1,
            payload ?? JsonSerializer.Serialize(ValidPayload),
            attemptCount,
            leaseId ?? LeaseId);
}
