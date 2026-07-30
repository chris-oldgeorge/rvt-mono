// The namespace is retained from the shared-kernel folder this file moved out
// of, so its consumers keep compiling; IDE0130 would force a rename ripple.
#pragma warning disable IDE0130
using System.Text.Json;
using Rvt.Monitor.Common.Delivery;

namespace Rvt.Monitor.CommonTests.Delivery;

[TestClass]
public sealed class MonitorDeliveryPayloadCodecTests
{
    [TestMethod]
    public void PayloadV1_SerializesWithPascalCasePropertyNames()
    {
        string json = JsonSerializer.Serialize(DeliveryFixture.ValidPayload);

        Assert.Contains("\"NotificationId\"", json);
        Assert.Contains("\"Timestamp\"", json);
        Assert.Contains("\"SerialId\"", json);
        Assert.DoesNotContain("\"notificationId\"", json);
    }

    [TestMethod]
    public void DecodeV1_ReturnsValidPayload()
    {
        MonitorDeliveryPayloadV1 payload = MonitorDeliveryPayloadCodec.Decode(DeliveryFixture.Message());

        Assert.AreEqual(DeliveryFixture.ValidPayload, payload);
    }

    [TestMethod]
    public void Decode_RejectsUnsupportedPayloadVersion()
    {
        MonitorDeliveryMessage message = DeliveryFixture.Message() with { PayloadVersion = 2 };

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsMalformedJson()
    {
        MonitorDeliveryMessage message = DeliveryFixture.Message(payload: "{ invalid json");

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsEmptySerialId()
    {
        MonitorDeliveryPayloadV1 payload = DeliveryFixture.ValidPayload with { SerialId = " " };
        MonitorDeliveryMessage message = DeliveryFixture.Message(payload: JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_RejectsNonUtcTimestamp()
    {
        MonitorDeliveryPayloadV1 payload = DeliveryFixture.ValidPayload with
        {
            Timestamp = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified)
        };
        MonitorDeliveryMessage message = DeliveryFixture.Message(payload: JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    [DataRow(MonitorDeliveryKind.MqttAlert)]
    [DataRow(MonitorDeliveryKind.Email)]
    [DataRow(MonitorDeliveryKind.Sms)]
    public void DecodeV1_RejectsEmptyNotificationForAlertOrContactDelivery(MonitorDeliveryKind kind)
    {
        MonitorDeliveryPayloadV1 payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        MonitorDeliveryMessage message = DeliveryFixture.Message(kind, JsonSerializer.Serialize(payload));

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_AllowsEmptyNotificationForDataDelivery()
    {
        MonitorDeliveryPayloadV1 payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        MonitorDeliveryMessage message = DeliveryFixture.Message(
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
        MonitorDeliveryPayloadV1 payload = DeliveryFixture.ValidPayload with { NotificationId = Guid.Empty };
        MonitorDeliveryMessage message = DeliveryFixture.Message(
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
        MonitorDeliveryMessage message = DeliveryFixture.Message(kind) with { NotificationId = Guid.NewGuid() };

        Assert.ThrowsExactly<InvalidDataException>(() => MonitorDeliveryPayloadCodec.Decode(message));
    }

    [TestMethod]
    public void DecodeV1_AllowsMissingRowNotificationReference()
    {
        MonitorDeliveryMessage message = DeliveryFixture.Message() with { NotificationId = null };

        Assert.AreEqual(DeliveryFixture.ValidPayload, MonitorDeliveryPayloadCodec.Decode(message));
    }
}
