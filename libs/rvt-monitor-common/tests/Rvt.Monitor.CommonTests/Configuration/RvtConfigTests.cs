using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.CommonTests.Configuration;

[TestClass]
public sealed class RvtConfigTests
{
    [TestMethod]
    [DataRow("AirQ", "AirQMonitor noise monitor data collector running ", "https://datacollector.airqweb.com", "rvt/noise/inserted", "rvt/noise/alerted")]
    [DataRow("MyAtm", "MyAtmMonitor dust monitor data collector running ", "https://api.my-atmosphere.cloud/api/", "rvt/dust/inserted", "rvt/dust/alerted")]
    [DataRow("Omnidots", "OmnidotsMonitor vibration monitor data collector running ", "https://honeycomb.omnidots.com", "rvt/vibration/inserted", "rvt/vibration/alerted")]
    [DataRow("Svantek", "SvantekMonitor noise monitor data collector running ", "https://svannet.com/api/v2.3/", "rvt/noise/inserted", "rvt/noise/alerted")]
    public void ResolveMonitorDefaultsPreservesMonitorSpecificRuntimeDefaults(
        string monitorKind,
        string serviceName,
        string baseUrl,
        string insertTopic,
        string alertTopic)
    {
        MonitorRuntimeDefaults defaults = RvtConfig.ResolveMonitorDefaults(monitorKind);

        Assert.AreEqual(serviceName, defaults.ServiceName);
        Assert.AreEqual(baseUrl, defaults.BaseUrl);
        Assert.AreEqual(insertTopic, defaults.InsertTopic);
        Assert.AreEqual(alertTopic, defaults.AlertTopic);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("unknown-monitor")]
    public void ResolveMonitorDefaultsFallsToNeutralDefaultsWithoutAKnownKind(string? monitorKind)
    {
        // The entry-assembly and base-directory sniffing was deleted by
        // legacy-retirement step 7; RVT__MONITOR_KIND is the only signal.
        MonitorRuntimeDefaults defaults = RvtConfig.ResolveMonitorDefaults(monitorKind);

        Assert.AreEqual("", defaults.Kind);
        Assert.AreEqual("RVT monitor data collector running ", defaults.ServiceName);
        Assert.AreEqual("", defaults.BaseUrl);
    }

    [TestMethod]
    public void ResolveCredentialSettingsPrefersOmnidotsNamesWhenRunningAsOmnidots()
    {
        Dictionary<string, string?> values = new()
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        MonitorCredentialSettings credentials = RvtConfig.ResolveCredentialSettings("Omnidots", values.GetValueOrDefault);

        Assert.AreEqual("omnidots-user", credentials.UserId);
        Assert.AreEqual("omnidots-auth", credentials.UserAuth);
        // The static-token escape hatch was removed by product ruling on
        // 2026-07-29; Omnidots always authenticates against the vendor.
        Assert.AreEqual(string.Empty, credentials.Token);
    }

    [TestMethod]
    public void ResolveCredentialSettingsPreservesAirQAndMyAtmNamesForTheirMonitorKinds()
    {
        Dictionary<string, string?> values = new()
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        MonitorCredentialSettings airqCredentials = RvtConfig.ResolveCredentialSettings("AirQ", values.GetValueOrDefault);
        MonitorCredentialSettings myAtmCredentials = RvtConfig.ResolveCredentialSettings("MyAtm", values.GetValueOrDefault);

        Assert.AreEqual("airq-user", airqCredentials.UserId);
        Assert.AreEqual("airq-auth", airqCredentials.UserAuth);
        Assert.AreEqual(string.Empty, airqCredentials.Token);
        Assert.AreEqual(string.Empty, myAtmCredentials.UserId);
        Assert.AreEqual(string.Empty, myAtmCredentials.UserAuth);
        Assert.AreEqual("myatm-token", myAtmCredentials.Token);
    }

    [TestMethod]
    public void ResolveCredentialSettingsFailsClosedWhenMonitorKindIsUnknown()
    {
        Dictionary<string, string?> values = new()
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        MonitorCredentialSettings credentials = RvtConfig.ResolveCredentialSettings("unknown", values.GetValueOrDefault);

        Assert.AreEqual(string.Empty, credentials.UserId);
        Assert.AreEqual(string.Empty, credentials.UserAuth);
        Assert.AreEqual(string.Empty, credentials.Token);
    }
}
