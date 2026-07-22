using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Configuration;

public sealed class RvtConfig
{
    private RvtConfig()
    {
    }

    private static readonly IConfigurationRoot Configuration = BuildConfiguration();

    private static readonly MonitorRuntimeDefaults LegacyDefaults = ResolveMonitorDefaults(
        Environment.GetEnvironmentVariable("RVT__MONITOR_KIND"),
        AppContext.BaseDirectory,
        Assembly.GetEntryAssembly()?.GetName().Name ?? "");

    private static MonitorRuntimeDefaults Defaults => LegacyDefaults;

    private static string GetSetting(string name, string defaultValue = "")
    {
        return GetOptionalSetting(name) ?? defaultValue;
    }

    private static string? GetOptionalSetting(string name) =>
        Environment.GetEnvironmentVariable(name) ?? Configuration[name];

    private static bool GetBoolSetting(string name, bool defaultValue = false) =>
        bool.TryParse(GetSetting(name), out var value) ? value : defaultValue;

    private static int GetIntSetting(string name, int defaultValue) =>
        int.TryParse(GetSetting(name), out var value) ? value : defaultValue;

    private static IConfigurationRoot BuildConfiguration()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .Build();
    }

    internal static MonitorRuntimeDefaults ResolveMonitorDefaults(
        string? monitorKind,
        string baseDirectory,
        string entryAssemblyName)
    {
        var normalized = NormalizeMonitorKind(monitorKind, baseDirectory, entryAssemblyName);
        return normalized switch
        {
            "airq" => new MonitorRuntimeDefaults(
                "airq",
                "AirQMonitor noise monitor data collector running ",
                "https://datacollector.airqweb.com",
                "rvt/noise/inserted",
                "rvt/noise/alerted",
                "https://www.rvtcloud.com/"),
            "myatm" => new MonitorRuntimeDefaults(
                "myatm",
                "MyAtmMonitor dust monitor data collector running ",
                "https://api.my-atmosphere.cloud/api/",
                "rvt/dust/inserted",
                "rvt/dust/alerted",
                ""),
            "omnidots" => new MonitorRuntimeDefaults(
                "omnidots",
                "OmnidotsMonitor vibration monitor data collector running ",
                "https://honeycomb.omnidots.com",
                "rvt/vibration/inserted",
                "rvt/vibration/alerted",
                ""),
            "svantek" => new MonitorRuntimeDefaults(
                "svantek",
                "SvantekMonitor noise monitor data collector running ",
                "https://svannet.com/api/v2.3/",
                "rvt/noise/inserted",
                "rvt/noise/alerted",
                "https://www.rvtcloud.com/"),
            _ => new MonitorRuntimeDefaults(
                "",
                "RVT monitor data collector running ",
                "",
                "",
                "",
                "https://www.rvtcloud.com/")
        };
    }

    private static string NormalizeMonitorKind(string? monitorKind, string baseDirectory, string entryAssemblyName)
    {
        var candidates = new[] { monitorKind, entryAssemblyName, baseDirectory };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Replace("-", "", StringComparison.OrdinalIgnoreCase)
                .Replace("_", "", StringComparison.OrdinalIgnoreCase)
                .Replace(".", "", StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();

            if (normalized.Contains("airq", StringComparison.Ordinal))
            {
                return "airq";
            }

            if (normalized.Contains("myatm", StringComparison.Ordinal))
            {
                return "myatm";
            }

            if (normalized.Contains("omnidots", StringComparison.Ordinal))
            {
                return "omnidots";
            }

            if (normalized.Contains("svantek", StringComparison.Ordinal))
            {
                return "svantek";
            }
        }

        return "";
    }

    internal static MonitorCredentialSettings ResolveCredentialSettings(
        string? monitorKind,
        Func<string, string?> getOptionalSetting)
    {
        static string Get(Func<string, string?> getOptionalSetting, string name) =>
            getOptionalSetting(name) ?? string.Empty;

        return NormalizeMonitorKind(monitorKind, "", "") switch
        {
            "airq" => new MonitorCredentialSettings(
                UserId: Get(getOptionalSetting, "RVT__AIRQ_USER_ID"),
                UserAuth: Get(getOptionalSetting, "RVT__AIRQ_USER_AUTH"),
                Token: string.Empty),
            "myatm" => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: Get(getOptionalSetting, "RVT__MYATM_TOKEN")),
            "omnidots" => new MonitorCredentialSettings(
                UserId: Get(getOptionalSetting, "RVT__OMNIDOTS_USER_ID"),
                UserAuth: Get(getOptionalSetting, "RVT__OMNIDOTS_USER_AUTH"),
                Token: Get(getOptionalSetting, "RVT__OMNIDOTS_TOKEN")),
            "svantek" => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: string.Empty),
            _ => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: string.Empty)
        };
    }

    internal static string MonitorKind => Defaults.Kind;
    internal static bool IsMyAtmMonitor => Defaults.Kind == "myatm";
    internal static bool IsOmnidotsMonitor => Defaults.Kind == "omnidots";
    internal static bool IsNoiseMonitor => Defaults.Kind is "airq" or "svantek";
    private static readonly Lazy<MonitorCredentialSettings> LazyCredentials =
        new(() => ResolveCredentialSettings(Defaults.Kind, GetOptionalSetting));

    private static MonitorCredentialSettings Credentials => LazyCredentials.Value;

    public static string SERVICE_NAME => GetSetting("RVT__SERVICE_NAME", Defaults.ServiceName);
    public static readonly string SERVICE_VERSION = GetSetting("RVT__SERVICE_VERSION", "v0.1.0");
    public static readonly bool MQTT_ENABLED = GetBoolSetting("RVT__MQTT_ENABLED");
    public static readonly string MQTT_HOSTNAME = GetSetting("RVT__MQTT_HOSTNAME", "rvt-mqtt-namespace.westeurope-1.ts.eventgrid.azure.net");
    public static readonly string MQTT_CLIENT_ID = GetSetting("RVT__MQTT_CLIENT_ID", "client1-session1");
    public static readonly string MQTT_USERNAME = GetSetting("RVT__MQTT_USERNAME", "client1-authn-ID");
    public static readonly string MQTT_CERTIFICATE_PATH = GetSetting("RVT__MQTT_CERTIFICATE_PATH");
    public static readonly string MQTT_PRIVATE_KEY_PATH = GetSetting("RVT__MQTT_PRIVATE_KEY_PATH");
    public static readonly bool SMS_ENABLED = GetBoolSetting("RVT__SMS_ENABLED");
    public static readonly bool EMAIL_ENABLED = GetBoolSetting("RVT__EMAIL_ENABLED");
    public static readonly string DB_CONNECTION_STRING = GetSetting("ConnectionStrings__DefaultConnection");
    public static readonly string DATABASE_PROVIDER = GetSetting("RVT__DATABASE_PROVIDER", "PostgreSql");
    public static readonly bool TESTLOCAL = GetBoolSetting("testlocal");
    public static string PORTAL_BASE_URL => GetSetting("RVT__PORTAL_BASE_URL", Defaults.PortalBaseUrl);
    public static string BASE_URL => GetSetting("RVT__BASE_URL", Defaults.BaseUrl);
    public static readonly string LOCAL_TIME_ZONE = GetSetting("RVT__LOCAL_TIME_ZONE", "GMT Standard Time");
    public static readonly string EMAIL_ALERT_FROM_EMAIL = GetSetting("RVT__EMAIL_ALERT_FROM_EMAIL", "NoReply@rvtgroup.co.uk");
    public static readonly string EMAIL_ALERT_FROM_NAME = GetSetting("RVT__EMAIL_ALERT_FROM_NAME", "RVT Cloud");
    public static readonly string SENDGRID_API_KEY = GetSetting("RVT__SENDGRID_API_KEY");
    public static readonly string SMS_API_SECRET = GetSetting("RVT__SMS_API_SECRET");
    public static readonly string SMS_API_KEY = GetSetting("RVT__SMS_API_KEY");
    public static readonly string SMS_SENDER = GetSetting("RVT__SMS_SENDER", "KrakenAlert");
    public static string INSERT_TOPIC => GetSetting("RVT__INSERT_TOPIC", Defaults.InsertTopic);
    public static string ALERT_TOPIC => GetSetting("RVT__ALERT_TOPIC", Defaults.AlertTopic);

    public static string USER_ID => Credentials.UserId;
    public static string USER_AUTH => Credentials.UserAuth;
    public static string TOKEN => Credentials.Token;
    public static readonly bool USE_TOKEN = GetBoolSetting("RVT__OMNIDOTS_USE_TOKEN", true);
    public static readonly string WEBHOOK_URL = GetSetting("RVT__OMNIDOTS_WEBHOOK_URL");
    public static readonly string WEBHOOK_SECRET = GetSetting("RVT__OMNIDOTS_WEBHOOK_SECRET");
    public static readonly string CONFIG_SECRET = GetSetting("RVT__OMNIDOTS_CONFIG_SECRET");
    public static readonly int NOTIFICATION_DELAY_MINUTES = GetIntSetting("RVT__NOTIFICATION_DELAY_MINUTES", 5);

    public static readonly string API_KEY = GetSetting("RVT__SVANTEK_API_KEY");
    public static readonly string BlobConnectionString = GetSetting("RVT__BLOB_CONNECTION_STRING");
    public static readonly string BlobServiceUri = GetSetting("RVT__BLOB_SERVICE_URI");
    public static readonly string AudioFolder = GetSetting("RVT__AUDIO_FOLDER", "audiofiles");
}

public sealed record MonitorRuntimeDefaults(
    string Kind,
    string ServiceName,
    string BaseUrl,
    string InsertTopic,
    string AlertTopic,
    string PortalBaseUrl);

internal sealed record MonitorCredentialSettings(
    string UserId,
    string UserAuth,
    string Token);
