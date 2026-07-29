using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;

namespace SvantekMonitorTests;

[TestClass]
public sealed class SvantekJobCancellationTests
{
    private static readonly string[] JobNames =
    [
        "StoreMonitors",
        "StoreNoiseLevels",
        "NotifySiteAverages",
        "CheckForOfflineMonitors",
        "NotifyBatteryLevels",
        "CheckForSoundRecordings"
    ];

    [TestInitialize]
    public void InitializeLogger() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(SvantekJobCancellationTests));

    [TestMethod]
    public async Task JobCatalog_PassesTheExactTokenToEveryScheduledServiceMethod()
    {
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        Mock<ISvantekMonitorJobs> service = CreateStrictJobsMock(token);

        foreach (string jobName in JobNames)
        {
            int result = await SvantekMonitorJobs.Catalog.RunAsync(jobName, service.Object, token);
            Assert.AreEqual(0, result, jobName);
        }

        service.VerifyAll();
    }

    [TestMethod]
    public async Task MonitorJobDispatcher_PassesTheExactTokenThroughTheRunner()
    {
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        Mock<ISvantekMonitorJobs> service = new(MockBehavior.Strict);
        service.Setup(jobs => jobs.StoreMonitorsAsync(token)).Returns(Task.CompletedTask);
        Type? dispatcherType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.SvantekMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);
        object? dispatcher = Activator.CreateInstance(
            dispatcherType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [service.Object],
            culture: null);
        Assert.IsNotNull(dispatcher);
        MethodInfo? runMethod = dispatcherType.GetMethod("RunAsync");
        Assert.IsNotNull(runMethod);

        Task<int>? runTask = runMethod.Invoke(dispatcher, ["StoreMonitors", token]) as Task<int>;

        Assert.IsNotNull(runTask);
        Assert.AreEqual(0, await runTask);
        service.VerifyAll();
    }

    [TestMethod]
    public void AddSvantekMonitor_RegistersJobsAsSingletonAlias_AndDispatcherDependsOnInterface()
    {
        ServiceCollection services = new();
        services.AddLogging();
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSvantekMonitor(configuration);
        TestUtil.UseTestMonitorContextFactory(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        SvantekService concrete = provider.GetRequiredService<SvantekService>();
        ISvantekMonitorJobs jobs = provider.GetRequiredService<ISvantekMonitorJobs>();
        Type? dispatcherType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.SvantekMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);
        ConstructorInfo? serviceConstructor = dispatcherType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ISvantekMonitorJobs)],
            modifiers: null);

        Assert.AreSame(concrete, jobs);
        Assert.IsNotNull(serviceConstructor);
    }

    [TestMethod]
    public void JobCatalog_DeclaresExactlyTheScheduledJobs()
    {
        CollectionAssert.AreEqual(
            JobNames.Order(StringComparer.Ordinal).ToArray(),
            SvantekMonitorJobs.Catalog.JobNames.Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public async Task StoreNoiseLevelsAsync_UsesBoundedPastOnlyWindows_AndPassesTokenToGatewayAndDatabase()
    {
        DateTime utcNow = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        NoiseMonitorReadDto monitor = new(
            Guid.NewGuid(),
            "fleet-1",
            "1001",
            7,
            3,
            utcNow,
            null,
            null,
            utcNow.AddDays(-30),
            false,
            SvantekApi.BatteryAlertType.Off,
            100);
        Mock<ISvantekMonitorQueries> monitorQueries = new(MockBehavior.Strict);
        monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, token))
            .ReturnsAsync([monitor]);
        Mock<IHttpClient> http = new(MockBehavior.Strict);
        List<(DateTime Start, DateTime End)> requestedWindows = [];
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                token))
            .Callback((string _, HttpContent content, CancellationToken _) =>
            {
                MultipartFormDataContent multipart = (MultipartFormDataContent)content;
                HttpContent dataPart = multipart.Single(part =>
                    part.Headers.ContentDisposition?.Name?.Trim('"') == "data");
                string json = dataPart.ReadAsStringAsync(token).GetAwaiter().GetResult();
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement argument = document.RootElement[0];
                requestedWindows.Add((
                    DateTime.Parse(argument.GetProperty("time_from").GetString()!),
                    DateTime.Parse(argument.GetProperty("time_to").GetString()!)));
            })
            .ReturnsAsync("""
                {"status":"ok","data":[{"point":3,"data":{"status":"no_data","results":[]}}]}
                """);
        Mock<ISvantekOperationalCommands> operational = new(MockBehavior.Strict);
        StoreNoiseLevelsHandler handler = new(
            new SvantekHttpGateway(http.Object, "key"),
            new SvantekMonitorReader(monitorQueries.Object, testLocal: false),
            Mock.Of<ISvantekRuleQueries>(),
            Mock.Of<ISvantekMonitorCommands>(),
            Mock.Of<ISvantekMeasurementCommands>(),
            operational.Object,
            new SvantekRuleProcessor(
                Mock.Of<ISvantekRuleQueries>(),
                Mock.Of<ISvantekOperationalCommands>(),
                Mock.Of<IAlertIngressPort>()),
            new NoiseRequestWindowCalculator(new SvantekImportOptions()),
            new FixedTimeProvider(utcNow));

        await handler.RunAsync(token);

        Assert.HasCount(14, requestedWindows);
        Assert.AreEqual(utcNow.AddDays(-7), requestedWindows[0].Start);
        Assert.AreEqual(utcNow, requestedWindows[^1].End);
        Assert.IsTrue(requestedWindows.All(window => window.End <= utcNow));
        Assert.IsTrue(requestedWindows.All(window => window.End - window.Start <= TimeSpan.FromHours(12)));
        monitorQueries.VerifyAll();
        http.VerifyAll();
    }

    private static Mock<ISvantekMonitorJobs> CreateStrictJobsMock(CancellationToken token)
    {
        Mock<ISvantekMonitorJobs> service = new(MockBehavior.Strict);
        service.Setup(jobs => jobs.StoreMonitorsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.StoreNoiseLevelsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.NotifySiteAveragesAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.CheckForOfflineMonitorsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.NotifyBatteryLevelsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.CheckForSoundRecordingsAsync(token)).Returns(Task.CompletedTask);
        return service;
    }


    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
