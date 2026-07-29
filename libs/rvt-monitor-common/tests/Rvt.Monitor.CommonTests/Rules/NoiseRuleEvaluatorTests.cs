using Microsoft.Extensions.Logging;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.CommonTests.Rules;

[TestClass]
public sealed class NoiseRuleEvaluatorTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        RvtLogger.CreateLogger(factory, nameof(NoiseRuleEvaluatorTests));
    }

    [TestMethod]
    public async Task EvaluateAsync_DoesNotDowngradePreviousAlertWhenActiveCautionIsSuppressed()
    {
        int updateCount = 0;
        Mock<IAlertIngressPort> alertIngress = NewAlertIngress();

        NoiseRuleEvaluator evaluator = new(
            _ => updateCount++,
            alertIngress.Object,
            "test.rules");

        RvtAlertRuleDto rule = CreateRule(AlertType.Caution, isActive: true);

        AlertType result = await evaluator.EvaluateAsync(CreateRequest(), rule, level: 12, previousAlert: AlertType.Alert);

        Assert.AreEqual(AlertType.Alert, result);
        Assert.IsTrue(rule.IsActive);
        Assert.AreEqual(0, updateCount);
        alertIngress.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task EvaluateAsync_SignalsBreachWithDurableRuleContract()
    {
        int updateCount = 0;
        Mock<IAlertIngressPort> alertIngress = NewAlertIngress();

        NoiseRuleEvaluator evaluator = new(
            _ => updateCount++,
            alertIngress.Object,
            "test.rules");

        RvtAlertRuleDto rule = CreateRule(AlertType.Alert, isActive: false);
        RuleEvaluationRequest request = CreateRequest();

        AlertType result = await evaluator.EvaluateAsync(request, rule, level: 12, previousAlert: AlertType.Ignore);

        Assert.AreEqual(AlertType.Alert, result);
        Assert.IsTrue(rule.IsActive);
        Assert.AreEqual(1, updateCount);
        alertIngress.Verify(ingress => ingress.AcceptAsync(
            It.Is<AlertSignal>(signal =>
                signal.Source == "test.rules" &&
                signal.SerialId == request.MonitorSerialId &&
                signal.AlertType == AlertType.Alert &&
                signal.Field == rule.Field &&
                signal.Level == 12 &&
                signal.Limit == rule.LimitOn &&
                signal.AveragingPeriod == rule.AveragingPeriod &&
                signal.EventTime == request.AlertTime &&
                signal.DeliveryChannels == (AlertDeliveryChannels.Mqtt | AlertDeliveryChannels.Email | AlertDeliveryChannels.Sms) &&
                signal.SuppressionWindow == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IAlertIngressPort> NewAlertIngress()
    {
        Mock<IAlertIngressPort> alertIngress = new();
        alertIngress
            .Setup(ingress => ingress.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertIngressResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                AlertOccurrenceOutcome.Accepted,
                IsDuplicate: false));
        return alertIngress;
    }

    private static RuleEvaluationRequest CreateRequest() =>
        new(
            FleetNr: "fleet-1",
            MonitorSerialId: "monitor-1",
            MonitorId: Guid.NewGuid(),
            ActivityTime: new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
            AlertTime: new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
            PublishTime: new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc));

    private static RvtAlertRuleDto CreateRule(AlertType alertType, bool isActive) =>
        new(
            ruleId: Guid.NewGuid(),
            serialId: "rule-1",
            field: "LAeq",
            limitOn: 10,
            limitOff: 8,
            averagingPeriod: 900,
            ruleActivityTime: new Rvt.Monitor.Common.Rules.AlertActivityTimeDto
            {
                Weekdays = true,
                Saturdays = true,
                Sundays = true
            },
            alertType: alertType,
            isActive: isActive,
            isDeleted: false,
            created: DateTime.UtcNow,
            accessed: null);
}
