using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmMonitorServiceRegistrationTests
{
    [TestMethod]
    public void AddMyAtmMonitor_RegistersSharedDeliveryCompositionAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyAtmVendor:BaseUrl"] = "https://vendor.example/",
                ["MyAtmVendor:ApiKey"] = "test-key"
            })
            .Build());
        services.AddMyAtmMonitor();
        var dbClient = new Mock<IDBClient>();
        services.AddSingleton(dbClient.Object);
        services.AddSingleton(new Mock<IMqttClient>().Object);
        services.AddSingleton(new Mock<IMessageService>().Object);

        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<MonitorDeliveryDispatcher>();
        var failureSink = provider.GetRequiredService<IMonitorDeliveryFailureSink>();

        Assert.AreSame(dbClient.Object, provider.GetRequiredService<IMyAtmAlertCommitCommands>());
        Assert.AreSame(dbClient.Object, provider.GetRequiredService<IMyAtmAccessoryCommands>());
        Assert.AreSame(dbClient.Object, provider.GetRequiredService<IMonitorDeliveryOutboxQueries>());
        Assert.AreSame(dbClient.Object, provider.GetRequiredService<IMonitorDeliveryOutboxCommands>());
        Assert.IsInstanceOfType<MyAtmDeliveryFailureSink>(failureSink);
        Assert.AreSame(failureSink, provider.GetRequiredService<IMonitorDeliveryFailureSink>());
        Assert.AreSame(dispatcher, provider.GetRequiredService<MonitorDeliveryDispatcher>());
        Assert.AreSame(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.AreEqual("https://vendor.example/", provider.GetRequiredService<MyAtmVendorOptions>().BaseUrl);
        Assert.IsInstanceOfType<MyAtmHttpGateway>(provider.GetRequiredService<MyAtmHttpGateway>());
        Assert.IsInstanceOfType<MyAtmMonitorReader>(provider.GetRequiredService<MyAtmMonitorReader>());
        Assert.IsInstanceOfType<MyAtmRuleEvaluator>(provider.GetRequiredService<MyAtmRuleEvaluator>());
        Assert.IsInstanceOfType<MyAtmRuleProcessor>(provider.GetRequiredService<MyAtmRuleProcessor>());
        Assert.IsInstanceOfType<StoreMonitorsHandler>(provider.GetRequiredService<StoreMonitorsHandler>());
        Assert.IsInstanceOfType<CheckForOfflineMonitorsHandler>(provider.GetRequiredService<CheckForOfflineMonitorsHandler>());
        Assert.IsInstanceOfType<ClearMonitorsOfflineFlagHandler>(provider.GetRequiredService<ClearMonitorsOfflineFlagHandler>());
        Assert.IsInstanceOfType<StoreDustLevelsHandler>(provider.GetRequiredService<StoreDustLevelsHandler>());
        Assert.IsInstanceOfType<ProcessDustLevelsHandler>(provider.GetRequiredService<ProcessDustLevelsHandler>());
        Assert.IsInstanceOfType<ClearOlderErrorMessagesHandler>(provider.GetRequiredService<ClearOlderErrorMessagesHandler>());
        Assert.IsInstanceOfType<StoreAccessoryInfoHandler>(provider.GetRequiredService<StoreAccessoryInfoHandler>());
        Assert.AreSame(provider.GetRequiredService<MyAtmApi>(), provider.GetRequiredService<MyAtmApi>());
        Assert.AreSame(provider.GetRequiredService<MyAtmService>(), provider.GetRequiredService<IMyAtmMonitorJobs>());
    }
}
