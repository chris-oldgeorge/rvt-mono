# Storage Provider Split — Task 10 Report

## Outcome

Task 10 completes the storage provider split's human-facing documentation and
records final bounded verification without changing production code, tests,
project references, package policy, or package locks.

The documented source graph is:

- `Rvt.Storage.Abstractions`: provider-neutral streaming contracts and the
  named `IObjectStorageClientFactory`;
- `Rvt.Storage.Local`: local filesystem adapter;
- `Rvt.Storage.AzureBlob`: Azure Blob adapter and Azure SDK ownership;
- `Rvt.Storage.S3`: S3 adapter and AWS SDK ownership.

Svantek resolves `svantek-sound-recordings`; ReportingMonitor resolves
`reporting-reports`. Both hosts reference all three provider adapters only
because they deliberately retain deployment-time selection and compose exactly
one provider per named resource.

## Documentation-test RED decision

The repository has no appropriate semantic documentation test to extend. The
existing documentation-layout guard checks the move manifest and stale moved
paths, while the release-automation test checks release instructions. Making
either assert storage package names, configuration aliases, resource names, or
URI schemes would be a grep-only test coupled to prose, which the Task 10 brief
explicitly forbids.

The planned documentation RED prerequisite is therefore inapplicable. No test
file or test logic was added or changed.

An exploratory run of `verify-documentation-layout.test.sh` during the first
Task 10 attempt was obstructed by Markdown files in the preserved untracked
`apps/.nuget-packages` cache. That output did not evaluate these documentation
changes and is not treated as a semantic documentation gate.

## Documentation changes

- `apps/monitors/README.md` names all four storage packages, the two logical
  resources, every preserved Local/Azure/S3 configuration alias, the
  deployment-time selection rationale, report URI formats, and pending Portal
  and independent reporting-service work.
- `docs/modules/monitors/reportingmonitor/README.md` documents the
  `reporting-reports` named boundary, provider ownership, unchanged aliases,
  and Local `file:`, Azure HTTPS, and S3 `s3:` persisted links.
- `docs/operations/monitors/container-builds.md` replaces Common-storage
  wording with explicit adapter composition while preserving every current
  operator example.
- `docs/development/rvt-monitor-common/dependency-license-review.md` attributes
  Azure and AWS packages to `Rvt.Storage.AzureBlob` and `Rvt.Storage.S3`
  instead of `Rvt.Monitor.Common`.

## Fresh verification ledger

All .NET commands used `--no-restore`, `-m:1`, and
`-p:UseSharedCompilation=false` so tracked locks remained immutable and MSBuild
did not require sandbox-blocked parallel worker pipes.

| Command | Result | Classification |
| --- | --- | --- |
| `./tests/verify-mono-layout.test.sh` | exit 0 | Pass |
| `./tests/verify-mono-solution.test.sh` | exit 0 | Pass |
| `./tests/verify-rvt-common-source-boundary.test.sh` | exit 0 | Pass after the separately committed Task 7 guard correction |
| `./tests/verify-rvt-common-source-boundary-regression.test.sh` | exit 0 | Pass; Abstractions-only Reporting Storage and all four forbidden-reference mutations verified |
| `dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false` | 148 passed, 0 failed | Full storage suite pass |
| `dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --filter 'FullyQualifiedName!~MonitorDeliveryMigrationContractTests' --nologo -v minimal -m:1 -p:UseSharedCompilation=false` | 340 passed, 0 failed | Bounded pass; excludes only two known missing-migration-path tests |
| `dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore --filter 'FullyQualifiedName!~TestDBClient&FullyQualifiedName!~SvantekPostgreSqlSchemaPatchTests&FullyQualifiedName!~SvantekDependencyBoundaryTests' --nologo -v minimal -m:1 -p:UseSharedCompilation=false` | 93 passed, 0 failed | Bounded pass; excludes only absent-PostgreSQL and repository-root-sensitive fixtures |
| `dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --filter 'FullyQualifiedName!~TestReportingDbClient' --nologo -v minimal -m:1 -p:UseSharedCompilation=false` | 74 passed, 0 failed | Bounded pass using the preserved untracked 10.0.9 override; excludes only unavailable PostgreSQL tests |
| `dotnet build Rvt.Mono.slnx --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false -p:CustomAfterMicrosoftCommonTargets=/tmp/rvt-storage-task10-exclude-future.targets` | 76 warnings, 0 errors; exit 0 | Bounded pass with a temporary targets file excluding exactly the two preserved untracked Portal C# copies |
| `git diff --check` | exit 0; no output | Pass |

The first sandboxed storage-suite attempt did not execute tests: MSBuild
repeatedly failed to create its named-pipe server with
`SocketException (13): Permission denied`; the process was terminated after it
exceeded the bounded window and exited 143. The later single-node run outside
that restriction is the fresh 148/148 result above.

## Ordinary gate classifications

The existing full-suite evidence remains applicable and was not retried merely
to reproduce known environmental failures:

- Common: 340 passed, two failed because the retained SQL Server/PostgreSQL
  monitor-delivery migration files are absent.
- Svantek: 93 passed, 40 failed because PostgreSQL is unavailable and retained
  schema/boundary fixtures are repository-root sensitive.
- ReportingMonitor: 74 passed, ten failed because
  `RVT__POSTGRES_INTEGRATION_CONNECTION` is unavailable.
- ReportingMonitor clean locked restore still requires the release plan's
  Logging.Abstractions reconciliation. Its untracked 10.0.9 override is
  verification-only and is not committed.
- The ordinary root build remains blocked by the preserved untracked
  `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs`. Only the exact-path-excluded build
  is green.
- Atomic lock regeneration and package verification for the complete
  eleven-package graph remain owned by the separate provider-package release
  migration. Task 10 makes no package/lock gate claim.

## Final boundary searches

The brief's raw regex exits 0 because `BlobStorageOptions` is a substring of
the provider-owned replacement name `AzureBlobStorageOptions`. Every match is
inside the Azure provider or its consumers/tests.

The exact whole-symbol follow-up returns no matches:

```bash
rg -n \
  '\b(IBlobStorageService|BlobStorageWriteRequest|BlobStorageWriteResult|BlobStorageOptions|BlobStorageProvider|AddMonitorBlobStorage)\b' \
  apps libs services \
  --glob '*.cs' --glob '*.csproj'
```

The Common vendor-SDK search also returns no matches:

```bash
rg -n 'AWSSDK.S3|Azure.Identity|Azure.Storage.Blobs' \
  libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
```

## Future Pending Work

All seven approved items remain pending:

1. Migrate Portal `MonitorPictureStorage` and `SiteArchiveService` from
   `BlobStorageClientFactory` to `IObjectStorageClientFactory`, preserving
   protected streaming, Local fallback, atomic writes, existing `blob://`
   monitor references, persisted archive URLs, and report/archive container
   boundaries.
2. Decide whether Portal customer-logo storage should use the shared
   named-client contract.
3. Decide whether
   `services/reporting/src/Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`
   should adopt `Rvt.Storage.AzureBlob`; it remains an independent adapter.
4. Make an independent deprecation/removal decision for
   `apps/portal/RVT.Utilities/AzureBlobService.cs`.
5. Consider dynamic provider discovery only if deployments require installing
   a provider without rebuilding a host.
6. Consider external-consumer migration tooling only if coordinated
   major-version adoption proves insufficient.
7. Review database, MQTT, scheduling, and observability dependencies as
   separate boundary projects after the communication and storage splits.

Microsoft Graph large-attachment upload-chunk non-caller timeout translation
remains the carry-forward merge blocker. Completing the storage split does not
make the overall branch merge-ready.
