using SendGrid;

namespace Rvt.Communication.SendGridMail;

public sealed class SendGridClientFactory : ISendGridClientFactory
{
    public ISendGridClient Create(string apiKey) => new SendGridClient(apiKey);
}
