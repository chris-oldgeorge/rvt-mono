using MyAtm.Api;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
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
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        RvtAlertRuleDto rule = CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive());
        DustDto sampleAtLimitOn = new(monitor.SerialId, 60, DateTime.UnixEpoch, 10, null, null, null, null, null, null);
        DustDto sampleAtLimitOff = new(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 8, null, null, null, null, null, null);
        MyAtmAlertTransitionEvaluator evaluator = new();

        MyAtmAlertTransition activation = evaluator.Evaluate(rule, isActive: false, sampleAtLimitOn, alertForFieldIsActive: false);
        MyAtmAlertTransition deactivation = evaluator.Evaluate(rule, isActive: true, sampleAtLimitOff, alertForFieldIsActive: false);

        Assert.IsTrue(activation.IsActive);
        Assert.IsTrue(activation.Activated);
        Assert.AreEqual(10d, activation.Level);
        Assert.IsFalse(deactivation.IsActive);
        Assert.IsFalse(deactivation.Activated);
    }

    [TestMethod]
    public void TransitionEvaluator_LeavesStateForMissingValueOrInactiveWindow()
    {
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        RvtAlertRuleDto inactiveRule = CreateRule(monitor, "Pm1", AlertType.Alert, new Rvt.Monitor.Common.Rules.AlertActivityTimeDto());
        RvtAlertRuleDto activeRule = CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive());
        DustDto missingValue = new(monitor.SerialId, 60, DateTime.UnixEpoch, null, null, null, null, null, null, null);
        DustDto outsideWindow = new(monitor.SerialId, 60, DateTime.UnixEpoch, 11, null, null, null, null, null, null);
        MyAtmAlertTransitionEvaluator evaluator = new();

        MyAtmAlertTransition missing = evaluator.Evaluate(activeRule, isActive: true, missingValue, alertForFieldIsActive: false);
        MyAtmAlertTransition inactive = evaluator.Evaluate(inactiveRule, isActive: false, outsideWindow, alertForFieldIsActive: false);

        Assert.IsTrue(missing.IsActive);
        Assert.IsFalse(missing.Activated);
        Assert.IsFalse(inactive.IsActive);
        Assert.IsFalse(inactive.Activated);
    }

    [TestMethod]
    public void TransitionEvaluator_DeactivatesDeletedRuleAndGivesAlertPrecedenceOverCaution()
    {
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        DustDto sample = new(monitor.SerialId, 60, DateTime.UnixEpoch, 11, null, null, null, null, null, null);
        MyAtmAlertTransitionEvaluator evaluator = new();

        MyAtmAlertTransition deleted = evaluator.Evaluate(
            CreateRule(monitor, "Pm1", AlertType.Alert, AlwaysActive(), isDeleted: true),
            isActive: true,
            sample,
            alertForFieldIsActive: false);
        MyAtmAlertTransition caution = evaluator.Evaluate(
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
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        Guid ruleId = Guid.NewGuid();
        RvtAlertRuleDto rule = new(
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
        List<DustDto> samples =
        [
            new(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 11, null, null, null, null, null, null),
            new(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(2), 12, null, null, null, null, null, null)
        ];

        MyAtmRuleEvaluation result = new MyAtmRuleEvaluator().Evaluate(
            monitor,
            Period.Minutes1,
            [rule],
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
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        Rvt.Monitor.Common.Rules.AlertActivityTimeDto activity = AlwaysActive();
        RvtAlertRuleDto alertRule = CreateRule(monitor, "Pm1", AlertType.Alert, activity);
        RvtAlertRuleDto cautionRule = CreateRule(monitor, "Pm1", AlertType.Caution, activity);
        DustDto[] samples =
        [
            new DustDto(monitor.SerialId, 60, DateTime.UnixEpoch.AddMinutes(1), 11, null, null, null, null, null, null)
        ];

        MyAtmRuleEvaluation result = new MyAtmRuleEvaluator().Evaluate(
            monitor,
            Period.Minutes1,
            [alertRule, cautionRule],
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
        DustMonitorDto monitor = MyAtmFixture.CustomerDeviceDtos(null, singleItem: true).Single();
        RvtAlertRuleDto rule = CreateRule(monitor, "Pm2_5", AlertType.Alert, AlwaysActive());
        DateTime triggeredAt = new(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc);
        DateTime commitTime = triggeredAt.AddSeconds(5);
        MyAtmRuleProcessor processor = new(new StubRuleQueries());

        MyAtmAlertCommit commit = processor.CreateAggregateCommit(
            monitor,
            rule,
            level: 11,
            triggeredAt,
            alertForFieldIsActive: false,
            commitTime);

        MyAtmAlertOccurrenceInput occurrence = commit.Occurrences.Single();
        string expectedKey = $"{monitor.Id:N}:{rule.RuleId:N}:{triggeredAt:O}:{AlertType.Alert}";
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
        public double? GetAverageDustLevel(string serialNumber, string columnName, DateTime start, DateTime end) => null;
    }
}
