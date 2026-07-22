// File summary: Covers the account message catalog content and the messenger that delivers it through the email port.
// Major updates:
// - 2026-07-15 pending Added on migrating account emails from MessageService to AccountMessenger + IEmailDelivery.

using RVT.BusinessLogic.Notifications;
using RVT.BusinessLogic.Ports.Notifications;

namespace RvtPortal.Spa.Tests;

public sealed class AccountNotificationTests
{
    // --- Catalog content (locks the user-visible email text through the refactor) ---

    [Fact]
    // Function summary: Verifies the password-set message keeps its subject and welcome copy.
    public void PasswordSet_HasWelcomeSubjectAndBody()
    {
        var message = AccountMessageCatalog.For(AccountMessageKind.PasswordSet);

        Assert.Equal("Welcome to the RVT Cloud", message.Subject);
        Assert.Contains("You have been added as a user to the RVT Cloud.", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Please set a password for your account by <a href='{callbackUrl}'>clicking here</a>.", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<title>RVT Cloud</title>", message.HtmlBody, StringComparison.Ordinal);
        Assert.StartsWith("<!DOCTYPE html>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies the password-reset message keeps its subject and reset copy.
    public void PasswordReset_HasResetSubjectAndBody()
    {
        var message = AccountMessageCatalog.For(AccountMessageKind.PasswordReset);

        Assert.Equal("Password reset", message.Subject);
        Assert.Contains("You have requested to reset your password to the RVT Cloud.", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Please <a href='{callbackUrl}'>click here</a> to reset the password.", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies every account message keeps the callback placeholder for the messenger to fill.
    public void EveryAccountMessage_CarriesCallbackPlaceholder()
    {
        foreach (var kind in Enum.GetValues<AccountMessageKind>())
        {
            var message = AccountMessageCatalog.For(kind);
            Assert.Contains("{callbackUrl}", message.HtmlBody, StringComparison.Ordinal);
        }
    }

    // --- Messenger (composes catalog + port; substitutes and HTML-encodes the callback URL) ---

    [Fact]
    // Function summary: Verifies the messenger sends the catalog subject with the callback URL substituted and HTML-encoded.
    public async Task SendPasswordSetAsync_SubstitutesEncodedCallbackAndSendsSubject()
    {
        var delivery = new RecordingEmailDelivery(EmailDeliveryResult.Success());
        var messenger = new AccountMessenger(delivery);

        var result = await messenger.SendPasswordSetAsync("user@example.test", "https://portal.test/set?code=a&b=c", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("user@example.test", delivery.LastTo);
        Assert.Equal("Welcome to the RVT Cloud", delivery.LastSubject);
        Assert.DoesNotContain("{callbackUrl}", delivery.LastBody, StringComparison.Ordinal);
        // The ampersand in the URL must be HTML-encoded in the delivered body.
        Assert.Contains("https://portal.test/set?code=a&amp;b=c", delivery.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a delivery failure is propagated to the caller with the provider response.
    public async Task SendPasswordResetAsync_PropagatesDeliveryFailure()
    {
        var delivery = new RecordingEmailDelivery(EmailDeliveryResult.Failure("503"));
        var messenger = new AccountMessenger(delivery);

        var result = await messenger.SendPasswordResetAsync("user@example.test", "https://portal.test/reset", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("503", result.ProviderResponse);
        Assert.Equal("Password reset", delivery.LastSubject);
    }

    private sealed class RecordingEmailDelivery : IEmailDelivery
    {
        private readonly EmailDeliveryResult result;

        public RecordingEmailDelivery(EmailDeliveryResult result)
        {
            this.result = result;
        }

        public string? LastTo { get; private set; }
        public string? LastSubject { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        // Function summary: Records the delivery request and returns the preconfigured result.
        public Task<EmailDeliveryResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            LastTo = to;
            LastSubject = subject;
            LastBody = htmlBody;
            return Task.FromResult(result);
        }
    }
}
