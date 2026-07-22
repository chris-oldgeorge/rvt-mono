namespace Rvt.Monitor.Common.Communications;

public sealed record EmailDeliveryRequest
{
    public EmailDeliveryRequest(
        string recipient,
        string subject,
        string plainTextBody,
        string htmlBody,
        IReadOnlyList<EmailAttachment> attachments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(plainTextBody);
        ArgumentNullException.ThrowIfNull(htmlBody);
        ArgumentNullException.ThrowIfNull(attachments);
        if (string.IsNullOrWhiteSpace(plainTextBody) && string.IsNullOrWhiteSpace(htmlBody))
        {
            throw new ArgumentException("An email body is required.", nameof(plainTextBody));
        }

        if (attachments.Any(attachment => attachment is null))
        {
            throw new ArgumentException("Attachments must not contain null items.", nameof(attachments));
        }

        Recipient = recipient;
        Subject = subject;
        PlainTextBody = plainTextBody;
        HtmlBody = htmlBody;
        Attachments = attachments.ToArray();
    }

    public string Recipient { get; }

    public string Subject { get; }

    public string PlainTextBody { get; }

    public string HtmlBody { get; }

    public IReadOnlyList<EmailAttachment> Attachments { get; }
}
