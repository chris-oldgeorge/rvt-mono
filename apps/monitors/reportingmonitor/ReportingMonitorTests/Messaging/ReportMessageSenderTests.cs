using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Rvt.Communication.Abstractions;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Messaging;

namespace ReportingMonitorTests.Messaging;

public sealed class ReportMessageSenderTests
{
    [Fact]
    public void MessagingProject_ReferencesCommunicationAbstractionsWithoutCommonOrSendGrid()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "apps",
            "monitors",
            "reportingmonitor",
            "Rvt.Reporting.Messaging",
            "Rvt.Reporting.Messaging.csproj");
        XDocument project = System.Xml.Linq.XDocument.Load(projectPath);
        string[] references = [.. project.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(reference => reference is not null)
            .Select(reference => reference!.Replace('\\', '/'))];

        Assert.Contains(references, reference =>
            reference.EndsWith(
                "Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj",
                StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Rvt.Monitor.Common", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("SendGrid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAsync_MapsReportAndExistingMessageContentToEmailPort()
    {
        RecordingEmailPort port = new();
        ReportMessageSender sender = CreateSender(port);
        RenderedReport report = Report();

        ReportSendResult result = await sender.SendAsync(
            "recipient@example.test",
            "AB1 2CD",
            report,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Sent ok", result.StatusMessage);
        EmailDeliveryRequest request = Assert.Single(port.Requests);
        Assert.Equal("recipient@example.test", request.Recipient);
        Assert.Equal("RVT Cloud report for AB1 2CD", request.Subject);
        Assert.Equal("Your RVT Cloud report is attached.", request.PlainTextBody);
        Assert.Equal("<p>Your RVT Cloud report is attached.</p>", request.HtmlBody);
        EmailAttachment attachment = Assert.Single(request.Attachments);
        Assert.Equal(report.FileName, attachment.FileName);
        Assert.Equal(report.ContentType, attachment.ContentType);
        await using Stream stream = attachment.OpenRead();
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, CancellationToken.None);
        Assert.Equal(report.Content, buffer.ToArray());
    }

    [Fact]
    public async Task SendAsync_DisabledReturnsSuccessWithoutCallingPort()
    {
        RecordingEmailPort port = new();
        ReportMessageSender sender = CreateSender(port, new ReportMessageSenderOptions { EmailEnabled = false });

        ReportSendResult result = await sender.SendAsync("recipient@example.test", "AB1", Report(), default);

        Assert.True(result.Success);
        Assert.Equal("Email disabled by configuration.", result.StatusMessage);
        Assert.Empty(port.Requests);
    }

    [Fact]
    public async Task SendAsync_TestModeUsesConfiguredOverrideRecipient()
    {
        RecordingEmailPort port = new();
        ReportMessageSender sender = CreateSender(port, new ReportMessageSenderOptions
        {
            EmailEnabled = true,
            EmailTestMode = true,
            TestReportToEmail = "test-recipient@example.test"
        });

        await sender.SendAsync("production@example.test", "AB1", Report(), default);

        Assert.Equal("test-recipient@example.test", Assert.Single(port.Requests).Recipient);
    }

    [Fact]
    public async Task SendAsync_TypedDeliveryFailureReturnsSafeProviderError()
    {
        ReportMessageSender sender = CreateSender(new ThrowingEmailPort(new EmailDeliveryException(
            "MicrosoftGraph",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromMinutes(1))));

        ReportSendResult result = await sender.SendAsync("recipient@example.test", "AB1", Report(), default);

        Assert.False(result.Success);
        Assert.Equal("MicrosoftGraph email delivery failed (Transient, code 429).", result.StatusMessage);
    }

    [Fact]
    public async Task SendAsync_UntypedFailureReturnsTypeOnly()
    {
        ReportMessageSender sender = CreateSender(new ThrowingEmailPort(
            new InvalidOperationException("secret recipient@example.test")));

        ReportSendResult result = await sender.SendAsync("recipient@example.test", "AB1", Report(), default);

        Assert.False(result.Success);
        Assert.Equal("Email delivery failed (InvalidOperationException).", result.StatusMessage);
    }

    [Fact]
    public async Task SendAsync_RequestedCancellationPropagates()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ReportMessageSender sender = CreateSender(new ThrowingEmailPort(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sender.SendAsync("recipient@example.test", "AB1", Report(), cancellation.Token));
    }

    private static ReportMessageSender CreateSender(
        IEmailDeliveryPort port,
        ReportMessageSenderOptions? options = null) =>
        new(port, Options.Create(options ?? new ReportMessageSenderOptions { EmailEnabled = true }));

    private static RenderedReport Report() =>
        new("report.pdf", "application/pdf", [1, 2, 3, 4]);

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Rvt.Mono.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the RVT mono-repository root.");
    }

    private sealed class RecordingEmailPort : IEmailDeliveryPort
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailPort(Exception exception) : IEmailDeliveryPort
    {
        public Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException(exception);
    }
}
