using Rvt.Communication.Abstractions;

namespace Rvt.Monitor.Common.Alerts;

/// <summary>
/// The MQTT alert channel's counterpart to <see cref="EmailDeliveryException"/>
/// and <see cref="SmsDeliveryException"/>. MQTT has no communication port, so
/// the exception lives with the alert adapters rather than in the provider
/// abstractions, but it classifies through the same dispatch policy.
/// </summary>
public sealed class MqttAlertDeliveryException(
    DeliveryFailureKind failureKind,
    string? code = null,
    Exception? innerException = null)
    : DeliveryException("Mqtt", "MQTT", failureKind, code, retryAfter: null, innerException);
