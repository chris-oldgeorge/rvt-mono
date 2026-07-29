using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Rvt.Monitor.Common.Mqtt;

// Summary: Publishes shared monitor lifecycle events (data inserted, alert raised) to the RVT MQTT topics.
public interface IMonitorEventPublisher
{
    void PublishAlert(DateTime timestamp, string serialId, string message, int? customerId = null);

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
        CancellationToken cancellationToken = default)
    {
        PublishAlert(timestamp, serialId, message, customerId);
        return Task.CompletedTask;
    }
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

    /// <summary>
    /// Blocking entry point retained only for the legacy synchronous rule
    /// evaluator. Callers that can await should use the asynchronous members;
    /// every asynchronous import path already does.
    /// </summary>
    public void PublishAlert(DateTime timestamp, string serialId, string message, int? customerId = null)
    {
        PublishAlertAsync(timestamp, serialId, message, customerId).GetAwaiter().GetResult();
    }

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
