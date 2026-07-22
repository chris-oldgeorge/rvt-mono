using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Communications;
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
        using var factory = LoggerFactory.Create(_ => { });
        RvtLogger.CreateLogger(factory, nameof(SharedRuntimeCompatibilityTests));
    }

    [DataTestMethod]
    [DataRow(AlertType.Alert, MessageService.MessageContent.MessageEnum.Alert)]
    [DataRow(AlertType.Caution, MessageService.MessageContent.MessageEnum.Caution)]
    [DataRow(AlertType.Offline, MessageService.MessageContent.MessageEnum.Offline)]
    [DataRow(AlertType.BatteryAlert, MessageService.MessageContent.MessageEnum.Battery_Alert)]
    [DataRow(AlertType.BatteryCaution, MessageService.MessageContent.MessageEnum.Battery_Caution)]
    public void DurablePlannerPreservesLegacyNotificationAndMessageSelection(
        AlertType alertType,
        MessageService.MessageContent.MessageEnum expectedMessage)
    {
        var request = new RuleNotificationRequest(
            FleetNr: "SV-1",
            SerialId: "SV-157206",
            AlertTime: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            LimitOn: 70,
            AveragingPeriod: 900,
            Level: 75.5,
            alertType,
            Field: "LAeq",
            MonitorId: Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var contacts = new List<Rvt.Monitor.Common.Rules.RvtContactDto>
        {
            new(true, false, "alerts@example.test", null, null, null)
        };
        var messages = new RecordingMessageService();
        Rvt.Monitor.Common.Rules.NotificationDto? legacyNotification = null;
        var legacyDispatcher = new RuleAlertNotificationDispatcher(
            messages,
            notification => legacyNotification = notification,
            (_, _, _) => { });

        legacyDispatcher.ProcessAlertForContacts(request, contacts);
        var plan = new RuleAlertDeliveryPlanner().Plan(
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

        var emails = plan.Deliveries.Where(delivery => delivery.Kind == MonitorDeliveryKind.Email).ToList();
        Assert.HasCount(1, emails);
        var email = emails[0];
        var payload = MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
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
        var contact = new Rvt.Monitor.Common.Rules.RvtContactDto(
            Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail,
            "alerts@example.test",
            "441234567890",
            email: true,
            sms: true,
            sendStartTime: null,
            sendEndTime: null);

        Assert.AreEqual(Rvt.Monitor.Common.Rules.ContactMethod.SMSAndEmail, contact.ContactMethod);
        Assert.IsNotInstanceOfType<Rvt.Monitor.Common.Notifications.RvtContactDto>(contact);

        var notificationContact = contact.ToNotificationDto();
        Assert.AreEqual(Rvt.Monitor.Common.Notifications.ContactMethod.SMSAndEmail, notificationContact.ContactMethod);
        Assert.AreEqual(contact.EmailAddress, notificationContact.EmailAddress);

        var rulesContact = Rvt.Monitor.Common.Rules.RvtContactDto.FromNotificationDto(notificationContact);
        Assert.AreEqual(contact.ContactMethod, rulesContact.ContactMethod);
        Assert.AreEqual(contact.PhoneNumber, rulesContact.PhoneNumber);
    }

    [TestMethod]
    public void RulesContactDtoDoesNotHideDuplicateReflectedOrSerializedContactMethodProperties()
    {
        var contact = new Rvt.Monitor.Common.Rules.RvtContactDto(
            Rvt.Monitor.Common.Rules.ContactMethod.Email,
            "alerts@example.test",
            null,
            sendStartTime: null,
            sendEndTime: null);

        var reflectedContactMethodProperties = typeof(Rvt.Monitor.Common.Rules.RvtContactDto)
            .GetProperties()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))
            .ToList();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(contact));
        var serializedContactMethodProperties = json.RootElement
            .EnumerateObject()
            .Where(property => property.Name == nameof(Rvt.Monitor.Common.Rules.RvtContactDto.ContactMethod))
            .ToList();

        Assert.AreEqual(1, reflectedContactMethodProperties.Count);
        Assert.AreEqual(1, serializedContactMethodProperties.Count);
    }

    [TestMethod]
    public void MaintainsRulesNotificationDtoCompatibilitySurface()
    {
        var notification = new Rvt.Monitor.Common.Rules.NotificationDto(
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
        var activity = new Rvt.Monitor.Common.Notifications.AlertActivityTimeDto
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
        public List<MessageService.MessageContent.MessageEnum> Messages { get; } = [];

        public Task SendMessageAsync(
            MessageService.MessageContent.MessageEnum message,
            MessageService.MessageContent.MessageTypeEnum messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "",
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public void Sendmessage(
            MessageService.MessageContent.MessageEnum message,
            MessageService.MessageContent.MessageTypeEnum messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "") => Messages.Add(message);

        public void SendMessage(
            MessageService.MessageContent.MessageEnum message,
            MessageService.MessageContent.MessageTypeEnum messsageType,
            Rvt.Monitor.Common.Notifications.RvtContactDto contact,
            string MonitorName,
            string url = "") => Messages.Add(message);
    }
}
