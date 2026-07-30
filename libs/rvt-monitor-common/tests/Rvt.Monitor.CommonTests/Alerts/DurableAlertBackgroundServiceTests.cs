using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Hosting;

namespace Rvt.Monitor.CommonTests.Alerts;

[TestClass]
public sealed class DurableAlertBackgroundServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 15, 16, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task RunIterationAsync_WhenApiIsEnabledAndQuartzIsDisabled_DispatchesOnePassAndCleansUp()
    {
        Mock<IAlertOutboxStore> store = EmptyStore();
        DurableAlertBackgroundService worker = CreateWorker(store.Object, MonitorExecutionMode.Api, schedulerEnabled: false);

        await worker.RunIterationAsync(UtcNow, CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            UtcNow,
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(MonitorExecutionMode.OneShot, true, false)]
    [DataRow(MonitorExecutionMode.QuartzScheduler, true, false)]
    [DataRow(MonitorExecutionMode.Unspecified, true, false)]
    [DataRow(MonitorExecutionMode.Api, true, true)]
    public async Task RunIterationAsync_InOneShotOrSchedulerModes_DoesNoWork(
        MonitorExecutionMode executionMode,
        bool apiEnabled,
        bool schedulerEnabled)
    {
        Mock<IAlertOutboxStore> store = EmptyStore();
        DurableAlertBackgroundService worker = CreateWorker(store.Object, executionMode, schedulerEnabled, apiEnabled: apiEnabled);

        await worker.RunIterationAsync(UtcNow, CancellationToken.None);

        store.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RunIterationAsync_PerformsCleanupAtMostOncePerUtcDay()
    {
        Mock<IAlertOutboxStore> store = EmptyStore();
        DurableAlertBackgroundService worker = CreateWorker(store.Object, MonitorExecutionMode.Api, schedulerEnabled: false);

        await worker.RunIterationAsync(UtcNow, CancellationToken.None);
        await worker.RunIterationAsync(UtcNow.AddHours(6), CancellationToken.None);
        await worker.RunIterationAsync(UtcNow.AddDays(1), CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            It.IsAny<DateTime>(),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddDays(1).AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RunIterationAsync_WhenDispatchFails_LogsSafeFailureAndRunsCleanupAndLaterIterations()
    {
        const string rawFailure = "provider leaked ops@example.test and secret-token";
        Mock<IAlertOutboxStore> store = new();
        store.SetupSequence(x => x.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(rawFailure))
            .ReturnsAsync((ClaimedAlertDelivery?)null);
        store.Setup(x => x.DeleteCompletedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        TestLogger<DurableAlertBackgroundService> logger = new();
        DurableAlertBackgroundService worker = CreateWorker(
            store.Object,
            MonitorExecutionMode.Api,
            schedulerEnabled: false,
            logger: logger);

        await worker.RunIterationAsync(UtcNow, CancellationToken.None);
        await worker.RunIterationAsync(UtcNow.AddMinutes(1), CancellationToken.None);

        store.Verify(x => x.ClaimNextDueAsync(
            It.IsAny<DateTime>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsTrue(logger.Messages.Any(entry =>
            entry.Contains("InvalidOperationException", StringComparison.Ordinal) &&
            !entry.Contains(rawFailure, StringComparison.Ordinal) &&
            !entry.Contains("ops@example.test", StringComparison.Ordinal) &&
            !entry.Contains("secret-token", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunIterationAsync_WhenCleanupFails_RetriesOnTheNextIterationAndLogsSafely()
    {
        // The daily stamp used to be written before the attempt, so one
        // transient failure skipped cleanup for a whole day.
        const string rawFailure = "database password=top-secret";
        Mock<IAlertOutboxStore> store = EmptyStore();
        store.SetupSequence(x => x.DeleteCompletedBeforeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(rawFailure))
            .ReturnsAsync(0);
        TestLogger<DurableAlertBackgroundService> logger = new();
        DurableAlertBackgroundService worker = CreateWorker(
            store.Object,
            MonitorExecutionMode.Api,
            schedulerEnabled: false,
            logger: logger);

        await worker.RunIterationAsync(UtcNow, CancellationToken.None);
        await worker.RunIterationAsync(UtcNow.AddMinutes(1), CancellationToken.None);
        await worker.RunIterationAsync(UtcNow.AddMinutes(2), CancellationToken.None);

        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddMinutes(1).AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Once);
        // The retry succeeded, so the day is now stamped and the third
        // iteration must not clean up again.
        store.Verify(x => x.DeleteCompletedBeforeAsync(
            UtcNow.AddMinutes(2).AddDays(-90),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(logger.Messages.Any(entry =>
            entry.Contains("InvalidOperationException", StringComparison.Ordinal) &&
            !entry.Contains(rawFailure, StringComparison.Ordinal) &&
            !entry.Contains("top-secret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task StartAsync_InOneShotMode_ExitsImmediatelyWithoutPolling()
    {
        Mock<IAlertOutboxStore> store = EmptyStore();
        DurableAlertBackgroundService worker = CreateWorker(
            store.Object,
            MonitorExecutionMode.OneShot,
            schedulerEnabled: false,
            apiEnabled: true);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        store.VerifyNoOtherCalls();
    }

    private static DurableAlertBackgroundService CreateWorker(
        IAlertOutboxStore store,
        MonitorExecutionMode executionMode,
        bool schedulerEnabled,
        bool apiEnabled = true,
        ILogger<DurableAlertBackgroundService>? logger = null)
    {
        DurableAlertOptions options = new();
        Mock<TimeProvider> timeProvider = new();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(UtcNow));
        Mock<IAlertDeliveryAdapter> adapter = new();
        adapter.SetupGet(x => x.Kind).Returns("MqttAlert");
        DurableAlertDispatcher dispatcher = new(
            store,
            [adapter.Object],
            Options.Create(options),
            timeProvider.Object,
            new TestLogger<DurableAlertDispatcher>());
        DurableAlertCleanupService cleanup = new(store, Options.Create(options), timeProvider.Object);
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MonitorApi:Enabled"] = apiEnabled.ToString(),
                ["MonitorScheduler:Enabled"] = schedulerEnabled.ToString(),
                ["Infrastructure"] = "local"
            })
            .Build();
        return new DurableAlertBackgroundService(
            dispatcher,
            cleanup,
            new MonitorExecutionModeContext(executionMode),
            configuration,
            Options.Create(options),
            timeProvider.Object,
            logger ?? new TestLogger<DurableAlertBackgroundService>());
    }

    private static Mock<IAlertOutboxStore> EmptyStore()
    {
        Mock<IAlertOutboxStore> store = new();
        store.Setup(x => x.ClaimNextDueAsync(
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimedAlertDelivery?)null);
        store.Setup(x => x.DeleteCompletedBeforeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return store;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
