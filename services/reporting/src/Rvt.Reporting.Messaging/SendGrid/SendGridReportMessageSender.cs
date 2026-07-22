using Microsoft.Extensions.Options;
using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Rvt.Reporting.Messaging.SendGrid;

/// <summary>
/// Sends generated report PDFs through SendGrid, with a test-mode recipient override.
/// Major updates: 2026-06-24 extracted report delivery adapter for containerized reporting.
/// </summary>
public sealed class SendGridReportMessageSender : IReportMessageSender
{
    private readonly SendGridReportMessageOptions _options;

    public SendGridReportMessageSender(IOptions<SendGridReportMessageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ReportSendResult> SendAsync(string recipientEmail, string sitePostcode, RenderedReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (!_options.EmailEnabled)
        {
            return new ReportSendResult(true, "Email disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ReportSendResult(false, "SendGrid API key is not configured.");
        }

        var effectiveRecipient = _options.EmailTestMode && !string.IsNullOrWhiteSpace(_options.TestReportToEmail)
            ? _options.TestReportToEmail
            : recipientEmail;

        var client = new SendGridClient(_options.ApiKey);
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(_options.FromEmail, _options.FromName),
            new EmailAddress(effectiveRecipient),
            $"RVT Cloud report for {sitePostcode}",
            "Your RVT Cloud report is attached.",
            "<p>Your RVT Cloud report is attached.</p>");

        message.AddAttachment(report.FileName, Convert.ToBase64String(report.Content), report.ContentType);
        var response = await client.SendEmailAsync(message, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? new ReportSendResult(true, "Sent ok")
            : new ReportSendResult(false, $"SendGrid returned {(int)response.StatusCode} {response.StatusCode}");
    }
}

public sealed class SendGridReportMessageOptions
{
    public bool EmailEnabled { get; set; } = true;

    public bool EmailTestMode { get; set; }

    public string? TestReportToEmail { get; set; }

    public string FromEmail { get; set; } = "NoReply@rvtgroup.co.uk";

    public string FromName { get; set; } = "RVT Cloud";

    public string? ApiKey { get; set; }
}
