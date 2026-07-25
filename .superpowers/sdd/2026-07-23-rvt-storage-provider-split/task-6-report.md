# Task 6 Report: Svantek Sound-Recording Migration

Date: 2026-07-25

## Scope

Task 6 started from `56fbe64` (`docs(storage): correct boundary guard state`)
in `.worktrees/release-platform-hardening`.

The preflight check confirmed that Communication Task 6 had already changed
`MonitorHost.RunAsync` to pass `IConfiguration` through every application
mode, and all monitor programs already use two-argument service callbacks.
Those files and the Omnidots callback tests remain unchanged.

This change is limited to Svantek's sound-recording storage composition,
consumer/API wiring, project references, tests, this report, and the
project-state entry. Legacy Common storage remains intact for Task 8. Portal,
the independent reporting service, package versions, and package locks are
unchanged.

## Named provider composition

`SvantekStorageComposition` owns the logical resource name
`svantek-sound-recordings` and registers exactly one provider:

- `BlobStorage:Provider`;
- `RVT:BLOB_PROVIDER`;
- `RVT__BLOB_PROVIDER`;
- Local when no non-blank value is configured.

Local, Azure Blob, and S3 matching is ordinal case-insensitive. Invalid values
throw the required exact allowed-provider message at composition time.
Provider-specific options continue through each extracted provider's existing
binding and validation. The Local default resolves object paths beneath
`/data/rvt/blobs/audiofiles` with no prefix.

`AddSvantekMonitor(IConfiguration)` invokes this composition root and resolves
`IObjectStorageClientFactory` for the API singleton. The old
`AddMonitorBlobStorage` call and Svantek's legacy `IBlobStorageService` usage
are removed, while the legacy Common types and implementations themselves
remain untouched.

## Streaming consumer

`CheckForSoundRecordingsHandler` now receives
`IObjectStorageClientFactory` and resolves the named sound-recordings client
once in its constructor. After the existing Svantek download, it wraps the
returned bytes in a non-writable `MemoryStream` and writes:

- the unchanged `{NotificationId}.wav` object key;
- the unchanged vendor response bytes;
- `audio/wav`;
- the exact caller cancellation token.

The database recording-link update still occurs only after the object write.
The test double copies the request stream before it is disposed and records
the key, bytes, content type, and token.

Public direct `SvantekApi` construction without a supplied storage dependency
retains the existing lazy explicit failure semantics. A missing factory
returns a missing client, so constructing the API still succeeds and an
attempted upload identifies the absent `IObjectStorageClientFactory`.

## Test-first evidence

### Composition RED/GREEN

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter FullyQualifiedName~StorageCompositionTests --nologo -v minimal
```

The initial RED failed compilation on the absent `Rvt.Storage` project
references and `Svantek.Api.Storage` composition root. After the minimal
composition implementation, all six original cases passed.

A follow-up precedence test configured a blank provider-neutral value over a
valid `RVT:BLOB_PROVIDER`. It failed 1/7 because the blank value was treated as
an invalid provider, then passed 7/7 after selection changed to the first
non-blank value in the required order.

### Streaming RED/GREEN

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter FullyQualifiedName~TestCheckForSoundRecordingStorage --nologo -v minimal
```

The streaming test rewrite first failed compilation with five expected
`IObjectStorageClientFactory` to `IBlobStorageService` argument mismatches.
After the consumer/API migration, the same filter passed 4/4.

### Callback and composition regression checks

```bash
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  --filter FullyQualifiedName~TestMonitorJobScheduling --nologo -v minimal
```

Passed 13/13 without changing the already-migrated callback graph.

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter "FullyQualifiedName~CommunicationsCompositionTests|FullyQualifiedName~SvantekImportOptionsTests|FullyQualifiedName~SvantekJobCancellationTests" \
  --nologo -v minimal
```

Passed 20/20, including communication provider selection, startup validation,
Svantek options validation, and job cancellation propagation.

## Final verification

```bash
dotnet build apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj \
  --no-restore --nologo -v minimal
```

The Svantek host built with zero warnings and errors.

The unfiltered Svantek suite compiled successfully and ran 133 tests. It
reported 93 passes and 40 failures:

- PostgreSQL fixture tests require
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, which is not available in this
  environment;
- four schema-patch checks and one source-boundary check use pre-existing
  repository-root assumptions that omit the `apps/monitors` path from this
  worktree.

Excluding only those known environment/root-sensitive classes passed the
complete runnable set, 93/93:

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter "FullyQualifiedName!~TestDBClient&FullyQualifiedName!~SvantekPostgreSqlSchemaPatchTests&FullyQualifiedName!~SvantekDependencyBoundaryTests" \
  --nologo -v minimal
```

Fresh final focused results were composition 7/7, streaming consumer 4/4,
shared host scheduling 13/13, and communication/options/cancellation 20/20.
`git diff --check` passed.

## Concerns and exclusions

Task 6 deliberately does not update lockfiles even though normal restore
observes the new direct provider project graph; solution/packaging and lock
integration remains Task 9 work. Final verification therefore uses the
already-restored assets with `--no-restore` after generated lock diffs are
removed.

ReportingMonitor migration remains Task 7. Legacy Common storage removal
remains Task 8. Portal storage and the independent `services/reporting` Azure
adapter remain excluded future work.
