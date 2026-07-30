using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Svantek.Api;
using Svantek.Api.Db;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
namespace SvantekMonitorTests;

// Summary: Pins the durable alert contract of SvantekRuleProcessor: one AcceptAsync per
// breach, rule latching via UpdateAlertRule, and no in-process contact fan-out.
// Major updates:
// - 2026-07-29 Legacy retirement step 4: rewritten from the synchronous IMessageService
//   send-and-audit suite; contact fan-out, notification writes, audits, and send-time
//   window filtering moved into the durable alert stack.
[TestClass]
public class TestRules
{
    private const string _serialId = "MyDevice";
    private const int _durationSeconds = 15 * 60;

    // Tuesday, quarter aligned; the 16 minute range holds exactly one full averaging period.
    private static readonly DateTime _periodStart = new(2023, 10, 3, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _rangeEnd = _periodStart.AddMinutes(16);
    private static readonly DateTime _alertTime = _periodStart.AddSeconds(_durationSeconds);

    public TestRules()
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        RvtLogger.CreateLogger(factory, "TestRules");
    }

    private static SvantekRuleProcessor CreateProcessor(
        out Mock<ISvantekRuleQueries> ruleQueries,
        out Mock<ISvantekOperationalCommands> operationalCommands,
        out Mock<IAlertIngressPort> alertIngress)
    {
        ruleQueries = new Mock<ISvantekRuleQueries>();
        operationalCommands = new Mock<ISvantekOperationalCommands>();
        alertIngress = new Mock<IAlertIngressPort>();
        alertIngress
            .Setup(ingress => ingress.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertIngressResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                AlertOccurrenceOutcome.Accepted,
                IsDuplicate: false));
        return new SvantekRuleProcessor(ruleQueries.Object, operationalCommands.Object, alertIngress.Object);
    }

    private static RvtAlertRuleDto CreateRule(
        double limitOn,
        double limitOff,
        AlertActivityTimeDto activity,
        bool isActive = false) =>
        new(Guid.NewGuid(), _serialId, "LAeq", limitOn, limitOff, _durationSeconds,
            activity, AlertType.Alert, isActive, false, DateTime.UtcNow, null);

    [TestMethod]
    [DynamicData(nameof(DateExclusion))]
    [DynamicData(nameof(TimeExclusion))]
    [DynamicData(nameof(LevelExclusion))]
    public async Task TestProcessRules_WithAlertRuleExclusion_EmitsNoSignal(List<RvtAlertRuleDto> rules)
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns(20.0);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), rules, _periodStart, _rangeEnd, TestContext.CancellationToken);

        ruleQueries.Verify(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime), Times.Exactly(1));
        ruleQueries.VerifyNoOtherCalls();
        operationalCommands.VerifyNoOtherCalls();
        alertIngress.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestProcessRules_Breach_EmitsOneDurableSignalAndLatchesRule()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        RvtAlertRuleDto rule = CreateRule(10.0, 1.0, SvantekFixture.CreateActiveRuleActivity(null, null));
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns(11.0);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [rule], _periodStart, _rangeEnd, TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.Source == "svantek.rules" &&
                signal.SerialId == _serialId &&
                signal.EventTime == _alertTime &&
                signal.AlertType == AlertType.Alert &&
                signal.Field == "LAeq" &&
                signal.Level == 11.0 &&
                signal.Limit == 10.0 &&
                signal.AveragingPeriod == _durationSeconds &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                signal.SuppressionWindow == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        alertIngress.VerifyNoOtherCalls();

        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", true))),
            Times.Exactly(1));
        operationalCommands.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestProcessRules_AlertRuleActivatedThenDeactivatedByNoiseLimitOnOff_SignalsOnceAndUnlatches()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        AlertActivityTimeDto activity = SvantekFixture.CreateActiveRuleActivity(null, null);
        RvtAlertRuleDto ruleOn = CreateRule(10.0, 8.0, activity);
        RvtAlertRuleDto ruleActive = CreateRule(10.0, 8.0, activity, isActive: true);
        ruleQueries.SetupSequence(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns(11.0).
            Returns(0.5);

        // The first run breaches and latches; the second drops below limit-off and unlatches.
        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [ruleOn], _periodStart, _rangeEnd, TestContext.CancellationToken);
        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [ruleActive], _periodStart, _rangeEnd, TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal => signal.SerialId == _serialId && signal.AlertType == AlertType.Alert),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        alertIngress.VerifyNoOtherCalls();

        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", true))),
            Times.Exactly(1));
        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", false))),
            Times.Exactly(1));
        operationalCommands.VerifyNoOtherCalls();
    }

    // Replaces TestStoreNoiseLevels_WithVaryingNumberOfContactsForAlertRule_Success: contact
    // fan-out is a durable-stack concern, so one breach is one signal however many contacts exist.
    [TestMethod]
    public async Task TestProcessRules_OneBreach_EmitsOneSignalWithoutReadingContacts()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        RvtAlertRuleDto rule = CreateRule(10.0, 1.0, SvantekFixture.CreateActiveRuleActivity(null, null));
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns(11.0);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [rule], _periodStart, _rangeEnd, TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", true))),
            Times.Exactly(1));
        operationalCommands.VerifyNoOtherCalls();
    }

    // Replaces TestStoreNoiseLevels_AlertRuleActivatedButSendMessageFails_Success: delivery
    // failures and audits live in the durable stack; a duplicate-reporting ingress result
    // still latches the rule after exactly one signal.
    [TestMethod]
    public async Task TestProcessRules_IngressReportsDuplicate_RuleStillLatchesAfterOneSignal()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        alertIngress
            .Setup(ingress => ingress.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertIngressResult(
                Guid.NewGuid(),
                NotificationId: null,
                AlertOccurrenceOutcome.Suppressed,
                IsDuplicate: true));
        RvtAlertRuleDto rule = CreateRule(10.0, 1.0, SvantekFixture.CreateActiveRuleActivity(null, null));
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns(11.0);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [rule], _periodStart, _rangeEnd, TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()),
            Times.Exactly(1));
        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", true))),
            Times.Exactly(1));
        operationalCommands.VerifyNoOtherCalls();
    }

    // Replaces TestStoreNoiseLevels_AlertRuleActivatedButSendMessageExcludedBySendTime_Success:
    // per-contact send-time windows are applied by the durable stack at delivery planning time,
    // so the breach signal is emitted whatever the time of day.
    [DataRow("2023-03-09T14:00:00Z")]
    [DataRow("2023-06-17T14:00:00Z")]
    [DataRow("2023-12-17T14:00:00Z")]
    [TestMethod]
    public async Task TestProcessRules_BreachSignalEmittedRegardlessOfContactSendWindows(string periodStartStr)
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        DateTime periodStart = DateTime.Parse(periodStartStr).ToUniversalTime();
        RvtAlertRuleDto rule = CreateRule(10.0, 1.0, SvantekFixture.CreateActiveRuleActivity(null, null));
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", periodStart, periodStart.AddSeconds(_durationSeconds))).
            Returns(11.0);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [rule], periodStart, periodStart.AddMinutes(16), TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal => signal.SerialId == _serialId && signal.AlertType == AlertType.Alert),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        alertIngress.VerifyNoOtherCalls();
        operationalCommands.Verify(c => c.UpdateAlertRule(It.Is<RvtAlertRuleDto>(d => TestUtil.VerifyAlertRuleDto(d, _serialId, "LAeq", true))),
            Times.Exactly(1));
        operationalCommands.VerifyNoOtherCalls();
    }

    // An averaging window holding no samples used to score 0.0 dB, which reads
    // as silence and unlatched the rule, so the next populated window re-fired
    // a breach contacts had already been told about.
    [TestMethod]
    public async Task TestProcessRules_EmptyWindow_LeavesALatchedRuleLatched()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        RvtAlertRuleDto rule = CreateRule(10.0, 1.0, SvantekFixture.CreateActiveRuleActivity(null, null), isActive: true);
        ruleQueries.Setup(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime)).
            Returns((double?)null);

        await processor.ProcessRulesAsync(SvantekFixture.ReadMonitorDto(_serialId), [rule], _periodStart, _rangeEnd, TestContext.CancellationToken);

        Assert.IsTrue(rule.IsActive);
        ruleQueries.Verify(q => q.GetAverageNoiseLevel(_serialId, "LAeq", _periodStart, _alertTime), Times.Exactly(1));
        ruleQueries.VerifyNoOtherCalls();
        operationalCommands.VerifyNoOtherCalls();
        alertIngress.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestSignalAlert_EmitsHandlerSignalWithEmailAndSmsChannels()
    {
        SvantekRuleProcessor processor = CreateProcessor(out Mock<ISvantekRuleQueries> ruleQueries,
                                                         out Mock<ISvantekOperationalCommands> operationalCommands,
                                                         out Mock<IAlertIngressPort> alertIngress);
        DateTime alertTime = new(2023, 10, 3, 13, 15, 0, DateTimeKind.Utc);

        await processor.SignalAlertAsync(_serialId, alertTime, 20.0, 0, 15.0, AlertType.BatteryCaution, "Battery level", TestContext.CancellationToken);

        alertIngress.Verify(c => c.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.Source == "svantek.rules" &&
                signal.SerialId == _serialId &&
                signal.EventTime == alertTime &&
                signal.AlertType == AlertType.BatteryCaution &&
                signal.Field == "Battery level" &&
                signal.Level == 15.0 &&
                signal.Limit == 20.0 &&
                signal.AveragingPeriod == 0 &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                signal.SuppressionWindow == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Exactly(1));
        alertIngress.VerifyNoOtherCalls();
        ruleQueries.VerifyNoOtherCalls();
        operationalCommands.VerifyNoOtherCalls();
    }

    private static IEnumerable<object[]> DateExclusion()
    {
        yield return new object[] {
            new List<RvtAlertRuleDto> { new(Guid.NewGuid(), _serialId, "LAeq", 19.0, 19.0, _durationSeconds,
                                            new AlertActivityTimeDto { Weekdays = false, Sundays = true, Saturdays = true,
                                            StartTime = null, EndTime = null}, AlertType.Alert, false, false,
                                            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
            }
        };
    }

    private static IEnumerable<object[]> TimeExclusion()
    {
        yield return new object[] {
            new List<RvtAlertRuleDto> { new(Guid.NewGuid(), _serialId, "LAeq", 19.0, 19.0, _durationSeconds,
                                            SvantekFixture.CreateActiveRuleActivity(DateTime.Parse("2023-09-25T00:01:02"), DateTime.Parse("2023-09-25T00:01:03")),
                                            AlertType.Alert, false, false,
                                            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
            }
        };
    }

    private static IEnumerable<object[]> LevelExclusion()
    {
        yield return new object[] {
            new List<RvtAlertRuleDto> { new(Guid.NewGuid(), _serialId, "LAeq", 50.0, 19.0, _durationSeconds,
                                            SvantekFixture.CreateActiveRuleActivity(null, null),
                                            AlertType.Alert, false, false,
                                            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(RvtAlertRuleDto.RULE_ALERT_DELAY_MINUTES+1))
            }
        };
    }

    public TestContext TestContext { get; set; } = null!;
}
