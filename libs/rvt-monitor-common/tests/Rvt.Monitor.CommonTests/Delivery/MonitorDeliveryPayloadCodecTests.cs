using System.Text.Json;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Notifications;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryPayloadCodecTests
{
    [TestMethod]
    public void PayloadV1_SerializesWithPascalCasePropertyNames()
    {
        var json = JsonSerializer.Serialize(DeliveryFixture.ValidPayload);

        Assert.Contains("\"NotificationId\"", json);
        Assert.Contains("\"Timestamp\"", json);
        Assert.Contains("\"SerialId\"", json);
        Assert.DoesNotContain("\"notificationId\"", json);
    }

    [TestMethod]
    public void DecodeV1_ReturnsValidPayload()
    {
        var payload = MonitorDeliveryPayloadCodec.Decode(DeliveryFixture.Message());

        Assert.AreEqual(DeliveryFixture.ValidPayload, payload);
    }

    [TestMethod]
    public void Decode_RejectsUnsupportedPayloadVersion()
    {
        var message = DeliveryFixture.Message() with { PayloadVersion = 2 };

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsMalformedJson()
    {
        var message = DeliveryFixture.Message(payload: "{ invalid json");

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsEmptySerialId()
    {
        var payload = DeliveryFixture.ValidPayload with { SerialId = " " };
        var message = DeliveryFixture.Message(payload: JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsNonUtcTimestamp()
    {
        var payload = DeliveryFixture.ValidPayload with
        {
            Timestamp = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified)
        };
        var message = DeliveryFixture.Message(payload: JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    [DataRow(MonitorDeliveryKind.MqttAlert)]
    [DataRow(MonitorDeliveryKind.Email)]
    [DataRow(MonitorDeliveryKind.Sms)]
    public void DecodeV1_RejectsEmptyNotificationForAlertOrContactDelivery(MonitorDeliveryKind kind)
    {
        var payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        var message = DeliveryFixture.Message(kind, JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_AllowsEmptyNotificationForDataDelivery()
    {
        var payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        var message = DeliveryFixture.Message(
            MonitorDeliveryKind.MqttDataInserted,
            JsonSerializer.Serialize(payload)) with
        {
            NotificationId = null
        };

        Assert.AreEqual(payload, MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsEmptyNotificationForUnknownDeliveryKind()
    {
        var payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        var message = DeliveryFixture.Message(
            (MonitorDeliveryKind)99,
            JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    [DataRow(MonitorDeliveryKind.MqttAlert)]
    [DataRow(MonitorDeliveryKind.Email)]
    [DataRow(MonitorDeliveryKind.Sms)]
    public void DecodeV1_RejectsMismatchedRowNotificationForAlertOrContactDelivery(MonitorDeliveryKind kind)
    {
        var message = DeliveryFixture.Message(kind) with { NotificationId = Guid.NewGuid() };

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_AllowsMissingRowNotificationReference()
    {
        var message = DeliveryFixture.Message() with { NotificationId = null };

        Assert.AreEqual(DeliveryFixture.ValidPayload, MonitorDeliveryPayloadCodec.Decode(message));
    }
}
