# Task 7 Report: ReportingMonitor Storage Consumer Migration

## Outcome

ReportingMonitor now writes generated reports through the provider-neutral
`IObjectStorageClientFactory` and resolves persisted report links outside the
generic storage contract. The host composes exactly one Local, Azure Blob, or
S3 provider for the public `reporting-reports` resource.

The migration preserves:

- report bytes, filename/key, content type, and cancellation;
- Local `file:` URIs, Azure blob URIs, and S3 `s3:` URIs;
- the `pdfreports` container and `rvtreports` prefix defaults;
- provider precedence across `BlobStorage:Provider`, `RVT:BLOB_PROVIDER`,
  `RVT__BLOB_PROVIDER`, then Local;
- the legacy `BLOB_REPORT_CONTAINER_NAME` configuration alias;
- the existing host/configuration and communication composition graph.

`StorageWriteResult` remains provider-neutral and exposes only its key.
`MonitorBlobReportStorage` passes that returned key to
`IReportObjectUriResolver`. `ReportingDbClient` continues to persist
`request.ReportUri.ToString()` into `report.report_link`, so provider-specific
absolute URI formatting remains unchanged at the persistence boundary.

## Dependency boundary

`Rvt.Reporting.Storage` no longer references legacy
`Rvt.Monitor.Common` storage. Its only storage project reference is
`Rvt.Storage.Abstractions`; provider projects are referenced only by the
ReportingMonitor host composition project.

During final review, the broad `Microsoft.AspNetCore.App` framework reference
was removed from `Rvt.Reporting.Storage`. The library's existing
`SpaCustomerLogoClient` needs only `IOptions<T>`, so it now uses the narrow
`Microsoft.Extensions.Options` package reference, matching the adjacent
reporting-library dependency arrangement.

## TDD evidence

The initial rewritten focused tests established RED against the legacy
`IBlobStorageService` adapter and old composition. The provider-neutral
adapter, explicit provider composition, and resolver then made the focused
storage and architecture slice GREEN at 10/10. Final review removed the broad
framework reference and reran that same slice successfully.

Focused command:

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --filter 'FullyQualifiedName~MonitorBlobReportStorageTests|FullyQualifiedName~ReportingDependencyBoundaryTests' \
  --nologo -v minimal \
  -p:RestoreLockedMode=false \
  '-p:NuGetLockFilePath=/tmp/rvt-storage-task7-locks/$(MSBuildProjectName).packages.lock.json'
```

Result: 10 passed, 0 failed.

Complete non-environment-dependent ReportingMonitor command:

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --filter 'FullyQualifiedName!~TestReportingDbClient' \
  --nologo -v minimal \
  -p:RestoreLockedMode=false \
  '-p:NuGetLockFilePath=/tmp/rvt-storage-task7-locks/$(MSBuildProjectName).packages.lock.json'
```

Result: 74 passed, 0 failed.

The unfiltered suite compiled and ran 84 tests. It reported 74 passed and 10
failed; every failure is a PostgreSQL integration test that explicitly
requires the unavailable `RVT__POSTGRES_INTEGRATION_CONNECTION`.

## Restore and lock constraint

A clean ReportingMonitor restore still encounters the known repository-owned
`Microsoft.Extensions.Logging.Abstractions` 10.0.4/provider-transitive 10.0.9
conflict and stale tracked locks. Task 7 verification therefore used the
untracked
`apps/monitors/reportingmonitor/Directory.Packages.props` override to pin
Logging.Abstractions 10.0.9 and supply Options 10.0.9 to the two narrow
reporting projects. Lock output was redirected per project to
`/tmp/rvt-storage-task7-locks`.

The temporary package override, local package cache, and `/tmp` locks are not
part of the Task 7 commit. No central package policy or tracked lock file was
changed, and this report does not claim the repository locked-restore gate is
green.

## Scope

Legacy Common storage remains for Task 8. Portal storage, the independent
reporting service, central package policy, tracked locks, and other consumers
were not changed.
