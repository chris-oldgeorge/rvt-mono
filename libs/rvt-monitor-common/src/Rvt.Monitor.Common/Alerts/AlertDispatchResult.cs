namespace Rvt.Monitor.Common.Alerts;

/// <summary>
/// What one <see cref="DurableAlertDispatcher.DispatchAsync"/> batch did.
/// </summary>
/// <param name="ClaimFailure">
/// The error that ended the batch early, if the outbox could not be read.
/// Reported rather than thrown so the counts and dead-letter ids the batch
/// already accumulated survive it.
/// </param>
public sealed record AlertDispatchResult(
    int Delivered,
    int Deferred,
    int Retried,
    IReadOnlyList<Guid> DeadLetteredIds,
    Exception? ClaimFailure)
{
    public int DeadLettered => DeadLetteredIds.Count;
}
