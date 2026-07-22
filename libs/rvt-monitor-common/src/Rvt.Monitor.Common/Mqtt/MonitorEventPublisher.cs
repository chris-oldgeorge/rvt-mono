using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;

namespace Rvt.Monitor.Common.Mqtt;

// Summary: Publishes shared monitor lifecycle events (data inserted, alert raised) to the RVT MQTT topics.
public interface IMonitorEventPublisher
{
    void PublishDataInserted(DateTime timestamp, string serialId, int? customerId = null);

    void PublishAlert(DateTime timestamp, string serialId, string message, int? customerId = null);

    Task PublishDataInsertedAsync(DateTime timestamp, string serialId, int? customerId = null)
    {
        PublishDataInserted(timestamp, serialId, customerId);
        return Task.CompletedTask;
    }

    Task PublishAlertAsync(DateTime timestamp, string serialId, string message, int? customerId = null)
    {
        PublishAlert(timestamp, serialId, message, customerId);
        return Task.CompletedTask;
    }
}

// Summary: Serializes RvtMqttMessage payloads and fire-and-forget publishes them via the RVT MQTT client.
// Major updates:
// - 2026-07-12 MQTT centralization: replaced per-monitor inline PublishAsync calls with one shared publisher.
// - 2026-07-12 RvtConfig cleanup: topics are injected instead of read from static configuration.
public class MonitorEventPublisher : IMonitorEventPublisher
{
    private const string DataInsertedMessage = "Dto Inserted";

    private readonly IMqttClient mqttClient;
    private readonly string insertTopic;
    private readonly string alertTopic;

    public MonitorEventPublisher(IMqttClient mqttClient, string insertTopic, string alertTopic)
    {
        this.mqttClient = mqttClient;
        this.insertTopic = insertTopic;
        this.alertTopic = alertTopic;
    }

    public void PublishDataInserted(DateTime timestamp, string serialId, int? customerId = null)
    {
        PublishDataInsertedAsync(timestamp, serialId, customerId).GetAwaiter().GetResult();
    }

    public void PublishAlert(DateTime timestamp, string serialId, string message, int? customerId = null)
    {
        PublishAlertAsync(timestamp, serialId, message, customerId).GetAwaiter().GetResult();
    }

    public Task PublishDataInsertedAsync(DateTime timestamp, string serialId, int? customerId = null) =>
        PublishAsync(insertTopic, timestamp, serialId, DataInsertedMessage, customerId);

    public Task PublishAlertAsync(DateTime timestamp, string serialId, string message, int? customerId = null) =>
        PublishAsync(alertTopic, timestamp, serialId, message, customerId);

    private Task PublishAsync(string topic, DateTime timestamp, string serialId, string message, int? customerId)
    {
        var mqttMessage = customerId.HasValue
            ? new RvtMqttMessage(timestamp, customerId.Value, serialId, message)
            : new RvtMqttMessage(timestamp, serialId, message);

        return mqttClient.PublishAsync(topic, JsonSerializer.Serialize(mqttMessage));
    }
}
