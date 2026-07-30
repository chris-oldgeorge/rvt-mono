using System.Text.Json;

namespace Rvt.Monitor.Common.Mqtt;

// Summary: Publishes shared monitor lifecycle events (data inserted, alert raised) to the RVT MQTT topics.
public interface IMonitorEventPublisher
{
    Task PublishDataInsertedAsync(
        DateTime timestamp,
        string serialId,
        int? customerId = null,
        CancellationToken cancellationToken = default);

    Task PublishAlertAsync(
        DateTime timestamp,
        string serialId,
        string message,
        int? customerId = null,
        CancellationToken cancellationToken = default);
}

// Summary: Serializes RvtMqttMessage payloads and fire-and-forget publishes them via the RVT MQTT client.
// Major updates:
// - 2026-07-12 MQTT centralization: replaced per-monitor inline PublishAsync calls with one shared publisher.
// - 2026-07-12 RvtConfig cleanup: topics are injected instead of read from static configuration.
public class MonitorEventPublisher(IMqttClient mqttClient, string insertTopic, string alertTopic) : IMonitorEventPublisher
{
    private const string _dataInsertedMessage = "Dto Inserted";

    private readonly IMqttClient _mqttClient = mqttClient;
    private readonly string _insertTopic = insertTopic;
    private readonly string _alertTopic = alertTopic;

    public Task PublishDataInsertedAsync(
        DateTime timestamp,
        string serialId,
        int? customerId = null,
        CancellationToken cancellationToken = default) =>
        PublishAsync(_insertTopic, timestamp, serialId, _dataInsertedMessage, customerId, cancellationToken);

    public Task PublishAlertAsync(
        DateTime timestamp,
        string serialId,
        string message,
        int? customerId = null,
        CancellationToken cancellationToken = default) =>
        PublishAsync(_alertTopic, timestamp, serialId, message, customerId, cancellationToken);

    private Task PublishAsync(
        string topic,
        DateTime timestamp,
        string serialId,
        string message,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        RvtMqttMessage mqttMessage = customerId.HasValue
            ? new RvtMqttMessage(timestamp, customerId.Value, serialId, message)
            : new RvtMqttMessage(timestamp, serialId, message);

        return _mqttClient.PublishAsync(topic, JsonSerializer.Serialize(mqttMessage), cancellationToken);
    }
}
