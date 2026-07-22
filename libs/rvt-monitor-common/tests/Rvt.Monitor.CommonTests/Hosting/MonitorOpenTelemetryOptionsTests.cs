using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.CommonTests.Hosting;

[TestClass]
public sealed class MonitorOpenTelemetryOptionsTests
{
    [TestMethod]
    public void Bind_WhenEnabled_UsesEndpointServiceNameAndLogLevelFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true",
                ["OpenTelemetry:OtlpEndpoint"] = "http://otel-collector:4317",
                ["OpenTelemetry:LogLevel"] = "Debug",
                ["OpenTelemetry:ServiceVersion"] = "v9.8.7",
                ["OTEL_SERVICE_NAME"] = "myatmmonitor"
            })
            .Build();

        var options = MonitorOpenTelemetryOptions.Bind(configuration, "FallbackMonitor");

        Assert.IsTrue(options.Enabled);
        Assert.AreEqual(new Uri("http://otel-collector:4317"), options.OtlpEndpoint);
        Assert.AreEqual("myatmmonitor", options.ServiceName);
        Assert.AreEqual("v9.8.7", options.ServiceVersion);
        Assert.AreEqual(LogLevel.Debug, options.LogLevel);
    }

    [TestMethod]
    public void Bind_WhenServiceNameIsNotConfigured_UsesMonitorName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true"
            })
            .Build();

        var options = MonitorOpenTelemetryOptions.Bind(configuration, "AirQMonitor");

        Assert.AreEqual("AirQMonitor", options.ServiceName);
    }
}
