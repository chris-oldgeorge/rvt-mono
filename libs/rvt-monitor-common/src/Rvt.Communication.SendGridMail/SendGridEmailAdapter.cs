using System.Net;
using Rvt.Communication.Abstractions;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Rvt.Communication.SendGridMail;

public sealed class SendGridEmailAdapter : IEmailDeliveryPort
{
    private readonly Lazy<ISendGridClient> client;
    private readonly SendGridMailOptions options;

    public SendGridEmailAdapter(
        ISendGridClientFactory clientFactory,
        SendGridMailOptions options)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        client = new Lazy<ISendGridClient>(
            () => clientFactory.Create(options.ApiKey),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Enabled ||
            string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Configuration,
                "Configuration");
        }

        SendGridMessage message = new()
        {
            From = new EmailAddress(options.FromEmail, options.FromName),
            Subject = request.Subject
        };
        message.AddTo(new EmailAddress(request.Recipient));
        message.AddContent(MimeType.Text, request.PlainTextBody);
        message.AddContent(MimeType.Html, request.HtmlBody);
        foreach (EmailAttachment attachment in request.Attachments)
        {
            using Stream stream = attachment.OpenRead();
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            message.AddAttachment(
                attachment.FileName,
                Convert.ToBase64String(buffer.ToArray()),
                attachment.ContentType);
        }

        Response response;
        try
        {
            response = await client.Value.SendEmailAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "Network");
        }
        catch (TaskCanceledException)
        {
            throw new EmailDeliveryException(
                "SendGrid",
                DeliveryFailureKind.Transient,
                "Timeout");
        }

        try
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            throw new EmailDeliveryException(
                "SendGrid",
                Classify(response.StatusCode),
                ((int)response.StatusCode).ToString(),
                response.Headers.RetryAfter?.Delta);
        }
        finally
        {
            response.Body.Dispose();
        }
    }

    private static DeliveryFailureKind Classify(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500
            ? DeliveryFailureKind.Transient
            : DeliveryFailureKind.Permanent;
}
