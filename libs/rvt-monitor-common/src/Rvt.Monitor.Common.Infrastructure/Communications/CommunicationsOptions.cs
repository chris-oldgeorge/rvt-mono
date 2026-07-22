using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Infrastructure.Communications;

public sealed record CommunicationsOptions
{
    public EmailProvider EmailProvider { get; init; } = EmailProvider.SendGrid;

    public bool EmailEnabled { get; init; } = true;

    public string SendGridApiKey { get; init; } = string.Empty;

    public string FromEmail { get; init; } = "NoReply@rvtgroup.co.uk";

    public string FromName { get; init; } = "RVT Cloud";

    public string MicrosoftTenantId { get; init; } = string.Empty;

    public string MicrosoftClientId { get; init; } = string.Empty;

    public string MicrosoftClientSecret { get; init; } = string.Empty;

    public string MicrosoftSenderAddress { get; init; } = string.Empty;

    public bool SmsEnabled { get; init; }

    public string SmsApiKey { get; init; } = string.Empty;

    public string SmsApiSecret { get; init; } = string.Empty;

    public string SmsSender { get; init; } = "KrakenAlert";

    public static CommunicationsOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new CommunicationsOptions
        {
            EmailProvider = ParseProvider(Get(configuration, "EMAIL_PROVIDER")),
            EmailEnabled = ParseBoolean(configuration, "EMAIL_ENABLED", defaultValue: true),
            SendGridApiKey = Get(configuration, "SENDGRID_API_KEY") ?? string.Empty,
            FromEmail = Get(configuration, "EMAIL_ALERT_FROM_EMAIL") ?? "NoReply@rvtgroup.co.uk",
            FromName = Get(configuration, "EMAIL_ALERT_FROM_NAME") ?? "RVT Cloud",
            MicrosoftTenantId = Get(configuration, "MICROSOFT_TENANT_ID") ?? string.Empty,
            MicrosoftClientId = Get(configuration, "MICROSOFT_CLIENT_ID") ?? string.Empty,
            MicrosoftClientSecret = Get(configuration, "MICROSOFT_CLIENT_SECRET") ?? string.Empty,
            MicrosoftSenderAddress = Get(configuration, "MICROSOFT_SENDER_ADDRESS") ?? string.Empty,
            SmsEnabled = ParseBoolean(configuration, "SMS_ENABLED", defaultValue: false),
            SmsApiKey = Get(configuration, "SMS_API_KEY") ?? string.Empty,
            SmsApiSecret = Get(configuration, "SMS_API_SECRET") ?? string.Empty,
            SmsSender = Get(configuration, "SMS_SENDER") ?? "KrakenAlert"
        };
    }

    public void Validate()
    {
        var missing = new List<string>();

        if (EmailEnabled)
        {
            if (EmailProvider == EmailProvider.SendGrid)
            {
                Require(SendGridApiKey, "RVT__SENDGRID_API_KEY", missing);
                Require(FromEmail, "RVT__EMAIL_ALERT_FROM_EMAIL", missing);
                Require(FromName, "RVT__EMAIL_ALERT_FROM_NAME", missing);
            }
            else if (EmailProvider == EmailProvider.MicrosoftGraph)
            {
                Require(MicrosoftTenantId, "RVT__MICROSOFT_TENANT_ID", missing);
                Require(MicrosoftClientId, "RVT__MICROSOFT_CLIENT_ID", missing);
                Require(MicrosoftClientSecret, "RVT__MICROSOFT_CLIENT_SECRET", missing);
                Require(MicrosoftSenderAddress, "RVT__MICROSOFT_SENDER_ADDRESS", missing);
            }
            else
            {
                missing.Add("RVT__EMAIL_PROVIDER");
            }
        }

        if (SmsEnabled)
        {
            Require(SmsApiKey, "RVT__SMS_API_KEY", missing);
            Require(SmsApiSecret, "RVT__SMS_API_SECRET", missing);
            Require(SmsSender, "RVT__SMS_SENDER", missing);
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Communications configuration is missing required settings: {string.Join(", ", missing)}.");
        }
    }

    private static string? Get(IConfiguration configuration, string name) =>
        configuration[$"RVT:{name}"] ?? configuration[$"RVT__{name}"];

    private static EmailProvider ParseProvider(string? configured)
    {
        if (configured is null)
        {
            return EmailProvider.SendGrid;
        }

        if (Enum.TryParse<EmailProvider>(configured, ignoreCase: true, out var provider) &&
            Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            "RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.");
    }

    private static bool ParseBoolean(
        IConfiguration configuration,
        string name,
        bool defaultValue)
    {
        var configured = Get(configuration, name);
        if (configured is null)
        {
            return defaultValue;
        }

        if (bool.TryParse(configured, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"RVT__{name} must be true or false.");
    }

    private static void Require(string value, string settingName, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(settingName);
        }
    }
}
