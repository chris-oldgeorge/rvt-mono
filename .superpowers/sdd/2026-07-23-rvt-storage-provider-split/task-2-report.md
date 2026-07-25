# Storage Provider Split Task 2 Report

## Status

Complete. This slice extracts the Local filesystem implementation into a
packable `Rvt.Storage.Local` provider over the Task 1 streaming contracts. The
legacy Common implementation remains intact. No application consumer,
composition root, cloud provider, solution inventory, packaging workflow, or
Portal source was changed.

## Starting State and Reference Review

- Worktree: `.worktrees/release-platform-hardening`
- Starting commit: `da0dfd255f3eafff95c32a1783f08ce5a4254454`
- CodeGraph was invoked first as required, but workspace policy rejected
  transmitting private repository code to the unverified CodeGraph
  destination. No bypass was attempted. The safer local read-only path was
  used to read the complete current `LocalFileBlobStorageService`, its full
  filesystem test suite, Local-only `BlobStorageOptions` tests, DI/startup
  tests, all Task 1 contracts, package configuration, and project conventions
  before editing.
- Unrelated untracked `.codegraph`, NuGet-cache, duplicate Portal/client source,
  and duplicate design-document files were preserved and excluded from the
  Task 2 commit.

## Implementation

- Added packable net10 `Rvt.Storage.Local` with only the required
  `Microsoft.Extensions.Configuration.Abstractions`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`, and
  `Microsoft.Extensions.Hosting.Abstractions` direct packages plus the
  `Rvt.Storage.Abstractions` project reference.
- Added `LocalStorageOptions.Bind`, preserving `/data/rvt/blobs`,
  `audiofiles`, empty-prefix defaults; provider-neutral configuration;
  `RVT:` and literal `RVT__` aliases; the legacy `AUDIO_FOLDER`; and custom
  reporting defaults/`BLOB_REPORT_CONTAINER_NAME`.
- Added all three `AddRvtLocalStorage` overloads. Registration validates a
  nonblank resource name immediately, uses the shared factory if one is not
  already registered, registers the concrete adapter as a keyed singleton,
  publishes exactly one `ObjectStorageClientRegistration`, and adds one startup
  validator. Factory lookup and keyed resolution reuse the exact same client
  instance.
- The startup hosted service resolves the required named client from
  `IObjectStorageClientFactory` during `StartAsync`.
- `LocalObjectStorageClient.WriteAsync` copies the caller-owned request stream
  into a same-directory `.filename.{Guid:N}.tmp` file using
  `FileMode.CreateNew`, asynchronous I/O, flush, and an atomic overwrite move.
  Optional content type uses the same strategy in an adjacent
  `.filename.content-type` file. A null content type removes stale metadata.
- Failed stream copies preserve the prior object and metadata and remove their
  temporary files. Successful overwrite leaves no `.*.tmp` files.
- `OpenReadAsync` returns `null` for a missing object; otherwise it returns an
  asynchronous sequential-read `FileStream`, optional content type, and
  content length through `StorageReadResult`.
- `DeleteIfExistsAsync` returns `true` then `false` across repeated deletion and
  removes adjacent metadata. `GetObjectUri` returns `new Uri(targetPath)`.
- All operations derive a full path under the configured root and retain the
  legacy relative containment check. Container and prefix accept normalized
  relative paths only. Reparse points are rejected along every existing object
  and metadata path component, both before and after parent creation and again
  before commit. Pre-cancelled mutations do not create directories, replace
  objects, or delete objects.

## Strict TDD Evidence

### Options and registration RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~LocalStorageOptionsTests|FullyQualifiedName~LocalStorageRegistrationTests' \
  --nologo -v minimal
```

The first setup attempt was not accepted as RED because an unnecessary direct
test package reference triggered central-package error NU1010. After replacing
that setup with the repository's established test framework reference, the
identical required command exited 1 with CS0234 at both test files because
`Rvt.Storage.Local` did not exist. This is the accepted first RED.

### Options and registration GREEN

After adding only the project, binding/registration/startup behavior, and an
operation shell, the identical command passed 11/11.

### Local client RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~LocalObjectStorageClientTests --nologo -v minimal
```

The command exited 1. Eighteen of twenty cases failed on the deliberate
`NotImplementedException` operation shell. The two traversal rows passed
because Task 1 already rejects unsafe keys at the public contract boundary.

### Local client GREEN

After implementing filesystem behavior, the identical command passed 20/20,
including real directory-symlink and target-file-symlink cases on macOS with no
platform skips.

### Complete Local GREEN

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~Local --nologo -v minimal
```

Result: 31/31 passed, 0 failed, 0 skipped.

## Final Verification

- Locked restore of the storage test project and both provider projects
  succeeded.
- The complete `Rvt.Storage.Tests` project passed 44/44.
- The fresh required Local filter passed 31/31 with no skips.
- `Rvt.Storage.Local` built with 0 warnings and 0 errors.
- Package inspection showed exactly the three required direct
  Microsoft.Extensions abstraction packages at 10.0.9 and no cloud-provider
  SDK.
- `git diff --check` passed.

## Dependency and Lock Handling

The requested central versions for all three Microsoft.Extensions
abstraction packages were already present at `10.0.9`; they were reused without
editing or duplicating `Directory.Packages.props`. Locked restore generated
`Rvt.Storage.Local/packages.lock.json` and added only the Local project edge to
the existing storage test lock. No unrelated package version or lock was
changed.

## Files

Production:

- `libs/rvt-monitor-common/src/Rvt.Storage.Local/Rvt.Storage.Local.csproj`
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageOptions.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalObjectStorageClient.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageServiceCollectionExtensions.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageStartupValidationHostedService.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/packages.lock.json`

Tests:

- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalStorageOptionsTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalObjectStorageClientTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalStorageRegistrationTests.cs`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/packages.lock.json`

State and evidence:

- `project_state.md`
- `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-2-report.md`

## Carry-Forward Pending Work

- Task 3: extract the Azure Blob adapter.
- Task 4: extract the S3 adapter.
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
