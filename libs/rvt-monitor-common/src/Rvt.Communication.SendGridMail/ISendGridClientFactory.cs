using SendGrid;

namespace Rvt.Communication.SendGridMail;

public interface ISendGridClientFactory
{
    ISendGridClient Create(string apiKey);
}
