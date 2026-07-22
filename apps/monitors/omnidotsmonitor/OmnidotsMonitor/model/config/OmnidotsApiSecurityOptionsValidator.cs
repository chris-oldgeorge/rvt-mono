using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Rvt.Monitor.Common.Hosting;

namespace Omnidots.Model.Config;

public sealed class OmnidotsApiSecurityOptionsValidator(MonitorExecutionModeContext executionMode)
    : IValidateOptions<OmnidotsApiSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, OmnidotsApiSecurityOptions options)
    {
        if (executionMode.Mode != MonitorExecutionMode.Api)
        {
            return ValidateOptionsResult.Success;
        }

        return OmnidotsApiSecurityValidation.IsApiReady(options)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(OmnidotsApiSecurityValidation.FailureMessage);
    }
}

internal static class OmnidotsApiSecurityValidation
{
    internal const string FailureMessage = "Omnidots API security configuration is invalid.";
    internal const int MinimumSecretByteCount = 32;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static bool IsApiReady(OmnidotsApiSecurityOptions? options)
    {
        if (options is null ||
            !IsAbsoluteHttpsUrl(options.WebhookUrl) ||
            options.NotificationDelayMinutes <= 0 ||
            options.WebhookConcurrencyLimit <= 0 ||
            options.ConfigureConcurrencyLimit <= 0)
        {
            return false;
        }

        return HasStrongDistinctSecrets(options.WebhookSecret, options.ConfigSecret);
    }

    internal static bool IsWebhookReady(OmnidotsApiSecurityOptions? options)
    {
        return options is not null &&
            options.NotificationDelayMinutes > 0 &&
            options.WebhookConcurrencyLimit > 0 &&
            HasStrongDistinctSecrets(options.WebhookSecret, options.ConfigSecret);
    }

    internal static bool IsConfigurationReady(OmnidotsApiSecurityOptions? options) =>
        options is not null &&
        IsAbsoluteHttpsUrl(options.WebhookUrl) &&
        options.NotificationDelayMinutes > 0 &&
        options.ConfigureConcurrencyLimit > 0 &&
        HasStrongDistinctSecrets(options.WebhookSecret, options.ConfigSecret);

    internal static bool TryGetSecretBytes(string? secret, out byte[] secretBytes)
    {
        secretBytes = [];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        try
        {
            secretBytes = StrictUtf8.GetBytes(secret);
            if (secretBytes.Length >= MinimumSecretByteCount)
            {
                return true;
            }

            CryptographicOperations.ZeroMemory(secretBytes);
            secretBytes = [];
            return false;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool HasStrongDistinctSecrets(string? webhookSecret, string? configSecret)
    {
        if (!TryGetSecretBytes(webhookSecret, out var webhookSecretBytes))
        {
            return false;
        }

        if (!TryGetSecretBytes(configSecret, out var configSecretBytes))
        {
            CryptographicOperations.ZeroMemory(webhookSecretBytes);
            return false;
        }

        try
        {
            return !CryptographicOperations.FixedTimeEquals(webhookSecretBytes, configSecretBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(webhookSecretBytes);
            CryptographicOperations.ZeroMemory(configSecretBytes);
        }
    }

    private static bool IsAbsoluteHttpsUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrEmpty(uri.Host);
}
