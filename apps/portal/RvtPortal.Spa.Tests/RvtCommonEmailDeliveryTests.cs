using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic.Ports.Notifications;
using Rvt.Communication.Abstractions;
using RvtPortal.Spa.Adapters.Notifications;

namespace RvtPortal.Spa.Tests;

public sealed class RvtCommonEmailDeliveryTests
{
    [Fact]
    public async Task SendAsync_DeliversExistingPortalMessageThroughSharedPort()
    {
        var port = new RecordingEmailPort();
        var adapter = CreateAdapter(port);

        var result = await adapter.SendAsync(
            "recipient@example.test",
            "Welcome",
            "<p>Welcome to RVT Cloud.</p>",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ProviderResponse);
        var request = Assert.Single(port.Requests);
        Assert.Equal("recipient@example.test", request.Recipient);
        Assert.Equal("Welcome", request.Subject);
        Assert.Equal(string.Empty, request.PlainTextBody);
        Assert.Equal("<p>Welcome to RVT Cloud.</p>", request.HtmlBody);
        Assert.Empty(request.Attachments);
    }

    [Fact]
    public async Task SendAsync_DebugModeUsesConfiguredOverrideRecipient()
    {
        var port = new RecordingEmailPort();
        var adapter = CreateAdapter(port, new PortalEmailOptions
        {
            UseDebugEmail = true,
            DebugEmailAddress = "debug@example.test"
        });

        await adapter.SendAsync(
            "production@example.test",
            "Welcome",
            "<p>Welcome.</p>",
            CancellationToken.None);

        Assert.Equal("debug@example.test", Assert.Single(port.Requests).Recipient);
    }

    [Fact]
    public async Task SendAsync_EmailDeliveryFailureReturnsPortalFailure()
    {
        var exception = new EmailDeliveryException(
            "SendGrid",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromMinutes(1));
        var adapter = CreateAdapter(new ThrowingEmailPort(exception));

        var result = await adapter.SendAsync(
            "recipient@example.test",
            "Welcome",
            "<p>Welcome.</p>",
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("SendGrid email delivery failed (Transient, code 429).", result.ProviderResponse);
    }

    [Fact]
    public async Task SendAsync_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var adapter = CreateAdapter(
            new ThrowingEmailPort(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.SendAsync(
                "recipient@example.test",
                "Welcome",
                "<p>Welcome.</p>",
                cancellation.Token));
    }

    private static RvtCommonEmailDelivery CreateAdapter(
        IEmailDeliveryPort port,
        PortalEmailOptions? options = null) =>
        new(
            port,
            Options.Create(options ?? new PortalEmailOptions()),
            NullLogger<RvtCommonEmailDelivery>.Instance);

    private sealed class RecordingEmailPort : IEmailDeliveryPort
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task SendAsync(
            EmailDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailPort(Exception exception) : IEmailDeliveryPort
    {
        public Task SendAsync(
            EmailDeliveryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException(exception);
    }
}
