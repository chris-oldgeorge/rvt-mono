using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.CommonTests.Rules;

[TestClass]
public sealed class SharedRuntimeCompatibilityTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        RvtLogger.CreateLogger(factory, nameof(SharedRuntimeCompatibilityTests));
    }

    [TestMethod]
    [DataRow(AlertType.Alert, LegacyMessageKind.Alert)]
    [DataRow(AlertType.Caution, LegacyMessageKind.Caution)]
    [DataRow(AlertType.Offline, LegacyMessageKind.Offline)]
    [DataRow(AlertType.BatteryAlert, LegacyMessageKind.Battery_Alert)]
    [DataRow(AlertType.BatteryCaution, LegacyMessageKind.Battery_Caution)]
    public void DurablePlannerPreservesLegacyNotificationAndMessageSelection(
        AlertType alertType,
        LegacyMessageKind expectedMessage)
    {
        RuleNotificationRequest request = new(
            FleetNr: "SV-1",
            SerialId: "SV-157206",
            AlertTime: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            LimitOn: 70,
            AveragingPeriod: 900,
            Level: 75.5,
            alertType,
            Field: "LAeq",
            MonitorId: Guid.Parse("11111111-2222-3333-4444-555555555555"));
        List<Common.Rules.RvtContactDto> contacts =
        [
            new(true, false, "alerts@example.test", null, null, null)
        ];
        RecordingMessageService messages = new();
        Rvt.Monitor.Common.Rules.NotificationDto? legacyNotification = null;
        RuleAlertNotificationDispatcher legacyDispatcher = new(
            messages,
            notification => legacyNotification = notification,
            (_, _, _) => { });

        legacyDispatcher.ProcessAlertForContacts(request, contacts);
        RuleAlertDeliveryPlan plan = new RuleAlertDeliveryPlanner().Plan(
            request,
            contacts,
            MonitorDeliveryProducers.Svantek,
            customerId: null,
            correlationKey: $"compatibility:{alertType}",
            createdAt: request.AlertTime);

        Assert.IsNotNull(legacyNotification);
        Assert.AreEqual(legacyNotification.NotificationTime, plan.Notification.NotificationTime);
        Assert.AreEqual(legacyNotification.LimitOn, plan.Notification.LimitOn);
        Assert.AreEqual(legacyNotification.AveragingPeriod, plan.Notification.AveragingPeriod);
        Assert.AreEqual(legacyNotification.Level, plan.Notification.Level);
        Assert.AreEqual(legacyNotification.AlertType, plan.Notification.AlertType);
        Assert.AreEqual(legacyNotification.AlertField, plan.Notification.AlertField);
        Assert.AreEqual(legacyNotification.MonitorId, plan.Notification.MonitorId);
        Assert.HasCount(1, messages.Messages);
        Assert.AreEqual(expectedMessage, messages.Messages[0]);

        List<MonitorDeliveryRequest> emails = [.. plan.Deliveries.Where(delivery => delivery.Kind == MonitorDeliveryKind.Email)];
        Assert.HasCount(1, emails);
        MonitorDeliveryRequest email = emails[0];
        MonitorDeliveryPayloadV1 payload = MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
            email.Id,
            email.Producer,
            email.NotificationId,
            email.CorrelationKey,
            email.DeliveryKey,
            email.Kind,
            email.Destination,
            email.PayloadVersion,
            email.Payload,
            AttemptCount: 1,
            LeaseId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
        Assert.AreEqual(alertType, payload.AlertType);
    }

    [TestMethod]
    public void MaintainsRulesContactDtoCompatibilitySurface()
    {
        Common.Rules.RvtContactDto contact = new(
            Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail,
            "alerts@example.test",
            "441234567890",
            email: true,
            sms: true,
            sendStartTime: null,
            sendEndTime: null);

        Assert.AreEqual(Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail, contact.ContactMethod);
        Assert.IsNotInstanceOfType<Rvt.Monitor.Common.Notifications.RvtContactDto>(contact);

        Common.Notifications.RvtContactDto notificationContact = contact.ToNotificationDto();
        Assert.AreEqual(Rvt.Monitor.Common.Notifications.ContactMethod.SMSAndEmail, notificationContact.ContactMethod);
        Assert.AreEqual(contact.EmailAddress, notificationContact.EmailAddress);

    }

    [TestMethod]
    public void RulesContactDtoDoesNotHideDuplicateReflectedOrSerializedContactMethodProperties()
    {
        Common.Rules.RvtContactDto contact = new(
            Rvt.Monitor.Common.Rules.ContactMethod.Email,
            "alerts@example.test",
            null,
            sendStartTime: null,
            sendEndTime: null);

        List<PropertyInfo> reflectedContactMethodProperties = [.. typeof(Rvt.Monitor.Common.Rules.RvtContactDto)
            .GetProperties()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))];

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(contact));
        List<JsonProperty> serializedContactMethodProperties = [.. json.RootElement
            .EnumerateObject()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))];

        Assert.HasCount(1, reflectedContactMethodProperties);
        Assert.HasCount(1, serializedContactMethodProperties);
    }

    [TestMethod]
    public void MaintainsRulesNotificationDtoCompatibilitySurface()
    {
        Common.Rules.NotificationDto notification = new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            limitOn: 10,
            averagingPeriod: 900,
            level: 11,
            closedTime: null,
            closedByUser: null,
            alertType: Rvt.Monitor.Common.Notifications.AlertType.Alert,
            alertField: "LAeq",
            monitorId: Guid.NewGuid());

        Assert.IsInstanceOfType<Rvt.Monitor.Common.Notifications.NotificationDto>(notification);
    }

    [TestMethod]
    public void MaintainsNotificationAlertActivityTimeDtoCompatibilitySurface()
    {
        Common.Notifications.AlertActivityTimeDto activity = new()
        {
            Weekdays = true,
            Saturdays = true,
            Sundays = true
        };

        Assert.IsTrue(activity.IsActive(DateTime.UtcNow));
        Assert.IsInstanceOfType<Rvt.Monitor.Common.Rules.AlertActivityTimeDto>(activity);
    }

    private sealed class RecordingMessageService : IMessageService
    {
        public List<LegacyMessageKind> Messages { get; } = [];

        public Task SendMessageAsync(
            LegacyMessageKind message,
            LegacyMessageChannel messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "",
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public void Sendmessage(
            LegacyMessageKind message,
            LegacyMessageChannel messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "") => Messages.Add(message);

        public void SendMessage(
            LegacyMessageKind message,
            LegacyMessageChannel messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "") => Messages.Add(message);
    }
}
