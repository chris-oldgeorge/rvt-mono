using Microsoft.Extensions.Configuration;

namespace Rvt.Communication.SendGridMail;

public sealed record SendGridMailOptions
{
    public bool Enabled { get; init; } = true;

    public string ApiKey { get; init; } = string.Empty;

    public string FromEmail { get; init; } = "NoReply@rvtgroup.co.uk";

    public string FromName { get; init; } = "RVT Cloud";

    public static SendGridMailOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new SendGridMailOptions
        {
            Enabled = ParseBoolean(configuration, "EMAIL_ENABLED", defaultValue: true),
            ApiKey = Get(configuration, "SENDGRID_API_KEY") ?? string.Empty,
            FromEmail = Get(configuration, "EMAIL_ALERT_FROM_EMAIL") ?? "NoReply@rvtgroup.co.uk",
            FromName = Get(configuration, "EMAIL_ALERT_FROM_NAME") ?? "RVT Cloud"
        };
    }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        List<string> missing = [];
        Require(ApiKey, "RVT__SENDGRID_API_KEY", missing);
        Require(FromEmail, "RVT__EMAIL_ALERT_FROM_EMAIL", missing);
        Require(FromName, "RVT__EMAIL_ALERT_FROM_NAME", missing);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"SendGrid mail configuration is missing required settings: {string.Join(", ", missing)}.");
        }
    }

    private static string? Get(IConfiguration configuration, string name) =>
        configuration[$"RVT:{name}"] ?? configuration[$"RVT__{name}"];

    private static bool ParseBoolean(
        IConfiguration configuration,
        string name,
        bool defaultValue)
    {
        string? configured = Get(configuration, name);
        if (configured is null)
        {
            return defaultValue;
        }

        if (bool.TryParse(configured, out bool value))
        {
            return value;
        }

        throw new InvalidOperationException($"RVT__{name} must be true or false.");
    }

    private static void Require(string value, string settingName, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(settingName);
        }
    }
}
