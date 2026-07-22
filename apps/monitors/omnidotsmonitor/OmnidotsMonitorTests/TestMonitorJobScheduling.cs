using System.Data;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Http;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Scheduling;
using Rvt.Monitor.Common.Utilities;

namespace OmnidotsAdapterTests;

[TestClass]
public sealed class TestMonitorJobScheduling
{
    public TestMonitorJobScheduling()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)),
            "TestMonitorJobScheduling");
    }

    [DataTestMethod]
    [DataRow("StoreVeffRecords", "/api/v1/get_veff_records")]
    [DataRow("StoreVdvRecords", "/api/v1/get_vdv_records")]
    public async Task RunAsync_ImportsRequestedVibrationSeriesWithinPastWindow(string jobName, string endpoint)
    {
        var api = TestUtil.CreateApiAndMocks(
            out var httpClient,
            out var dbClient,
            out var mqttClient,
            out var messageService);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
            .Returns(OmnidotsFixture.AuthenticateTask());
        dbClient.Setup(client => client.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));

        string? requestedUrl = null;
        httpClient.Setup(client => client.GetAsync(It.Is<string>(url => url.StartsWith(endpoint, StringComparison.Ordinal))))
            .Callback<string>(url => requestedUrl = url)
            .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));

        using var provider = LegacyJobProvider(api);
        var earliestStartTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(2)).Subtract(TimeSpan.FromMinutes(5)).ToUnixTimeMilliseconds();
        var earliestEndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var task = InvokeJobRunner(jobName, provider);

        Assert.AreEqual(0, await task);
        var latestStartTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(2)).Subtract(TimeSpan.FromMinutes(5)).ToUnixTimeMilliseconds();
        var latestEndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.IsNotNull(requestedUrl);
        var query = ParseQuery(requestedUrl);
        var startTime = long.Parse(query["start_time"]);
        var endTime = long.Parse(query["end_time"]);

        Assert.IsTrue(startTime >= earliestStartTime, $"start_time {startTime} was before {earliestStartTime}.");
        Assert.IsTrue(startTime <= latestStartTime, $"start_time {startTime} was after {latestStartTime}.");
        Assert.IsTrue(endTime >= earliestEndTime, $"end_time {endTime} was before {earliestEndTime}.");
        Assert.IsTrue(endTime <= latestEndTime, $"end_time {endTime} was after {latestEndTime}.");
        httpClient.Verify(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_MonitoringAwaitsOperationalWarningDelivery()
    {
        var utcNow = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var database = new Mock<IDBClient>();
        database.As<IOmnidotsImportCursorQueries>();
        database.As<IOmnidotsMeasurementImportCommands>();
        database.As<IOmnidotsTraceQueries>();
        database.Setup(client => client.ReadMonitorList(null))
            .Returns(OmnidotsFixture.MonitorsList(1, utcNow - TimeSpan.FromHours(2)));
        var delivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifier = new Mock<IOmnidotsMonitoringNotifier>(MockBehavior.Strict);
        notifier.Setup(value => value.SendNoDataWarningAsync(
                "operations@example.test",
                utcNow,
                It.IsAny<CancellationToken>()))
            .Returns(delivery.Task);
        var api = new OmnidotsApi(
            Mock.Of<IHttpClient>(),
            database.Object,
            Mock.Of<IMqttClient>(),
            Mock.Of<IMessageService>(),
            testLocal: false,
            new OmnidotsMonitoringOptions
            {
                Recipient = "operations@example.test",
                TimeZoneId = "UTC",
                WindowStart = TimeSpan.FromHours(8),
                WindowEnd = TimeSpan.FromHours(18),
                StaleAfter = TimeSpan.FromHours(1)
            },
            notifier.Object,
            new FixedTimeProvider(utcNow));
        using var provider = LegacyJobProvider(api);

        var run = InvokeJobRunner("Monitoring", provider);

        Assert.IsFalse(run.IsCompleted);
        delivery.SetResult();
        Assert.AreEqual(0, await run);
        notifier.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_StorePeakRecordsLastDataTime_UsesPeakCursorAndAtomicImport()
    {
        var api = TestUtil.CreateApiAndMocks(
            out var httpClient,
            out var dbClient,
            out var mqttClient,
            out var messageService,
            out var cursorQueries,
            out var importCommands);
        var monitor = OmnidotsFixture.MonitorsList(
            1,
            lastDataTime: new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc)).Single();
        var cursor = new DateTime(2026, 7, 13, 6, 30, 0, DateTimeKind.Utc);
        string? requestedUrl = null;

        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
            .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
        httpClient.Setup(client => client.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal))))
            .Callback<string>(url => requestedUrl = url)
            .Returns(OmnidotsFixture.StringTask(OmnidotsFixture.PeakRecordsJson()));
        dbClient.Setup(client => client.ReadMonitorList(null)).Returns([monitor]);
        cursorQueries.Setup(query => query.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak))
            .Returns(cursor);
        cursorQueries.Setup(query => query.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Peak))
            .Returns(cursor.AddDays(-1));
        var before = DateTime.UtcNow;

        var result = await RunJob(api, "StorePeakRecordsLastDataTime");
        var after = DateTime.UtcNow;

        Assert.AreEqual(0, result);
        Assert.IsNotNull(requestedUrl);
        var query = ParseQuery(requestedUrl);
        Assert.AreEqual(DateTimeUtil.GetMillis(cursor.AddMinutes(-5)), long.Parse(query["start_time"]));
        var endTime = DateTimeUtil.JAN1_1970.AddMilliseconds(long.Parse(query["end_time"]));
        Assert.IsTrue(endTime >= before.AddSeconds(-1) && endTime <= after);
        cursorQueries.Verify(query => query.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak), Times.Once);
        cursorQueries.Verify(query => query.ReadLatestMeasurementTime(
            It.IsAny<string>(), It.IsAny<OmnidotsMeasurementSeries>()), Times.Never);
        importCommands.Verify(command => command.ImportPeakRecords(
            "1", It.Is<DataTable>(table => table.Rows.Count == 2), It.IsAny<DateTime>()), Times.Once);
        dbClient.Verify(command => command.InsertPeakRecordsTable(It.IsAny<DataTable>()), Times.Never);
        dbClient.Verify(command => command.WriteLatestTimestamp(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_StorePeakRecordsLastDataTime_UsesLatestPeakMeasurementWhenCursorAbsent()
    {
        var api = TestUtil.CreateApiAndMocks(
            out var httpClient,
            out var dbClient,
            out var mqttClient,
            out var messageService,
            out var cursorQueries,
            out var importCommands);
        var monitor = OmnidotsFixture.MonitorsList(
            1,
            lastDataTime: new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc)).Single();
        var latestMeasurement = new DateTime(2026, 7, 12, 4, 20, 0, DateTimeKind.Utc);
        string? requestedUrl = null;

        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
            .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
        httpClient.Setup(client => client.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal))))
            .Callback<string>(url => requestedUrl = url)
            .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
        dbClient.Setup(client => client.ReadMonitorList(null)).Returns([monitor]);
        cursorQueries.Setup(query => query.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Peak))
            .Returns(latestMeasurement);

        Assert.AreEqual(0, await RunJob(api, "StorePeakRecordsLastDataTime"));

        Assert.IsNotNull(requestedUrl);
        Assert.AreEqual(
            DateTimeUtil.GetMillis(latestMeasurement.AddMinutes(-5)),
            long.Parse(ParseQuery(requestedUrl)["start_time"]));
        cursorQueries.Verify(query => query.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak), Times.Once);
        cursorQueries.Verify(query => query.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Peak), Times.Once);
        importCommands.Verify(command => command.ImportPeakRecords(
            It.IsAny<string>(), It.IsAny<DataTable>(), It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_StorePeakRecordsLastDataTime_BootstrapsFromMonitorTimestampWhenNoStoredPeakData()
    {
        var api = TestUtil.CreateApiAndMocks(
            out var httpClient,
            out var dbClient,
            out var mqttClient,
            out var messageService,
            out var cursorQueries,
            out var importCommands);
        var bootstrap = new DateTime(2026, 7, 2, 9, 45, 0, DateTimeKind.Utc);
        var monitor = OmnidotsFixture.MonitorsList(1, lastDataTime: bootstrap).Single();
        string? requestedUrl = null;

        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
            .Returns(OmnidotsFixture.AuthenticateTask("peak-token"));
        httpClient.Setup(client => client.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_peak_records", StringComparison.Ordinal))))
            .Callback<string>(url => requestedUrl = url)
            .Returns(OmnidotsFixture.StringTask("{\"ok\":true,\"samples\":[]}"));
        dbClient.Setup(client => client.ReadMonitorList(null)).Returns([monitor]);

        Assert.AreEqual(0, await RunJob(api, "StorePeakRecordsLastDataTime"));

        Assert.IsNotNull(requestedUrl);
        Assert.AreEqual(
            DateTimeUtil.GetMillis(bootstrap.AddMinutes(-5)),
            long.Parse(ParseQuery(requestedUrl)["start_time"]));
        cursorQueries.Verify(query => query.ReadImportCursor("1", OmnidotsMeasurementSeries.Peak), Times.Once);
        cursorQueries.Verify(query => query.ReadLatestMeasurementTime("1", OmnidotsMeasurementSeries.Peak), Times.Once);
        importCommands.Verify(command => command.ImportPeakRecords(
            It.IsAny<string>(), It.IsAny<DataTable>(), It.IsAny<DateTime>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_WhenOnlyMonitorImportFails_ReturnedTaskFaults()
    {
        var api = TestUtil.CreateApiAndMocks(
            out var httpClient,
            out var dbClient,
            out var mqttClient,
            out var messageService);
        httpClient.Setup(client => client.PostAsync("/api/v1/user/authenticate", It.IsAny<HttpContent>()))
            .Returns(OmnidotsFixture.AuthenticateTask());
        dbClient.Setup(client => client.ReadMonitorList(null)).Returns(OmnidotsFixture.MonitorsList(1));
        httpClient.Setup(client => client.GetAsync(It.Is<string>(url =>
                url.StartsWith("/api/v1/get_veff_records", StringComparison.Ordinal))))
            .Returns(OmnidotsFixture.StringTask("invalid-json"));

        using var provider = LegacyJobProvider(api);
        var task = InvokeJobRunner("StoreVeffRecords", provider);

        var exception = await Assert.ThrowsExactlyAsync<OmnidotsImportException>(async () => await task);
        Assert.AreEqual("StoreVeffRecords", exception.Operation);
        Assert.IsTrue(task.IsFaulted);
        dbClient.Verify(client => client.HandleException("StoreVeffRecords serialId=1", It.IsAny<Exception>()), Times.Once);
    }

    [TestMethod]
    public void AppSettings_ContainsStaggeredVeffAndVdvSchedules()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        Assert.IsTrue(File.Exists(appSettingsPath), $"Expected appsettings at '{appSettingsPath}'.");
        using var appSettings = File.OpenRead(appSettingsPath);
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(appSettings)
            .Build();
        var jobs = MonitorSchedulerOptions.Bind(configuration).GetEnabledJobs();

        Assert.IsTrue(jobs.Any(job => job.Name == "StoreVeffRecords" && job.Cron == "0 0 0/2 * * ?"));
        Assert.IsTrue(jobs.Any(job => job.Name == "StoreVdvRecords" && job.Cron == "0 15 0/2 * * ?"));
        Assert.IsTrue(jobs.Any(job => job.Name == "DispatchAlerts" && job.Cron == "0 0/1 * * * ?"));
        Assert.IsTrue(jobs.Any(job => job.Name == "CleanupAlerts" && job.Cron == "0 15 3 * * ?"));
        Assert.IsTrue(configuration.GetValue<bool>($"{OmnidotsTraceCollectionOptions.SectionName}:Enabled"));
        CollectionAssert.AreEqual(
            new[] { "23423" },
            configuration.GetSection($"{OmnidotsTraceCollectionOptions.SectionName}:AllowedSerialIds").Get<string[]>());
        Assert.AreEqual(
            1,
            configuration.GetValue<int>($"{OmnidotsTraceCollectionOptions.SectionName}:MaxMonitorsPerRun"));
    }

    [TestMethod]
    public void QuartzDispatcher_SupportsDurableAlertJobs()
    {
        var dispatcher = new OmnidotsMonitorJobDispatcher();

        Assert.IsTrue(dispatcher.SupportedJobNames.Contains("DispatchAlerts"));
        Assert.IsTrue(dispatcher.SupportedJobNames.Contains("CleanupAlerts"));
    }

    [TestMethod]
    public async Task RunAsync_DispatchAlerts_ResolvesOnlyCommonDispatcherAndReturnsZero()
    {
        var store = new Mock<IAlertOutboxStore>(MockBehavior.Strict);
        store.Setup(outbox => outbox.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimedAlertDelivery?)null);
        var services = AlertJobServices(store.Object);
        services.AddSingleton<OmnidotsService>(_ =>
            throw new AssertFailedException("DispatchAlerts must not resolve the legacy OmnidotsService."));
        using var provider = services.BuildServiceProvider();

        var result = await InvokeJobRunner("DispatchAlerts", provider);

        Assert.AreEqual(0, result);
        store.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_CleanupAlerts_DeletesRowsOlderThanNinetyDays()
    {
        var now = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var store = new Mock<IAlertOutboxStore>(MockBehavior.Strict);
        store.Setup(outbox => outbox.DeleteCompletedBeforeAsync(
                now.AddDays(-90),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var services = AlertJobServices(store.Object, new FixedTimeProvider(now));
        using var provider = services.BuildServiceProvider();

        var result = await InvokeJobRunner("CleanupAlerts", provider);

        Assert.AreEqual(0, result);
        store.VerifyAll();
    }

    [TestMethod]
    public async Task MonitorHost_OneShotDispatchWithAmbientApiEnabledAndNoEndpointSecrets_ReportsJobFailure()
    {
        var now = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var delivery = new ClaimedAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "delivery-key",
            "Exploding",
            "redacted-destination",
            "{}",
            "Leased",
            1,
            now,
            Guid.NewGuid(),
            now.AddMinutes(2),
            null,
            null,
            now.AddMinutes(-1));
        var claims = new Queue<ClaimedAlertDelivery?>([delivery, null]);
        var store = new Mock<IAlertOutboxStore>(MockBehavior.Strict);
        store.Setup(outbox => outbox.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => claims.Dequeue());
        store.Setup(outbox => outbox.RetryAsync(
                delivery.Id,
                delivery.LeaseId,
                It.IsAny<DateTime>(),
                "Alert delivery failed (InvalidOperationException).",
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(outbox => outbox.DeleteCompletedBeforeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var adapter = new Mock<IAlertDeliveryAdapter>(MockBehavior.Strict);
        adapter.SetupGet(value => value.Kind).Returns("Exploding");
        adapter.Setup(value => value.DeliverAsync(delivery, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("raw failure must remain internal"));

        var exitCode = await MonitorHost.RunAsync<OmnidotsMonitorJobDispatcher>(
            OneShotArgs("DispatchAlerts"),
            "OmnidotsMonitor",
            _ => "DispatchAlerts",
            InvokeJobRunner,
            _ => Assert.Fail("API mapping must not run for one-shot alert dispatch."),
            configureServices: services =>
            {
                services.AddOmnidotsMonitor();
                TestUtil.UseTestMonitorContextFactory(services);
                services.RemoveAll<IAlertOutboxStore>();
                services.RemoveAll<IAlertDeliveryAdapter>();
                services.AddSingleton(store.Object);
                services.AddSingleton(adapter.Object);
                services.PostConfigure<DurableAlertOptions>(options =>
                {
                    options.BatchSize = 1;
                    options.MaxAttempts = 1;
                });
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            });

        Assert.AreEqual(1, exitCode);
        store.Verify(outbox => outbox.ClaimNextDueAsync(
            now,
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(outbox => outbox.RetryAsync(
            delivery.Id,
            delivery.LeaseId,
            It.IsAny<DateTime>(),
            "Alert delivery failed (InvalidOperationException).",
            true,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
        adapter.VerifyGet(value => value.Kind, Times.Once);
        adapter.Verify(value => value.DeliverAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
        adapter.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task MonitorHost_OneShotCleanupWithAmbientApiEnabledAndNoEndpointSecrets_ReturnsJobResult()
    {
        var now = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var store = new Mock<IAlertOutboxStore>(MockBehavior.Strict);
        store.Setup(outbox => outbox.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimedAlertDelivery?)null);
        store.Setup(outbox => outbox.DeleteCompletedBeforeAsync(
                now.AddDays(-90),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var exitCode = await MonitorHost.RunAsync<OmnidotsMonitorJobDispatcher>(
            OneShotArgs("CleanupAlerts"),
            "OmnidotsMonitor",
            _ => "CleanupAlerts",
            InvokeJobRunner,
            _ => Assert.Fail("API mapping must not run for one-shot alert cleanup."),
            configureServices: services =>
            {
                services.AddOmnidotsMonitor();
                TestUtil.UseTestMonitorContextFactory(services);
                services.RemoveAll<IAlertOutboxStore>();
                services.AddSingleton(store.Object);
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            });

        Assert.AreEqual(0, exitCode);
        store.Verify(outbox => outbox.DeleteCompletedBeforeAsync(
            now.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
    }

    private static string[] OneShotArgs(string jobName) =>
    [
        "--job", jobName,
        "--MonitorApi:Enabled=true",
        "--MonitorScheduler:Enabled=false",
        "--Omnidots:Monitoring:Recipient=operations@example.test",
        "--Omnidots:Monitoring:TimeZoneId=Europe/London",
        "--Omnidots:Monitoring:WindowStart=08:30:00",
        "--Omnidots:Monitoring:WindowEnd=18:00:00",
        "--Omnidots:Monitoring:StaleAfter=01:00:00",
        "--RVT:EMAIL_ENABLED=false",
        "--RVT:SMS_ENABLED=false",
        "--hostBuilder:reloadConfigOnChange=false"
    ];

    private static IReadOnlyDictionary<string, string> ParseQuery(string url)
    {
        var queryStart = url.IndexOf('?');
        Assert.IsTrue(queryStart >= 0, $"URL '{url}' did not contain a query string.");

        return url[(queryStart + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => Uri.UnescapeDataString(part.Length == 2 ? part[1] : string.Empty),
                StringComparer.Ordinal);
    }

    private static async Task<int> RunJob(OmnidotsApi api, string jobName)
    {
        using var provider = LegacyJobProvider(api);
        return await InvokeJobRunner(jobName, provider);
    }

    private static ServiceProvider LegacyJobProvider(OmnidotsApi api)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new OmnidotsService(api));
        return services.BuildServiceProvider();
    }

    private static ServiceCollection AlertJobServices(
        IAlertOutboxStore store,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(store);
        services.AddSingleton<IOptions<DurableAlertOptions>>(Options.Create(new DurableAlertOptions()));
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<DurableAlertDispatcher>();
        services.AddSingleton<DurableAlertCleanupService>();
        return services;
    }

    private static Task<int> InvokeJobRunner(string jobName, IServiceProvider provider)
    {
        var runner = typeof(OmnidotsApi).Assembly.GetType("Omnidots.Api.MonitorJobRunner", throwOnError: true)!;
        var method = runner.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var parameters = method.GetParameters();
        Assert.IsTrue(parameters.Length is 2 or 3);
        Assert.AreEqual(typeof(IServiceProvider), parameters[1].ParameterType);
        return (Task<int>)method.Invoke(
            null,
            parameters.Length == 2
                ? [jobName, provider]
                : [jobName, provider, CancellationToken.None])!;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
