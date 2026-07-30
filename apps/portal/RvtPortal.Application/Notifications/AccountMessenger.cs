using System.Text.Encodings.Web;
using RvtPortal.Application.Ports.Notifications;

namespace RvtPortal.Application.Notifications;

public interface IAccountMessenger
{
    // Function summary: Sends the password-set email for a newly created or unconfirmed account.
    Task<EmailDeliveryResult> SendPasswordSetAsync(string email, string callbackUrl, CancellationToken cancellationToken);

    // Function summary: Sends the password-reset email for an existing account.
    Task<EmailDeliveryResult> SendPasswordResetAsync(string email, string callbackUrl, CancellationToken cancellationToken);

    // Function summary: Sends a confirmation link for a pending profile email change.
    Task<EmailDeliveryResult> SendEmailChangeAsync(string email, string callbackUrl, CancellationToken cancellationToken);
}

public sealed class AccountMessenger : IAccountMessenger
{
    private readonly IEmailDelivery _emailDelivery;

    // Function summary: Initializes the messenger with the email delivery port.
    public AccountMessenger(IEmailDelivery emailDelivery)
    {
        _emailDelivery = emailDelivery;
    }

    // Function summary: Sends the password-set email for a newly created or unconfirmed account.
    public Task<EmailDeliveryResult> SendPasswordSetAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        => SendAsync(AccountMessageKind.PasswordSet, email, callbackUrl, cancellationToken);

    // Function summary: Sends the password-reset email for an existing account.
    public Task<EmailDeliveryResult> SendPasswordResetAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        => SendAsync(AccountMessageKind.PasswordReset, email, callbackUrl, cancellationToken);

    // Function summary: Sends a confirmation link for a pending profile email change.
    public Task<EmailDeliveryResult> SendEmailChangeAsync(string email, string callbackUrl, CancellationToken cancellationToken)
        => SendAsync(AccountMessageKind.EmailChange, email, callbackUrl, cancellationToken);

    private Task<EmailDeliveryResult> SendAsync(AccountMessageKind kind, string email, string callbackUrl, CancellationToken cancellationToken)
    {
        AccountMessage message = AccountMessageCatalog.For(kind);
        string body = message.HtmlBody.Replace("{callbackUrl}", HtmlEncoder.Default.Encode(callbackUrl), StringComparison.Ordinal);
        return _emailDelivery.SendAsync(email, message.Subject, body, cancellationToken);
    }
}
