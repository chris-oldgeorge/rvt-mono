using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Delivery;
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
        CollectionAssert.Contains(supportedJobs, "CleanupOutbox");
    }

    // The other three monitors purge their alert outbox daily at 0 15 3; MyAtm's
    // delivery outbox had no purge at all, so completed rows accumulated forever.
    [TestMethod]
    public void AppSettings_SchedulesTheOutboxPurgeAtTheSiblingCadence()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        JsonElement cleanupJob = document.RootElement
            .GetProperty("MonitorScheduler")
            .GetProperty("Jobs")
            .EnumerateArray()
            .Single(job => job.GetProperty("Name").GetString() == "CleanupOutbox");

        Assert.IsTrue(cleanupJob.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("0 15 3 * * ?", cleanupJob.GetProperty("Cron").GetString());
    }

    [TestMethod]
    public async Task CleanupService_PurgesCompletedRowsOlderThanTheRetentionWindow()
    {
        Mock<IMonitorDeliveryOutboxCommands> commands = new();
        commands.Setup(outbox => outbox.DeleteCompletedBeforeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        MonitorDeliveryOptions options = new()
        {
            Producer = MonitorDeliveryProducers.MyAtm,
            InsertTopic = "insert",
            AlertTopic = "alert",
            PortalBaseUrl = "https://portal.example/"
        };
        // The shared DurableAlertOptions default, which the other three run on.
        Assert.AreEqual(90, options.CompletedRetentionDays);
        DateTime utcNow = new(2026, 7, 30, 3, 15, 0, DateTimeKind.Utc);
        MonitorDeliveryCleanupService subject = new(commands.Object, options, TimeProvider.System);

        int deleted = await subject.CleanupAsync(utcNow, TestContext.CancellationToken);

        Assert.AreEqual(3, deleted);
        commands.Verify(outbox => outbox.DeleteCompletedBeforeAsync(
            MonitorDeliveryProducers.MyAtm,
            utcNow.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;

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
