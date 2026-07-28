using Microsoft.Extensions.Configuration;

namespace Rvt.Communication.TransmitSms;

public sealed record TransmitSmsOptions
{
    public bool Enabled { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string ApiSecret { get; init; } = string.Empty;

    public string Sender { get; init; } = "KrakenAlert";

    public static TransmitSmsOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new TransmitSmsOptions
        {
            Enabled = ParseBoolean(configuration, "SMS_ENABLED", defaultValue: false),
            ApiKey = Get(configuration, "SMS_API_KEY") ?? string.Empty,
            ApiSecret = Get(configuration, "SMS_API_SECRET") ?? string.Empty,
            Sender = Get(configuration, "SMS_SENDER") ?? "KrakenAlert"
        };
    }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        List<string> missing = new List<string>();
        Require(ApiKey, "RVT__SMS_API_KEY", missing);
        Require(ApiSecret, "RVT__SMS_API_SECRET", missing);
        Require(Sender, "RVT__SMS_SENDER", missing);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"TransmitSMS configuration is missing required settings: {string.Join(", ", missing)}.");
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

    private static void Require(string value, string settingName, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(settingName);
        }
    }
}
