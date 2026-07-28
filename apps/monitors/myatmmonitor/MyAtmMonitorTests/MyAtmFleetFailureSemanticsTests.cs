using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Http;
using MyAtm.Api.UseCases;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmFleetFailureSemanticsTests
{
    [TestInitialize]
    public void InitializeLogger()
    {
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug)),
            nameof(MyAtmFleetFailureSemanticsTests));
    }

    [TestMethod]
    public async Task StoreDustLevels_FailedEarlyMonitorAndRecorder_DoNotPreventLaterCommit()
    {
        DateTime now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        DustMonitorDto first = CreateMonitor("11111", now.AddMinutes(-2));
        DustMonitorDto second = CreateMonitor("22222", now.AddMinutes(-2));
        IOException primary = new IOException("vendor unavailable");
        InvalidOperationException recording = new InvalidOperationException("error store unavailable");
        Mock<IHttpClient> http = new Mock<IHttpClient>(MockBehavior.Strict);
        Mock<IMyAtmMonitorQueries> monitorQueries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmRuleQueries> ruleQueries = new Mock<IMyAtmRuleQueries>(MockBehavior.Strict);
        Mock<IMyAtmDustImportCommands> importCommands = new Mock<IMyAtmDustImportCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        http.Setup(client => client.GetAsync(
                It.Is<string>(path => path.Contains(first.SerialId, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(primary);
        http.Setup(client => client.GetAsync(
                It.Is<string>(path => path.Contains(second.SerialId, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new[]
            {
                MyAtmFixture.CreateDeviceMeasurement(now.AddMinutes(-1), 1, 2, 3)
            }));
        monitorQueries.Setup(queries => queries.ReadMonitorList(123, null)).Returns([first, second]);
        ruleQueries.Setup(queries => queries.ReadRules(second.SerialId, Period.Minutes1)).Returns([]);
        importCommands.Setup(commands => commands.CommitDustImportAsync(
                It.Is<MyAtmDustImportCommit>(commit => commit.Monitor.SerialId == second.SerialId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DustImportCommitResult(Array.Empty<MonitorDeliveryRequest>()));
        operational.Setup(commands => commands.HandleException(
                $"StoreDustLevels SerialId={first.SerialId}",
                It.IsAny<AdapterException>()))
            .Throws(recording);
        StoreDustLevelsHandler handler = new StoreDustLevelsHandler(
            new MyAtmHttpGateway(http.Object, devicePageSize: 100),
            new MyAtmMonitorReader(monitorQueries.Object, operational.Object, testLocal: false),
            ruleQueries.Object,
            importCommands.Object,
            operational.Object,
            new MyAtmRuleEvaluator(),
            new FixedTimeProvider(now),
            maxPagesPerMonitorPerRun: 10);

        MyAtmJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync<DeviceMeasurement>(123, Period.Minutes1));

        Assert.HasCount(
            1,
            aggregate.Failures,
            string.Join(Environment.NewLine, aggregate.Failures.Select(failure =>
                $"{failure.Identifier}: {failure.Exception}")));
        Assert.IsInstanceOfType<AdapterException>(aggregate.Failures[0].Exception);
        Assert.AreSame(primary, aggregate.Failures[0].Exception.InnerException);
        Assert.AreSame(recording, aggregate.Failures[0].RecordingException);
        importCommands.VerifyAll();
        http.VerifyAll();
    }

    [TestMethod]
    public async Task ProcessDustLevels_FailedEarlyRuleAndRecorder_DoNotPreventLaterRuleCommit()
    {
        DateTime now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        RvtAlertRuleDto first = CreateDeletedActiveRule("11111");
        RvtAlertRuleDto second = CreateDeletedActiveRule("22222");
        IOException primary = new IOException("commit unavailable");
        InvalidOperationException recording = new InvalidOperationException("error store unavailable");
        Mock<IMyAtmMonitorQueries> monitorQueries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmRuleQueries> ruleQueries = new Mock<IMyAtmRuleQueries>(MockBehavior.Strict);
        Mock<IMyAtmAlertCommitCommands> commits = new Mock<IMyAtmAlertCommitCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        ruleQueries.Setup(queries => queries.ReadRules(Period.Hours8)).Returns([first, second]);
        commits.Setup(commands => commands.CommitAlertAsync(
                It.Is<MyAtmAlertCommit>(commit => commit.RuleStateMutations.Single().RuleId == first.RuleId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(primary);
        commits.Setup(commands => commands.CommitAlertAsync(
                It.Is<MyAtmAlertCommit>(commit => commit.RuleStateMutations.Single().RuleId == second.RuleId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));
        operational.Setup(commands => commands.HandleException(
                $"ProcessDustLevels RuleId={first.RuleId} SerialId={first.SerialId}",
                primary))
            .Throws(recording);
        ProcessDustLevelsHandler handler = new ProcessDustLevelsHandler(
            monitorQueries.Object,
            ruleQueries.Object,
            commits.Object,
            operational.Object,
            new MyAtmRuleProcessor(ruleQueries.Object, "https://portal.example/"),
            new FixedTimeProvider(now),
            testLocal: false);

        MyAtmJobAggregateException aggregate = await Assert.ThrowsExactlyAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync<AvgDeviceMeasurement>(123, Period.Hours8));

        Assert.HasCount(1, aggregate.Failures);
        Assert.AreSame(primary, aggregate.Failures[0].Exception);
        Assert.AreSame(recording, aggregate.Failures[0].RecordingException);
        Assert.IsTrue(first.IsActive);
        Assert.IsFalse(second.IsActive);
        commits.VerifyAll();
        operational.VerifyAll();
        monitorQueries.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StoreDustLevels_RequestedCancellationStopsWithoutRecordingFailure()
    {
        DustMonitorDto monitor = CreateMonitor("11111", DateTime.UnixEpoch);
        Mock<IMyAtmMonitorQueries> monitorQueries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        monitorQueries.Setup(queries => queries.ReadMonitorList(123, null)).Returns([monitor]);
        StoreDustLevelsHandler handler = new StoreDustLevelsHandler(
            new MyAtmHttpGateway(Mock.Of<IHttpClient>(), devicePageSize: 100),
            new MyAtmMonitorReader(monitorQueries.Object, operational.Object, testLocal: false),
            Mock.Of<IMyAtmRuleQueries>(),
            Mock.Of<IMyAtmDustImportCommands>(),
            operational.Object,
            new MyAtmRuleEvaluator(),
            TimeProvider.System,
            maxPagesPerMonitorPerRun: 10);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.RunAsync<DeviceMeasurement>(123, Period.Minutes1, cancellation.Token));

        operational.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ProcessDustLevels_RequestedCancellationStopsWithoutRecordingFailure()
    {
        Mock<IMyAtmRuleQueries> ruleQueries = new Mock<IMyAtmRuleQueries>(MockBehavior.Strict);
        Mock<IMyAtmAlertCommitCommands> commits = new Mock<IMyAtmAlertCommitCommands>(MockBehavior.Strict);
        Mock<IMyAtmOperationalCommands> operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        ruleQueries.Setup(queries => queries.ReadRules(Period.Hours8)).Returns([CreateDeletedActiveRule("11111")]);
        ProcessDustLevelsHandler handler = new ProcessDustLevelsHandler(
            Mock.Of<IMyAtmMonitorQueries>(),
            ruleQueries.Object,
            commits.Object,
            operational.Object,
            new MyAtmRuleProcessor(ruleQueries.Object, "https://portal.example/"),
            TimeProvider.System,
            testLocal: false);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.RunAsync<AvgDeviceMeasurement>(123, Period.Hours8, cancellation.Token));

        commits.VerifyNoOtherCalls();
        operational.VerifyNoOtherCalls();
    }

    private static DustMonitorDto CreateMonitor(string serialId, DateTime lastDataTime) => new(
        Guid.NewGuid(),
        123,
        DateTime.UnixEpoch,
        serialId,
        "AQ Guard",
        1,
        0,
        0,
        "Site",
        "UTC",
        "Customer",
        lastDataTime,
        null,
        null,
        null,
        "Palas",
        "1.0",
        $"Fleet-{serialId}",
        false);

    private static RvtAlertRuleDto CreateDeletedActiveRule(string serialId) => new(
        Guid.NewGuid(),
        serialId,
        "Pm1",
        10,
        8,
        28_800,
        new Rvt.Monitor.Common.Rules.AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
        AlertType.Alert,
        isActive: true,
        isDeleted: true,
        DateTime.UnixEpoch,
        null);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedTimeProvider(DateTime now)
        {
            this.now = new DateTimeOffset(now);
        }

        public override DateTimeOffset GetUtcNow() => now;
    }
}
