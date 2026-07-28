using System.Net;
using System.Text.Json;
using Rvt.Communication.Abstractions;

namespace Rvt.Communication.TransmitSms;

public sealed class TransmitSmsAdapter : ISmsDeliveryPort
{
    private const string ProviderName = "TransmitSMS";

    private readonly TransmitSmsClient? client;
    private readonly IHttpClientFactory? httpClientFactory;
    private readonly TransmitSmsOptions options;

    public TransmitSmsAdapter(HttpClient httpClient, TransmitSmsOptions options)
    {
        client = new TransmitSmsClient(httpClient);
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal TransmitSmsAdapter(
        IHttpClientFactory httpClientFactory,
        TransmitSmsOptions options)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task SendAsync(
        SmsDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        if (httpClientFactory is not null)
        {
            using HttpClient operationClient = httpClientFactory.CreateClient(
                TransmitSmsServiceCollectionExtensions.HttpClientName);
            await SendAsync(
                new TransmitSmsClient(operationClient),
                request,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await SendAsync(client!, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(
        TransmitSmsClient operationClient,
        SmsDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await operationClient.SendAsync(
                new TransmitSmsRequest(
                    options.ApiKey,
                    options.ApiSecret,
                    request.Recipient,
                    request.Content,
                    options.Sender),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw Failure(DeliveryFailureKind.Transient, "timeout");
        }
        catch (HttpRequestException)
        {
            throw Failure(DeliveryFailureKind.Transient, "network");
        }
        catch (JsonException)
        {
            throw Failure(DeliveryFailureKind.Transient, "invalid-response");
        }
        catch (TransmitSmsException exception)
        {
            throw Failure(
                Classify(exception.StatusCode),
                exception.Code,
                exception.RetryAfter);
        }
    }

    private void EnsureConfigured()
    {
        if (!options.Enabled)
        {
            throw Failure(DeliveryFailureKind.Configuration, "disabled");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.IsNullOrWhiteSpace(options.ApiSecret) ||
            string.IsNullOrWhiteSpace(options.Sender))
        {
            throw Failure(DeliveryFailureKind.Configuration, "missing-settings");
        }
    }

    private static DeliveryFailureKind Classify(HttpStatusCode? statusCode)
    {
        if (statusCode is null)
        {
            return DeliveryFailureKind.Permanent;
        }

        int value = (int)statusCode.Value;
        return statusCode is HttpStatusCode.RequestTimeout || value == 429 || value >= 500
            ? DeliveryFailureKind.Transient
            : DeliveryFailureKind.Permanent;
    }

    private static SmsDeliveryException Failure(
        DeliveryFailureKind kind,
        string code,
        TimeSpan? retryAfter = null) =>
        new(ProviderName, kind, code, retryAfter);
}
