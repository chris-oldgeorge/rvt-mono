using Moq;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.UseCases;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Delivery;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class CheckForOfflineMonitorsHandlerTests
{
    [TestMethod]
    public async Task RunAsync_InvalidTimezoneOnOneMonitor_ContinuesAndCommitsValidOfflineMonitor()
    {
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var invalid = CreateMonitor("11111", "Invalid/Timezone", now.AddHours(-25));
        var valid = CreateMonitor("22222", "UTC", now.AddHours(-25));
        var ruleQueries = new Mock<IMyAtmRuleQueries>(MockBehavior.Strict);
        var monitorQueries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var siteQueries = new Mock<IMyAtmSiteScheduleQueries>(MockBehavior.Strict);
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var commits = new Mock<IMyAtmAlertCommitCommands>(MockBehavior.Strict);
        ruleQueries.Setup(queries => queries.ReadRules(null)).Returns(MyAtmFixture.OfflineRules());
        ruleQueries.Setup(queries => queries.ReadAlertContacts(valid.Id)).Returns([]);
        monitorQueries.Setup(queries => queries.ReadMonitorList(123, It.IsAny<DateTime?>()))
            .Returns([invalid, valid]);
        siteQueries.Setup(queries => queries.ReadSiteSchedule(valid.Id)).Returns(AlwaysOpen());
        operational.Setup(commands => commands.HandleException(
            "CheckForOfflineMonitors serialId=11111",
            It.Is<InvalidOperationException>(exception =>
                exception.Message.Contains("timezone", StringComparison.OrdinalIgnoreCase))));
        commits.Setup(commands => commands.CommitAlertAsync(
                It.Is<MyAtmAlertCommit>(commit =>
                    commit.MonitorStateMutation!.MonitorId == valid.Id &&
                    commit.MonitorStateMutation.Offline == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MyAtmAlertCommitResult(true, Array.Empty<MonitorDeliveryRequest>()));
        var handler = CreateHandler(
            ruleQueries,
            monitorQueries,
            siteQueries,
            operational,
            commits,
            now);

        var exception = await Assert.ThrowsAsync<MyAtmJobAggregateException>(() =>
            handler.RunAsync(123));

        Assert.HasCount(1, exception.Failures);
        Assert.AreEqual("CheckForOfflineMonitors serialId=11111", exception.Failures[0].Identifier);
        Assert.IsFalse(invalid.Offline);
        Assert.IsTrue(valid.Offline);
        ruleQueries.VerifyAll();
        monitorQueries.VerifyAll();
        siteQueries.VerifyAll();
        operational.VerifyAll();
        commits.VerifyAll();
    }

    [TestMethod]
    public async Task RunAsync_ElapsedWallClockIsClosedSiteTime_DoesNotMarkOffline()
    {
        var now = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor("11111", "UTC", now.AddDays(-2));
        var ruleQueries = new Mock<IMyAtmRuleQueries>(MockBehavior.Strict);
        var monitorQueries = new Mock<IMyAtmMonitorQueries>(MockBehavior.Strict);
        var siteQueries = new Mock<IMyAtmSiteScheduleQueries>(MockBehavior.Strict);
        var operational = new Mock<IMyAtmOperationalCommands>(MockBehavior.Strict);
        var commits = new Mock<IMyAtmAlertCommitCommands>(MockBehavior.Strict);
        ruleQueries.Setup(queries => queries.ReadRules(null)).Returns(MyAtmFixture.OfflineRules());
        monitorQueries.Setup(queries => queries.ReadMonitorList(123, It.IsAny<DateTime?>()))
            .Returns([monitor]);
        siteQueries.Setup(queries => queries.ReadSiteSchedule(monitor.Id)).Returns(new MyAtmSiteSchedule
        {
            WeekdayStart = TimeSpan.FromHours(8),
            WeekdayEnd = TimeSpan.FromHours(18)
        });
        var handler = CreateHandler(
            ruleQueries,
            monitorQueries,
            siteQueries,
            operational,
            commits,
            now);

        await handler.RunAsync(123);

        Assert.IsFalse(monitor.Offline);
        commits.VerifyNoOtherCalls();
        operational.VerifyNoOtherCalls();
    }

    private static CheckForOfflineMonitorsHandler CreateHandler(
        Mock<IMyAtmRuleQueries> ruleQueries,
        Mock<IMyAtmMonitorQueries> monitorQueries,
        Mock<IMyAtmSiteScheduleQueries> siteQueries,
        Mock<IMyAtmOperationalCommands> operational,
        Mock<IMyAtmAlertCommitCommands> commits,
        DateTime now)
    {
        var reader = new MyAtmMonitorReader(monitorQueries.Object, operational.Object, testLocal: false);
        var processor = new MyAtmRuleProcessor(ruleQueries.Object, "https://portal.example/");
        return new CheckForOfflineMonitorsHandler(
            ruleQueries.Object,
            reader,
            siteQueries.Object,
            commits.Object,
            operational.Object,
            processor,
            new FixedTimeProvider(now));
    }

    private static DustMonitorDto CreateMonitor(
        string serialId,
        string timeZone,
        DateTime lastDataTime) => new(
            Guid.NewGuid(),
            123,
            DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc),
            serialId,
            "AQ Guard",
            1,
            0,
            0,
            "Site",
            timeZone,
            "Customer",
            lastDataTime,
            null,
            null,
            null,
            "Palas",
            "1.0",
            $"Fleet-{serialId}",
            false);

    private static MyAtmSiteSchedule AlwaysOpen() => new()
    {
        WeekdayStart = TimeSpan.Zero,
        WeekdayEnd = TimeSpan.FromHours(24),
        SaturdayStart = TimeSpan.Zero,
        SaturdayEnd = TimeSpan.FromHours(24),
        SundayStart = TimeSpan.Zero,
        SundayEnd = TimeSpan.FromHours(24)
    };

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
