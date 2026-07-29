using Microsoft.Extensions.Logging;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Rvt.Communication.Abstractions;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;

namespace Omnidots.Api
{
    // Summary: Dispatches Omnidots alert notifications to monitor contacts over email and SMS.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApiWebhook).
    public class OmnidotsRuleProcessor
    {
        private readonly IOmnidotsRuleQueries _ruleQueries;
        private readonly IOmnidotsOperationalCommands _operationalCommands;
        private readonly IMessageService _messageService;
        private readonly string _portalBaseUrl;

        public OmnidotsRuleProcessor(
            IOmnidotsRuleQueries ruleQueries,
            IOmnidotsOperationalCommands operationalCommands,
            IMessageService messageService,
            string portalBaseUrl)
        {
            _ruleQueries = ruleQueries;
            _operationalCommands = operationalCommands;
            _messageService = messageService;
            _portalBaseUrl = portalBaseUrl;
        }

        public void ProcessAlertForContacts(VibrationMonitorDto monitor, NotificationDto notification)
        {
            _operationalCommands.WriteNotification(notification);
            List<RvtContactDto> contacts = _ruleQueries.ReadAlertContacts(monitor.Id);

            if (contacts != null && contacts.Count() > 0)
            {

                LegacyMessageKind messageToSend = LegacyMessageKind.Offline; //Overenginnered this to make the messages stand alone....
                switch (notification.AlertType)
                {
                    case AlertType.Alert:
                        messageToSend = LegacyMessageKind.Alert;
                        break;
                    case AlertType.Caution:
                        messageToSend = LegacyMessageKind.Caution;
                        break;
                    case AlertType.Offline:
                        messageToSend = LegacyMessageKind.Offline;
                        break;
                    case AlertType.BatteryAlert:
                        messageToSend = LegacyMessageKind.Battery_Alert;
                        break;
                    case AlertType.BatteryCaution:
                        messageToSend = LegacyMessageKind.Battery_Caution;
                        break;
                }
                string notificationUrl = "";
                if (notification.AlertType == AlertType.Alert || notification.AlertType == AlertType.Caution)
                {
                    notificationUrl = $"{_portalBaseUrl}Notification/View/{notification.Id}";
                }

                foreach (RvtContactDto? contact in contacts.Where(x => x.Email))
                {
                    try
                    {
                        if (contact.ShouldSendAtTime(notification.NotificationTime))
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact email={Value1}",
                                    SensitiveLogRedactor.Redact(contact.EmailAddress));
                            }
                            _messageService.Sendmessage(messageToSend, LegacyMessageChannel.Email, contact, monitor.FleetNr!, notificationUrl);
                            _operationalCommands.WriteNotificationAudit(notification.Id, contact.EmailAddress, NotificationConstants.SENT_OK);
                        }
                        else
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                                    SensitiveLogRedactor.Redact(contact.ToString()));
                            }
                        }
                    }
                    catch (CommsException e)
                    {
                        _operationalCommands.WriteNotificationAudit(notification.Id, e.Address, e.Message);
                    }
                }
                foreach (RvtContactDto? contact in contacts.Where(x => x.SMS))
                {
                    try
                    {
                        if (contact.ShouldSendAtTime(notification.NotificationTime))
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("ProcessAlertForContacts sendMessage for contact phoneNumber={Value1}",
                                    SensitiveLogRedactor.Redact(contact.PhoneNumber));
                            }
                            _messageService.Sendmessage(messageToSend, LegacyMessageChannel.SMS, contact, monitor.FleetNr!, notificationUrl);
                            _operationalCommands.WriteNotificationAudit(notification.Id, contact.PhoneNumber!, NotificationConstants.SENT_OK);
                        }
                        else
                        {
                            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
                            {
                                RvtLogger.Logger.LogInformation("Contact ShouldSendAtTime skipped sending message contact={Value1}",
                                    SensitiveLogRedactor.Redact(contact.ToString()));
                            }
                        }
                    }
                    catch (CommsException e)
                    {
                        _operationalCommands.WriteNotificationAudit(notification.Id, e.Address, e.Message);
                    }

                }
            }


        }
    }
}
