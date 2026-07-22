// File summary: Composes account email templates with the email delivery port to send account-lifecycle emails.
// Major updates:
// - 2026-07-15 pending Replaced the ad-hoc `new MessageService(...)` construction with an injectable messenger
//   over the IEmailDelivery port. HTML-encoding of the callback URL is centralized here (was done at each caller).
// - 2026-07-22 pending Added the confirmation message used by pending profile email changes.

using System.Text.Encodings.Web;
using RVT.BusinessLogic.Ports.Notifications;

namespace RVT.BusinessLogic.Notifications;

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
    private readonly IEmailDelivery emailDelivery;

    // Function summary: Initializes the messenger with the email delivery port.
    public AccountMessenger(IEmailDelivery emailDelivery)
    {
        this.emailDelivery = emailDelivery;
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
        var message = AccountMessageCatalog.For(kind);
        var body = message.HtmlBody.Replace("{callbackUrl}", HtmlEncoder.Default.Encode(callbackUrl), StringComparison.Ordinal);
        return emailDelivery.SendAsync(email, message.Subject, body, cancellationToken);
    }
}
