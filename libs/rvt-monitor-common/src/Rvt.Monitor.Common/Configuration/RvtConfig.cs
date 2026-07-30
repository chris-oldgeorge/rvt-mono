using Microsoft.Extensions.Configuration;

namespace Rvt.Monitor.Common.Configuration;

public sealed class RvtConfig
{
    private const string _airQKind = "airq";
    private const string _myAtmKind = "myatm";
    private const string _omnidotsKind = "omnidots";
    private const string _svantekKind = "svantek";

    private RvtConfig()
    {
    }

    private static readonly IConfigurationRoot Configuration = BuildConfiguration();

    // Lazy so the kind is read on first use, not when an unrelated static
    // field first touches this type — test hosts set RVT__MONITOR_KIND in an
    // assembly initializer that must win that race.
    private static readonly Lazy<MonitorRuntimeDefaults> _resolvedDefaults =
        new(() => ResolveMonitorDefaults(Environment.GetEnvironmentVariable("RVT__MONITOR_KIND")));

    private static MonitorRuntimeDefaults Defaults => _resolvedDefaults.Value;

    private static string GetSetting(string name, string defaultValue = "")
    {
        return GetOptionalSetting(name) ?? defaultValue;
    }

    private static string? GetOptionalSetting(string name) =>
        Environment.GetEnvironmentVariable(name) ?? Configuration[name];

    private static bool GetBoolSetting(string name, bool defaultValue = false) =>
        bool.TryParse(GetSetting(name), out bool value) ? value : defaultValue;

    private static int GetIntSetting(string name, int defaultValue) =>
        int.TryParse(GetSetting(name), out int value) ? value : defaultValue;

    private static IConfigurationRoot BuildConfiguration()
    {
        string environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .Build();
    }

    internal static MonitorRuntimeDefaults ResolveMonitorDefaults(string? monitorKind)
    {
        string normalized = NormalizeMonitorKind(monitorKind);
        return normalized switch
        {
            _airQKind => new MonitorRuntimeDefaults(
                _airQKind,
                "AirQMonitor noise monitor data collector running ",
                "https://datacollector.airqweb.com",
                "rvt/noise/inserted",
                "rvt/noise/alerted",
                "https://www.rvtcloud.com/"),
            _myAtmKind => new MonitorRuntimeDefaults(
                _myAtmKind,
                "MyAtmMonitor dust monitor data collector running ",
                "https://api.my-atmosphere.cloud/api/",
                "rvt/dust/inserted",
                "rvt/dust/alerted",
                ""),
            _omnidotsKind => new MonitorRuntimeDefaults(
                _omnidotsKind,
                "OmnidotsMonitor vibration monitor data collector running ",
                "https://honeycomb.omnidots.com",
                "rvt/vibration/inserted",
                "rvt/vibration/alerted",
                ""),
            _svantekKind => new MonitorRuntimeDefaults(
                _svantekKind,
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

    /// <summary>
    /// Resolves which monitor the process is running as.
    /// </summary>
    /// <remarks>
    /// <c>RVT__MONITOR_KIND</c> is the only signal; every deployed vendor
    /// monitor (airq/myatm/omnidots/svantek) declares it. The reporting
    /// monitor deliberately declares no kind — it has no vendor defaults here
    /// and runs on the neutral fallback. The entry-assembly and base-directory
    /// sniffing that once backed this up was deleted on 2026-07-29
    /// (legacy-retirement step 7) — library behaviour no longer depends on
    /// what the executable is named.
    /// </remarks>
    private static string NormalizeMonitorKind(string? monitorKind)
    {
        if (string.IsNullOrWhiteSpace(monitorKind))
        {
            return "";
        }

        string normalized = monitorKind.Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        if (normalized.Contains(_airQKind, StringComparison.Ordinal))
        {
            return _airQKind;
        }

        if (normalized.Contains(_myAtmKind, StringComparison.Ordinal))
        {
            return _myAtmKind;
        }

        if (normalized.Contains(_omnidotsKind, StringComparison.Ordinal))
        {
            return _omnidotsKind;
        }

        if (normalized.Contains(_svantekKind, StringComparison.Ordinal))
        {
            return _svantekKind;
        }

        return "";
    }

    internal static MonitorCredentialSettings ResolveCredentialSettings(
        string? monitorKind,
        Func<string, string?> getOptionalSetting)
    {
        static string Get(Func<string, string?> getOptionalSetting, string name) =>
            getOptionalSetting(name) ?? string.Empty;

        return NormalizeMonitorKind(monitorKind) switch
        {
            _airQKind => new MonitorCredentialSettings(
                UserId: Get(getOptionalSetting, "RVT__AIRQ_USER_ID"),
                UserAuth: Get(getOptionalSetting, "RVT__AIRQ_USER_AUTH"),
                Token: string.Empty),
            _myAtmKind => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: Get(getOptionalSetting, "RVT__MYATM_TOKEN")),
            _omnidotsKind => new MonitorCredentialSettings(
                UserId: Get(getOptionalSetting, "RVT__OMNIDOTS_USER_ID"),
                UserAuth: Get(getOptionalSetting, "RVT__OMNIDOTS_USER_AUTH"),
                Token: string.Empty),
            _svantekKind => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: string.Empty),
            _ => new MonitorCredentialSettings(
                UserId: string.Empty,
                UserAuth: string.Empty,
                Token: string.Empty)
        };
    }


    /// <summary>
    /// The resolved monitor-specific rule behaviour. Shared rules take this as
    /// an explicit value rather than reading process-wide state, so they can be
    /// evaluated for a chosen monitor without depending on which executable is
    /// running.
    /// </summary>
    public static MonitorRulePolicy RulePolicy => MonitorRulePolicy.ForMonitorKind(Defaults.Kind);
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
    public static readonly string DB_CONNECTION_STRING = GetSetting("ConnectionStrings__DefaultConnection");
    public static readonly bool TESTLOCAL = GetBoolSetting("testlocal");
    public static string PORTAL_BASE_URL => GetSetting("RVT__PORTAL_BASE_URL", Defaults.PortalBaseUrl);
    public static string BASE_URL => GetSetting("RVT__BASE_URL", Defaults.BaseUrl);
    public static string INSERT_TOPIC => GetSetting("RVT__INSERT_TOPIC", Defaults.InsertTopic);
    public static string ALERT_TOPIC => GetSetting("RVT__ALERT_TOPIC", Defaults.AlertTopic);

    public static string USER_ID => Credentials.UserId;
    public static string USER_AUTH => Credentials.UserAuth;
    public static string TOKEN => Credentials.Token;

    public static readonly string API_KEY = GetSetting("RVT__SVANTEK_API_KEY");
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
