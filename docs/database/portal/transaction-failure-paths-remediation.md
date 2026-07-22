# Transaction failure-path remediation

Generated: 2026-07-15.

Three defects found in a post-merge review, all the same class - **the failure path lies**:

1. **Error results commit** - `DeleteCompanyCommand` returns an error result mid-loop, but the transaction
   pipeline saves + commits anyway, leaving a partial delete.
2. **Failures report success** - `SiteArchiveService.Process` swallows every exception, returns `""`, and the
   handler still marks the site archived and reports 200.
3. **Retries corrupt state** - the retrying execution strategy re-runs the whole handler against a change
   tracker that was never reset, so a transient error turns into duplicate inserts.

Each step is test-first: the test must be shown to **fail against current behaviour** before the fix, then pass.
The relevant tests run on the SQLite harness, not the InMemory provider - InMemory has no transactions, so it
cannot exercise rollback.

## Step 1 - Commit gate: error results must roll back

`Application/Common` gains:

```csharp
public interface ITransactionOutcome { bool ShouldCommit { get; } }
```

`EfCoreUnitOfWork.ExecuteInTransactionAsync` (both the fresh-transaction and ambient-transaction branches)
checks the operation's return value: `response is ITransactionOutcome { ShouldCommit: false }` -> roll back and
return the response unchanged, instead of save + commit.

Handlers keep returning result objects; no exception-based control flow, no controller changes. Each
transactional result type implements the interface, typically `ShouldCommit => !NotFound && Errors.Count == 0`
(read per type - some use different failure members).

Pre-work: read every `ITransactionalRequest` handler and confirm none intentionally persists work while
returning an error. Any that does simply does not implement the interface (documented inline).

Test (SQLite, must fail first): company with two users, second `UserManager.DeleteAsync` forced to fail; assert
the error result is returned AND `SiteUsers` rows + user 1 + the company all still exist. Current behaviour
loses user 1 and every SiteUsers row.

## Step 2 - Site archive: wire the export into the live path and fail honestly

Investigation changed this step. The bug the review flagged was in **dead code**: `ArchiveSiteCommand` was never
sent, so `ArchiveSiteCommandHandler`, `ISiteArchiveService.Process`, and the whole `SiteArchiveService` export
machinery were unreachable. The live `/archive` endpoint called `SiteApplicationService.ArchiveAsync`, which just
set `Archived = true` and wrote a `SiteArchived` row with `PictureLink = null!` - no export at all.

Decision (with the user): the export feature is wanted, so wire it into the live path.

1. `ISiteArchiveService.Process(Guid siteId, CancellationToken)` - takes the token; `Process` stops swallowing
   (the catch-all that returned `""` is gone) and threads the token through the export, download, and
   zip/upload. A failed export now throws instead of reporting a phantom success.
2. `SiteApplicationService.ArchiveAsync` (the live path) now runs the export **before** marking the site
   archived, outside any transaction. On failure it returns `ApplicationResult.ExternalServiceUnavailable` (503)
   and leaves the site active; on success it records the real archive URL.
3. The redundant dead `ArchiveSiteCommand` + `ArchiveSiteCommandHandler` are deleted, consolidating to one
   archive path. `CqrsArchitectureTests` is repurposed from `ArchiveSiteCommandHandler_DependsOnArchivePort` to
   `SiteApplicationService_DependsOnArchivePort`, guarding the live path's port dependency.
4. `SiteArchived.CreateDate` is left as `DateTime.Now` - that is the consistent convention across all four
   SiteArchived/audit writes, so it was not part of this fix.

Test infra: the blob-backed export is faked in `SpaTestApplicationFactory` (a success fake, plus a failing fake
behind an `archiveExportFails` flag) - the existing happy-path test previously did not exercise the service at
all, so this makes it honest. New test (verified failing against the un-wired path): a failing export returns
503 and leaves the site active with no archive row.

## Step 3 - Retry safety: clear trackers on retry only

In the `strategy.ExecuteAsync` delegate, count attempts; on attempt > 1 only, `ChangeTracker.Clear()` on all
three contexts before re-running the operation. First attempt is untouched (so entities loaded before the
command - e.g. auth resolution - survive), and on a retry the rollback already undid the DB writes, so the stale
tracker is pure poison.

Test (must fail first): SQLite + a `DbCommandInterceptor` that throws a designated transient exception on the
first commit; assert a create handler produces exactly one row after the retry. Current code stages a duplicate
on the re-run.

## Sequencing

1. Commit gate - foundation; step 2's error result relies on "errors don't commit".
2. Site archive - independent once degated.
3. Retry clear - same `EfCoreUnitOfWork` method as step 1; lands after it to avoid conflicting edits.

One branch (`fix/transaction-failure-paths`), three commits, one PR. Each step: failing test -> fix -> full
suite -> commit. Final full rebuild to confirm the warning count holds at 2.
