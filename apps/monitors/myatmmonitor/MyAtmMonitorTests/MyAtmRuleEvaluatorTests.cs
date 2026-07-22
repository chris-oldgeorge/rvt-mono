using MyAtm.Api;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using RulesContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace MyAtmMonitorTests;

[TestClass]
public sealed class MyAtmRuleEvaluatorTests
{
    [TestMethod]
    public void TransitionEvaluator_ActivatesAtLimitOnAndDeactivatesAtLimitOff()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var rule = CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive());
        var sampleAtLimitOn = new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch, 10, null, null, null, null, null, null);
        var sampleAtLimitOff = new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 8, null, null, null, null, null, null);
        var evaluator = new MyAtmAlertTransitionEvaluator();

        var activation = evaluator.Evaluate(rule, isActive: false, sampleAtLimitOn, alertForFieldIsActive: false);
        var deactivation = evaluator.Evaluate(rule, isActive: true, sampleAtLimitOff, alertForFieldIsActive: false);

        Assert.IsTrue(activation.IsActive);
        Assert.IsTrue(activation.Activated);
        Assert.AreEqual(10d, activation.Level);
        Assert.IsFalse(deactivation.IsActive);
        Assert.IsFalse(deactivation.Activated);
    }

    [TestMethod]
    public void TransitionEvaluator_LeavesStateForMissingValueOrInactiveWindow()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var inactiveRule = CreateRule(monitor, "Pm1", AlertType.Alert, new Rvt.Monitor.Common.Rules.AlertActivityTimeDto());
        var activeRule = CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive());
        var missingValue = new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch, null, null, null, null, null, null, null);
        var outsideWindow = new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch, 11, null, null, null, null, null, null);
        var evaluator = new MyAtmAlertTransitionEvaluator();

        var missing = evaluator.Evaluate(activeRule, isActive: true, missingValue, alertForFieldIsActive: false);
        var inactive = evaluator.Evaluate(inactiveRule, isActive: false, outsideWindow, alertForFieldIsActive: false);

        Assert.IsTrue(missing.IsActive);
        Assert.IsFalse(missing.Activated);
        Assert.IsFalse(inactive.IsActive);
        Assert.IsFalse(inactive.Activated);
    }

    [TestMethod]
    public void TransitionEvaluator_DeactivatesDeletedRuleAndGivesAlertPrecedenceOverCaution()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var sample = new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch, 11, null, null, null, null, null, null);
        var evaluator = new MyAtmAlertTransitionEvaluator();

        var deleted = evaluator.Evaluate(
            CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive(), isDeleted: true),
            isActive: true,
            sample,
            alertForFieldIsActive: false);
        var caution = evaluator.Evaluate(
            CreateRule(monitor, "Pm1", AlertType.Caution, AlwaysActive()),
            isActive: false,
            sample,
            alertForFieldIsActive: true);

        Assert.IsFalse(deleted.IsActive);
        Assert.IsFalse(deleted.Activated);
        Assert.IsFalse(caution.IsActive);
        Assert.IsFalse(caution.Activated);
    }

    [TestMethod]
    public void Evaluate_RepeatedOverLimitSamplesCreatesOneActivationAndProposal()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var ruleId = Guid.NewGuid();
        var rule = new RvtAlertRuleDto(
            ruleId,
            monitor.SerialId,
            "Pm1",
            limitOn: 10,
            limitOff: 8,
            averagingPeriod: 60,
            new Rvt.Monitor.Common.Rules.AlertActivityTimeDto { Weekdays = true, Saturdays = true, Sundays = true },
            AlertType.Alert,
            isActive: false,
            isDeleted: false,
            created: DateTime.UnixEpoch,
            accessed: null);
        var samples = new List<DustDto>
        {
            new(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 11, null, null, null, null, null, null),
            new(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(2), 12, null, null, null, null, null, null)
        };

        var result = new MyAtmRuleEvaluator().Evaluate(
            monitor,
            Period.Minutes1,
            new[] { rule },
            samples,
            DateTime.UnixEpoch.AddMinutes(3));

        Assert.HasCount(1, result.RuleStateMutations);
        Assert.AreEqual(ruleId, result.RuleStateMutations[0].RuleId);
        Assert.IsTrue(result.RuleStateMutations[0].IsActive);
        Assert.AreEqual(DateTime.UnixEpoch.AddMinutes(3), result.RuleStateMutations[0].Accessed);
        Assert.HasCount(1, result.AlertOccurrences);
        Assert.AreEqual(ruleId, result.AlertOccurrences[0].RuleId);
        Assert.AreEqual(DateTime.UnixEpoch.AddMinutes(1), result.AlertOccurrences[0].TriggeredAt);
    }

    [TestMethod]
    public void Evaluate_AlertSuppressesSameFieldCaution()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var activity = AlwaysActive();
        var alertRule = CreateRule(monitor, "Pm1", AlertType.Alert, activity);
        var cautionRule = CreateRule(monitor, "Pm1", AlertType.Caution, activity);
        var samples = new[]
        {
            new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 11, null, null, null, null, null, null)
        };

        var result = new MyAtmRuleEvaluator().Evaluate(
            monitor,
            Period.Minutes1,
            new[] { alertRule, cautionRule },
            samples,
            DateTime.UnixEpoch.AddMinutes(2));

        Assert.HasCount(1, result.AlertOccurrences);
        Assert.AreEqual(AlertType.Alert, result.AlertOccurrences[0].AlertType);
        Assert.HasCount(1, result.RuleStateMutations);
        Assert.AreEqual(alertRule.RuleId, result.RuleStateMutations[0].RuleId);
    }

    [TestMethod]
    public void AggregateOccurrence_UsesTheSharedPlannerWithoutChangingItsDeterministicCorrelationKey()
    {
        var monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        var rule = CreateRule(monitor, "Pm2_5", AlertType.Alert, AlwaysActive());
        var triggeredAt = new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc);
        var commitTime = triggeredAt.AddSeconds(5);
        var processor = new MyAtmRuleProcessor(new StubRuleQueries(), "https://portal.example.test/");

        var commit = processor.CreateAggregateCommit(
            monitor,
            rule,
            level: 11,
            triggeredAt,
            alertForFieldIsActive: false,
            commitTime);

        var occurrence = commit.Occurrences.Single();
        var expectedKey = $"{monitor.Id:N}:{rule.RuleId:N}:{triggeredAt:O}:{AlertType.Alert}";
        Assert.AreEqual(expectedKey, occurrence.Key);
        Assert.IsNotNull(occurrence.DeliveryPlan);
        Assert.AreEqual(
            MonitorDeliveryIdentity.CreateGuid($"notification:{expectedKey}"),
            occurrence.DeliveryPlan.Notification.Id);
        Assert.IsTrue(occurrence.DeliveryPlan.Deliveries.All(delivery => delivery.CorrelationKey == expectedKey));
    }

    private static RvtAlertRuleDto CreateRule(
        DustMonitorDto monitor,
        string field,
        AlertType alertType,
        Rvt.Monitor.Common.Rules.AlertActivityTimeDto activity,
        bool isDeleted = false) =>
        new(
            Guid.NewGuid(),
            monitor.SerialId,
            field,
            limitOn: 10,
            limitOff: 8,
            averagingPeriod: 60,
            activity,
            alertType,
            isActive: false,
            isDeleted,
            created: DateTime.UnixEpoch,
            accessed: null);

    private static Rvt.Monitor.Common.Rules.AlertActivityTimeDto AlwaysActive() =>
        new() { Weekdays = true, Saturdays = true, Sundays = true };

    private sealed class StubRuleQueries : MyAtm.Api.Db.IMyAtmRuleQueries
    {
        public List<RvtAlertRuleDto> ReadRules(string? serialId) => [];
        public List<RvtAlertRuleDto> ReadRules(string? serialId, Period period) => [];
        public List<RvtAlertRuleDto> ReadRules(Period period) => [];
        public List<RulesContactDto> ReadAlertContacts(Guid monitorId) => [];
        public bool HasOpenNotification(Guid monitorId, string alertField, AlertType alertType) => false;
        public double? GetAverageDustLevel(string serialNumber, string columnName, DateTime start, DateTime end) => null;
    }
}
