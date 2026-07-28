using System.Text;
using System.Text.Json;
using Omnidots.Model.Config;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.UseCases;

public sealed class ProcessWebhookHandler(
    IAlertIngressPort ingress,
    OmnidotsAlarmTranslator translator,
    OmnidotsApiSecurityOptions securityOptions,
    OmnidotsWebhookSignatureValidator signatureValidator)
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IAlertIngressPort ingress = ingress;
    private readonly OmnidotsAlarmTranslator translator = translator;
    private readonly OmnidotsApiSecurityOptions securityOptions = securityOptions;
    private readonly OmnidotsWebhookSignatureValidator signatureValidator = signatureValidator;

    public async Task<AlertIngressResult> RunAsync(
        ReadOnlyMemory<byte> body,
        string signature,
        CancellationToken cancellationToken = default)
    {
        OmnidotsApiSecurityGuard.EnsureWebhookReady(securityOptions);
        if (!signatureValidator.IsValid(body.Span, signature, securityOptions.WebhookSecret))
        {
            throw new OmnidotsWebhookAuthenticationException();
        }

        string json = DecodeJson(body.Span);
        AlarmDataV2 alarm = JsonSerializer.Deserialize<AlarmDataV2>(json)
            ?? throw AdapterException.Of("Invalid alarm payload.");
        AlertSignal signal = translator.Translate(
            alarm,
            body.Span,
            TimeSpan.FromMinutes(securityOptions.NotificationDelayMinutes));

        return await ingress.AcceptAsync(signal, cancellationToken);
    }

    private static string DecodeJson(ReadOnlySpan<byte> body)
    {
        try
        {
            string json = StrictUtf8.GetString(body);
            return json.Length > 0 && json[0] == '\uFEFF'
                ? json[1..]
                : json;
        }
        catch (DecoderFallbackException)
        {
            throw new JsonException("Webhook payload must be valid UTF-8.");
        }
    }
}
