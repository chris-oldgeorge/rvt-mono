using System.Text;
using System.Text.Json;
using Omnidots.Model.Config;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.UseCases;

public sealed class ProcessWebhookHandler
{
    private static readonly UTF8Encoding _strictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IAlertIngressPort _ingress;
    private readonly OmnidotsAlarmTranslator _translator;
    private readonly OmnidotsApiSecurityOptions _securityOptions;
    private readonly OmnidotsWebhookSignatureValidator _signatureValidator;

    public ProcessWebhookHandler(
        IAlertIngressPort ingress,
        OmnidotsAlarmTranslator translator,
        OmnidotsApiSecurityOptions securityOptions,
        OmnidotsWebhookSignatureValidator signatureValidator)
    {
        _ingress = ingress;
        _translator = translator;
        _securityOptions = securityOptions;
        _signatureValidator = signatureValidator;
    }

    public async Task<AlertIngressResult> RunAsync(
        ReadOnlyMemory<byte> body,
        string signature,
        CancellationToken cancellationToken = default)
    {
        OmnidotsApiSecurityGuard.EnsureWebhookReady(_securityOptions);
        if (!_signatureValidator.IsValid(body.Span, signature, _securityOptions.WebhookSecret))
        {
            throw new OmnidotsWebhookAuthenticationException();
        }

        string json = DecodeJson(body.Span);
        AlarmDataV2 alarm = JsonSerializer.Deserialize<AlarmDataV2>(json)
            ?? throw AdapterException.Of("Invalid alarm payload.");
        AlertSignal signal = _translator.Translate(
            alarm,
            body.Span,
            TimeSpan.FromMinutes(_securityOptions.NotificationDelayMinutes));

        return await _ingress.AcceptAsync(signal, cancellationToken);
    }

    private static string DecodeJson(ReadOnlySpan<byte> body)
    {
        try
        {
            string json = _strictUtf8.GetString(body);
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
