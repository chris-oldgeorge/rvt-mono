using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Mqtt;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

namespace Rvt.Monitor.CommonTests.Rules;

[TestClass]
public sealed class NoiseRuleEvaluatorTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        using var factory = LoggerFactory.Create(builder => builder.AddConsole());
        RvtLogger.CreateLogger(factory, nameof(NoiseRuleEvaluatorTests));
    }

    [TestMethod]
    public void Evaluate_DoesNotDowngradePreviousAlertWhenActiveCautionIsSuppressed()
    {
        var updateCount = 0;
        var contactsWereRead = false;
        var notificationWasProcessed = false;
        var eventPublisher = new FakeEventPublisher();

        var evaluator = new NoiseRuleEvaluator(
            _ => updateCount++,
            _ =>
            {
                contactsWereRead = true;
                return [];
            },
            (_, _) => notificationWasProcessed = true,
            eventPublisher);

        var rule = CreateRule(AlertType.Caution, isActive: true);

        var result = evaluator.Evaluate(CreateRequest(), rule, level: 12, previousAlert: AlertType.Alert);

        Assert.AreEqual(AlertType.Alert, result);
        Assert.IsTrue(rule.IsActive);
        Assert.AreEqual(0, updateCount);
        Assert.IsFalse(contactsWereRead);
        Assert.IsFalse(notificationWasProcessed);
        Assert.IsFalse(eventPublisher.AlertWasPublished);
    }

    private sealed class FakeEventPublisher : IMonitorEventPublisher
    {
        public bool AlertWasPublished { get; private set; }

        public void PublishDataInserted(DateTime timestamp, string serialId, int? customerId = null)
        {
        }

        public void PublishAlert(DateTime timestamp, string serialId, string message, int? customerId = null)
        {
            AlertWasPublished = true;
        }
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
