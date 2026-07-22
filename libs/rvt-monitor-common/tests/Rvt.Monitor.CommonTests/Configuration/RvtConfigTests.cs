using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.CommonTests.Configuration;

[TestClass]
public sealed class RvtConfigTests
{
    [TestMethod]
    public void RuntimeDefaultsResolver_UsesTheExplicitMonitorKind()
    {
        var resolver = new MonitorRuntimeDefaultsResolver("SvantekMonitor");

        Assert.AreEqual("svantek", resolver.Defaults.Kind);
        Assert.AreEqual("https://svannet.com/api/v2.3/", resolver.Defaults.BaseUrl);
    }

    [DataTestMethod]
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
        var defaults = RvtConfig.ResolveMonitorDefaults(monitorKind, baseDirectory: "", entryAssemblyName: "");

        Assert.AreEqual(serviceName, defaults.ServiceName);
        Assert.AreEqual(baseUrl, defaults.BaseUrl);
        Assert.AreEqual(insertTopic, defaults.InsertTopic);
        Assert.AreEqual(alertTopic, defaults.AlertTopic);
    }

    [DataTestMethod]
    [DataRow("/tmp/rvt/airqmonitor/AirQMonitor/bin/Debug/net10.0", "AirQMonitor noise monitor data collector running ")]
    [DataRow("/tmp/rvt/myatmmonitor/MyAtmMonitor/bin/Debug/net10.0", "MyAtmMonitor dust monitor data collector running ")]
    [DataRow("/tmp/rvt/omnidotsmonitor/OmnidotsMonitor/bin/Debug/net10.0", "OmnidotsMonitor vibration monitor data collector running ")]
    [DataRow("/tmp/rvt/svantekmonitor/SvantekMonitor/bin/Debug/net10.0", "SvantekMonitor noise monitor data collector running ")]
    public void ResolveMonitorDefaultsCanInferMonitorKindFromBaseDirectory(string baseDirectory, string serviceName)
    {
        var defaults = RvtConfig.ResolveMonitorDefaults(monitorKind: "", baseDirectory, entryAssemblyName: "");

        Assert.AreEqual(serviceName, defaults.ServiceName);
    }

    [TestMethod]
    public void ResolveCredentialSettingsPrefersOmnidotsNamesWhenRunningAsOmnidots()
    {
        var values = new Dictionary<string, string?>
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        var credentials = RvtConfig.ResolveCredentialSettings("Omnidots", values.GetValueOrDefault);

        Assert.AreEqual("omnidots-user", credentials.UserId);
        Assert.AreEqual("omnidots-auth", credentials.UserAuth);
        Assert.AreEqual("omnidots-token", credentials.Token);
    }

    [TestMethod]
    public void ResolveCredentialSettingsPreservesAirQAndMyAtmNamesForTheirMonitorKinds()
    {
        var values = new Dictionary<string, string?>
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        var airqCredentials = RvtConfig.ResolveCredentialSettings("AirQ", values.GetValueOrDefault);
        var myAtmCredentials = RvtConfig.ResolveCredentialSettings("MyAtm", values.GetValueOrDefault);

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
        var values = new Dictionary<string, string?>
        {
            ["RVT__AIRQ_USER_ID"] = "airq-user",
            ["RVT__AIRQ_USER_AUTH"] = "airq-auth",
            ["RVT__MYATM_TOKEN"] = "myatm-token",
            ["RVT__OMNIDOTS_USER_ID"] = "omnidots-user",
            ["RVT__OMNIDOTS_USER_AUTH"] = "omnidots-auth",
            ["RVT__OMNIDOTS_TOKEN"] = "omnidots-token"
        };

        var credentials = RvtConfig.ResolveCredentialSettings("unknown", values.GetValueOrDefault);

        Assert.AreEqual(string.Empty, credentials.UserId);
        Assert.AreEqual(string.Empty, credentials.UserAuth);
        Assert.AreEqual(string.Empty, credentials.Token);
    }
}
