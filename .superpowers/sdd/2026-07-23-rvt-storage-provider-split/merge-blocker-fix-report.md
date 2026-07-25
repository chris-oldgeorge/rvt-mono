# Storage Provider Split — Merge-Blocker Fix Report

Date: 2026-07-26

Base: `7804962` (`fix(storage): translate provider timeouts and restore split scope`)

## Scope

This corrective task resolves the final carry-forward branch merge blocker and
the parked license-table classification:

1. Microsoft Graph large-attachment upload chunks leaked a non-caller
   `OperationCanceledException` instead of the adapter's safe transient timeout
   contract.
2. The dependency license review classified
   `Microsoft.Extensions.Configuration.Abstractions` 10.0.9 as transitive even
   though the current solution graph contains a direct reference.

Unrelated untracked files, storage-provider behavior, package policy, project
files, package versions, and locks were preserved. No push was performed.

## Root cause and Graph repair

`SendAuthenticatedAsync` already distinguishes caller cancellation from an
HTTP/provider timeout. Its first filtered catch rethrows when the supplied
caller token is cancelled, and its following cancellation catch translates any
other `OperationCanceledException` to:

```text
EmailDeliveryException("MicrosoftGraph", Transient, "Timeout")
```

`SendUploadChunkAsync` had the same filtered caller-cancellation catch but
followed it only with `HttpRequestException` handling. A chunk handler timeout
therefore escaped as raw cancellation while the caller token remained active.

The production change is one non-caller cancellation catch between the
existing caller filter and network-error catch. Status handling, chunk size and
ranges, unauthenticated upload-session PUT behavior, response disposal, and
caller cancellation are unchanged.

## Strict TDD evidence

Two focused tests drive the real large-attachment adapter flow through draft
creation and upload-session creation to the first PUT chunk:

- `SendAsync_UploadChunkTimeoutIsTransientAndSafe` uses an active caller token,
  throws `OperationCanceledException` at the upload PUT, and requires provider
  `MicrosoftGraph`, `Transient`, code `Timeout`, and no provider text exposure.
- `SendAsync_UploadChunkCallerCancellationPropagates` cancels the supplied
  token at that same PUT boundary and requires an `OperationCanceledException`
  carrying the caller token.

The test boundary double is `UploadChunkCancellationHandler`. Test variables
are `providerMessage`, `cancellation`, and `attachment`.

The first authored run correctly exposed raw timeout cancellation, but its
caller control also used an overly narrow exact-type assertion. `HttpClient`
surfaces the preserved caller cancellation as the valid derived
`TaskCanceledException`. The control was corrected to assert the
`OperationCanceledException` contract before any production edit.

Correct RED command:

```bash
dotnet test \
  libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~UploadChunkTimeoutIsTransientAndSafe|FullyQualifiedName~UploadChunkCallerCancellationPropagates' \
  --nologo --verbosity minimal
```

Correct RED result with production unchanged: exit 1; 1 passed, 1 failed,
0 skipped. The caller-cancellation control passed. The timeout case expected
exact `EmailDeliveryException` and received raw `OperationCanceledException`.

The first corrected RED retry inside the restricted sandbox was aborted because
vstest could not bind its local communication socket
(`SocketException (13): Permission denied`). The same bounded command was
rerun outside that restriction and produced the RED result above.

After the single production catch, the identical command exited 0 with
2 passed, 0 failed, 0 skipped.

## License review

Only the classification cell for
`Microsoft.Extensions.Configuration.Abstractions` 10.0.9 changes from
`transitive` to `direct`. The package version, license expression and URLs,
approval, and the complete 101-pair inventory are unchanged.

## Verification

- Focused upload-chunk timeout/caller cancellation: 2 passed, 0 failed,
  0 skipped.
- Complete Microsoft Graph adapter project: 37 passed, 0 failed, 0 skipped.
- Bounded neutral Communication project: 31 passed, 0 failed, 0 skipped.
- No `packages.lock.json`, `Directory.Packages.props`, or project-file diff is
  part of this task.
- `git diff --check`: no output.

Existing MSTest analyzer warnings remain in the neutral Communication project:
parallelization is not explicitly configured, `DataTestMethod` is obsolete,
and one dynamic-data source type argument can use auto-detection. They predate
this correction and do not affect the green results.

## Resolution

The Graph upload-chunk non-caller timeout merge blocker is resolved, and upload
caller cancellation remains intact. The parked license label is corrected.
No known task-scoped functional concern remains.
