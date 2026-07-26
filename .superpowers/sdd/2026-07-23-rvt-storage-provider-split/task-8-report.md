# Task 8 Report: Remove Legacy Storage From Rvt.Monitor.Common

## Outcome

The legacy blob-storage API and Local, Azure Blob, and S3 implementations have
been removed from `Rvt.Monitor.Common`. The four Common test files that owned
that legacy surface have also been removed.

`Rvt.Monitor.Common.csproj` no longer references:

- `AWSSDK.S3`
- `Azure.Identity`
- `Azure.Storage.Blobs`

Their central versions remain in
`libs/rvt-monitor-common/Directory.Packages.props`, and the active provider
projects retain their SDK references. No provider project, central package
policy, tracked lock, Portal storage, or independent reporting-service storage
was changed.

## Active-consumer proof

Before deletion, an exact-symbol and namespace scan found every use of the
legacy Common API only in the 12 approved Common production files and four
approved Common storage test files.

Svantek and ReportingMonitor already reference `Rvt.Storage.Abstractions`,
`Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3` from their
host projects. Their production composition roots use `Rvt.Storage` contracts
and provider-owned options.

After deletion, this semantic exact-symbol scan returns exit code 1 with no
matches:

```bash
rg -n \
  '\b(IBlobStorageService|BlobStorageWriteRequest|BlobStorageWriteResult|BlobStorageOptions|BlobStorageProvider|AddMonitorBlobStorage|LocalFileBlobStorageService|AzureBlobStorageService|S3BlobStorageService)\b|Rvt\.Monitor\.Common\.Storage' \
  apps libs services \
  --glob '*.cs' --glob '*.csproj'
```

The brief's raw unbounded alternation does not return exit code 1 because
`BlobStorageOptions` is a substring of the replacement
`AzureBlobStorageOptions`, and `AzureBlobStorageService` is a substring of the
replacement `AzureBlobStorageServiceCollectionExtensions`. Its remaining
matches are exclusively those active provider-owned replacement names; they
are not uses of a legacy Common symbol.

The Common vendor scan returns exit code 1 with no matches:

```bash
rg -n \
  'AWSSDK.S3|Azure.Identity|Azure.Storage.Blobs|using Amazon|using Azure.Storage' \
  libs/rvt-monitor-common/src/Rvt.Monitor.Common \
  --glob '*.cs' --glob '*.csproj'
```

## TDD boundary evidence

Two repository-level assertions were added to
`StorageDependencyBoundaryTests`:

- `Common_ReferencesNoCloudProviderSdkPackages`
- `Common_ProductionSourceUsesNoCloudProviderNamespaces`

The package guard loads `Rvt.Monitor.Common` through the real project snapshot
and rejects `AWSSDK.S3`, `Azure.Identity`, and `Azure.Storage.Blobs`. The
production-source guard uses the existing semantic Roslyn analysis and rejects
`Amazon.*` and `Azure.Storage`.

The first focused run established RED before deletion:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundaryTests --nologo -v minimal
```

Result: 2 failed and 4 passed. The failures reported the active `AWSSDK.S3`
package reference and `Amazon.*` usage in
`Storage/S3BlobStorageService.cs`, proving both assertion paths exercised the
legacy boundary.

The identical command after deletion established GREEN: 6 passed, 0 failed,
0 skipped.

Independent review found that a trailing-dot namespace marker could miss a
root namespace import such as `using Amazon;`. The focused regression
`SourceAnalyzer_RootNamespaceMatchesChildNamespaceGuard` failed before the
matcher treated the marker root as part of the guarded namespace, then passed
after that hardening. The complete storage suite also remained green.

## Verification

- Complete `Rvt.Storage.Tests`: 148 passed, 0 failed.
- Complete `Rvt.Monitor.CommonTests`: 340 passed, 2 failed. Both failures are
  pre-existing repository-layout assertions for the missing legacy dual-provider
  monitor-delivery migration files. Excluding only
  `MonitorDeliveryMigrationContractTests` passes 340/340.
- Complete `SvantekMonitorTests`: 93 passed, 40 failed. Failures are the known
  absent `RVT__POSTGRES_INTEGRATION_CONNECTION` and pre-existing
  repository-root-sensitive schema/boundary fixtures. The storage composition
  and sound-recording slice passes 11/11.
- Complete `ReportingMonitorTests`: 74 passed, 10 failed. All ten failures
  explicitly require the absent `RVT__POSTGRES_INTEGRATION_CONNECTION`.
  Excluding only `TestReportingDbClient` passes 74/74.
- `git diff --check`: clean.

All four prescribed full commands compiled the changed Common library and
their consumer graphs. The bounded green results distinguish environmental
and repository-layout constraints from this storage-removal change.

## Locks and residual constraints

The prescribed restore-capable test commands rewrote tracked lock files in the
working tree. Those exact test-generated lock diffs were restored to `HEAD`
before commit. The final Task 8 diff contains no tracked lock change.

The untracked
`apps/monitors/reportingmonitor/Directory.Packages.props` override remains
preserved. The known ReportingMonitor clean locked-restore conflict remains
release-plan work; Task 8 does not claim that gate is green. The untracked
local package cache, temporary Portal duplicate files, documentation duplicate,
and `.codegraph/` directory also remain unmodified and excluded.
