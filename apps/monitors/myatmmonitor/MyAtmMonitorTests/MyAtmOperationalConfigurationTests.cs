using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmOperationalConfigurationTests
{
    [TestMethod]
    public void AppSettings_DefinesMyAtmOperationalConfiguration()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        var hasOptions = document.RootElement.TryGetProperty("MyAtmMonitor", out var options);

        Assert.IsTrue(hasOptions, "MyAtmMonitor configuration section is required.");
        Assert.IsTrue(options.GetProperty("CustomerId").GetInt32() > 0);
        Assert.IsTrue(options.GetProperty("DevicePageSize").GetInt32() > 0);
        Assert.IsTrue(options.GetProperty("MeasurementPageSize").GetInt32() > 0);
        Assert.IsTrue(options.GetProperty("AccessoryPageSize").GetInt32() > 0);
        Assert.IsTrue(options.GetProperty("MaxPagesPerMonitorPerRun").GetInt32() > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(options.GetProperty("PortalBaseUrl").GetString()));
    }

    [TestMethod]
    public void MonitorJobDispatcher_SupportsAccessoryImport()
    {
        var dispatcherType = typeof(MyAtmApi).Assembly.GetType("MyAtm.Api.MyAtmMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);

        var dispatcher = Activator.CreateInstance(dispatcherType!);
        Assert.IsNotNull(dispatcher);

        var supportedJobs = dispatcherType!.GetProperty("SupportedJobNames")!.GetValue(dispatcher) as IEnumerable<string>;
        Assert.IsNotNull(supportedJobs);
        CollectionAssert.Contains(supportedJobs!.ToList(), "StoreAccessoryInfo");
        CollectionAssert.Contains(supportedJobs.ToList(), "DispatchOutbox");
    }

    [TestMethod]
    public void AppSettings_DefinesApprovedDustAndDispatchSchedules()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        var jobs = document.RootElement
            .GetProperty("MonitorScheduler")
            .GetProperty("Jobs")
            .EnumerateArray()
            .ToList();
        var dustJob = jobs.Single(job => job.GetProperty("Name").GetString() == "StoreDustLevels");
        var dispatchJob = jobs.Single(job => job.GetProperty("Name").GetString() == "DispatchOutbox");

        Assert.IsTrue(dustJob.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("0 0/30 * * * ?", dustJob.GetProperty("Cron").GetString());
        Assert.IsTrue(dispatchJob.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("0 * * * * ?", dispatchJob.GetProperty("Cron").GetString());
    }

    [TestMethod]
    public async Task MonitorJobRunner_DispatchOutboxPropagatesCancellationTokenToSharedDispatcher()
    {
        var database = new Mock<IDBClient>();
        var mqttClient = new Mock<IMqttClient>();
        var messageService = new Mock<IMessageService>();
        using var cancellation = new CancellationTokenSource();
        CancellationToken observedToken = default;
        database.Setup(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, DateTime _, TimeSpan _, CancellationToken token) => observedToken = token)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
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
        services.AddSingleton(database.Object);
        services.AddSingleton(mqttClient.Object);
        services.AddSingleton(messageService.Object);
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMyAtmMonitorJobs>();

        var result = await InvokeMonitorJobRunnerAsync("DispatchOutbox", service, cancellation.Token);

        Assert.AreEqual(0, result);
        Assert.AreEqual(cancellation.Token, observedToken);
    }

    private static async Task<int> InvokeMonitorJobRunnerAsync(
        string jobName,
        IMyAtmMonitorJobs service,
        CancellationToken cancellationToken)
    {
        var runnerType = typeof(MyAtmApi).Assembly.GetType("MyAtm.Api.MonitorJobRunner");
        Assert.IsNotNull(runnerType);
        var runMethod = runnerType.GetMethod(
            "RunAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(IMyAtmMonitorJobs), typeof(CancellationToken) },
            modifiers: null);
        Assert.IsNotNull(runMethod);
        var task = runMethod.Invoke(null, new object[] { jobName, service, cancellationToken }) as Task<int>;
        Assert.IsNotNull(task);
        return await task;
    }
}
