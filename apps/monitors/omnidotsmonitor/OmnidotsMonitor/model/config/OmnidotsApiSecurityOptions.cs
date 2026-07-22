using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Omnidots.Model.Config;

public sealed class OmnidotsApiSecurityOptions
{
    public const string SectionName = "Omnidots:Api";

    public string WebhookUrl { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string ConfigSecret { get; set; } = string.Empty;

    public int NotificationDelayMinutes { get; set; } = 5;

    public int WebhookConcurrencyLimit { get; set; } = 8;

    public int ConfigureConcurrencyLimit { get; set; } = 2;
}

internal sealed class OmnidotsApiSecurityOptionsSetup(IConfiguration configuration)
    : IConfigureOptions<OmnidotsApiSecurityOptions>
{
    public void Configure(OmnidotsApiSecurityOptions options)
    {
        ConfigureString(
            options,
            nameof(OmnidotsApiSecurityOptions.WebhookUrl),
            "RVT__OMNIDOTS_WEBHOOK_URL",
            static (target, value) => target.WebhookUrl = value);
        ConfigureString(
            options,
            nameof(OmnidotsApiSecurityOptions.WebhookSecret),
            "RVT__OMNIDOTS_WEBHOOK_SECRET",
            static (target, value) => target.WebhookSecret = value);
        ConfigureString(
            options,
            nameof(OmnidotsApiSecurityOptions.ConfigSecret),
            "RVT__OMNIDOTS_CONFIG_SECRET",
            static (target, value) => target.ConfigSecret = value);
        ConfigureInt32(
            options,
            nameof(OmnidotsApiSecurityOptions.NotificationDelayMinutes),
            "RVT__NOTIFICATION_DELAY_MINUTES",
            static (target, value) => target.NotificationDelayMinutes = value);
        ConfigureInt32(
            options,
            nameof(OmnidotsApiSecurityOptions.WebhookConcurrencyLimit),
            legacyKey: null,
            static (target, value) => target.WebhookConcurrencyLimit = value);
        ConfigureInt32(
            options,
            nameof(OmnidotsApiSecurityOptions.ConfigureConcurrencyLimit),
            legacyKey: null,
            static (target, value) => target.ConfigureConcurrencyLimit = value);
    }

    private void ConfigureString(
        OmnidotsApiSecurityOptions options,
        string propertyName,
        string legacyKey,
        Action<OmnidotsApiSecurityOptions, string> assign)
    {
        var value = configuration[$"{OmnidotsApiSecurityOptions.SectionName}:{propertyName}"];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = GetLegacyValue(legacyKey);
        }

        if (value is not null)
        {
            assign(options, value);
        }
    }

    private void ConfigureInt32(
        OmnidotsApiSecurityOptions options,
        string propertyName,
        string? legacyKey,
        Action<OmnidotsApiSecurityOptions, int> assign)
    {
        var value = configuration[$"{OmnidotsApiSecurityOptions.SectionName}:{propertyName}"];
        if (value is null && legacyKey is not null)
        {
            value = GetLegacyValue(legacyKey);
        }

        if (value is not null)
        {
            assign(
                options,
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0);
        }
    }

    private string? GetLegacyValue(string legacyKey) =>
        configuration[legacyKey] ?? configuration[legacyKey.Replace("__", ":", StringComparison.Ordinal)];
}
