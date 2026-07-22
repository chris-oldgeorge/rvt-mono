using SendGrid;

namespace Rvt.Monitor.Common.Infrastructure.Email.SendGrid;

public interface ISendGridClientFactory
{
    ISendGridClient Create(string apiKey);
}
