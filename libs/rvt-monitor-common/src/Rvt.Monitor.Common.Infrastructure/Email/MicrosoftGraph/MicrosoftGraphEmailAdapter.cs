using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;

namespace Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;

public sealed class MicrosoftGraphEmailAdapter(
    HttpClient httpClient,
    IMicrosoftGraphAccessTokenProvider tokenProvider,
    CommunicationsOptions options) : IEmailDeliveryPort
{
    internal const long SmallAttachmentLimit = 3L * 1024 * 1024;
    internal const long MaximumAttachmentLength = 150L * 1024 * 1024;
    private const int UploadChunkLength = 3 * 1024 * 1024;
    private static readonly Uri GraphBaseUri = new("https://graph.microsoft.com/v1.0/");

    public async Task SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.EmailEnabled ||
            options.EmailProvider != EmailProvider.MicrosoftGraph ||
            string.IsNullOrWhiteSpace(options.MicrosoftSenderAddress))
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Configuration,
                "Configuration");
        }

        if (request.Attachments.Any(attachment => !IsAttachmentSizeSupported(attachment.Length)))
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Permanent,
                "AttachmentTooLarge");
        }

        if (request.Attachments.Any(attachment => attachment.Length >= SmallAttachmentLimit))
        {
            await SendLargeMessageAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var attachments = request.Attachments.Count == 0
            ? null
            : request.Attachments.Select(ToSmallAttachment).ToArray();
        var hasHtmlBody = !string.IsNullOrWhiteSpace(request.HtmlBody);
        var payload = new GraphSendMailRequest(
            new GraphMessage(
                request.Subject,
                new GraphItemBody(
                    hasHtmlBody ? "HTML" : "Text",
                    hasHtmlBody ? request.HtmlBody : request.PlainTextBody),
                [new GraphRecipient(new GraphEmailAddress(request.Recipient))],
                attachments),
            true);
        var json = JsonSerializer.Serialize(payload, MicrosoftGraphJsonContext.Default.GraphSendMailRequest);
        var uri = new Uri(
            GraphBaseUri,
            $"users/{Uri.EscapeDataString(options.MicrosoftSenderAddress)}/sendMail");

        await SendAuthenticatedAsync(uri, json, readResponseBody: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendLargeMessageAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var body = Body(request);
        var draft = new GraphMessage(
            request.Subject,
            body,
            [new GraphRecipient(new GraphEmailAddress(request.Recipient))],
            null);
        var senderPath = $"users/{Uri.EscapeDataString(options.MicrosoftSenderAddress)}";
        var draftUri = new Uri(GraphBaseUri, $"{senderPath}/messages");
        var draftJson = JsonSerializer.Serialize(
            draft,
            MicrosoftGraphJsonContext.Default.GraphMessage);
        var draftBody = await SendAuthenticatedAsync(
            draftUri,
            draftJson,
            readResponseBody: true,
            cancellationToken).ConfigureAwait(false);
        var draftResponse = JsonSerializer.Deserialize(
            draftBody!,
            MicrosoftGraphJsonContext.Default.GraphDraftResponse);
        if (string.IsNullOrWhiteSpace(draftResponse?.Id))
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Permanent,
                "InvalidDraftResponse");
        }

        var draftId = Uri.EscapeDataString(draftResponse.Id);
        foreach (var attachment in request.Attachments)
        {
            if (attachment.Length < SmallAttachmentLimit)
            {
                var attachmentJson = JsonSerializer.Serialize(
                    ToSmallAttachment(attachment),
                    MicrosoftGraphJsonContext.Default.GraphFileAttachment);
                await SendAuthenticatedAsync(
                    new Uri(GraphBaseUri, $"{senderPath}/messages/{draftId}/attachments"),
                    attachmentJson,
                    readResponseBody: false,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            var session = await CreateUploadSessionAsync(
                senderPath,
                draftId,
                attachment,
                cancellationToken).ConfigureAwait(false);
            await UploadAttachmentAsync(session, attachment, cancellationToken).ConfigureAwait(false);
        }

        await SendAuthenticatedAsync(
            new Uri(GraphBaseUri, $"{senderPath}/messages/{draftId}/send"),
            "{}",
            readResponseBody: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MicrosoftGraphUploadSession> CreateUploadSessionAsync(
        string senderPath,
        string draftId,
        EmailAttachment attachment,
        CancellationToken cancellationToken)
    {
        var request = new GraphUploadSessionRequest(new GraphAttachmentItem(
            "file",
            attachment.FileName,
            attachment.Length,
            attachment.ContentType));
        var json = JsonSerializer.Serialize(
            request,
            MicrosoftGraphJsonContext.Default.GraphUploadSessionRequest);
        var responseBody = await SendAuthenticatedAsync(
            new Uri(
                GraphBaseUri,
                $"{senderPath}/messages/{draftId}/attachments/createUploadSession"),
            json,
            readResponseBody: true,
            cancellationToken).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(
            responseBody!,
            MicrosoftGraphJsonContext.Default.GraphUploadSessionResponse);
        if (!Uri.TryCreate(response?.UploadUrl, UriKind.Absolute, out var uploadUri) ||
            uploadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Permanent,
                "InvalidUploadSession");
        }

        return new MicrosoftGraphUploadSession(uploadUri);
    }

    private async Task UploadAttachmentAsync(
        MicrosoftGraphUploadSession session,
        EmailAttachment attachment,
        CancellationToken cancellationToken)
    {
        using var stream = attachment.OpenRead();
        var buffer = new byte[UploadChunkLength];
        long offset = 0;
        while (offset < attachment.Length)
        {
            var requested = (int)Math.Min(buffer.Length, attachment.Length - offset);
            var read = 0;
            while (read < requested)
            {
                var current = await stream.ReadAsync(
                    buffer.AsMemory(read, requested - read),
                    cancellationToken).ConfigureAwait(false);
                if (current == 0)
                {
                    throw new EmailDeliveryException(
                        "MicrosoftGraph",
                        DeliveryFailureKind.Permanent,
                        "AttachmentRead");
                }

                read += current;
            }

            using var message = new HttpRequestMessage(HttpMethod.Put, session.UploadUrl);
            message.Content = new ByteArrayContent(buffer, 0, read);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            message.Content.Headers.ContentLength = read;
            message.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                offset,
                offset + read - 1,
                attachment.Length);
            await SendUploadChunkAsync(message, cancellationToken).ConfigureAwait(false);
            offset += read;
        }
    }

    private async Task SendUploadChunkAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Transient,
                "Network");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            throw CreateStatusException(response);
        }
    }

    private static GraphFileAttachment ToSmallAttachment(EmailAttachment attachment)
    {
        using var stream = attachment.OpenRead();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new GraphFileAttachment(
            "#microsoft.graph.fileAttachment",
            attachment.FileName,
            attachment.ContentType,
            Convert.ToBase64String(buffer.ToArray()));
    }

    private async Task<string?> SendAuthenticatedAsync(
        Uri uri,
        string json,
        bool readResponseBody,
        CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DeliveryException)
        {
            throw;
        }
        catch
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Permanent,
                "Authentication");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new EmailDeliveryException(
                "MicrosoftGraph",
                DeliveryFailureKind.Transient,
                "Network");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return readResponseBody
                    ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                    : null;
            }

            throw CreateStatusException(response);
        }
    }

    private static GraphItemBody Body(EmailDeliveryRequest request)
    {
        var hasHtmlBody = !string.IsNullOrWhiteSpace(request.HtmlBody);
        return new GraphItemBody(
            hasHtmlBody ? "HTML" : "Text",
            hasHtmlBody ? request.HtmlBody : request.PlainTextBody);
    }

    private static EmailDeliveryException CreateStatusException(HttpResponseMessage response) =>
        new(
            "MicrosoftGraph",
            Classify(response.StatusCode),
            ((int)response.StatusCode).ToString(),
            response.Headers.RetryAfter?.Delta);

    private static DeliveryFailureKind Classify(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500
            ? DeliveryFailureKind.Transient
            : DeliveryFailureKind.Permanent;

    internal static bool IsAttachmentSizeSupported(long length) =>
        length >= 0 && length <= MaximumAttachmentLength;
}
