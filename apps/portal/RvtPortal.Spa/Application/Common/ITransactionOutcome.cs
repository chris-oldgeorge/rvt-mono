// File summary: Lets a command result tell the transaction pipeline whether its work should be committed.
// Major updates:
// - 2026-07-15 pending Added so error/not-found results roll back instead of committing a partial write.

namespace RvtPortal.Spa.Application.Common;

/// <summary>
/// Implemented by command results so the Unit of Work can commit only successful work.
///
/// Handlers signal failure by <em>returning</em> a result with errors populated, not by throwing. Without this,
/// the transaction pipeline saves and commits whatever a handler staged before it decided to fail - e.g.
/// DeleteCompanyCommand deletes some users, hits a failure on the next, returns an error, and the pipeline still
/// commits the partial delete. When <see cref="ShouldCommit"/> is <c>false</c>, the Unit of Work rolls back.
/// </summary>
public interface ITransactionOutcome
{
    /// <summary>
    /// <c>true</c> when the handler completed successfully and its writes should be persisted; <c>false</c> when
    /// it is returning a failure (not found, validation errors, ...) whose staged writes must be discarded.
    /// </summary>
    bool ShouldCommit { get; }
}
