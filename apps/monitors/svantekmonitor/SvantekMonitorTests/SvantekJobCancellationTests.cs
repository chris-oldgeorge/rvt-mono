using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Communications;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Svantek.Api;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Api.UseCases;
using Svantek.Model.Config;
using SvantekMonitor.model.dto;

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
    public async Task MonitorJobRunner_PassesTheExactTokenToEveryScheduledServiceMethod()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var service = CreateStrictJobsMock(token);

        foreach (var jobName in JobNames)
        {
            var result = await InvokeRunnerAsync(jobName, service.Object, token);
            Assert.AreEqual(0, result, jobName);
        }

        service.VerifyAll();
    }

    [TestMethod]
    public async Task MonitorJobDispatcher_PassesTheExactTokenThroughTheRunner()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var service = new Mock<ISvantekMonitorJobs>(MockBehavior.Strict);
        service.Setup(jobs => jobs.StoreMonitorsAsync(token)).Returns(Task.CompletedTask);
        var dispatcherType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.SvantekMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);
        var dispatcher = Activator.CreateInstance(
            dispatcherType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [service.Object],
            culture: null);
        Assert.IsNotNull(dispatcher);
        var runMethod = dispatcherType.GetMethod("RunAsync");
        Assert.IsNotNull(runMethod);

        var runTask = runMethod.Invoke(dispatcher, ["StoreMonitors", token]) as Task<int>;

        Assert.IsNotNull(runTask);
        Assert.AreEqual(0, await runTask);
        service.VerifyAll();
    }

    [TestMethod]
    public void AddSvantekMonitor_RegistersJobsAsSingletonAlias_AndDispatcherDependsOnInterface()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSvantekMonitor();

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<SvantekService>();
        var jobs = provider.GetRequiredService<ISvantekMonitorJobs>();
        var dispatcherType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.SvantekMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);
        var serviceConstructor = dispatcherType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ISvantekMonitorJobs)],
            modifiers: null);

        Assert.AreSame(concrete, jobs);
        Assert.IsNotNull(serviceConstructor);
    }

    [TestMethod]
    public void MonitorJobDispatcher_WithoutJobs_IdentifiesTheInterfaceDependency()
    {
        var dispatcherType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.SvantekMonitorJobDispatcher");
        Assert.IsNotNull(dispatcherType);
        var dispatcher = Activator.CreateInstance(dispatcherType, nonPublic: true);
        Assert.IsNotNull(dispatcher);
        var runMethod = dispatcherType.GetMethod("RunAsync");
        Assert.IsNotNull(runMethod);

        var invocation = Assert.ThrowsExactly<TargetInvocationException>(
            () => runMethod.Invoke(dispatcher, ["StoreMonitors", CancellationToken.None]));
        var exception = Assert.IsInstanceOfType<InvalidOperationException>(invocation.InnerException);

        StringAssert.Contains(exception.Message, nameof(ISvantekMonitorJobs));
    }

    [TestMethod]
    public async Task StoreNoiseLevelsAsync_UsesBoundedPastOnlyWindows_AndPassesTokenToGatewayAndDatabase()
    {
        var utcNow = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var monitor = new NoiseMonitorReadDto(
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
        var monitorQueries = new Mock<ISvantekMonitorQueries>(MockBehavior.Strict);
        monitorQueries.Setup(queries => queries.ReadMonitorListAsync(null, token))
            .ReturnsAsync([monitor]);
        var http = new Mock<IHttpClient>(MockBehavior.Strict);
        var requestedWindows = new List<(DateTime Start, DateTime End)>();
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                token))
            .Callback((string _, HttpContent content, CancellationToken _) =>
            {
                var multipart = (MultipartFormDataContent)content;
                var dataPart = multipart.Single(part =>
                    part.Headers.ContentDisposition?.Name?.Trim('"') == "data");
                var json = dataPart.ReadAsStringAsync(token).GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(json);
                var argument = document.RootElement[0];
                requestedWindows.Add((
                    DateTime.Parse(argument.GetProperty("time_from").GetString()!),
                    DateTime.Parse(argument.GetProperty("time_to").GetString()!)));
            })
            .ReturnsAsync("""
                {"status":"ok","data":[{"point":3,"data":{"status":"no_data","results":[]}}]}
                """);
        var operational = new Mock<ISvantekOperationalCommands>(MockBehavior.Strict);
        var handler = new StoreNoiseLevelsHandler(
            new SvantekHttpGateway(http.Object, "key"),
            new SvantekMonitorReader(monitorQueries.Object, testLocal: false),
            Mock.Of<ISvantekRuleQueries>(),
            Mock.Of<ISvantekMonitorCommands>(),
            Mock.Of<ISvantekMeasurementCommands>(),
            operational.Object,
            new SvantekRuleProcessor(
                Mock.Of<ISvantekRuleQueries>(),
                Mock.Of<ISvantekOperationalCommands>(),
                Mock.Of<IMessageService>(),
                Mock.Of<IMonitorEventPublisher>()),
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
        var service = new Mock<ISvantekMonitorJobs>(MockBehavior.Strict);
        service.Setup(jobs => jobs.StoreMonitorsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.StoreNoiseLevelsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.NotifySiteAveragesAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.CheckForOfflineMonitorsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.NotifyBatteryLevelsAsync(token)).Returns(Task.CompletedTask);
        service.Setup(jobs => jobs.CheckForSoundRecordingsAsync(token)).Returns(Task.CompletedTask);
        return service;
    }

    private static async Task<int> InvokeRunnerAsync(
        string jobName,
        ISvantekMonitorJobs service,
        CancellationToken cancellationToken)
    {
        var runnerType = typeof(SvantekApi).Assembly.GetType("Svantek.Api.MonitorJobRunner");
        Assert.IsNotNull(runnerType);
        var runMethod = runnerType.GetMethod(
            "RunAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(ISvantekMonitorJobs), typeof(CancellationToken)],
            modifiers: null);
        Assert.IsNotNull(runMethod);
        var task = runMethod.Invoke(null, [jobName, service, cancellationToken]) as Task<int>;
        Assert.IsNotNull(task);
        return await task;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
