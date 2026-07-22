
using Infobip.Api.Client;
using Infobip.Api.Client.Api;
using Infobip.Api.Client.Model;
using Microsoft.Extensions.Logging;
using MyAtm.Api;


namespace Rvt.Monitor.Common.Communications
{

    internal class SmsSender
    {
        private static readonly string BASE_URL = Environment.GetEnvironmentVariable("RVT__SMS_BASE_URL") ?? string.Empty;
        private static readonly string API_KEY = Environment.GetEnvironmentVariable("RVT__SMS_API_KEY") ?? string.Empty;
        private static readonly string SENDER = Environment.GetEnvironmentVariable("RVT__SMS_SENDER") ?? "RvtAlert";
        private readonly bool DoSend =
            bool.TryParse(Environment.GetEnvironmentVariable("RVT__SMS_ENABLED"), out var smsEnabled) && smsEnabled;

        internal bool SendSms(string recipient, string content)
        {
            if (!DoSend)
            {
                RvtLogger.Logger.LogInformation("SMS sending is disabled ! recipient={}", recipient);
                return false;
            }

            if (string.IsNullOrWhiteSpace(BASE_URL) || string.IsNullOrWhiteSpace(API_KEY))
            {
                RvtLogger.Logger.LogWarning("SMS sending is enabled but SMS provider settings are missing. recipient={}", recipient);
                return false;
            }

            var configuration = new Configuration()
            {
                BasePath = BASE_URL,
                ApiKeyPrefix = "App",
                ApiKey = API_KEY
            };

            var sendSmsApi = new SendSmsApi(configuration);

            var smsMessage = new SmsTextualMessage()
            {
                From = SENDER,
                Destinations = new List<SmsDestination>()
                {
                    new SmsDestination(to: recipient)
                },
                Text = content
            };

            var smsRequest = new SmsAdvancedTextualRequest()
            {
                Messages = new List<SmsTextualMessage>() { smsMessage }
            };

            try
            {
                var smsResponse = sendSmsApi.SendSmsMessage(smsRequest);
                return true;
            }
            catch (ApiException apiException)
            {
                RvtLogger.Logger.LogError("Error occurred! \n\tMessage: {}\n\tError content", apiException.ErrorContent);
                return false;
            }
        }
    }
}
