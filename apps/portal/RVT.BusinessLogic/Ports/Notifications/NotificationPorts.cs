// File summary: Defines transport-neutral notification ports used by RVT business workflows.
// Major updates:
// - 2026-07-15 pending Introduced the email delivery port for the hexagonal notification-adapter refactor.

namespace RVT.BusinessLogic.Ports.Notifications;

// Function summary: Carries the outcome of an email delivery attempt without exposing transport details.
public sealed record EmailDeliveryResult(bool Succeeded, string? ProviderResponse)
{
    // Function summary: Builds a successful delivery result.
    public static EmailDeliveryResult Success() => new(true, null);

    // Function summary: Builds a failed delivery result carrying the provider-reported response.
    public static EmailDeliveryResult Failure(string? providerResponse) => new(false, providerResponse);
}

public interface IEmailDelivery
{
    // Function summary: Delivers an HTML email through the configured provider adapter.
    Task<EmailDeliveryResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}
