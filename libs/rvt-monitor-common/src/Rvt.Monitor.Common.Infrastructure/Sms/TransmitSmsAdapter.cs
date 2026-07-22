using System.Net;
using System.Text.Json;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Infrastructure.Communications;

namespace Rvt.Monitor.Common.Infrastructure.Sms;

public sealed class TransmitSmsAdapter : ISmsDeliveryPort
{
    private const string ProviderName = "TransmitSMS";

    private readonly TransmitSmsClient client;
    private readonly CommunicationsOptions options;

    public TransmitSmsAdapter(HttpClient httpClient, CommunicationsOptions options)
    {
        client = new TransmitSmsClient(httpClient);
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task SendAsync(
        SmsDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        try
        {
            await client.SendAsync(
                new TransmitSmsRequest(
                    options.SmsApiKey,
                    options.SmsApiSecret,
                    request.Recipient,
                    request.Content,
                    options.SmsSender),
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
        if (!options.SmsEnabled)
        {
            throw Failure(DeliveryFailureKind.Configuration, "disabled");
        }

        if (string.IsNullOrWhiteSpace(options.SmsApiKey) ||
            string.IsNullOrWhiteSpace(options.SmsApiSecret) ||
            string.IsNullOrWhiteSpace(options.SmsSender))
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

        var value = (int)statusCode.Value;
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
