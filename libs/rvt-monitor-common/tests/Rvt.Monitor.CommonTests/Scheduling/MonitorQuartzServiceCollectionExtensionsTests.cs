using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rvt.Monitor.Common.Scheduling;

namespace Rvt.Monitor.CommonTests.Scheduling;

[TestClass]
public sealed class MonitorQuartzServiceCollectionExtensionsTests
{
    private static readonly IReadOnlySet<string> _supportedJobNames =
        new HashSet<string>(StringComparer.Ordinal) { "StoreMonitors" };

    [TestMethod]
    public void AddMonitorQuartzScheduler_DoesNotRegisterHostedSchedulerWhenDisabled()
    {
        ServiceCollection services = new();

        services.AddMonitorQuartzScheduler<TestDispatcher>(CreateConfiguration(enabled: false), "TestMonitor", _supportedJobNames);

        Assert.IsFalse(services.Any(service => service.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void AddMonitorQuartzScheduler_ThrowsForUnsupportedConfiguredJob()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(enabled: true, jobName: "MissingJob");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddMonitorQuartzScheduler<TestDispatcher>(configuration, "TestMonitor", _supportedJobNames));

        Assert.Contains("MissingJob", exception.Message);
    }

    [TestMethod]
    public void AddMonitorQuartzScheduler_RegistersHostedSchedulerWhenEnabled()
    {
        ServiceCollection services = new();

        services.AddMonitorQuartzScheduler<TestDispatcher>(CreateConfiguration(enabled: true), "TestMonitor", _supportedJobNames);

        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IMonitorJobDispatcher)));
        Assert.IsTrue(services.Any(service => service.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void AddMonitorQuartzScheduler_DoesNotRegisterHostedSchedulerWhenInfrastructureIsAzure()
    {
        ServiceCollection services = new();

        services.AddMonitorQuartzScheduler<TestDispatcher>(
            CreateConfiguration(enabled: true, infrastructure: "azure"),
            "TestMonitor",
            _supportedJobNames);

        Assert.IsFalse(services.Any(service => service.ServiceType == typeof(IMonitorJobDispatcher)));
        Assert.IsFalse(services.Any(service => service.ServiceType == typeof(IHostedService)));
    }

    private static IConfiguration CreateConfiguration(
        bool enabled,
        string jobName = "StoreMonitors",
        string infrastructure = "local")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Infrastructure"] = infrastructure,
                ["MonitorScheduler:Enabled"] = enabled.ToString(),
                ["MonitorScheduler:TimeZoneId"] = "UTC",
                ["MonitorScheduler:Jobs:0:Name"] = jobName,
                ["MonitorScheduler:Jobs:0:Cron"] = "0 2 * * * ?",
                ["MonitorScheduler:Jobs:0:Enabled"] = "true"
            })
            .Build();
    }

    private sealed class TestDispatcher : IMonitorJobDispatcher
    {
        public IReadOnlySet<string> SupportedJobNames { get; } = new HashSet<string> { "StoreMonitors" };

        public Task<int> RunAsync(string jobName, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
