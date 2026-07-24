using Microsoft.Extensions.Options;
using Rvt.Communication.Abstractions;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Messaging;

namespace Rvt.Reporting.Service.Tests;

public sealed class ReportMessageSenderTests
{
    [Fact]
    public async Task SendAsync_MapsReportBytesToOneEmailAttachment()
    {
        var port = new RecordingEmailPort();
        var sender = CreateSender(port);
        var report = Report();

        var result = await sender.SendAsync(
            "recipient@example.test",
            "AB1 2CD",
            report,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Sent ok", result.StatusMessage);
        var request = Assert.Single(port.Requests);
        Assert.Equal("recipient@example.test", request.Recipient);
        Assert.Equal("RVT Cloud report for AB1 2CD", request.Subject);
        Assert.Equal("Your RVT Cloud report is attached.", request.PlainTextBody);
        Assert.Equal("<p>Your RVT Cloud report is attached.</p>", request.HtmlBody);
        var attachment = Assert.Single(request.Attachments);
        Assert.Equal(report.FileName, attachment.FileName);
        Assert.Equal(report.ContentType, attachment.ContentType);
        await using var stream = attachment.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CancellationToken.None);
        Assert.Equal(report.Content, buffer.ToArray());
    }

    [Fact]
    public async Task SendAsync_DisabledReturnsSuccessWithoutCallingPort()
    {
        var port = new RecordingEmailPort();
        var sender = CreateSender(
            port,
            new ReportMessageSenderOptions { EmailEnabled = false });

        var result = await sender.SendAsync(
            "recipient@example.test",
            "AB1",
            Report(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Email disabled by configuration.", result.StatusMessage);
        Assert.Empty(port.Requests);
    }

    [Fact]
    public async Task SendAsync_DisabledIgnoresPreCancelledCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var port = new RecordingEmailPort();
        var sender = CreateSender(
            port,
            new ReportMessageSenderOptions { EmailEnabled = false });

        var result = await sender.SendAsync(
            "recipient@example.test",
            "AB1",
            Report(),
            cancellation.Token);

        Assert.True(result.Success);
        Assert.Equal("Email disabled by configuration.", result.StatusMessage);
        Assert.Empty(port.Requests);
    }

    [Fact]
    public async Task SendAsync_TestModeUsesConfiguredOverrideRecipient()
    {
        var port = new RecordingEmailPort();
        var sender = CreateSender(port, new ReportMessageSenderOptions
        {
            EmailEnabled = true,
            EmailTestMode = true,
            TestReportToEmail = "test-recipient@example.test"
        });

        await sender.SendAsync(
            "production@example.test",
            "AB1",
            Report(),
            CancellationToken.None);

        Assert.Equal("test-recipient@example.test", Assert.Single(port.Requests).Recipient);
    }

    [Fact]
    public async Task SendAsync_DeliveryFailureReturnsReportResult()
    {
        var sender = CreateSender(new ThrowingEmailPort(new EmailDeliveryException(
            "SendGrid",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromMinutes(1))));

        var result = await sender.SendAsync(
            "recipient@example.test",
            "AB1",
            Report(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("SendGrid email delivery failed (Transient, code 429).", result.StatusMessage);
    }

    [Fact]
    public async Task SendAsync_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sender = CreateSender(
            new ThrowingEmailPort(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sender.SendAsync(
                "recipient@example.test",
                "AB1",
                Report(),
                cancellation.Token));
    }

    [Fact]
    public void MessagingAssembly_DoesNotReferenceSendGridSdk()
    {
        var references = typeof(ReportMessageSender).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name?.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static ReportMessageSender CreateSender(
        IEmailDeliveryPort port,
        ReportMessageSenderOptions? options = null) =>
        new(
            port,
            Options.Create(options ?? new ReportMessageSenderOptions { EmailEnabled = true }));

    private static RenderedReport Report() =>
        new("report.pdf", "application/pdf", [1, 2, 3, 4]);

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
