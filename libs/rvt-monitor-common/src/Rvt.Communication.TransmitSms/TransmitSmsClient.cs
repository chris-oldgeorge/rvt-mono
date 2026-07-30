using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rvt.Communication.TransmitSms;

internal sealed class TransmitSmsClient
{
    internal const string UnknownErrorCode = "UNKNOWN";

    internal static readonly Uri DefaultEndpoint = new("https://api.transmitsms.com/send-sms.json");

    private readonly HttpClient httpClient;
    private readonly Uri endpoint;

    internal TransmitSmsClient(HttpClient httpClient)
        : this(httpClient, DefaultEndpoint)
    {
    }

    internal TransmitSmsClient(HttpClient httpClient, Uri endpoint)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    internal async Task SendAsync(
        TransmitSmsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpRequestMessage message = new(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{request.ApiKey}:{request.ApiSecret}")));
        message.Content = new FormUrlEncodedContent(BuildFields(request));

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new TransmitSmsException(
                ((int)response.StatusCode).ToString(),
                response.StatusCode,
                RetryAfter(response.Headers.RetryAfter));
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        TransmitSmsResponse? result = JsonSerializer.Deserialize(
            responseBody,
            TransmitSmsJsonContext.Default.TransmitSmsResponse);
        string? errorCode = result?.Error?.Code;
        if (string.Equals(errorCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The provider answered 200 but the body carried no recognisable
        // error code — an empty body, a `null` literal, or a schema change.
        // That is not evidence the message was rejected, so it must not
        // dead-letter a message the provider may well have accepted.
        if (errorCode is null)
        {
            throw new TransmitSmsException(
                UnknownErrorCode,
                statusCode: null,
                retryAfter: null,
                isUnparseableSuccessBody: true);
        }

        throw new TransmitSmsException(errorCode, statusCode: null, retryAfter: null);
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildFields(TransmitSmsRequest request)
    {
        yield return new KeyValuePair<string, string>("message", request.Message);
        yield return new KeyValuePair<string, string>("to", request.Recipient);

        if (!string.IsNullOrWhiteSpace(request.Sender))
        {
            yield return new KeyValuePair<string, string>("from", request.Sender);
        }
    }

    private static TimeSpan? RetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is not { } date)
        {
            return null;
        }

        TimeSpan delay = date - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }
}

internal sealed record TransmitSmsRequest(
    string ApiKey,
    string ApiSecret,
    string Recipient,
    string Message,
    string? Sender);

internal sealed class TransmitSmsException(
    string code,
    HttpStatusCode? statusCode,
    TimeSpan? retryAfter,
    bool isUnparseableSuccessBody = false) : Exception("TransmitSMS request failed.")
{
    internal string Code { get; } = code;

    internal HttpStatusCode? StatusCode { get; } = statusCode;

    internal TimeSpan? RetryAfter { get; } = retryAfter;

    /// <summary>
    /// True when the transport succeeded but the response body carried no
    /// recognisable error code. Distinct from a provider rejection so the
    /// adapter can retry instead of dead-lettering an accepted message.
    /// </summary>
    internal bool IsUnparseableSuccessBody { get; } = isUnparseableSuccessBody;
}

internal sealed class TransmitSmsResponse
{
    [JsonPropertyName("error")]
    public TransmitSmsError? Error { get; init; }
}

internal sealed class TransmitSmsError
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

[JsonSerializable(typeof(TransmitSmsResponse))]
internal sealed partial class TransmitSmsJsonContext : JsonSerializerContext;
