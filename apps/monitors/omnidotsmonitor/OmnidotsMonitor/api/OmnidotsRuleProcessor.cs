using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;

namespace Omnidots.Api
{
    // Summary: Dispatches Omnidots alert notifications to monitor contacts over email and SMS.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiWebhook).
    public class OmnidotsRuleProcessor
    {
        private readonly IOmnidotsRuleQueries ruleQueries;
        private readonly IOmnidotsOperationalCommands operationalCommands;
        private readonly IMessageService messageService;
        private readonly string portalBaseUrl;

        public OmnidotsRuleProcessor(
            IOmnidotsRuleQueries ruleQueries,
            IOmnidotsOperationalCommands operationalCommands,
            IMessageService messageService,
            string portalBaseUrl)
        {
            this.ruleQueries = ruleQueries;
            this.operationalCommands = operationalCommands;
            this.messageService = messageService;
            this.portalBaseUrl = portalBaseUrl;
        }

        public void ProcessAlertForContacts(VibrationMonitorDto monitor, NotificationDto notification)
        {
            operationalCommands.WriteNotification(notification);
            var contacts = ruleQueries.ReadAlertContacts(monitor.Id);

            if (contacts != null && contacts.Count() > 0)
            {

                MessageService.MessageContent.MessageEnum messageToSend = MessageService.MessageContent.MessageEnum.Offline; //Overenginnered this to make the messages stand alone....
                switch (notification.AlertType)
                {
                    case AlertType.Alert:
                        messageToSend = MessageService.MessageContent.MessageEnum.Alert;
                        break;
                    case AlertType.Caution:
                        messageToSend = MessageService.MessageContent.MessageEnum.Caution;
                        break;
                    case AlertType.Offline:
                        messageToSend = MessageService.MessageContent.MessageEnum.Offline;
                        break;
                    case AlertType.BatteryAlert:
                        messageToSend = MessageService.MessageContent.MessageEnum.Battery_Alert;
                        break;
                    case AlertType.BatteryCaution:
                        messageToSend = MessageService.MessageContent.MessageEnum.Battery_Caution;
                        break;
                }
                var notificationUrl = "";
                if (notification.AlertType == AlertType.Alert || notification.AlertType == AlertType.Caution)
                {
                    notificationUrl = $"{RvtConfig.PORTAL_BASE_URL}Notification/View/{notification.Id}";
                }

                foreach (var contact in contacts.Where(x => x.Email))
                {
                    try
                    {
                        if (contact.ShouldSendAtTime(notification.NotificationTime))
                        {
                            RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact email={Value1}",
                                SensitiveLogRedactor.Redact(contact.EmailAddress));
                            messageService.Sendmessage(messageToSend, MessageService.MessageContent.MessageTypeEnum.Email, contact, monitor.FleetNr!, notificationUrl);
                            operationalCommands.WriteNotificationAudit(notification.Id, contact.EmailAddress, NotificationConstants.SENT_OK);
                        }
                        else
                        {
                            RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                                SensitiveLogRedactor.Redact(contact.ToString()));
                        }
                    }
                    catch (CommsException e)
                    {
                        operationalCommands.WriteNotificationAudit(notification.Id, e.Address, e.Message);
                    }
                }
                foreach (var contact in contacts.Where(x => x.SMS))
                {
                    try
                    {
                        if (contact.ShouldSendAtTime(notification.NotificationTime))
                        {
                            RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact phoneNumber={Value1}",
                                SensitiveLogRedactor.Redact(contact.PhoneNumber));
                            messageService.Sendmessage(messageToSend, MessageService.MessageContent.MessageTypeEnum.SMS, contact, monitor.FleetNr!, notificationUrl);
                            operationalCommands.WriteNotificationAudit(notification.Id, contact.PhoneNumber!, NotificationConstants.SENT_OK);
                        }
                        else
                        {
                            RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                                SensitiveLogRedactor.Redact(contact.ToString()));
                        }
                    }
                    catch (CommsException e)
                    {
                        operationalCommands.WriteNotificationAudit(notification.Id, e.Address, e.Message);
                    }

                }
            }


        }
    }
}
