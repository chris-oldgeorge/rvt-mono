namespace Rvt.Communication.Abstractions;

public sealed record SmsDeliveryRequest
{
    public SmsDeliveryRequest(string recipient, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        Recipient = recipient;
        Content = content;
    }

    public string Recipient { get; }
    public string Content { get; }
}
