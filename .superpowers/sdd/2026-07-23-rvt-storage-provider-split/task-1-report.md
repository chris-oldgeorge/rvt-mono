# Storage Provider Split Task 1 Report

## Status

Complete. This slice introduces only provider-neutral streaming storage
contracts and their focused MSTest coverage. No storage provider, application
consumer, composition root, solution inventory, packaging script, or legacy
storage implementation was changed.

## Starting State and CodeGraph

- Worktree: `.worktrees/release-platform-hardening`
- Starting commit: `0b655b6a510ad562a53487835a5675690b9df9a3`
- CodeGraph placed the new library beside the existing provider-neutral
  communication abstractions and surfaced the existing Portal
  `BlobStorageClientFactory` and Common blob implementations as later
  provider/consumer migration work. They were not edited.
- The worktree already contained unrelated untracked `.codegraph`,
  NuGet-cache, duplicate Portal/client source, and design-document files. They
  were preserved and excluded from staging.

## Implementation

- Added a packable net10 `Rvt.Storage.Abstractions` project under the
  `Rvt.Storage` namespace.
- Added `StorageObjectKey`, whose private construction boundary normalizes
  separator and empty-segment variations while rejecting empty, rooted, UNC,
  Windows-drive-rooted, `.`-segment, and `..`-segment names.
- Added streaming write/read values and `IObjectStorageClient` with optional
  cancellation tokens.
- Added ordinal named-client registrations and lookup. Construction rejects
  blank resource names and duplicate ordinal names; lookup rejects blank names
  and uses the required missing-resource message without listing any other
  registrations.
- Added provider-neutral failure kinds and `ObjectStorageException`. Its
  message is built exclusively from failure kind, logical resource name, and
  optional validated key; inner exception text is never reflected.
- `StorageReadResult.DisposeAsync()` awaits content disposal first and disposes
  the optional provider lease in a `finally` block. The test forces content
  disposal to fail, proving the lease is still disposed in the required order.
- Added a net10 MSTest project with only `Microsoft.NET.Test.Sdk`,
  `MSTest.TestAdapter`, and `MSTest.TestFramework` as direct test packages.
  Locked restore generated the source and test `packages.lock.json` files; the
  source lock has an empty net10 dependency set.

## Strict TDD Evidence

### Key contract RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageObjectKeyTests --nologo -v minimal
```

Result: exit 1. Compilation failed with CS0103 at both uses because
`StorageObjectKey` did not exist.

### Key contract GREEN

The identical command passed 8/8 after the minimal validated key type was
added.

### Remaining contracts RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~ObjectStorageClientFactoryTests|FullyQualifiedName~StorageReadResultTests|FullyQualifiedName~ObjectStorageExceptionTests' \
  --nologo -v minimal
```

Result: exit 1. Compilation failed with CS0246 because
`IObjectStorageClient`, `StorageWriteRequest`, `StorageWriteResult`, and
`StorageReadResult` did not exist.

### Complete abstraction GREEN

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~Abstractions --nologo -v minimal
```

Result: 13/13 passed, 0 failed, 0 skipped.

## Boundary and Verification Evidence

```bash
dotnet list libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj \
  package --include-transitive --no-restore
```

Result: `No packages were found for this framework.` The abstraction source
uses only base-class-library types and contains no configuration, DI, provider
SDK, or filesystem API dependency.

The final focused test command passed 13/13, the abstraction project built
successfully through that test run, and `git diff --check` passed.

MSTest 4.0.2 emits the existing analyzer advisories that the test assembly has
not explicitly selected a parallelization policy and that the plan-mandated
`DataTestMethod` attribute is obsolete in favor of `TestMethod`. These are
warnings only; the required test cases all execute and pass.

## Files

Production:

- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageObjectKey.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageWriteRequest.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageWriteResult.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageReadResult.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/IObjectStorageClient.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/IObjectStorageClientFactory.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageClientRegistration.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageClientFactory.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageFailureKind.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageException.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/packages.lock.json`

Tests:

- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/StorageObjectKeyTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/ObjectStorageClientFactoryTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/StorageReadResultTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/ObjectStorageExceptionTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/packages.lock.json`

State and evidence:

- `project_state.md`
- `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-1-report.md`

## Carry-Forward Pending Work

- Task 2: extract the Local storage adapter.
- Task 3: extract the Azure Blob storage adapter.
- Task 4: extract the S3 storage adapter.
- Task 5: enforce provider contract parity and dependency isolation.
- Task 6: migrate Svantek sound recordings.
- Task 7: migrate ReportingMonitor while preserving persisted report links.
- Task 8: remove legacy storage from `Rvt.Monitor.Common`.
- Task 9: add source-solution and packaging integration.
- Task 10: perform final verification and documentation.
- Portal blob unification and the independent `services/reporting` Azure
  adapter remain explicitly excluded future work.
- All previously documented communication release/lock, Portal, reporting,
  dynamic-plugin, external-compatibility, notification/business/API,
  persisted-record, database, MQTT, scheduling, and observability work remains
  pending and unchanged.
