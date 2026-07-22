using MyAtm.Api;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace MyAtm.Model.Dto;

public sealed record RuleStateMutation(
    Guid RuleId,
    bool ExpectedIsActive,
    DateTime? ExpectedAccessed,
    bool IsActive,
    DateTime? Accessed);

public sealed record AlertOccurrenceProposal(
    string Key,
    Guid MonitorId,
    Guid RuleId,
    Period Period,
    AlertType AlertType,
    string Field,
    double LimitOn,
    double Level,
    DateTime TriggeredAt,
    IReadOnlyList<RvtContactDto> Contacts);

public sealed record MyAtmRuleEvaluation(
    IReadOnlyList<RuleStateMutation> RuleStateMutations,
    IReadOnlyList<AlertOccurrenceProposal> AlertOccurrences);
