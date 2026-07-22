// File summary: Outbound email adapter delivering account emails through the shared RVT common email port.
// Major updates:
// - 2026-07-17 pending Swapped the portal's own SendGrid client for the shared Rvt.Monitor.Common email adapter.
//   The seam worked as designed: the core port (RVT.BusinessLogic.Ports.Notifications.IEmailDelivery) is unchanged,
//   so AccountMessenger, the auth/user workflows, and their error handling were not touched by this swap.
// - 2026-07-15 pending Moved SendGrid delivery out of RVT.Utilities.EmailSender behind the IEmailDelivery port.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RVT.BusinessLogic.Ports.Notifications;
using Rvt.Monitor.Common.Communications;

namespace RvtPortal.Spa.Adapters.Notifications;

// Portal-side email settings bound from the existing "EmailConfiguration" section. Property names are preserved
// so deployed configuration keys are unchanged; they are mapped onto the shared CommunicationsOptions at startup.
public sealed class PortalEmailOptions
{
    public bool UseDebugEmail { get; set; }
    public string DebugEmailAddress { get; set; } = string.Empty;
    public string CopyEmailAddress { get; set; } = string.Empty;
    public string SENDGRID_API_KEY { get; set; } = string.Empty;
    public string Sending_Email_Address { get; set; } = string.Empty;
}

public sealed class RvtCommonEmailDelivery : IEmailDelivery
{
    private readonly IEmailDeliveryPort emailDeliveryPort;
    private readonly PortalEmailOptions options;
    private readonly ILogger<RvtCommonEmailDelivery> logger;

    // Function summary: Initializes the adapter with the shared email port and portal email settings.
    public RvtCommonEmailDelivery(
        IEmailDeliveryPort emailDeliveryPort,
        IOptions<PortalEmailOptions> options,
        ILogger<RvtCommonEmailDelivery> logger)
    {
        this.emailDeliveryPort = emailDeliveryPort;
        this.options = options.Value;
        this.logger = logger;
    }

    // Function summary: Sends an HTML email through the shared email port, honoring the debug-recipient override.
    public async Task<EmailDeliveryResult> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (options.UseDebugEmail)       // if debug mode, override destination email
        {
            to = options.DebugEmailAddress;
        }

        try
        {
            // The shared port signals failure by throwing; the portal's port contract returns a result instead,
            // so translate here and keep business workflows free of transport exceptions.
            await emailDeliveryPort.SendAsync(
                new EmailDeliveryRequest(to, subject, plainTextBody: string.Empty, htmlBody, attachments: []),
                cancellationToken);

            return EmailDeliveryResult.Success();
        }
        catch (EmailDeliveryException exception)
        {
            logger.LogWarning(exception, "Email delivery failed for recipient {Recipient}.", to);
            return EmailDeliveryResult.Failure(exception.Message);
        }
    }
}
