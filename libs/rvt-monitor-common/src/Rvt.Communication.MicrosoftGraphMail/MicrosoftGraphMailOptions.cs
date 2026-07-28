using Microsoft.Extensions.Configuration;

namespace Rvt.Communication.MicrosoftGraphMail;

public sealed record MicrosoftGraphMailOptions
{
    public bool Enabled { get; init; } = true;
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string SenderAddress { get; init; } = string.Empty;

    public static MicrosoftGraphMailOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new MicrosoftGraphMailOptions
        {
            Enabled = ParseBoolean(configuration, "EMAIL_ENABLED", defaultValue: true),
            TenantId = Get(configuration, "MICROSOFT_TENANT_ID") ?? string.Empty,
            ClientId = Get(configuration, "MICROSOFT_CLIENT_ID") ?? string.Empty,
            ClientSecret = Get(configuration, "MICROSOFT_CLIENT_SECRET") ?? string.Empty,
            SenderAddress = Get(configuration, "MICROSOFT_SENDER_ADDRESS") ?? string.Empty
        };
    }

    public void Validate()
    {
        if (!Enabled) return;

        List<string> missing = new List<string>();
        Require(TenantId, "RVT__MICROSOFT_TENANT_ID", missing);
        Require(ClientId, "RVT__MICROSOFT_CLIENT_ID", missing);
        Require(ClientSecret, "RVT__MICROSOFT_CLIENT_SECRET", missing);
        Require(SenderAddress, "RVT__MICROSOFT_SENDER_ADDRESS", missing);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Microsoft Graph mail configuration is missing required settings: {string.Join(", ", missing)}.");
        }
    }

    private static string? Get(IConfiguration configuration, string name) =>
        configuration[$"RVT:{name}"] ?? configuration[$"RVT__{name}"];

    private static bool ParseBoolean(IConfiguration configuration, string name, bool defaultValue)
    {
        string? configured = Get(configuration, name);
        if (configured is null) return defaultValue;
        if (bool.TryParse(configured, out bool value)) return value;
        throw new InvalidOperationException($"RVT__{name} must be true or false.");
    }

    private static void Require(string value, string settingName, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value)) missing.Add(settingName);
    }
}
