using SendGrid;

namespace Rvt.Monitor.Common.Infrastructure.Email.SendGrid;

public sealed class SendGridClientFactory : ISendGridClientFactory
{
    public ISendGridClient Create(string apiKey) => new SendGridClient(apiKey);
}
