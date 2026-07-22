using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace MyAtm.Api;

public sealed class MyAtmRuleEvaluator
{
    private readonly MyAtmAlertTransitionEvaluator transitionEvaluator;

    public MyAtmRuleEvaluator()
        : this(new MyAtmAlertTransitionEvaluator())
    {
    }

    public MyAtmRuleEvaluator(MyAtmAlertTransitionEvaluator transitionEvaluator)
    {
        this.transitionEvaluator = transitionEvaluator;
    }

    public MyAtmRuleEvaluation Evaluate(
        DustMonitorDto monitor,
        Period period,
        IReadOnlyList<RvtAlertRuleDto> rules,
        IReadOnlyList<DustDto> samples,
        DateTime utcNow)
    {
        var originalStates = rules.ToDictionary(rule => rule.RuleId, rule => (rule.IsActive, rule.Accessed));
        var states = originalStates.ToDictionary(pair => pair.Key, pair => pair.Value);
        var occurrences = new List<AlertOccurrenceProposal>();

        foreach (var sample in samples.OrderBy(sample => sample.SampleTime))
        {
            foreach (var rule in rules.OrderBy(rule => rule.AlertType))
            {
                var state = states[rule.RuleId];
                var alertForFieldIsActive = rule.AlertType == AlertType.Caution && rules.Any(otherRule =>
                    otherRule.AlertType == AlertType.Alert &&
                    MyAtmAlertTransitionEvaluator.NormalizeField(otherRule.Field) == MyAtmAlertTransitionEvaluator.NormalizeField(rule.Field) &&
                    states[otherRule.RuleId].IsActive);
                var transition = transitionEvaluator.Evaluate(rule, state.IsActive, sample, alertForFieldIsActive);
                if (transition.IsActive != state.IsActive)
                {
                    states[rule.RuleId] = (transition.IsActive, transition.Activated ? utcNow : state.Accessed);
                }

                if (!transition.Activated)
                {
                    continue;
                }

                occurrences.Add(new AlertOccurrenceProposal(
                    $"{monitor.Id:N}:{rule.RuleId:N}:{DateTimeUtil.AsUtc(sample.SampleTime):O}:{rule.AlertType}",
                    monitor.Id,
                    rule.RuleId,
                    period,
                    rule.AlertType,
                    rule.Field,
                    rule.LimitOn,
                    transition.Level!.Value,
                    sample.SampleTime,
                    Array.Empty<RvtContactDto>()));
            }
        }

        var mutations = rules
            .Where(rule => states[rule.RuleId] != originalStates[rule.RuleId])
            .Select(rule => new RuleStateMutation(
                rule.RuleId,
                originalStates[rule.RuleId].IsActive,
                originalStates[rule.RuleId].Accessed,
                states[rule.RuleId].IsActive,
                states[rule.RuleId].Accessed))
            .ToList();

        return new MyAtmRuleEvaluation(mutations, occurrences);
    }

}
