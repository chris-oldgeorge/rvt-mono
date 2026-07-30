using System.Globalization;
using AirQ.Api;
using AirQ.Api.Db;
using AirQ.Api.UseCases;
using AirQ.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace AirQMonitorTests;

/// <summary>
/// An averaging window holding no samples used to score 0.0 dB, which reads as
/// silence: it unlatched breached rules so the next populated window re-fired an
/// alert contacts had already been told about, and it wrote fabricated 0.0 dB
/// daily averages into the reports table. MyAtm's <c>GetAverageDustLevel</c> is
/// the correct model - null means "no data, no transition".
/// </summary>
[TestClass]
public sealed class AirQEmptyAveragingWindowTests
{
    // The first monitor AirQFixture produces.
    private const string _serialId = "Device1";

    private static readonly DateTime _start = new(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddHours(3);

    public AirQEmptyAveragingWindowTests() =>
        RvtLogger.CreateLogger(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            nameof(AirQEmptyAveragingWindowTests));

    [TestMethod]
    public async Task ProcessRules_EmptyHourWindow_LeavesALatchedRuleLatchedAndSignalsNothing()
    {
        Mock<IAirQRuleQueries> ruleQueries = new();
        ruleQueries
            .Setup(queries => queries.GetAverageNoiseLevel(
                _serialId,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .Returns((double?)null);
        Mock<IAirQOperationalCommands> operationalCommands = new();
        Mock<IAlertIngressPort> alertIngress = new();
        AirQRuleProcessor processor = new(
            ruleQueries.Object,
            operationalCommands.Object,
            alertIngress.Object);
        RvtAlertRuleDto latched = Rule(averagingPeriod: 3600, isActive: true);

        await processor.ProcessRulesV2Async(
            AirQFixture.MonitorDtos(_start, NoiseMonitorStatus.ACTIVE)[0],
            [latched],
            _start,
            _end,
            dtos: null,
            TestContext.CancellationToken);

        // Three empty hours must not read as three hours of silence.
        Assert.IsTrue(latched.IsActive);
        operationalCommands.Verify(
            commands => commands.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()),
            Times.Never);
        alertIngress.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task NotifySiteAverages_EmptySiteDay_WritesNoDailyAverageAndLeavesTheRuleLatched()
    {
        DateTime date = new(2026, 7, 29);
        SiteMonitorsWithSiteHoursDto site = new(
            Guid.NewGuid(),
            "123",
            _serialId,
            0,
            false,
            Guid.NewGuid(),
            "Site",
            TimeSpan.Parse("09:00:00", CultureInfo.InvariantCulture),
            TimeSpan.Parse("17:00:00", CultureInfo.InvariantCulture));
        Mock<IAirQMonitorQueries> monitorQueries = new();
        monitorQueries
            .Setup(queries => queries.ReadSiteMonitorsWithSiteHours(date))
            .Returns([site]);
        Mock<IAirQRuleQueries> ruleQueries = new();
        ruleQueries
            .Setup(queries => queries.GetAverageNoiseLevel(
                _serialId,
                "LAeq",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .Returns((double?)null);
        Mock<IAirQMeasurementCommands> measurementCommands = new();
        Mock<IAirQOperationalCommands> operationalCommands = new();
        Mock<IAlertIngressPort> alertIngress = new();
        NotifySiteAveragesHandler handler = new(
            monitorQueries.Object,
            ruleQueries.Object,
            measurementCommands.Object,
            operationalCommands.Object,
            new AirQRuleProcessor(ruleQueries.Object, operationalCommands.Object, alertIngress.Object));

        await handler.RunAsync(date, TestContext.CancellationToken);

        // No samples means no average: nothing may reach the reports table, and
        // the site-hours rules must not even be read.
        measurementCommands.Verify(
            commands => commands.WriteDailyAverage(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<DateTime>()),
            Times.Never);
        ruleQueries.Verify(queries => queries.ReadRules(It.IsAny<string>()), Times.Never);
        operationalCommands.Verify(
            commands => commands.UpdateAlertRule(It.IsAny<RvtAlertRuleDto>()),
            Times.Never);
        alertIngress.VerifyNoOtherCalls();
    }

    private static RvtAlertRuleDto Rule(int averagingPeriod, bool isActive) =>
        new(
            Guid.NewGuid(),
            _serialId,
            "LAeq",
            limitOn: 70.0,
            limitOff: 60.0,
            averagingPeriod,
            AirQFixture.CreateActiveRuleActivity(null, null),
            AlertType.Alert,
            isActive,
            isDeleted: false,
            DateTime.UtcNow,
            null);

    public TestContext TestContext { get; set; } = null!;
}
