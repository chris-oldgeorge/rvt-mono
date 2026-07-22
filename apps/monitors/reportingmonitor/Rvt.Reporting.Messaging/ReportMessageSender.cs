using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Communications;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;

namespace Rvt.Reporting.Messaging;

public sealed class ReportMessageSender(
    IEmailDeliveryPort emailDelivery,
    IOptions<ReportMessageSenderOptions> options) : IReportMessageSender
{
    private const string PlainTextBody = "Your RVT Cloud report is attached.";
    private const string HtmlBody = "<p>Your RVT Cloud report is attached.</p>";
    private readonly ReportMessageSenderOptions options = options.Value;

    public async Task<ReportSendResult> SendAsync(
        string recipientEmail,
        string sitePostcode,
        RenderedReport report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.EmailEnabled)
        {
            return new ReportSendResult(true, "Email disabled by configuration.");
        }

        var effectiveRecipient = options.EmailTestMode &&
            !string.IsNullOrWhiteSpace(options.TestReportToEmail)
                ? options.TestReportToEmail
                : recipientEmail;
        var request = new EmailDeliveryRequest(
            effectiveRecipient,
            $"RVT Cloud report for {sitePostcode}",
            PlainTextBody,
            HtmlBody,
            [new EmailAttachment(report.FileName, report.ContentType, report.Content)]);

        try
        {
            await emailDelivery.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return new ReportSendResult(true, "Sent ok");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DeliveryException exception)
        {
            return new ReportSendResult(false, exception.Message);
        }
        catch (Exception exception)
        {
            return new ReportSendResult(
                false,
                $"Email delivery failed ({exception.GetType().Name}).");
        }
    }
}

public sealed class ReportMessageSenderOptions
{
    public bool EmailEnabled { get; init; } = true;

    public bool EmailTestMode { get; init; }

    public string? TestReportToEmail { get; init; }
}
