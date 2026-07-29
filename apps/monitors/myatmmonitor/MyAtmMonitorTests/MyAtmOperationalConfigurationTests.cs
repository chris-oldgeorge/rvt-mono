using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Mqtt;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmOperationalConfigurationTests
{
    [TestMethod]
    public void AppSettings_DefinesMyAtmOperationalConfiguration()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        bool hasOptions = document.RootElement.TryGetProperty("MyAtmMonitor", out JsonElement options);

        Assert.IsTrue(hasOptions, "MyAtmMonitor configuration section is required.");
        Assert.IsGreaterThan(0, options.GetProperty("CustomerId").GetInt32());
        Assert.IsGreaterThan(0, options.GetProperty("DevicePageSize").GetInt32());
        Assert.IsGreaterThan(0, options.GetProperty("MeasurementPageSize").GetInt32());
        Assert.IsGreaterThan(0, options.GetProperty("AccessoryPageSize").GetInt32());
        Assert.IsGreaterThan(0, options.GetProperty("MaxPagesPerMonitorPerRun").GetInt32());
        Assert.IsFalse(string.IsNullOrWhiteSpace(options.GetProperty("PortalBaseUrl").GetString()));
    }

    [TestMethod]
    public void JobCatalog_SupportsAccessoryImport()
    {
        List<string> supportedJobs = [.. MyAtmMonitorJobs.Catalog.JobNames];

        CollectionAssert.Contains(supportedJobs, "StoreAccessoryInfo");
        CollectionAssert.Contains(supportedJobs, "DispatchOutbox");
    }

    [TestMethod]
    public void AppSettings_DefinesApprovedDustAndDispatchSchedules()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        List<JsonElement> jobs = [.. document.RootElement
            .GetProperty("MonitorScheduler")
            .GetProperty("Jobs")
            .EnumerateArray()];
        JsonElement dustJob = jobs.Single(job => job.GetProperty("Name").GetString() == "StoreDustLevels");
        JsonElement dispatchJob = jobs.Single(job => job.GetProperty("Name").GetString() == "DispatchOutbox");

        Assert.IsTrue(dustJob.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("0 0/30 * * * ?", dustJob.GetProperty("Cron").GetString());
        Assert.IsTrue(dispatchJob.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("0 * * * * ?", dispatchJob.GetProperty("Cron").GetString());
    }

    [TestMethod]
    public async Task JobCatalog_DispatchOutboxPropagatesCancellationTokenToSharedDispatcher()
    {
        Mock<IDBClient> database = new();
        Mock<IMqttClient> mqttClient = new();
        using CancellationTokenSource cancellation = new();
        CancellationToken observedToken = default;
        database.Setup(query => query.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, DateTime _, TimeSpan _, CancellationToken token) => observedToken = token)
            .ReturnsAsync((MonitorDeliveryMessage?)null);
        ServiceCollection services = new();
        services.AddLogging();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyAtmVendor:BaseUrl"] = "https://vendor.example/",
                ["MyAtmVendor:ApiKey"] = "test-key"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMyAtmMonitor(configuration);
        services.AddSingleton(database.Object);
        services.AddSingleton(mqttClient.Object);
        using ServiceProvider provider = services.BuildServiceProvider();
        IMyAtmMonitorJobs service = provider.GetRequiredService<IMyAtmMonitorJobs>();

        int result = await MyAtmMonitorJobs.Catalog.RunAsync("DispatchOutbox", service, cancellation.Token);

        Assert.AreEqual(0, result);
        Assert.AreEqual(cancellation.Token, observedToken);
    }

}
