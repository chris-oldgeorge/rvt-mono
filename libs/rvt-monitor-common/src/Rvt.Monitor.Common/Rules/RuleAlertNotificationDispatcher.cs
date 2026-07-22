using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.Common.Rules;

public sealed class RuleAlertNotificationDispatcher
{
    private readonly IMessageService messageService;
    private readonly Action<NotificationDto> writeNotification;
    private readonly Action<Guid, string, string> writeNotificationAudit;

    public RuleAlertNotificationDispatcher(
        IMessageService messageService,
        Action<NotificationDto> writeNotification,
        Action<Guid, string, string> writeNotificationAudit)
    {
        this.messageService = messageService;
        this.writeNotification = writeNotification;
        this.writeNotificationAudit = writeNotificationAudit;
    }

    public void ProcessAlertForContacts(RuleNotificationRequest request, List<RvtContactDto> contacts)
    {
        var notification = new NotificationDto(
            id: Guid.NewGuid(),
            notificationTime: request.AlertTime,
            limitOn: request.LimitOn,
            averagingPeriod: request.AveragingPeriod,
            level: request.Level,
            closedTime: null,
            closedByUser: null,
            alertType: request.AlertType,
            alertField: request.Field,
            monitorId: request.MonitorId);

        writeNotification(notification);

        if (contacts == null || contacts.Count == 0)
        {
            return;
        }

        var messageToSend = ToMessage(request.AlertType);
        var notificationUrl = request.AlertType is Rvt.Monitor.Common.Notifications.AlertType.Alert or Rvt.Monitor.Common.Notifications.AlertType.Caution
            ? $"{RvtConfig.PORTAL_BASE_URL}Notification/View/{notification.Id}"
            : "";

        foreach (var contact in contacts.Where(x => x.Email))
        {
            SendEmail(notification.Id, contact, request.AlertTime, messageToSend, request.FleetNr, notificationUrl);
        }

        foreach (var contact in contacts.Where(x => x.SMS))
        {
            SendSms(notification.Id, contact, request.AlertTime, messageToSend, request.FleetNr, notificationUrl);
        }
    }

    private static MessageService.MessageContent.MessageEnum ToMessage(Rvt.Monitor.Common.Notifications.AlertType alertType) =>
        alertType switch
        {
            Rvt.Monitor.Common.Notifications.AlertType.Alert => MessageService.MessageContent.MessageEnum.Alert,
            Rvt.Monitor.Common.Notifications.AlertType.Caution => MessageService.MessageContent.MessageEnum.Caution,
            Rvt.Monitor.Common.Notifications.AlertType.BatteryAlert => MessageService.MessageContent.MessageEnum.Battery_Alert,
            Rvt.Monitor.Common.Notifications.AlertType.BatteryCaution => MessageService.MessageContent.MessageEnum.Battery_Caution,
            _ => MessageService.MessageContent.MessageEnum.Offline
        };

    private void SendEmail(
        Guid notificationId,
        RvtContactDto contact,
        DateTime alertTime,
        MessageService.MessageContent.MessageEnum messageToSend,
        string fleetNr,
        string notificationUrl)
    {
        try
        {
            if (contact.ShouldSendAtTime(alertTime))
            {
                RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact email={Value1}",
                    SensitiveLogRedactor.Redact(contact.EmailAddress));
                messageService.Sendmessage(
                    messageToSend,
                    MessageService.MessageContent.MessageTypeEnum.Email,
                    contact.ToNotificationDto(),
                    fleetNr,
                    notificationUrl);
                writeNotificationAudit(notificationId, contact.EmailAddress, Rvt.Monitor.Common.Notifications.NotificationConstants.SENT_OK);
            }
            else
            {
                RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                    SensitiveLogRedactor.Redact(contact.ToString()));
            }
        }
        catch (CommsException e)
        {
            writeNotificationAudit(notificationId, e.Address, e.Message);
        }
    }

    private void SendSms(
        Guid notificationId,
        RvtContactDto contact,
        DateTime alertTime,
        MessageService.MessageContent.MessageEnum messageToSend,
        string fleetNr,
        string notificationUrl)
    {
        try
        {
            if (contact.ShouldSendAtTime(alertTime))
            {
                RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact phoneNumber={Value1}",
                    SensitiveLogRedactor.Redact(contact.PhoneNumber));
                messageService.Sendmessage(
                    messageToSend,
                    MessageService.MessageContent.MessageTypeEnum.SMS,
                    contact.ToNotificationDto(),
                    fleetNr,
                    notificationUrl);
                writeNotificationAudit(notificationId, contact.PhoneNumber!, Rvt.Monitor.Common.Notifications.NotificationConstants.SENT_OK);
            }
            else
            {
                RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                    SensitiveLogRedactor.Redact(contact.ToString()));
            }
        }
        catch (CommsException e)
        {
            writeNotificationAudit(notificationId, e.Address, e.Message);
        }
    }
}
