using MyAtm.Api;
using Rvt.Monitor.Common.Delivery;

namespace MyAtm.Model.Dto;

public sealed record MyAtmDustImportCommit(
    DustMonitorDto Monitor,
    Period Period,
    IReadOnlyList<DustDto> Measurements,
    DateTime Watermark,
    IReadOnlyList<RuleStateMutation> RuleStateMutations,
    IReadOnlyList<AlertOccurrenceProposal> AlertOccurrences,
    DateTime UtcNow);

public sealed record DustImportCommitResult(IReadOnlyList<MonitorDeliveryRequest> OutboxMessages);
