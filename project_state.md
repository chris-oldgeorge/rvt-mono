# Project State

## Provider package release migration Task 1 - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Worktree: `.worktrees/release-platform-hardening`. Task 1 starts from the
  approved provider source-split head `e8089dd`.
- `libs/rvt-monitor-common/release/package-catalog.tsv` is the release source of
  truth for exactly eleven ordered package IDs and their real project paths:
  Common, IntegrationTesting, five Communication packages, and four Storage
  packages.
- The clean-split `PackageVersion` default is `1.0.0-rc.1`; `Version` derives
  from it. `PinSynchronizedRvtProjectReferenceVersions` runs for packable
  projects after `_GetProjectReferenceVersions` and before `GenerateNuspec`,
  updating every `_ProjectReferencesWithVersions` item whose `Filename` begins
  `Rvt.` so `ProjectVersion` is exact `[$(PackageVersion)]`.
- Test variables are `PackageRoot`, `Artifacts`, `rows`, and
  `expectedPackageIds`. `PackageRoot` resolves
  `libs/rvt-monitor-common`; the TSV parser requires exactly two literal
  tab-separated columns, exact ordered IDs, and an existing project for every
  path.
- Strict TDD evidence: focused RED compiled and failed 0/1 with
  `DirectoryNotFoundException` while the catalog was absent. The identical
  no-restore slice passes 1/1 after the minimal catalog implementation. The
  required SendGrid MSBuild property probe prints `1.0.0-rc.1`.
- A no-restore SendGrid pack probe in `/private/tmp` succeeds and emits the real
  `Rvt.Communication.Abstractions` dependency as exact `[1.0.0-rc.1]`.
  SendGrid remains `9.29.3`.
- `Directory.Packages.props` was reviewed and intentionally remains unchanged:
  no obsolete infrastructure-only entry is present, every entry is referenced,
  and `AWSSDK.S3` `4.0.100.3`, `Azure.Identity` `1.15.0`,
  `Azure.Storage.Blobs` `12.25.0`, and `SendGrid` `9.29.3` are retained.
- No lockfile or active-consumer reference changed. All unrelated untracked
  files remain preserved. Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-provider-package-release-migration/task-1-report.md`.

## Storage provider split merge-blocker fix - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- This corrective scope starts from storage final-review commit `7804962`.
  Microsoft Graph large-attachment upload chunks now distinguish caller
  cancellation from provider/HTTP timeouts exactly like authenticated Graph
  requests: a cancelled caller token still propagates cancellation, while an
  `OperationCanceledException` with an active caller token becomes the
  secret-safe `EmailDeliveryException("MicrosoftGraph",
  DeliveryFailureKind.Transient, "Timeout")`.
- Strict Graph TDD evidence:
  - corrected RED ran the two upload-chunk cases with production unchanged:
    caller cancellation passed and the timeout test failed because exact
    `EmailDeliveryException` was expected but raw
    `OperationCanceledException` escaped;
  - after the single non-caller cancellation catch, the identical two-test
    slice passed 2/2;
  - the complete Microsoft Graph adapter project passed 37/37 and the bounded
    neutral Communication project passed 31/31.
  Test variables are `providerMessage`, `cancellation`, and `attachment`;
  `UploadChunkCancellationHandler` reaches the real PUT chunk boundary after
  draft and upload-session creation.
- The dependency license table now classifies
  `Microsoft.Extensions.Configuration.Abstractions` 10.0.9 as direct. Its
  version, approval, license metadata, and the 101-pair inventory are
  unchanged.
- No package policy, project file, or lock changed. All unrelated untracked
  files remain preserved. The first corrected RED retry inside the restricted
  sandbox was aborted because vstest could not bind its local communication
  socket; the bounded rerun outside that restriction produced the required
  1-pass/1-fail RED evidence.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/merge-blocker-fix-report.md`.

## Storage provider split final review fix - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- This corrective scope starts from reviewed storage documentation head
  `0539691`. It resolves the whole-plan findings for Azure/S3 non-caller
  cancellation, source-split lock/catalog scope, and the 20-project dependency
  inventory. It does not push or expand any Future Pending Work.
- `AzureBlobObjectStorageClient` and `S3ObjectStorageClient` now translate a
  provider-thrown `OperationCanceledException` to the secret-safe neutral
  `ObjectStorageException` with `StorageFailureKind.Unavailable`, the configured
  resource name, the requested `StorageObjectKey`, and the provider exception
  retained only as `InnerException`. The existing first catch filter still
  rethrows cancellation unchanged when the supplied caller token is cancelled.
  Status classification, not-found handling, streaming leases, and S3
  disposal are unchanged.
- Strict provider TDD evidence:
  - focused RED compiled and failed all 6 new Azure/S3
    `WriteAsync`/`OpenReadAsync`/`DeleteIfExistsAsync` cases because exact
    `ObjectStorageException` was expected and raw
    `OperationCanceledException` escaped;
  - after the minimal catches, the same 6 cases plus both existing
    caller-cancellation controls passed 8/8;
  - the full storage suite passed 154/154.
  Test variables are `providerMessage`, `cancellation`, and `expectedKey`.
- Source-split scope is restored: the five locks formerly tracked under
  `Rvt.Storage.Abstractions`, `Rvt.Storage.Local`,
  `Rvt.Storage.AzureBlob`, `Rvt.Storage.S3`, and `Rvt.Storage.Tests` are
  deleted. No other repository lock is modified. Complete atomic lock
  regeneration remains delegated to the provider-package release migration.
- The conditional `Microsoft.CodeAnalysis.CSharp` catalog entry and private
  storage-test reference are removed. The package-free boundary analyzer uses
  BCL regular expressions and a lexical sanitizer to discard comments and
  literal text, retain executable interpolation holes, resolve local/global
  aliases across source files, distinguish the covered user-defined
  filesystem lookalikes, and report source paths for real dependencies.
  Existing dependency-boundary regressions all remain and the focused boundary
  slice passes 13/13. Analyzer variables are `AliasUsingPattern`,
  `NamespaceUsingPattern`, `QualifiedNamePattern`, `DeclaredTypePattern`,
  `ImplicitSystemIoTypes`, `sanitizedSources`, `aliases`, and
  `filesByDependency`.
- Temporary restore classification:
  - the planned `RestorePackagesWithLockFile=false` solution attempt was
    bounded and failed immediately with `NU1005`, because .NET 10 rejects that
    setting while other solution projects retain checked-in locks;
  - a serial retry with `RestorePackagesWithLockFile=true`,
    `RestoreLockedMode=false`, and `NuGetLockFilePath` redirected to the
    isolated `/private/tmp/rvt-storage-final-fix-locks.ks9vR8` location
    restored all 20 projects without a tracked lock write;
  - refreshed assets contain 101 distinct package/version pairs and no
    CodeAnalysis compiler package.
- `dependency-license-review.md` now exactly matches those 101 pairs and
  attributes Azure/AWS dependencies to `Rvt.Storage.AzureBlob` and
  `Rvt.Storage.S3`. The refreshed vulnerability-audit attempt was bounded but
  DNS-blocked before a result because `api.nuget.org` was unavailable; no
  current audit-clean claim is made.
- Repository verification passes mono layout, mono solution, RVT common source
  boundary, and its mutation regression companion. No storage lock exists in
  the working tree, no `Microsoft.CodeAnalysis.CSharp` occurrence remains in
  project/package catalog source, and `git diff --check` is clean.
- Existing MSTest analyzer warnings remain outside this correction.
  Microsoft Graph large-attachment upload-chunk non-caller timeout translation
  remains the external carry-forward merge blocker; this storage fix does not
  make the overall provider branch merge-ready.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/final-review-fix-report.md`.

## Storage provider Task 10 - final verification and documentation - 2026-07-26 (complete)

- Storage operator documentation now names the provider-neutral
  `Rvt.Storage.Abstractions` contract and the `Rvt.Storage.Local`,
  `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3` adapters. Consumers resolve
  `IObjectStorageClient` through the named `IObjectStorageClientFactory`
  contract; Svantek uses `svantek-sound-recordings` and ReportingMonitor uses
  `reporting-reports`.
- Svantek and ReportingMonitor reference all three adapter projects only to
  retain deliberate deployment-time selection among Local, Azure Blob, and
  S3. Each host composes exactly one provider for its named resource.
  Existing `BlobStorage:*`, `RVT:*`, and literal `RVT__*` configuration aliases,
  defaults, and legacy container fallbacks are unchanged.
- ReportingMonitor continues to resolve its persisted report URI outside the
  generic storage port. `report.report_link` retains Local `file:`, Azure HTTPS,
  and S3 `s3:` absolute formats.
- `Rvt.Monitor.Common` owns no storage implementation and references none of
  `AWSSDK.S3`, `Azure.Identity`, or `Azure.Storage.Blobs`. The dependency
  license review now attributes Azure SDK dependencies to
  `Rvt.Storage.AzureBlob` and AWS SDK dependencies to `Rvt.Storage.S3`;
  Abstractions and Local remain cloud-SDK independent.
- No behaviorally useful documentation-assertion test exists for these
  operator statements. The repository documentation tests cover move-manifest
  layout and release-automation instructions; extending either would be the
  prohibited grep-only source-text coupling. Task 10's planned documentation
  RED prerequisite is therefore inapplicable, and no test logic changed.
- Fresh shell verification passes
  `verify-mono-layout.test.sh`, `verify-mono-solution.test.sh`,
  `verify-rvt-common-source-boundary.test.sh`, and the new
  `verify-rvt-common-source-boundary-regression.test.sh`. Final
  `git diff --check` has no output.
- Bounded, no-restore, single-node verification passes storage 148/148,
  Common 340/340 excluding only the two known
  `MonitorDeliveryMigrationContractTests`, Svantek 93/93 excluding only its
  unavailable PostgreSQL/repository-root fixtures, and ReportingMonitor 74/74
  excluding only `TestReportingDbClient`. Reporting uses the preserved
  untracked Logging.Abstractions/Options 10.0.9 verification override.
- The established ordinary full-suite classifications remain unchanged:
  Common has 340 passes and two missing-migration-path failures; Svantek has
  93 passes and 40 absent-PostgreSQL/repository-root-sensitive failures;
  ReportingMonitor has 74 passes and ten tests requiring
  `RVT__POSTGRES_INTEGRATION_CONNECTION`. Those known blockers were not
  retried as green gates.
- A bounded root build imported a temporary targets file that removed exactly
  the two preserved untracked Portal C# copies. It succeeds with 76 existing
  analyzer/advisory warnings and 0 errors. The ordinary root build remains
  blocked by those two `* 2.cs` files and is not claimed green.
- The brief's raw legacy-symbol regex matches only the provider-owned
  replacement identifier `AzureBlobStorageOptions`; an exact whole-symbol
  search returns no legacy symbol matches. The Common project vendor-SDK
  search also returns no matches.
- Package release verification remains pending rather than green. Task 10
  changes no package policy or lock, and the complete eleven-package
  release/lock migration still owns atomic lock regeneration and the clean
  ReportingMonitor locked-restore reconciliation.
- Future Pending Work remains pending and outside this implementation:
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
- Microsoft Graph large-attachment upload-chunk non-caller timeout translation
  remains the carry-forward merge blocker. Storage completion does not make
  the overall branch merge-ready.
- Full Task 10 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-10-report.md`.

## Storage provider Task 7 boundary-guard follow-up - 2026-07-26 (complete)

- `Rvt.Reporting.Storage` was already correctly migrated in Task 7 to reference
  `Rvt.Storage.Abstractions` only for storage. The shared
  `verify-rvt-common-source-boundary.sh` guard incorrectly retained its former
  `Rvt.Monitor.Common` requirement.
- The guard now requires the Abstractions project and rejects a Common project
  reference. Review fix round 1 also rejects Local, Azure Blob, and S3 provider
  references, enforcing that Reporting Storage depends only on Abstractions for
  storage. Its isolated behavioral regression first failed on the stale Common
  requirement, then on the missing Local-provider rejection, and now passes all
  four forbidden-reference mutations; the normal source-boundary test also
  passes.
- This corrective guard/test-only change does not alter Task 10's four pending
  documentation edits, consumer source, package policy, locks, or future
  pending work. The Graph upload-chunk timeout translation remains the
  carry-forward merge blocker.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-7-boundary-fix-report.md`.

## Storage provider Task 9 - source solution wiring - 2026-07-26 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 9 starts from the
  Task 8 commit `6b678a5`. The four production storage projects and
  `Rvt.Storage.Tests` are represented exactly once in both
  `libs/rvt-monitor-common/rvt-common.sln` and `Rvt.Mono.slnx`.
- `apps/monitors/rvt-monitors.sln` contains the four production storage
  projects, but not `Rvt.Storage.Tests`. This reflects the active Svantek and
  ReportingMonitor hosts, which each directly reach Abstractions, Local,
  Azure Blob, and S3.
- Strict guard evidence: before solution wiring,
  `tests/verify-mono-solution.test.sh` failed because the solution had 46
  projects while the repository module inventory had 51. After the solution
  edits, the unchanged guard passes. The three `dotnet sln ... list` commands
  show each intended storage project once.
- Restore verification redirected every lock to
  `/tmp/rvt-storage-task9-locks.gI98g4` with
  `RestorePackagesWithLockFile=true`, `RestoreLockedMode=false`, and
  `--disable-parallel`. Both `rvt-common.sln` and `Rvt.Mono.slnx` restored
  successfully without rewriting a tracked repository lock.
- `rvt-common.sln` builds with 0 errors and 64 existing analyzer warnings.
  The first unmodified root build reached and compiled all five new storage
  entries but was blocked by duplicate types from the preserved untracked
  Portal `* 2.cs` copies. A temporary MSBuild import under `/tmp`, excluding
  only those future-pending copies, allowed `Rvt.Mono.slnx` to build with 0
  errors and 76 existing analyzer/advisory warnings.
- No package catalog, permanent central version, repository lock, solution
  guard, Portal/reporting-service source, or ReportingMonitor override is part
  of Task 9. The untracked ReportingMonitor `Directory.Packages.props` remains
  a verification-only override, and complete lock regeneration remains owned
  by the later provider-package release migration plan.
- The Graph large-attachment upload-chunk non-caller timeout translation
  remains the carry-forward merge blocker. Portal/reporting-service storage
  migration and every other documented future-pending item remain unchanged.
- Full Task 9 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-9-report.md`.

## Storage provider Task 8 - Common legacy removal - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 8 starts from the
  Task 7 commit. The 12 legacy files under
  `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/` and their four
  Common storage test files are removed.
- `Rvt.Monitor.Common.csproj` no longer references `AWSSDK.S3`,
  `Azure.Identity`, or `Azure.Storage.Blobs`. Central versions remain in
  `libs/rvt-monitor-common/Directory.Packages.props`; the S3 and Azure Blob
  provider projects retain their SDK references.
- The new persistent guard loads
  `StorageProjectSnapshot.Load("Rvt.Monitor.Common")`.
  `Common_ReferencesNoCloudProviderSdkPackages` forbids the three SDK package
  identities above, while
  `Common_ProductionSourceUsesNoCloudProviderNamespaces` forbids the semantic
  dependency markers `Amazon.` and `Azure.Storage`.
- Strict TDD evidence: before deletion, the focused boundary command failed
  the two new assertions for `AWSSDK.S3` and the Common S3 source namespace,
  with four existing boundary tests passing. After deletion, the identical
  command passes 6/6. Review hardening added a root-namespace regression that
  failed before `Amazon.` also matched `using Amazon;`, then passed after the
  semantic matcher fix.
- The exact legacy-symbol/Common-namespace scan and the Common vendor scan both
  return no matches. The brief's raw substring regex still matches only
  provider-owned replacement names containing `BlobStorageOptions` or
  `AzureBlobStorageService`; this is a command-pattern false positive, not a
  residual legacy Common API use.
- Complete storage tests pass 148/148. The bounded Common set passes 340/340,
  Svantek storage composition/upload passes 11/11, and ReportingMonitor's
  non-DB set passes 74/74.
- Full Common reports 340 passed and two pre-existing missing-migration-file
  failures. Full Svantek reports 93 passed and 40 absent-PostgreSQL/stale-root
  fixture failures. Full ReportingMonitor reports 74 passed and ten failures
  that explicitly require `RVT__POSTGRES_INTEGRATION_CONNECTION`.
- Restore-capable verification temporarily rewrote tracked locks; those exact
  diffs were restored to `HEAD`. No central version, provider SDK reference,
  tracked lock, Portal storage, independent reporting-service source, or
  unrelated file is included in Task 8.
- The untracked ReportingMonitor `Directory.Packages.props` override remains
  preserved. Its clean locked-restore conflict remains release-plan work and
  is not claimed green.
- Full Task 8 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-8-report.md`.

## Storage provider Task 7 - ReportingMonitor report storage - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 7 starts from the
  Task 6 commit `ab7e5e0`. ReportingMonitor now writes through the named
  provider-neutral `IObjectStorageClientFactory` resource
  `reporting-reports`; report bytes, filename/key, content type, cancellation,
  and the provider-returned normalized key are preserved.
- The ReportingMonitor host deliberately references and composes exactly one
  Local, Azure Blob, or S3 provider. Selection retains the Svantek precedence,
  blank-value fallback, case-insensitive names, Local default, and exact safe
  unsupported-provider message.
- Reporting defaults remain container `pdfreports` and prefix `rvtreports`,
  including the legacy `BLOB_REPORT_CONTAINER_NAME` alias. A concrete-client
  `IReportObjectUriResolver` preserves Local `file:`, Azure blob, and S3 `s3:`
  URI formats outside `IObjectStorageClient`; `StorageWriteResult` still
  exposes only a key.
- Persisted URI behavior is unchanged: `ReportGenerationService` forwards the
  resolved URI and `ReportingDbClient` stores
  `request.ReportUri.ToString()` in `report.report_link`.
- `Rvt.Reporting.Storage` references only `Rvt.Storage.Abstractions` for
  storage. Its broad interim `Microsoft.AspNetCore.App` reference was removed;
  the existing logo client now receives `IOptions<T>` through a narrow
  `Microsoft.Extensions.Options` package reference.
- Strict TDD evidence includes the initial focused compile failure against the
  legacy blob interface followed by the provider-neutral GREEN slice. Final
  focused storage/architecture verification passes 10/10, and the complete
  non-environment ReportingMonitor set passes 74/74.
- The unfiltered ReportingMonitor suite compiles and runs 84 tests: 74 pass;
  10 PostgreSQL integration tests remain unavailable because
  `RVT__POSTGRES_INTEGRATION_CONNECTION` is not set.
- Verification used an untracked ReportingMonitor `Directory.Packages.props`
  override for Logging.Abstractions/Options 10.0.9 and per-project
  `NuGetLockFilePath` output under `/tmp/rvt-storage-task7-locks`. No tracked
  lock or central package policy is changed, and the repository locked-restore
  gate is not claimed green.
- Legacy Common storage removal remains Task 8; solution/packaging and final
  verification remain Tasks 9 and 10. Portal and the independent reporting
  service remain out of scope.
- Full Task 7 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-7-report.md`.

## Storage provider Task 6 - Svantek sound recordings - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 6 starts from the
  Task 5 review baseline commit `56fbe64`. The previously completed
  `MonitorHost` configuration callback migration was preserved unchanged.
- Svantek now references `Rvt.Storage.Abstractions`, Local, Azure Blob, and S3
  directly. `AddSvantekMonitor(IConfiguration)` composes exactly one provider
  for the named `svantek-sound-recordings` resource.
- Provider selection preserves the existing precedence and blank-value
  fallback across `BlobStorage:Provider`, `RVT:BLOB_PROVIDER`,
  `RVT__BLOB_PROVIDER`, then Local. Values are case-insensitive. Unsupported
  values fail at composition with the exact allowed-provider message. Local
  defaults remain `/data/rvt/blobs/audiofiles` with an empty prefix.
- `CheckForSoundRecordingsHandler` accepts `IObjectStorageClientFactory`,
  resolves its named client once during construction, and writes the existing
  `{NotificationId}.wav` key through a read-only `MemoryStream`. Bytes,
  `audio/wav`, cancellation, and the database recording-link update remain
  unchanged. Direct `SvantekApi` construction without a storage dependency
  retains a lazy, explicit missing-storage failure through a missing
  factory/client pair.
- Strict TDD evidence: composition first failed compilation on the absent
  storage references/composition root, then passed 6/6. A follow-up blank-key
  precedence regression failed 1/7 before its minimal fallback fix, then the
  composition slice passed 7/7. The streaming test rewrite failed compilation
  with five expected factory-to-legacy-port mismatches, then passed 4/4 after
  production migration.
- The shared Omnidots host scheduling guard passed 13/13. Svantek
  communication/options/cancellation composition passed 20/20, and the
  complete runnable non-environment Svantek set passed 93/93. The unfiltered
  suite compiled and ran 133 tests: 93 passed; 40 remain blocked by the absent
  PostgreSQL integration connection and pre-existing repository-root-sensitive
  schema/boundary fixtures.
- The Svantek host builds with zero warnings and errors. `git diff --check`
  passes.
- No package lock, Common legacy storage source, Portal/reporting-service
  source, or host callback source changed. Common storage removal remains Task
  8 work; ReportingMonitor migration and solution/packaging/final verification
  remain Tasks 7, 9, and 10.
- Full Task 6 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-6-report.md`.

## Storage provider Task 5 - contract parity and dependency isolation - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 5 starts from Task 4
  commit `65608a4`. The storage test project now has one reusable eight-case
  `IObjectStorageClient` contract inherited by real Local, Azure Blob, and S3
  provider fixtures.
- The Local contract fixture uses a unique real temporary filesystem root.
  Azure and S3 instantiate the concrete provider clients and use strict,
  stateful SDK-boundary doubles backed by ordinal in-memory object
  dictionaries; no client under test is replaced and no network is used.
- Shared parity covers non-seekable writes and normalized returned keys,
  streamed bytes/content type/content length, missing reads, overwrite,
  idempotent delete, and pre-cancelled write/read/delete. Each cancellation
  case performs a later uncancelled read to prove the original provider object
  did not mutate.
- Dependency guards parse the four storage project files and scan only their
  production C# sources, explicitly excluding `obj`/`bin`. They keep
  Abstractions provider/framework/filesystem independent, Local cloud-SDK
  independent, Azure limited to its Azure SDK dependencies, S3 limited to its
  AWS SDK dependency, and all providers directly referenced only to
  Abstractions.
- Review hardening makes Azure raw-response disposal directly observable and
  proves S3 response-lease disposal through the response stream's distinct
  second disposal. MSBuild dependency parsing handles `Include`, `Update`, and
  `Remove` identities in order. Round 2 replaces the interim lexer with one
  project-wide Roslyn compilation: semantic symbols include interpolation
  holes and cross-file global aliases while excluding comments, literal text,
  and user-defined filesystem lookalikes.
- Test-first evidence: the shared fixture shells failed 24/24 before their
  real implementations and then passed 24/24. The boundary snapshot shell
  failed 4/4 before implementation and then passed 4/4. The complete storage
  suite initially passed 137/137. Review hardening failed its five focused
  regressions before implementation, then passed 5/5; the expanded contract,
  boundary, and full suites passed 26/26, 7/7, and 142/142 respectively.
  Round 2 then failed three of six focused semantic regressions before
  implementation and passed 6/6 after it; the final boundary, contract, and
  full suites passed 10/10, 26/26, and 145/145. `git diff --check` passed.
- `Microsoft.CodeAnalysis.CSharp` 5.0.0 is centrally conditioned to the
  `Rvt.Storage.Tests` project and referenced with `PrivateAssets="all"`.
  Its direct/transitive graph is recorded only in the storage test lock; no
  provider project or provider lock changed.
- No real provider inconsistency surfaced, so no provider production code,
  provider package version, or provider lock changed. The test-only Roslyn
  central entry and `Rvt.Storage.Tests` lock are the only package/lock changes.
  Tasks 6-10 remain pending: consumer migrations, legacy Common storage
  removal, solution/packaging integration, and final
  verification/documentation. Portal storage and the independent
  `services/reporting` Azure adapter remain future work.
- Full Task 5 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-5-report.md`.

## Storage provider Task 4 - S3 adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 4 starts from Task 3
  commit `e7e6e5b`. `Rvt.Storage.S3` is now a separate packable net10 provider
  project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` S3 implementation remains present and unchanged for
  Task 8.
- `S3StorageOptions` binds `Bucket`, `Prefix`, `Region`, `ServiceUrl`, and
  `ForcePathStyle` from the current provider-neutral, `RVT:`, and literal
  `RVT__` aliases. `Bucket` is required when the named client resolves;
  prefixes use the shared traversal-safe key normalization; service URLs must
  be absolute. No access-key, secret-key, credential, or token option exists.
- `S3ObjectStorageClient` constructs `AmazonS3Config` with the current exact
  endpoint behavior: region-only configuration sets `RegionEndpoint`;
  compatible-S3 configuration sets normalized `ServiceURL` and, when supplied,
  trimmed `AuthenticationRegion`. It uses `new AmazonS3Client(config)` so the
  SDK default credential chain remains in effect.
- `AddRvtS3Storage` registers one keyed singleton client, one named
  `ObjectStorageClientRegistration`, the shared client factory, and startup
  validation through `IHostedService`, matching the Local and Azure provider
  composition pattern.
- Writes pass the original request stream to `PutObjectAsync` with
  `AutoCloseStream = false`, copy optional content type, and prefix provider
  keys. Reads return the raw `GetObjectResponse.ResponseStream`, content type,
  and length without buffering and retain the response as the shared disposal
  lease. Deletes probe metadata first, return `false` for `NoSuchKey` or 404,
  and otherwise delete the same bucket/key and return `true`.
- S3 failures classify 403 as `AccessDenied`, 409 as `Conflict`, 408, 429, and
  5xx as `Unavailable`, other 4xx as `InvalidRequest`, and the remainder as
  `Unknown`. Caller cancellation propagates. Shared messages omit provider
  response and inner exception text. Provider URIs escape each prefix/key path
  segment independently.
- Strict TDD evidence: options/registration first failed compilation because
  the S3 provider was absent, then passed 18/18. Client behavior then failed
  13/15 on the deliberate unimplemented operation shell (URI and disposal
  behavior already passed), then passed 15/15. The complete S3 filter passed
  33/33, the complete storage project passed 109/109, and the provider built
  with zero warnings and errors. All AWS operation tests use strict
  `IAmazonS3` doubles and no network.
- Existing central `AWSSDK.S3` 4.0.100.3 and Microsoft.Extensions 10.0.9
  versions were reused unchanged. Locked restore added only the new S3
  provider lock and the S3 project/AWS SDK graph in the storage test lock.
- Tasks 5-10 remain pending. Provider parity and dependency-boundary tests,
  consumer migrations, legacy Common storage removal, solution/packaging work,
  Portal storage, the independent `services/reporting` Azure adapter, and all
  other future-pending work remain excluded and unchanged.
- Full Task 4 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-4-report.md`.

## Storage provider Task 3 - Azure Blob adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 3 starts from Task 2
  commit `406f057`. `Rvt.Storage.AzureBlob` is now a separate packable net10
  provider project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` Azure storage implementation remains present and
  unchanged for Task 8.
- `AzureBlobStorageOptions` preserves provider-neutral, `RVT:`, literal
  `RVT__`, audio-folder, and custom reporting-container aliases and precedence.
  A connection string takes precedence over the service URI; otherwise the
  provider requires an absolute service URI and uses
  `DefaultAzureCredential`. Containers are required and trimmed, and prefixes
  use the shared traversal-safe key normalization.
- `AddRvtAzureBlobStorage` follows the Local provider pattern: one keyed
  singleton client, one named registration, the shared client factory, and
  startup validation through `IHostedService`.
- `AzureBlobObjectStorageClient` streams the original request stream to Azure
  after `CreateIfNotExistsAsync`, applies optional content type, and returns
  only the provider-neutral key. Reads stream `DownloadStreamingAsync` content
  and metadata without buffering, return `null` for 404, and retain the raw
  Azure response as the shared disposal lease. Deletes return the SDK boolean;
  `GetObjectUri` remains concrete-provider API.
- Azure failures classify 403 as `AccessDenied`, 409 as `Conflict`, and 408,
  429, and 5xx as `Unavailable`; caller cancellation propagates. Shared
  exception messages never copy Azure response or inner exception text.
- Strict TDD evidence: options/registration first failed compilation because
  the Azure provider was absent, then passed 18/18. Client behavior then failed
  14/14 on the deliberate unimplemented operation shell, then passed 14/14.
  The complete Azure filter passed 32/32, the complete storage project passed
  76/76, and the provider built with zero warnings and errors. All Azure
  operation tests use strict SDK doubles and no network.
- Existing central Azure/Microsoft.Extensions versions were reused unchanged.
  Locked restore added only the new Azure provider lock plus the Azure project
  graph and strict-test Moq graph in the storage test lock.
- Tasks 4-10 remain pending. S3 extraction, parity and architecture tests,
  consumer migration, legacy Common storage removal, solution/packaging work,
  Portal storage, and the independent `services/reporting` Azure adapter remain
  excluded and unchanged.
- Full Task 3 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-3-report.md`.

## Storage provider Task 2 - Local filesystem adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 2 starts from Task 1
  commit `da0dfd2`. `Rvt.Storage.Local` is now a separate packable net10
  provider project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` storage implementation remains present and unchanged for
  Task 8.
- `LocalStorageOptions` preserves the local defaults and provider-neutral,
  `RVT:`, literal `RVT__`, audio-folder, and reporting-container aliases.
  `AddRvtLocalStorage` registers a named keyed singleton client, one named
  registration, the shared client factory, and startup validation through
  `IHostedService`.
- `LocalObjectStorageClient` streams request content into same-directory
  create-new temporary files, flushes before atomic overwrite, removes
  temporary files on success/failure, and stores optional content type in an
  adjacent atomically replaced metadata file. Reads return async sequential
  file streams; delete reports existence and removes metadata.
- Local filesystem access retains rooted containment and pre/post-create
  reparse-point checks for object and metadata paths. Real-filesystem tests
  cover unsafe container/prefix values, traversal rejection at the validated
  key boundary, directory and target-file symlinks, failed-copy preservation,
  overwrite, cleanup, missing reads, idempotent delete, and cancellation before
  mutation.
- Strict TDD evidence: options/registration first failed compilation because
  `Rvt.Storage.Local` was absent, then passed 11/11. Client behavior then failed
  18/20 on the deliberate unimplemented operation shell (the two key-boundary
  rows already passed), then passed 20/20. The complete Local filter passed
  31/31.
- The required Microsoft.Extensions `10.0.9` central versions already existed
  and were not duplicated or changed. Locked restore added only the new Local
  project lock and the Local project edge in the storage test lock.
- Tasks 3-10 remain pending. Portal blob unification, the independent
  `services/reporting` Azure adapter, all communication release/lock work, and
  every other previously documented future-pending item remain excluded and
  unchanged.
- Full Task 2 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-2-report.md`.

## Storage provider Task 1 - provider-neutral streaming contracts - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; base before Task 1 was
  `0b655b6`. `Rvt.Storage.Abstractions` now provides validated normalized object
  keys, streaming write/read contracts, named logical-resource lookup, shared
  failure classification, and secret-safe storage exceptions under the
  `Rvt.Storage` namespace.
- `StorageObjectKey` trims and normalizes separators while rejecting empty,
  rooted, UNC, Windows-drive-rooted, dot-segment, and traversal names.
  `ObjectStorageClientFactory` uses ordinal resource matching, rejects blank
  names and duplicates, and does not enumerate registrations in unknown-name
  errors. `StorageReadResult` asynchronously disposes content before disposing
  its provider lease from a `finally` block.
- Strict TDD evidence: the key slice first failed compilation because
  `StorageObjectKey` was absent, then passed 8/8. The remaining abstraction
  slice first failed compilation because the client/request/result contracts
  were absent, then the complete abstraction filter passed 13/13.
- The net10 abstraction project has no direct or transitive packages and no
  configuration, dependency-injection, provider SDK, or filesystem APIs. The
  test project directly references only Microsoft.NET.Test.Sdk,
  MSTest.TestAdapter, MSTest.TestFramework, and the abstraction project.
- Tasks 2-10 remain pending: Local, Azure Blob, and S3 provider extraction;
  provider parity and dependency-boundary enforcement; Svantek and
  ReportingMonitor consumer migration; legacy Common storage removal; solution
  and packaging integration; and final verification/documentation. Portal blob
  unification and the independent `services/reporting` Azure adapter remain
  excluded future work, along with all previously recorded non-storage pending
  work.
- Full Task 1 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-1-report.md`.

## Communication final-review HTTP hardening - 2026-07-25 (complete)

- Microsoft Graph mail and TransmitSMS keep their singleton delivery-port
  composition, while their DI-held adapters now retain `IHttpClientFactory`
  rather than transient typed clients. Each delivery creates and disposes one
  named factory-managed `HttpClient`, preventing process-long client capture
  without changing the singleton neutral workflow API. Public direct
  `HttpClient` adapter constructors remain available.
- Microsoft Graph non-caller HTTP cancellation now maps to a safe transient
  `EmailDeliveryException` with code `Timeout`; caller-requested cancellation
  still propagates. Malformed successful draft and upload-session JSON maps to
  the existing safe permanent `InvalidDraftResponse` and
  `InvalidUploadSession` codes without response content in exception text.
- Strict TDD RED: Graph focused tests failed 4/5 for the exact timeout,
  malformed-response, and retained-client symptoms while caller cancellation
  passed; TransmitSMS lifetime failed 1/1 with request client IDs `[1,1]`
  instead of `[1,2]`. GREEN: the same Graph tests passed 5/5 and TransmitSMS
  passed 1/1.
- Full suites passed: Graph 35/35, TransmitSMS 25/25, Abstractions 20/20, and
  workflow 31/31. The four runnable vendor monitor communication composition
  suites passed 12/12. Both provider projects and all five monitor hosts built
  sequentially with zero warnings and errors. The source-boundary/prerequisite
  guard and `git diff --check` passed.
- ReportingMonitor's focused test project remains blocked by the documented
  release-lock `NU1109` (central Logging.Abstractions 10.0.4 versus transitive
  10.0.9), although its host build is green. The five retained locks naming
  removed Infrastructure remain unchanged for the separate eleven-package
  release/lock plan.
- Storage, Portal, reporting behavior, central package versions, locks, and all
  future-pending work remain excluded. Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-communication-provider-split/final-fix-report.md`.

## Communication provider Task 9 - verification and documentation - 2026-07-25 (complete)

- The source-level communication split is verified. The project graph is
  `Rvt.Communication.Abstractions` at the base; the provider-neutral
  `Rvt.Communication` workflow and each of SendGridMail,
  MicrosoftGraphMail, and TransmitSms reference Abstractions directly.
  `Rvt.Monitor.Common` retains only an Abstractions reference for compatibility.
  `Rvt.Monitor.Common.Infrastructure` is removed and is not a facade.
- All five monitor hosts directly reference and compose Abstractions, the
  workflow, SendGrid, Microsoft Graph, and TransmitSMS. They select SendGrid by
  default or Microsoft Graph by exact case-insensitive
  `RVT:EMAIL_PROVIDER`/`RVT__EMAIL_PROVIDER` configuration. Portal remains
  explicitly SendGrid-only.
- Both reporting messaging projects now reference only Abstractions. The
  monitor Reporting host owns dynamic SendGrid/Microsoft Graph selection; the
  containerized reporting-service host explicitly owns its existing
  SendGrid-only selection.
- Fresh bounded library verification passed 126/126: Abstractions 20,
  workflow 31, SendGrid 20, Graph 31, and TransmitSMS 24. Fresh focused vendor
  monitor composition tests passed 12/12. Portal passed 381 with eight known
  provider-gated skips, and the containerized reporting service passed 14/14.
- The full monitor suites remain environment/baseline-gated rather than green:
  AirQ 87 passed/33 failed, MyAtm 139/69, Omnidots 337/64, and Svantek 86/40.
  The failures require the missing PostgreSQL integration connection and, for
  MyAtm/Svantek, include retained pre-monorepo path assumptions. No
  communication-focused regression was identified.
- Dependency isolation is green: both neutral `dotnet list --include-transitive`
  results exclude SendGrid, Azure Identity, Azure Storage, and AWS S3, and the
  source-boundary guard passed.
- The aggregate/locked gate is explicitly **not green**. ReportingMonitor is
  blocked by central Logging.Abstractions 10.0.4 versus transitive 10.0.9;
  RuntimeConsumer lacks the not-yet-packed `Rvt.Communication` 0.2.0-rc.1
  artifact; TestConsumer cannot resolve its expected RVT type; and five
  retained monitor/package-validation locks still name the removed
  Infrastructure project. These are owned by the dedicated eleven-package
  release/lock plan.
- Storage isolation and every future-pending item remain out of scope. Portal
  `BlobStorageClientFactory`/service unification through
  `IObjectStorageClientFactory`, customer-logo and reporting storage adoption,
  synchronous legacy message removal, dynamic plugins, external compatibility
  tooling, notification/business/API/persisted-record changes, database, MQTT,
  scheduling, observability, and the full eleven-package release pipeline all
  remain pending.
- Exact graph, command results, known failures, and the complete pending-work
  list are recorded in
  `docs/architecture/rvt-monitor-common/communications.md` and the Task 9
  report.

## Portal test-host SendGrid follow-up - 2026-07-25 (complete)

- Both Portal test-host paths now provide deterministic, non-secret legacy
  `EmailConfiguration:SENDGRID_API_KEY` and
  `EmailConfiguration:Sending_Email_Address` values through `UseSetting`.
  This makes the values available while minimal-host `Program.ConfigureServices`
  eagerly constructs `SendGridMailOptions`; the custom factory's later
  `ConfigureAppConfiguration` collection is intentionally unchanged for its
  other test settings.
- TDD RED: `SwaggerDocument_IsAvailable` and
  `HealthEndpoints_ExposeLivenessAndReadiness` each failed 0/1 because enabled
  SendGrid validation reported the missing API key and from-address. GREEN:
  both fixtures passed together 2/2, the Task 7 Portal composition/adapter
  tests passed 12/12, and the complete Portal suite passed 381 with eight known
  opt-in PostgreSQL integration skips (389 total). Output retained five known
  NU1903 advisories for `System.Security.Cryptography.Xml` 10.0.7.
- No production code or provider validation changed. Verification used a
  temporary MSBuild import that excluded only the preserved untracked
  `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs` files.
- All previously documented future-pending work remains out of scope,
  including Portal blob client/service unification through
  `IObjectStorageClientFactory`, customer-logo and reporting storage adoption,
  legacy storage utility migration, dynamic plugins and external compatibility
  tooling, synchronous `IMessageService` removal, and later notification,
  API/persisted-record, database, MQTT, scheduling, and observability work.

## Communication provider Task 7 - Portal and reporting mail migration - 2026-07-24 (complete)

- The Portal host now source-references only
  `Rvt.Communication.Abstractions` and `Rvt.Communication.SendGridMail` for
  communication. It retains the existing `PortalEmailOptions` binding and
  explicitly maps the existing `EmailConfiguration` keys into
  `SendGridMailOptions`; the manual Infrastructure, SendGrid factory, and
  concrete adapter registrations are removed.
- The Portal `RvtCommonEmailDelivery` adapter remains the translation seam
  between the business-layer `IEmailDelivery` result contract and
  `IEmailDeliveryPort`. Focused tests cover request mapping, the existing
  debug-recipient override, typed provider-failure translation, and caller
  cancellation.
- The monitor reporting messaging project now references only Communication
  Abstractions for email delivery. Its existing attachment mapping,
  disabled/test-recipient behavior, cancellation, and result semantics are
  preserved.
- The containerized reporting messaging project no longer references or
  constructs the SendGrid SDK. Its provider-neutral `ReportMessageSender`
  sends one `EmailAttachment` through `IEmailDeliveryPort`, while the service
  host explicitly registers SendGridMail from the existing `RVT:EMAIL_*` and
  `RVT:SENDGRID_API_KEY` settings. Disabled email remains a successful no-op
  even when the supplied cancellation token was already cancelled, preserving
  the legacy containerized sender behavior.
- The root source-boundary guard now enforces the Task 7 graph for the Portal,
  monitor reporting messaging, and containerized reporting projects. The two
  monitor lock files updated by restore reflect the active provider graph and
  removal of the old Infrastructure/Common messaging edges.
- Verification: Portal focused tests passed 12/12 through a temporary MSBuild
  import that excluded only the two preserved untracked duplicate `* 2.cs`
  files; monitor reporting sender tests passed 7/7; containerized reporting
  sender tests passed 7/7; the source-boundary guard and its regression harness
  passed; and scoped Portal, monitor-reporting, and service-reporting builds
  completed with zero compiler warnings and errors. Portal test restore/test
  output retained five existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7.
- Future pending work remains out of scope: unify Portal blob storage
  client/service use through `IObjectStorageClientFactory`; migrate
  customer-logo storage; decide the independent reporting-service Azure
  storage path; migrate the legacy Portal storage utility; dynamic provider
  plugins; external-consumer compatibility tooling; notification content or
  business changes; public API or persisted-record changes; removal of the
  legacy synchronous `IMessageService`; and later database, MQTT, scheduling,
  and observability dependency-boundary reviews.

## Communication provider Task 6 - monitor composition roots - 2026-07-24 (complete)

- `MonitorHost.RunAsync` now passes the effective `IConfiguration` to its
  service-composition callback in API, Quartz scheduler, and one-shot modes.
  All five monitor `Program.cs` entry points and every direct registration
  caller pass that configuration into `AddAirQMonitor`, `AddMyAtmMonitor`,
  `AddOmnidotsMonitor`, `AddReportingMonitor`, or `AddSvantekMonitor`.
- Each monitor composition root explicitly registers the neutral
  `Rvt.Communication` workflow, selects SendGrid by default or Microsoft Graph
  by a case-insensitive exact provider match, reads `RVT:EMAIL_PROVIDER` before
  literal `RVT__EMAIL_PROVIDER`, and always registers TransmitSMS. Invalid
  values fail at registration with the safe exact message
  `RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.` without echoing the
  configured value.
- The five active monitor host projects no longer reference
  `Rvt.Monitor.Common.Infrastructure`. Each retains `Rvt.Monitor.Common` and
  directly references Communication Abstractions, Communication, SendGridMail,
  MicrosoftGraphMail, and TransmitSms. The source-reference matrix and shell
  boundary guard enforce this graph while retaining the Portal/Infrastructure
  boundary for later tasks.
- Verification: all five focused communication suites passed 3/3 (15 total);
  `MonitorHostTests` passed 3/3; the source-reference matrix passed 12/12; the
  shell source-boundary guard passed; all five hosts built with zero warnings
  and errors; and `apps/monitors/rvt-monitors.sln` built with zero errors. The
  solution build retained the known NU1900 warning because NuGet vulnerability
  metadata was unreachable. No restore was needed.

## Communication provider Task 5 - TransmitSMS extraction - 2026-07-24 (complete)

- `Rvt.Communication.TransmitSms` now owns the TransmitSMS client and adapter,
  `TransmitSmsOptions`, its startup validation service, and `AddTransmitSms`
  overloads. It reads `RVT:` keys before literal `RVT__` aliases; defaults to
  disabled SMS, empty credentials, and `KrakenAlert` sender. Enabled validation
  names missing key/secret/sender settings without exposing configured secrets.
- The adapter preserves the existing form POST endpoint, Basic authorization,
  cancellation behavior, provider-code-only errors, retry-after parsing, and
  delivery-failure classifications. Provider registration adds one typed HTTP
  adapter, one SMS port, options, and startup validator; an existing SMS port
  fails with `An SMS delivery provider is already registered.`.
- `Rvt.Monitor.Common.Infrastructure` temporarily project-references the
  provider and resolves its provider-owned options/adapter/validator directly
  from `IConfiguration`. `CommunicationsOptions` is now email-only and no
  longer parses, exposes, or validates SMS settings. Its packed dependency is
  exactly pinned to `[$(PackageVersion)]` through the temporary bridge.
- The solution includes the provider and test projects in the RVT Monitor
  Common library/test folders. The temporary build/package graph is now eight
  artifacts, with TransmitSMS restored, packed, required in artifact checks,
  and cleared from the local package cache before package validation.
- Verification: TransmitSMS tests passed 24/24; Infrastructure tests passed
  17/17 after the ownership-boundary follow-up; source-boundary/package guard
  and solution inventory guard passed;
  Infrastructure package packing with `-m:1` produced a nuspec pinned to
  `[0.2.0-rc.1]`; and Infrastructure plus all five monitor hosts built with
  `--no-restore`. The provider test build still reports the existing MSTest
  parallelization/data-test analyzer warnings.

## Communication provider Task 3 - SendGrid mail extraction - 2026-07-24 (complete)

- `Rvt.Communication.SendGridMail` now owns the SendGrid adapter, client factory,
  `SendGridMailOptions`, its startup validation service, and explicit
  `AddSendGridMail` overloads. Its `RVT:` configuration keys take precedence
  over literal `RVT__` fallbacks. The provider registers one email port, one
  factory, one options instance, and one provider-specific hosted validator;
  duplicate email ports fail with the required exact message.
- `Rvt.Monitor.Common.Infrastructure` temporarily source-references and package
  depends on SendGridMail while keeping its existing SendGrid/Microsoft Graph
  selector. Its temporary composition resolves provider-owned SendGrid options.
  This bridge is scheduled for removal in Task 8 rather than becoming a facade.
- The portal has a direct SendGridMail source reference because it owns a
  separate SendGrid composition root. It maps its existing `EmailConfiguration`
  settings to `SendGridMailOptions` and retains its original service lifetimes.
- The temporary package graph is now six artifacts:
  `Rvt.Monitor.Common`, `Rvt.Communication.Abstractions`,
  `Rvt.Communication`, `Rvt.Communication.SendGridMail`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting`. `scripts/build-mono.sh` restores, packs,
  verifies, and clears cached copies of all six. Infrastructure packs with
  `-m:1`; its SendGridMail dependency is exactly pinned to `[$(PackageVersion)]`.
- Verification: provider tests 20/20; Infrastructure compatibility tests 52/52;
  package-artifact tests 14/14; Common communication boundary tests 3/3; source
  and temporary-six-package bridge guard passed; Infrastructure and all five
  monitor hosts built with `--no-restore`. A Portal build no longer reports a
  SendGrid reference error but remains blocked by the accepted unrelated
  untracked duplicate Portal source files.
- Follow-up review fix: `Rvt.Mono.slnx` includes SendGridMail under
  `/Libraries/RVT Monitor Common/` and its tests under
  `/Libraries/RVT Monitor Common/Tests/`. The solution inventory guard now
  recognizes all 44 module projects.

## Communication workflow Task 2 - 2026-07-24 (complete)

- Worktree: `.worktrees/release-platform-hardening`; base before Task 2 was
  `6b80074` (Task 1 communication abstractions).
- Commit `486748c` added `Rvt.Communication` and
  `Rvt.CommunicationTests`, moved the
  provider-neutral workflow types/tests from Common, added idempotent
  `AddRvtCommunication`, and made Infrastructure temporarily reference and
  package-depend on Communication.
- RED/GREEN evidence exists in `.superpowers/sdd/task-2-report.md`: workflow
  suite passed 31/31; bridge guard passed after requiring five temporary
  packages and a single-node Infrastructure pack; package artifact suite passed
  12/12 after synchronized dependency pinning.
- Confirmed local MSBuild issue: parallel Infrastructure `dotnet pack` stalls
  after Communication's `GetTargetFrameworks`; identical `-m:1` pack completes
  in 2.3 seconds. `scripts/build-mono.sh` now uses `-m:1` only for that pack.
- Verification constraint: Infrastructure restore from the bridge stalls after NU1900
  inability to reach nuget.org vulnerability data. Its last bounded command
  was stopped at 2m11s; audit-disabled and `--disable-parallel` retry restores
  also timed out after 55 seconds. A bounded `dotnet build Rvt.Mono.slnx
  --no-restore --nologo` compiled Task 2 projects but failed on eight duplicate
  Portal types from unrelated untracked `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs` files. Full aggregate build is not
  freshly proven.
- Preserve unrelated untracked `.codegraph/`, `apps/.nuget-packages/`, and
  `apps/portal/RvtPortal.Client/src/localDate 2.ts`, plus the two Portal C#
  suffixed files and `docs/superpowers/specs/2026-07-23-rvtportal-sites-application-boundary-design 2.md`; do not stage.
  Task 2 retained scoped verification evidence without modifying those files.
- Scoped handoff verification (all `--no-restore`) passed for Rvt.Communication,
  Infrastructure, and all five monitor hosts. The source-boundary guard passed;
  Communication passed 31/31; package validation passed 12/12; Common remained
  at its accepted 376 passed/2 missing-migration-path failures. The independent
  task review approved the implementation with aggregate verification excluded
  for the unrelated Portal duplicate files and restore environment issue.

## Release Platform Hardening - 2026-07-23

- The portal now maps explicit `/api/health/live` and `/api/health/ready`
  probes. Liveness has no dependency checks; readiness runs the three EF
  context checks plus schema validation and returns only statuses/check names.
  The cutover runbook designates readiness as the deployment gate.
- Existing proxy hardening remains configured before redirects, rate limiting,
  authentication, and CSRF: only forwarded client IP and scheme are accepted,
  defaults are cleared, and immediate peers must be configured in
  `ForwardedHeaders:KnownProxies` or `KnownNetworks`.
- Blob storage clients are constructed by the shared
  `IBlobStorageClientFactory` for both connection-string and managed-identity
  modes. Monitor pictures and site archives no longer independently construct
  blob service clients. Report generation and Omnidots adapters use bounded
  named client timeouts and translate malformed URLs, downstream timeouts, and
  connection failures without reflecting secrets or vendor response bodies.
- Calendar selections retain the full ISO date, local date-only defaults no
  longer serialize through UTC, and calendar/breach effects ignore responses
  after cancellation. Help Admin is explicitly excluded from the release
  surface until its temporary asset editor receives stable row-key coverage.
- Focused verification: `SpaHostSmokeTests` passed 5/5;
  storage/archive/report/Omnidots tests passed 18/18; the corrected host-filter
  controls passed 2/2; and the complete client suite passed 68/68 after a
  production build. The full portal backend suite passed 376 tests with eight
  explicit PostgreSQL/Timescale provider skips (384 discovered). Client lint completed with no errors and its two existing
  `DataViewPanels.tsx` fast-refresh warnings. The planned
  `DashboardRoutePanels`, `DashboardPanels`, and `ContractSitePanels` test
  files are absent in this checkout, so their exact scoped Vitest command has
  no matching files.
- Production-provider gate: an existing local TimescaleDB container was
  brought online after the first loopback connection attempt was refused.
  The second selected run reached PostgreSQL and reported 22 passed, 4
  failed (26 total). Root cause investigation found: the seed fixture passed
  UTC-naive values to Npgsql without an explicit `timestamp` type; seven
  aggregate view expressions were implicitly promoted to `timestamptz`; and
  PostgreSQL normalized the equivalent `created` default expression with a
  different spelling. The approved idempotent schema deploy applied all seven
  deploy scripts, including the corrected aggregate-view definitions. The
  final provider gate passed 26/26 against the database; the complete portal
  suite passed 376 tests with the eight opt-in provider tests skipped. Source
  remediation adds explicit view casts, typed test parameters, and a
  version-tolerant default assertion. Every temporary credential file was
  deleted immediately after use.
- Release-artifact verification: `RvtPortal.Spa` published successfully in
  Release with `wwwroot/index.html`, hashed SPA assets, and the host DLL.
  The client release gate then passed lint (two pre-existing fast-refresh
  warnings only), 68/68 Vitest tests, 4/4 Playwright browser smoke tests, and
  a production build. Targeted npm overrides update only the ESLint development
  dependency tree; a fresh full `npm audit` reports zero vulnerabilities. The
  repository has no portal production deployment manifest or target
  configuration. Before traffic can be deployed, an operator must supply the
  deployment target/secret store, the public HTTPS host (`Spa:PublicBaseUrl`
  and matching `AllowedHosts`), and the immediate reverse-proxy IP addresses or
  CIDR ranges for `ForwardedHeaders:KnownProxies`/`KnownNetworks`.

## Provider Adapter Project Split Design - 2026-07-23

- Approved direction: perform a clean major-version split with no temporary
  `Rvt.Monitor.Common.Infrastructure` facade. Provider-neutral communication
  and storage contracts become standalone projects; SendGrid, Microsoft Graph,
  TransmitSMS, Local, Azure Blob, and S3 each become individual adapter
  projects selected explicitly by application composition roots.
- Design specification:
  `docs/superpowers/specs/2026-07-23-rvt-provider-adapter-project-split-design.md`.
  Execution is divided into three implementation plans:
  `docs/superpowers/plans/2026-07-23-rvt-communication-provider-split.md`,
  `docs/superpowers/plans/2026-07-23-rvt-storage-provider-split.md`, and
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`.
  The release plan updates the hard-coded three-package validation, SBOM,
  solution, and package-consumer assumptions to the approved eleven-package
  graph.
- Target production package set: `Rvt.Monitor.Common`,
  `Rvt.Monitor.IntegrationTesting`, `Rvt.Communication.Abstractions`,
  `Rvt.Communication`, `Rvt.Communication.SendGridMail`,
  `Rvt.Communication.MicrosoftGraphMail`,
  `Rvt.Communication.TransmitSms`, `Rvt.Storage.Abstractions`,
  `Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3`.
  The clean-split release baseline is `1.0.0-rc.1`;
  `Rvt.Monitor.Common.Infrastructure` is removed rather than retained as a
  facade or meta-package.
- Storage abstraction definitions: `IObjectStorageClient` provides streaming
  write, open-read, and delete-if-exists operations;
  `IObjectStorageClientFactory.GetRequiredClient(resourceName)` resolves a
  named host-configured resource; `StorageObjectKey` protects provider-neutral
  object names; and provider adapters translate vendor failures into
  `ObjectStorageException` with `StorageFailureKind`.
- Communication abstraction definitions: `IEmailDeliveryPort` and
  `ISmsDeliveryPort` remain transport ports; `LegacyMessageKind` and
  `LegacyMessageChannel` replace implementation-owned nested message enums;
  `RvtContactDto` remains source-compatible in the
  `Rvt.Monitor.Common.Notifications` namespace but is compiled by
  `Rvt.Communication.Abstractions` to avoid a project cycle.
- TODO(storage): after the provider split, migrate Portal
  `MonitorPictureStorage` and `SiteArchiveService` from direct Azure
  `BlobContainerClient` construction to `IObjectStorageClientFactory`. Preserve
  protected streaming, local fallback and atomic writes, existing `blob://`
  references, persisted archive URLs, and report/archive container boundaries.
  Treat customer logos and the reporting service adapter as later explicit
  decisions.
- Future pending work is now explicit in the design: dynamic provider plugins;
  external-consumer compatibility tooling if coordinated major migration proves
  impossible; notification content/business changes; public API or persisted
  record changes; legacy synchronous `IMessageService` removal; and later
  database, MQTT, scheduling, and observability dependency-boundary reviews.

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Documents/rvt-mono`
- Status: Documentation consolidation is complete. All 122 non-entry module
  Markdown documents are centralized under root `docs/`, with a guarded root
  index and valid retained repository/module entry points. The completed RVT
  common source-reference migration remains in effect: active monitor and
  portal consumers are source-referenced, their 12 tracked locks reflect the
  source graph, and the two package-validation consumers restore locally
  packed artifacts through artifact-scoped validation locks.
- Design: `docs/superpowers/specs/2026-07-22-rvt-mono-repository-design.md`
- Plan: `docs/superpowers/plans/2026-07-22-rvt-mono-repository-bootstrap.md`
- Requested outcome: fresh unified Git history and a shared root solution for
  `rvt-monitors`, `rvtportal-spa-alpha`, `rvt-reporting`, and
  `rvt-reporting-new`.
- Intended modules: `apps/monitors`, `apps/portal`,
  `libs/rvt-monitor-common`, and `services/reporting`.
- Root solution: `Rvt.Mono.slnx`.
- Approved design: `docs/superpowers/specs/2026-07-22-rvt-common-source-reference-design.md` changes active consumers to source project references, while package-validation remains package-based against locally packed artifacts. This is an explicit decision to review if independent package consumption becomes required again.
- Implemented plan: `docs/superpowers/plans/2026-07-22-rvt-common-source-reference-migration.md`.
- Implemented design: `docs/superpowers/specs/2026-07-22-documentation-consolidation-design.md` consolidates all module Markdown into root `docs/`, retaining root/module README and AGENTS entry points.
- Implemented plan: `docs/superpowers/plans/2026-07-22-documentation-consolidation.md`.
- Aggregate project count: 38 projects across all four module roots.
- Important boundary: active application consumers use the in-repository RVT
  source projects; only `libs/rvt-monitor-common/package-validation` consumes
  RVT packages. Do not merge reporting implementations or database schemas.
- Imported source snapshots:
  - `apps/monitors` from `chris-oldgeorge/rvt-monitors` at
    `5935f40614073afa6c4ef954db1308a72a5f8f2b`.
  - `apps/portal` from `chris-oldgeorge/rvtportal-spa-alpha` at
    `8355070f094a591297c9f8468057f44a6c876986`.
  - `libs/rvt-monitor-common` from `RVT-Group-LTD/rvt-reporting` at
    `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
  - `services/reporting` from `chris-oldgeorge/rvt-reporting-new` at
    `e602e8317e35bd94a1eb4dd017759b91713ea111`.
- Import staging directory: `/private/tmp/rvt-mono-import.2w115l` (retained
  through Task 3 final verification).
- Import verification: all staged repositories were checked out detached at
  their exact manifest revisions; imported trees checksum-match the staged
  content with `.git` excluded; no nested `.git` directory exists below the
  module roots.
- Known environment note: authenticated GitHub metadata access was available;
  source clone/restore access must be verified during implementation. Never
  record credentials in this repository.
- Task 1 guard: `.gitignore` excludes generated files, environment files, and
  `.superpowers/sdd/` controller state. `docs/imports/source-manifest.md` pins
  the four approved source snapshots. Repository bootstrap commits through the
  source import are design `1327b84`, plan `0abf895`, guard `ae65789`, and
  source import `31d168f`.
- Task 3 guard: `tests/verify-mono-solution.test.sh` runs
  `scripts/verify-mono-solution.sh`. It compares normalized, sorted module
  `*.csproj` paths with the normalized, sorted `dotnet sln Rvt.Mono.slnx list`
  paths, requires matching project counts and per-module representation, and
  enforces exact project placement under `Apps/Monitors`, `Apps/Portal`,
  `Libraries/RVT Monitor Common`, and `Services/Reporting`, with test projects
  in each module's corresponding `Tests` solution folder.
- Source-reference migration Task 1: `tests/verify-rvt-common-source-boundary.test.sh`
  invokes `scripts/verify-rvt-common-source-boundary.sh`. The guard declares
  the three shared source projects, requires the approved app/portal project
  references, rejects their common-package references, and preserves
  package-only validation consumers. Each package-validation project rejects
  source references to all three shared projects while retaining its required
  package references.
- Source-reference migration Task 2: the five monitor hosts now directly
  reference `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure`; the
  five current monitor test consumers reference `Rvt.Monitor.IntegrationTesting`
  (with `ReportingMonitorTests` retaining its direct Common edge); the reporting
  messaging/storage projects directly reference Common; and the portal host
  directly references Infrastructure. MSBuild now supplies build ordering for
  these active graphs.
- Monitor central package variables `RvtCommonVersion`,
  `RvtCommonInfrastructureVersion`, and `RvtIntegrationTestingVersion`, plus
  their three `PackageVersion` entries, were removed. Active monitor and portal
  NuGet configs now retain only nuget.org; the shared library NuGet config maps
  `Rvt.*` to the root `artifacts/packages` feed for package validation.
- Package-validation remains intentionally package-based at `0.2.0-rc.1`.
  `scripts/build-mono.sh` packs exactly `Rvt.Monitor.Common`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting` to `artifacts/packages`, validates the two
  package consumers from an isolated `artifacts/nuget-packages` cache, restores
  `Rvt.Mono.slnx`, builds with `--no-restore`, and tests with `--no-build`.
  Its legacy package-validation compatibility path is deterministically replaced
  on each run, so a stale directory or symlink from a temporary test feed cannot
  block the next build.
  Normal builds opt those two consumers into per-project locks under
  `artifacts/validation-locks`; freshly emitted NuGet archives have different
  content hashes, so this keeps their committed `0.2.0-rc.1` package-policy
  locks and all other tracked locks unchanged. The shared library NuGet
  configuration maps `Rvt.*` only to the root local feed and retains nuget.org
  for third-party packages; GitHub Packages and credentials are not used.
- Verification results:
  - `tests/verify-mono-solution.test.sh` and
    `tests/verify-mono-layout.test.sh` pass.
  - `dotnet sln Rvt.Mono.slnx list` reports all 38 module projects.
  - The source-boundary guard passes after the active-consumer conversion.
  - Both active module solutions restore successfully and their restore graphs
    reach the shared source projects. Verbose network traces contacted only
    nuget.org; the preserved shared-library config still appears in NuGet's
    configured-feed summary.
  - Portal restore reports four existing NU1903 high-severity advisories for
    `System.Security.Cryptography.Xml` 10.0.7; remediation is outside Task 2.
  - During Task 2, `dotnet restore Rvt.Mono.slnx` was blocked by private package access:
    GitHub Packages returns HTTP 401 for the RVT organization feed. Cached RVT
    `0.2.0-rc.1` packages also produce NU1403 content-hash validation errors.
  - During Task 2, `dotnet build Rvt.Mono.slnx --no-restore --nologo` exited
    with 16 errors from the same NU1301/NU1403 package state; unaffected
    projects compiled.
  - Package feeds and dependency declarations were not changed in Task 2.

## RVT Common Local Package Validation - 2026-07-22

- The missing-artifact regression check records the expected pre-restore
  failure and names `Rvt.Monitor.Common.0.2.0-rc.1.nupkg`; its mutation RED run
  catches `RuntimeConsumer` restore before artifact verification. Its GREEN run
  proves neither package-validation consumer nor aggregate restore can occur
  before all three packages exist.
- The local package sequence restores and packs the three shared projects,
  restores all 38 aggregate projects from nuget.org plus
  `artifacts/packages`, and builds the aggregate solution with 0 errors. The
  existing four NU1903 advisories for `System.Security.Cryptography.Xml`
  10.0.7 remain outside this task.
- The package artifact suite passes 8/8. RuntimeConsumer and TestConsumer stay
  package-based at `0.2.0-rc.1`; build-time artifact locks are generated under
  `artifacts/validation-locks`. The 12 active monitor consumer locks were
  regenerated from their source-reference graphs: none retain a direct RVT
  package, and normalized comparison proves all non-RVT dependency data is
  unchanged.
- Focused source-boundary architecture verification passes 12/12 for monitors
  and 7/7 for the portal. The monitor suite now enforces the approved source
  matrix, source-consumer lock shape, local validation boundary, version, and
  feed policy. The portal suite now requires the Infrastructure source project
  and a nuget.org-only credential-free configuration.
- A full build-sequence diff fingerprint is identical before and after restore,
  pack, package validation, aggregate restore/build, and the nonzero aggregate
  test stage. A normal successful run therefore introduces no tracked lockfile
  changes.
- The aggregate test stage remains nonzero for imported test assumptions that
  are outside this migration. Database-backed tests report exactly:
  `System.InvalidOperationException: Set RVT__POSTGRES_INTEGRATION_CONNECTION
  to run PostgreSQL integration tests.` Other imported architecture and
  migration-contract test assumptions still
  resolve pre-mono paths, including
  `/Users/oldgeorge/Documents/rvt-mono/reportingmonitor/ReportingMonitor/api`
  and `/Users/oldgeorge/Documents/rvt-mono/rvt-monitors.sln`, which do not exist
  in the aggregate layout. No package versions or test behavior were changed
  to mask these failures.

## Documentation Consolidation Task 1 - 2026-07-22

- Current state: the documentation move guard and exhaustive manifest are
  defined; no documentation has moved and no links have been rewritten yet.
- File structure added by this task:
  - `docs/documentation-move-manifest.md` maps all 122 tracked non-entry module
    Markdown sources to unique destinations below the root `docs/` hierarchy.
  - `scripts/verify-documentation-layout.sh` enforces the manifest, retained
    entry points, destination presence, absence of module documentation, and
    absence of stale links to moved sources.
  - `tests/verify-documentation-layout.test.sh` is the strict-mode executable
    wrapper for the guard.
- Retained entry points are the root `README.md`, the four module-root
  `README.md` files, and `apps/monitors/AGENTS.md` plus
  `apps/portal/AGENTS.md`.
- Guard variables: `repo_root` is derived from the script location;
  `manifest_path` is `docs/documentation-move-manifest.md`;
  `expected_manifest_entries=122`; `failures` counts issue groups; `sources`
  and `destinations` hold parsed manifest rows; `retained_paths` holds the seven
  required entry points; and `missing_sources` scopes stale-link checks to
  documents that have actually moved.
- Expected verification state: `tests/verify-documentation-layout.test.sh`
  exits nonzero with exactly two issue groups—122 non-entry Markdown sources
  remain below module roots and all 122 manifest destinations are absent.
  Task 2 is responsible for resolving those expected failures with Git-aware
  moves.
- Review follow-up: the exact old-path stale-reference scan is repository-wide
  text scanning, with `.git`, `.superpowers/sdd`, and the move manifest
  excluded. The Markdown link resolver remains scoped to Markdown. The
  `tests/verify-documentation-layout-regression.test.sh` fixture moves all 122
  manifest documents and proves a source-code reference in the MyAtm monitor
  architecture-test path is reported as the sole stale reference.

## Documentation Consolidation Task 3 - 2026-07-22

- Current state: all 122 manifest documents are present exactly once below the
  root documentation hierarchy, and the seven retained root/module README and
  AGENTS entry points remain beside their code.
- Root navigation: `docs/index.md` is the central documentation hub, grouped
  into architecture, development, operations, release, database, modules,
  history, and imports. Root and module READMEs link into that hub or directly
  to their current module documentation.
- Repaired references: the portal development-guideline references, the
  monitor ReportingMonitor README link, the MyAtm architecture-test document
  path, portal/monitor AGENTS state paths, and the portal development-secrets
  script link now resolve from their current locations.
- Guard variables added to `scripts/verify-documentation-layout.sh`:
  `documentation_index` names `docs/index.md`; `index_targets` holds one
  required link for each guarded documentation category. The regression
  fixture uses `stale_document_path` and the `STALE_DOCUMENT_PATH` environment
  variable to inject its intentional old path only into the temporary test
  repository, preserving the repository-wide production scan.
- Verification: `tests/verify-documentation-layout.test.sh` passes with 122
  moves and seven retained entry points;
  `tests/verify-documentation-layout-regression.test.sh` passes while proving
  source-code stale references are rejected. The final stale-link scan and
  whitespace check are clean. The obsolete untracked suffixed copy
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests 2.cs`
  was removed after the final repository push; the tracked mono-repository
  boundary test remains authoritative.

## Documentation Consolidation Final Review Fix - 2026-07-22

- Manifest-derived stale-reference enforcement now derives each moved
  module's `docs/**` form from the 122 source paths and scans arbitrary tracked
  text with `git grep -I`. The scan excludes the move manifest, internal SDD
  review packages, and `docs/history/**`; historical documents therefore keep
  their original evidence while current docs, code, scripts, SQL, and
  configuration remain guarded.
- Guard variables added by the final fix: `module_relative_source` is the
  current manifest source with its module-root prefix removed, and
  `missing_module_relative_sources` contains only `docs/**` forms whose source
  document has actually moved. `stale_reference_count` includes both exact
  repository-root source forms and module-relative forms.
- Repaired references: 50 occurrences in non-Markdown tracked files and 27 in
  current Markdown now use their manifest destinations. This includes the
  shared release-automation documentation path, portal EF/database/Sonar and
  development-secrets references, monitor release-export configuration, SQL
  comments, and current database/onboarding/release documentation.
- Regression structure: the tracked non-Markdown fixture
  `tests/fixtures/documentation-layout-stale-source-reference/libs/rvt-monitor-common/scripts/release-documentation.txt`
  injects the old shared-library module-relative release path only in its
  temporary repository.
  The regression requires the guard to report both that form and the existing
  exact old source path.
- Verification: both documentation guards pass; the explicit manifest-derived
  scan reports zero current stale alias groups; Bash syntax checks pass; the
  shared release-document destination exists; and
  `git diff --check` is clean. The obsolete untracked suffixed C# copy was
  subsequently removed, leaving the tracked boundary test as the sole copy.

## Immediate Blockers Task 2 - 2026-07-22

- Portal startup now explicitly registers `TimeProvider.System`, satisfying
  report-generation client and report-rule dependency resolution without a
  framework-service assumption.
- Vibration traces use the mapped `OmnidotsTrace` entity from
  `RVTSearchContext.OmnidotsTraces` end-to-end. `IMonitorService`,
  `MonitorData`, graph dataset mapping, and the data-view test fake all carry
  `SearchQueryResult<OmnidotsTrace>`; the unmapped `OmnidotsTraces` DTO is no
  longer on the execution path.
- Regression coverage: a host scope resolves `IReportGenerationClient`; a
  SQLite-backed test inserts a mapped `omnidots_trace` row and verifies
  `MonitorService.GetVibrationTraces` reads it. The focused run passes 9/9;
  the portal test project passes 316/319 with three intentional opt-in
  PostgreSQL skips. Restore continues to report existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7.

## Immediate Blockers Task 3 - 2026-07-22

- Site and monitor company-user authorization now share
  `Application/Sites/ActiveSiteAssignment.cs`. Its `ForUser(userId, nowUtc)`
  expression requires `StartDate <= nowUtc` and no `EndDate` or
  `EndDate >= nowUtc`, so both boundaries are inclusive.
- `SiteApplicationService` now receives the registered `TimeProvider`; its
  detail and list paths evaluate the shared assignment predicate at
  `timeProvider.GetUtcNow().UtcDateTime`. Monitor detail, picture, inventory,
  and option paths reuse the same assignment expression for company users.
- Installer monitor reads require an assigned row with non-null actor/row
  company ids that match. The protected picture endpoint therefore preserves
  its existing `404` response for missing and unauthorized monitors while
  same-company pictures remain readable.
- `IMonitorAdministrationReadService.OptionsAsync` now accepts the
  `PortalUserContext` actor. Admin option behavior remains global; installers
  receive contracts/sites for their company; company users receive contracts
  and non-archived sites reached through currently active assignments.
- Regression coverage fixes the site-authorization clock at
  `2026-07-22T12:00:00Z` and covers expired, future, exact-boundary active,
  same-company, and cross-company controls. The focused workflow run passes
  26/26; the initial portal test project run passed 319/322 with three expected opt-in
  PostgreSQL skips. Existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7 remain unchanged.
- Task 3 review follow-up closes the remaining specified consumers:
  `DashboardApplicationService`, `AlertLevelApplicationService`,
  `NotificationApplicationService`, and both notification-close handlers now
  receive the registered `TimeProvider` and reuse `ActiveSiteAssignment.ForUser`.
  Future and expired assignments cannot expose dashboard/alert data or mutate
  notification close state; exact `StartDate == nowUtc` / `EndDate == nowUtc`
  remains authorized. The fixed test instant is `2026-07-22T12:00:00Z`.
- Monitor option contract scope is now the intersection of visible site ids
  and the actor's company id for installer/company-user callers. This prevents
  a second company's contract leaking when both contracts point to one site;
  admin option behavior remains global.
- Follow-up verification: the four new exploit/control tests pass 4/4, the
  four covering workflow classes pass 41/41, and the portal test project
  passes 323/326 with the same three opt-in PostgreSQL skips. The duplicate
  `SiteApplicationService` file header was consolidated.

## RVT Portal AI Review Analysis - 2026-07-22

- Source review: `/Users/oldgeorge/Desktop/RvTPortal AI Review.docx` was read
  structurally and rendered as 14 pages. It contains two overlapping technical
  review passes and eight reviewer comments; there are no tracked insertions or
  deletions.
- Action plan:
  `docs/superpowers/plans/2026-07-22-rvt-portal-review-remediation.md` contains
  the normalized finding register, comment disposition, five implementation
  phases, 16 test-driven tasks, and the final release gate.
- Highest-priority confirmed current defects are: inactive/future site
  assignments granting access, installer cross-company monitor-picture reads,
  missing `TimeProvider` DI registration, an unmapped vibration-trace query,
  request-host-derived password-reset links, unspecified contract dates written
  to `timestamptz`, omitted existing-database repair SQL, unscoped monitor
  options, and the absence of an active root GitHub workflow after the monorepo
  import.
- The disputed timestamp finding remains a validation-first item: current code
  passes UTC bounds to PostgreSQL `timestamp without time zone` telemetry and
  returns values without restoring UTC kind. The plan requires a real-Postgres
  test to distinguish throwing paths from return-shifted paths before repair.
- Reviewer-comment disposition: the monitor-picture dismissal confuses
  admin-only upload with installer-enabled read access; the schema-deploy issue
  labelled "Hallucination" is confirmed because the repair file is absent from
  `ScriptRunner` and publish content; What3Words requires a retain-or-remove
  product decision; Help Admin remains deferred if it is excluded from release;
  the destructive dev-restore defect is real but lower production priority.
- Superseded observations: root `project_state.md` exists; the current workspace
  is not the reviewed SMB checkout; Word/AppleDouble/build debris is absent;
  SendGrid uses a singleton client factory; and the runtime client container
  already uses `nginx-unprivileged`.
- Planned CI variable: `RVT_TEST_POSTGRES_CONNECTION` is the portal-specific
  real-PostgreSQL test connection. It is distinct from monitor-suite integration
  variables recorded elsewhere in this file.

## Immediate Blockers Task 4 - 2026-07-22

- Public account-action links now use only the bound `SpaOptions.PublicBaseUrl`
  through `SpaPublicLinkBuilder`; neither `AuthApplicationService` nor the
  sibling `UserAccountNotificationService` can fall back to request scheme,
  host, or path base. The existing request-origin records remain at controller
  boundaries for API compatibility, with auth also carrying the internal
  correlation id used for safe provider-failure logging.
- Outside Development/Testing, `Program.cs` requires `Spa:PublicBaseUrl` to be
  an absolute HTTPS base URI without credentials/query/fragment. `AllowedHosts`
  must be nonempty, contain no wildcard, and contain that URI's exact host.
  Checked-in `appsettings.json` leaves the public base blank and limits local
  hosts to `localhost;127.0.0.1`; deployments must override both settings.
- Forwarded-header variables are `ForwardedHeaders:KnownProxies` (individual IP
  addresses) and `ForwardedHeaders:KnownNetworks` (CIDR ranges). Framework trust
  defaults are cleared, `ForwardLimit` is one, only `X-Forwarded-For` and
  `X-Forwarded-Proto` are enabled, and `UseForwardedHeaders` runs before HTTPS
  redirect, correlation/observability, CSRF, rate limiting, and authentication.
- Profile email edits update name, phone, and company role independently while
  leaving `ApplicationUser.Email`, `UserName`, and `EmailConfirmed` unchanged.
  A `GenerateChangeEmailTokenAsync` token is delivered to the requested address;
  `GET /api/auth/change-email` applies it with `ChangeEmailAsync` and then aligns
  the username. `AccountMessageKind.EmailChange` supplies the dedicated message.
- Anonymous forgot-password paths return the same generic 200 response for
  unknown, unconfirmed, missing-origin, delivery-failure, and provider-exception
  cases. Provider diagnostics stay in structured internal logs with the API
  correlation id.
- Regression files are `SecurityHardeningTests.cs` and `SpaHostSmokeTests.cs`;
  they cover malicious/configured host controls, the sibling notification path,
  pending-to-confirmed email change, provider-failure uniformity, production
  startup validation, configured proxy/network trust, untrusted peers, cleared
  defaults, and disabled forwarded-host processing.

### Task 4 review follow-up

- Admin `PUT /api/users/{id}` email edits now follow the same pending-confirmation
  contract as self-service profile edits. The update command applies name,
  phone, role, company role, and company assignment without replacing the
  confirmed `Email` or `UserName`; the workflow sends an Identity change-email
  token to the requested address using the configured public SPA base URL.
- `GET /api/auth/change-email` now treats email, username, confirmation state,
  and the token's security stamp as one logical transition. If username update
  fails after Identity accepts the email token, the original values are restored
  and persisted before a validation response is returned. Restoring the security
  stamp keeps that same confirmation link valid for a safe retry after the
  conflicting username is resolved.
- Regression controls are
  `AdminEmailChange_RemainsPendingAndResetUsesConfirmedAddress` and
  `EmailChangeConfirmation_WhenUserNameUpdateFails_RollsBackAndTokenCanRetry`.
  They prove non-email admin edits still apply, reset delivery stays on the
  confirmed address, confirmation reaches the requested address, failed
  username alignment leaves no partial Identity state, and the token can retry.

### Task 4 second review follow-up

- Confirmed-account `GET /api/auth/change-email` transitions now run inside an
  execution-strategy-aware `ApplicationDbContext` transaction. Both
  `ChangeEmailAsync` and `SetUserNameAsync` commit together; an Identity failure
  result or exception rolls back the database transaction and clears the stale
  change tracker. Compensation remains only for the non-relational EF InMemory
  test provider, which cannot begin a transaction; it is not the production
  atomicity guarantee.
- Admin edits now branch on the account's pre-update confirmation state.
  Confirmed users retain the pending change-email workflow. For an unconfirmed
  invited user, the transactional update command replaces email and username,
  explicitly keeps `EmailConfirmed` false, rotates the security stamp so the old
  invitation token fails, and sends the normal password-set confirmation link
  to the replacement address. Independent name/phone/role/company edits remain.
- Relational SQLite controls force both a duplicate-username Identity failure
  and a validator exception after email persistence. Both observe the original
  database state afterward and successfully retry the same change-email token.
  The unconfirmed-invite control proves the replacement address cannot log in
  or receive reset mail before confirmation, the old token is invalid, and the
  new recipient completes confirmation plus initial-password sign-in.

## Immediate Blockers Resume Checkpoint - 2026-07-22

- Resume instruction: start the next session with `Read project_state.md to get
  up to speed`, then work in
  `/Users/oldgeorge/Documents/rvt-mono/.worktrees/immediate-blockers` on branch
  `codex/immediate-blockers`. Do not resume in the root checkout on `main`.
- Base/planning commit: `5048052`. Task 2 is complete at `4173f8a` and passed an
  independent review. Task 3 is complete through `4bc2ac9` and passed an
  independent re-review after both tenant-authorization gaps were fixed.
- Task 4 initial auth hardening is committed at `1f3bcc4`; its first review
  follow-up is committed at `b9b6c46`. The second review then required real
  atomicity for confirmed email/username transitions and a separate onboarding
  path for unconfirmed admin-managed email edits.
- Those second-review fixes are implemented in the checkpoint after `b9b6c46`:
  an execution-strategy-aware `ApplicationDbContext` transaction protects the
  confirmed transition, relational SQLite tests cover result and exception
  rollback plus token retry, and unconfirmed replacements stay unconfirmed,
  invalidate the old invite, and use the normal initial-password onboarding
  link.
- Latest implementer evidence before the pause: 3/3 critical relational tests,
  64/64 owning-slice tests, and 337 portal tests passed; three opt-in PostgreSQL
  tests remained skipped. Resumed verification then passed 3/3 relational
  controls, 30/30 Task 4 tests, 337 full-project tests with the same three
  skips, and a zero-warning host build. The deterministic authorization-clock
  fixture correction is test-only commit `a6dda94`.
- Task 4 final review is complete at `a6dda94`. The reviewer found no remaining
  Critical, High, Medium, or Low issues: relational result/exception rollback,
  token retry, invited-user onboarding, origin enforcement, proxy trust,
  forgot-password uniformity, and legitimate route/DTO behavior are approved.
- Tasks 5 and 6 remain untouched: establish the explicit UTC/search timestamp
  contract, then complete schema deployment and failure reporting. Both require
  TDD and independent task review. Real PostgreSQL verification still requires
  `RVT_TEST_POSTGRES_CONNECTION`.
- Known non-task state: `apps/.nuget-packages/` is an untracked generated cache;
  do not commit it. Existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisories remain outside this repair tranche.

## Immediate Blockers Task 5 Review Completion - 2026-07-23

- Task 5 implementation is complete through `0bd22c2` and passed independent
  re-review with no Critical, Important, or Minor findings.
- The approved contract rejects non-UTC API inputs, converts client
  `datetime-local` values to UTC ISO instants, deliberately strips Kind only at
  PostgreSQL `timestamp without time zone` query boundaries, restores UTC on
  grid/graph/monitor-summary reads, and persists contract calendar dates as UTC
  midnight without workstation-local conversion.
- Checked-in PostgreSQL aggregate view SQL, EF runtime mappings, and the search
  model snapshot now agree. UTC-naive aggregate fallbacks use
  `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'`; only genuine daily aggregates remain
  `date`. Deterministic tests cover UTC and Europe/London display without skips.
- Verification passed 347 backend tests with six provider-gated skips, all 68
  client tests, both builds, and diff checks. The provider-gated metadata/query,
  telemetry JSON, and contract-persistence tests are authored but were not run
  because `RVT_TEST_POSTGRES_CONNECTION` is unset. Do not claim live Npgsql or
  deployed-schema closure until those tests execute successfully.

## Immediate Blockers Task 6 Implementation - 2026-07-23

- `RVT.SchemaDeploy` now resolves and executes one deterministic sequence:
  `create_unmapped_schema.sql`, `restore_unmapped_column_defaults.sql`, then
  ordinally sorted `post-load/*.sql`. Dry-run and actual execution use the same
  resolver; publish output includes the repair exactly once under `sql/`.
- `ScriptRunner.RunAsync(NpgsqlConnection, CancellationToken)` accepts a
  caller-owned open connection so provider tests can execute two deployments
  inside one rollback-owned transaction. The provider-gated idempotency test
  verifies canonical defaults and fingerprints seeded row values/counts before
  and after the second run.
- `share-dev-database.sh` no longer suppresses `pg_restore`. It returns the
  exact restore status, stops before post-restore success output on failure,
  and requires valid nonzero public-table and TimescaleDB hypertable counts
  before printing `Restore complete.`
- TDD evidence: RED was 4 expected failures, 4 passes, and 2 provider skips;
  GREEN is 8 focused passes with 2 provider skips. The full portal project
  passes 352 tests with seven provider-gated skips. Shell syntax, a fake-Docker
  failure/zero/success harness, actual publish contents, the portal solution
  build, and diff checks pass.
- A pre-commit implementation review found no Critical, Important, or Minor
  issues. Live PostgreSQL double-run/idempotency remains unverified because
  `RVT_TEST_POSTGRES_CONNECTION` is unset; it must run against a dedicated test
  database before deployed-schema closure is claimed.

## Immediate Blockers Task 6 Review Fix - 2026-07-23

- Canonical schema deployment is now complete-or-fail. `ResolveScripts`
  requires `create_unmapped_schema.sql`,
  `restore_unmapped_column_defaults.sql`, and at least one non-AppleDouble
  `post-load/*.sql` file before returning a list. Missing stages throw a clear
  `DeployException` before dry-run output or any PostgreSQL connection attempt.
- The canonical order remains create, repair, then ordinally sorted post-load
  scripts, each exactly once. Both public execution paths still use the same
  validated resolver.
- Review-fix test variables are `dryRun`, `verificationCounts`,
  `expectedStage`, and `expectedError`. Six stage cases cover dry-run and real
  execution mode for missing create, missing repair, and sidecar-only
  post-load. Shell cases independently cover `5|0`, `x|2`, and `5|x` in
  addition to the existing `0|0`, failure-status, and success controls.
- Review-fix RED was `6 failed, 11 passed, 2 provider skips`; GREEN was
  `17 passed, 2 provider skips`. The full portal project passes `361` with
  seven provider skips (`368` total), publish contains the repair exactly once
  and dry-runs seven scripts in order, Bash syntax and diff checks pass, and the
  portal solution builds with zero errors plus the five existing NU1903
  advisories.
- Live PostgreSQL remains deliberately unclaimed because
  `RVT_TEST_POSTGRES_CONNECTION` is unset.

## Immediate Blockers Task 4 Verification Resume - 2026-07-23

- Task 4 implementation checkpoint `74d8696` passed the three exact critical
  relational/onboarding controls, the 30-test security/host slice, and the host
  build. The full portal suite initially exposed an unrelated midnight-sensitive
  Task 3 test fixture: its authorization clock was fixed at July 22 while its
  contract/deployment seed dates came from the July 23 wall clock.
- `NotificationAlertWorkflowTests.SeedNotificationAlertScenarioAsync` now has
  optional variable `scenarioNowUtc`. The two fixed-clock active-assignment
  tests pass their existing `nowUtc.UtcDateTime`, keeping all scenario dates and
  authorization boundaries on the same deterministic instant. Production code
  and non-fixed scenario tests are unchanged.
- After the fixture correction, both affected boundary tests pass 2/2 and the
  full portal project passes 337 tests with the same three opt-in PostgreSQL
  skips. Existing NU1903 advisories and the untracked `apps/.nuget-packages/`
  cache remain outside Task 4.

## Immediate Blockers Task 5 - 2026-07-23

- Status: implementation and non-provider verification are complete on
  `codex/immediate-blockers`; final disposition is `DONE_WITH_CONCERNS` because
  real PostgreSQL evidence is unavailable. `RVT_TEST_POSTGRES_CONNECTION` is
  unset, the repository has no Testcontainers harness, and sandbox/approval
  failures prevented Docker image inspection or container startup.
- Timestamp contract:
  - application search bounds are UTC `DateTime` values;
  - `SearchTimestampPolicy.ToDatabase` accepts only `Kind=Utc`, preserves ticks,
    and changes the provider-bound value to `Kind=Unspecified` for PostgreSQL
    `timestamp without time zone`;
  - `SearchTimestampPolicy.FromDatabase` preserves ticks and restores
    `Kind=Utc` before telemetry rows and graph points reach JSON;
  - daily aggregate `SampleTime` values keep database `date` semantics;
  - contract `OnHireDate` and nullable `OffHireDate` persist as UTC midnight,
    preserving calendar dates without workstation-local conversion.
- File structure:
  - new policy:
    `apps/portal/RvtPortal.Spa/Application/Monitors/SearchTimestampPolicy.cs`;
  - query-boundary changes:
    `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorService.cs`;
  - API-return boundary:
    `apps/portal/RvtPortal.Spa/Application/Data/DataApplicationService.cs`;
  - complete EF mapping audit:
    `apps/portal/RVT.DataAccess/Context/RVTSearchContext.cs`;
  - contract persistence helper:
    `apps/portal/RvtPortal.Spa/Application/Contracts/ContractCommands.cs`;
  - backend controls:
    `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`,
    `apps/portal/RvtPortal.Spa.Tests/SearchTimestampPostgresTests.cs`, and
    `apps/portal/RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`;
  - client contract seam/control:
    `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.tsx` and
    `DataViewPanels.test.tsx`.
- EF provider mapping variables: `dateTimeColumnType` remains
  `timestamp without time zone` for PostgreSQL and `datetime` for SQL Server.
  The model test enumerates all twelve `SampleTime` properties. The approved
  daily/date entries are `NoiseLevel1dayAvg`, `NoiseLevelSiteAvg`, and
  `OmnidotsPeakLevel1dayPeak`; the other nine use `dateTimeColumnType`.
- Test variables and provider gate:
  `RequiresPostgresFactAttribute.ConnectionVariable` is
  `RVT_TEST_POSTGRES_CONNECTION`; the inserted provider-test timestamp is
  `2026-07-01 14:30:00`, queried with UTC bounds and expected in JSON as
  `2026-07-01T14:30:00Z`. The separate provider test persists contract date
  `2026-07-01` through `UtcTimestampGuardInterceptor`.
- TDD evidence: the focused pre-change run failed all seven backend cases for
  the intended Kind/mapping/guard/JSON reasons, and the Europe/London client
  control failed before the formatter contract was exposed. The corresponding
  focused backend run passes 7/7; the owning backend slice passes 20 with the
  two new PostgreSQL tests skipped; the full portal suite passes 344 with five
  PostgreSQL skips (349 total). The client test passes under both
  `TZ=Europe/London` and `TZ=UTC`; the full client suite passes 66 with the
  timezone-specific control skipped under the Athens host zone, and the client
  production build succeeds. The portal host build succeeds with zero warnings
  and zero errors.
- Provider concern: neither new PostgreSQL test has executed against Npgsql and
  a live schema. They are discovered and skip explicitly when the connection
  variable is absent; this task must not be treated as provider-closed until
  both run with `RVT_TEST_POSTGRES_CONNECTION` configured.
- Environment note: one exact provider-filter attempt without `-m:1` entered an
  MSBuild IPC retry loop after sandbox socket denial; a targeted process-stop
  escalation was rejected by the broken approval backend. Fresh serial
  (`-m:1`) focused, owning, full-suite, and build commands nevertheless
  completed successfully. The existing NU1903 advisories and untracked
  `apps/.nuget-packages/` cache remain outside Task 5.

## Immediate Blockers Task 5 Review Follow-up - 2026-07-23

- Status: the review findings are fixed on `codex/immediate-blockers`. This
  section supersedes the earlier Task 5 statements that treated
  `NoiseLevelSiteAvg.SampleTime` as `date` and described the client timezone
  control as conditional.
- PostgreSQL/EF contract:
  - `noise_level_site_avg.sample_time` and
    `air_q_noise_level_site_avg.sample_time` are non-daily
    `timestamp without time zone` values;
  - only `NoiseLevel1dayAvg` and `OmnidotsPeakLevel1dayPeak` remain `date`
    among the mapped noise/vibration aggregates;
  - the 8-hour dust, hourly AirQ/final noise, and 1/5/15/20-minute vibration
    `COALESCE` fallbacks now use
    `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'`, preventing PostgreSQL from
    promoting the UTC-naive aggregate expression to `timestamptz`;
  - `RVTSearchContextModelSnapshot` now matches the runtime PostgreSQL model
    for `NoiseLevel15minAvg` and `NoiseLevelSiteAvg`;
  - `SearchTimestampPostgresTests` compares runtime EF metadata, snapshot
    source, and checked-in view SQL, while the provider-gated
    `AggregateViews_HaveExpectedProviderTypesAndAcceptUtcNaiveBounds` variable
    `expectedViewTypes` inspects and queries all affected views plus the two
    genuine daily views.
- Request boundary:
  - `DataApplicationService.NormalizeUtc` was removed;
  - application workflows return `InvalidTimestamp` for Local or Unspecified
    `FromDate`/`ToDate` values instead of relabeling ticks;
  - `DataController.TimestampQueryFields` is `["fromDate", "toDate"]` and
    rejects wire values that are not explicit `Z` instants, preserving the
    distinction model binding loses for offset strings;
  - `DataViewPanels.fromDateToApi` converts browser `datetime-local` wall time
    through `new Date(value).toISOString()` before API calls.
- Response/display boundary:
  - `MonitorDetailSummaryService.BuildMetric` applies
    `SearchTimestampPolicy.FromDatabase` to dust, noise, and vibration metric
    timestamps, so detail JSON includes `Z`;
  - `formatDateTime(value, timeZone?)` defaults to the production local zone,
    while the ordinary client test exercises explicit `UTC` and
    `Europe/London` zones in one process with no conditional skip.
- Review-fix TDD:
  - RED backend: `0 passed, 5 failed, 2 provider skips`; RED client:
    `0 passed, 2 failed`;
  - focused GREEN: backend `5 passed, 2 provider skips`; client `2 passed`;
  - owning backend slice: `34 passed, 2 provider skips`;
  - full portal suite: `347 passed, 6 provider skips, 353 total`;
  - full client suite: `68 passed`, no skips;
  - client production build succeeded;
  - portal solution build succeeded after restoring the previously absent
    `RVT.SchemaDeploy/obj/project.assets.json`, with only the repository's
    existing five NU1903 advisory warnings.
- Provider concern remains: `RVT_TEST_POSTGRES_CONNECTION` is unset, so the
  expanded live metadata/query test and the existing telemetry/provider tests
  are discovered but not executed. No provider workaround was attempted after
  controller direction.

## Immediate Blockers Final Whole-Branch Review Fix - 2026-07-23

- Trace-index timestamps now follow the same explicit UTC application/plain
  PostgreSQL timestamp boundary as other search telemetry. Both
  `MonitorDataSource.GetTraceIndexesAsync` range bounds and
  `MonitorService.GetVibrationTracesIndex` point-in-time bounds pass through
  `SearchTimestampPolicy.ToDatabase`, preserving ticks, converting UTC Kind to
  Unspecified only at the EF query boundary, and rejecting Local or
  Unspecified application inputs.
- Trace list `StartTime`/`EndTime` and trace detail `FromDate`/`ToDate` pass
  through `SearchTimestampPolicy.FromDatabase` before DTO serialization.
  Database-style Unspecified values therefore leave the API as explicit UTC
  JSON instants ending in `Z`; routes and DTO property names are unchanged.
- Timestamp controls added to
  `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs` inspect real EF command
  parameter Kinds, reject both non-UTC Kinds, and drive the authenticated HTTP
  trace list/detail routes with database-style values. The PostgreSQL contract
  test covers runtime and snapshot `OmnidotsTracesIndex.StartTime`/`EndTime`
  mappings. The provider-gated `TraceIndexes_UtcBounds_QuerySuccessfullyAndReturnUtcJson`
  test inserts a real trace index, queries listing/detail with UTC bounds, and
  checks list/detail JSON `Z` output when
  `RVT_TEST_POSTGRES_CONNECTION` is configured.
- `share-dev-database.sh` now always makes a best-effort
  `timescaledb_post_restore()` call after `pg_restore` fails. Cleanup failure
  emits a distinct manual-cleanup diagnostic, while both cleanup-success and
  cleanup-failure paths return the original `pg_restore` status exactly and
  skip ANALYZE, verification queries, and completion output. The ordinary
  successful restore sequence is unchanged.
- Final-review variables are `databaseFromDate`, `databaseToDate`,
  `cleanup_status`, `cleanupStatus`, `FAKE_POST_RESTORE_STATUS`,
  `TraceBoundCommandProbe.DateTimeParameters`, `databaseStartTime`,
  `databaseStart`, and `databaseEnd`.
- TDD evidence: the first focused RED run produced six intended failures, one
  existing mapping pass, and one PostgreSQL skip; its focused GREEN run passed
  seven with one PostgreSQL skip. The point-in-time trace-index follow-up RED
  failed all three intended cases and its GREEN passed all three.
- Verification: the owning backend/schema slice passes 37 tests with three
  explicit PostgreSQL skips; the dedicated fake-Docker restore harness passes
  7/7; `bash -n` passes; the full portal project passes 369 tests with eight
  PostgreSQL skips (377 total); and the portal solution builds with zero errors
  plus the five pre-existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisory warnings. `git diff --check` is clean.
- Live PostgreSQL remains the sole provider evidence gap because
  `RVT_TEST_POSTGRES_CONNECTION` is unset. The new trace listing/detail test is
  discovered and explicitly skipped; no deployed-schema closure is claimed.
  The generated untracked `apps/.nuget-packages/` cache remains excluded from
  the commit.
- Final independent whole-branch re-review of `5048052..b5e5bd1` found no
  Critical, Important, or Minor issues. It confirmed both prior Important
  findings are resolved and assessed the five-area immediate-blocker tranche as
  ready to merge, subject to running the eight provider-gated tests against a
  dedicated PostgreSQL/TimescaleDB database before production rollout.

## Immediate Blockers Main Integration - 2026-07-23

- Local `main` was fast-forwarded from `5048052` to the reviewed immediate-
  blockers tip `0b30293`. The pending integration commit only corrects the
  password-reset security tests described below; production behavior is
  unchanged.
- The former worktree produced a false-green result because its
  `apps/portal/RvtPortal.Spa/appsettings.json` carried the macOS `hidden` file
  flag. ASP.NET Core's physical configuration provider omitted that file, so
  `AllowedHosts` was absent. The normal checkout correctly loads
  `AllowedHosts=localhost;127.0.0.1` and rejects `Host: attacker.example`
  before the authentication controller runs.
- `SecurityHardeningTests` now separates three contracts:
  a disallowed request host returns `400` without delivery; an allowed request
  with no `Authentication:PublicBaseUrl` returns the generic `200` response
  without delivery; and an allowed request with a configured public base URL
  sends a callback rooted at that configured origin.
- Current verification from the normal checkout:
  portal backend `370 passed, 8 provider-gated skips, 378 total`; client
  `68 passed`; client production build succeeded; monorepo solution/layout,
  RVT source-boundary, local-package sequencing, and documentation guards all
  passed. Four existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisories remain.
- Generated local state is intentionally untracked:
  `.codegraph/` in the normal checkout and
  `apps/.nuget-packages/` in the former immediate-blockers worktree.
- Next approved branch: `codex/sites-application-boundary`. Its scope is to
  introduce `RvtPortal.Application` and extract the complete Sites slice
  incrementally, retaining route and payload compatibility and avoiding a
  broad rewrite. The design must be written and reviewed before implementation.

## Sites Application Boundary Design - 2026-07-23

- Active branch: `codex/sites-application-boundary`, created from and tracking
  the pushed `main` integration commit `f7db3dd`.
- Approved design:
  `docs/superpowers/specs/2026-07-23-rvtportal-sites-application-boundary-design.md`.
- The new `RvtPortal.Application` project is BCL-only for the first slice: no
  NuGet or project references, ASP.NET Core, EF Core, DataAccess, host, or
  vendor SDK dependencies.
- `RvtPortal.Application` owns Sites use cases, contracts, results,
  `PortalUserContext`, the explicit UTC active-assignment policy, and focused
  read/write/archive/logo/user-directory/unit-of-work ports.
- `RvtPortal.Spa` retains controllers, API mapping, DI composition, optimized
  EF projections, Identity, archive/storage implementations, and the existing
  three-context `EfCoreUnitOfWork`.
- The full Sites surface includes list/options/detail, monitors, open
  notifications, create/update/archive, notification settings, authorization,
  and all customer-logo operations. Routes and payloads remain unchanged.
- Implementation order is scaffold, policies/contracts, reads, writes,
  controller cutover, and documentation. The design review and a task-level
  implementation plan are required before production code changes.

## Communication Abstractions Extraction - 2026-07-24

- `Rvt.Communication.Abstractions` now owns provider-neutral delivery ports,
  requests, failure exceptions, notification contracts, `LegacyMessageKind`,
  `LegacyMessageChannel`, and the source-compatible
  `Rvt.Monitor.Common.Notifications.RvtContactDto` type. The abstractions
  project has no project or provider dependencies.
- `Rvt.Monitor.Common` now references Abstractions; its active callers use the
  moved contracts and top-level legacy enums. `MessageService`,
  `NotificationDeliveryService`, and `NotificationMessageComposer` remain in
  Common for the next communication implementation-move task.
- Focused verification: abstraction tests pass 20/20. Common tests pass
  405/407; the only two failures are the accepted
  `MonitorDeliveryMigrationContractTests` path baseline. The former three
  `CommunicationsBoundaryTests` path failures are green.
- Aggregate source compilation reported no C# errors after the split. The
  immediate four-package validation bridge is in place for the Common
  package's new `Rvt.Communication.Abstractions` dependency; the broader
  package-release migration remains a later task.

### Task 1 Package Validation Bridge Fix

- The immediate four-package bridge now restores, packs, asserts, and evicts
  `Rvt.Communication.Abstractions` alongside the prior three packages. The
  RuntimeConsumer artifact lock was regenerated and includes the Common
  package's transitive abstraction dependency.
- The focused prerequisite guard passes, `dotnet build Rvt.Mono.slnx
  --no-restore --nologo` succeeds with zero errors, and Task 1's focused
  abstraction/Common results remain 20/20 and 405/407 respectively. The two
  Common migration-path failures and the unrelated aggregate imported-suite
  path/provider failures remain baseline exceptions.

## Communication Task 4: Microsoft Graph Mail Extraction - 2026-07-24

- `Rvt.Communication.MicrosoftGraphMail` owns Microsoft Graph email delivery:
  the Graph adapter, token-provider port and Azure Identity implementation,
  source-generated Graph models/context, upload sessions, provider options,
  registration, and startup validation. Its only project dependency is
  `Rvt.Communication.Abstractions`; Azure Identity is provider-local.
- `MicrosoftGraphMailOptions` maps `RVT:...` before literal `RVT__...` aliases
  for `EMAIL_ENABLED`, tenant ID, client ID, client secret, and sender address.
  Disabled providers permit absent credentials; enabled validation reports all
  missing key names without including secret values.
- The temporary `Rvt.Monitor.Common.Infrastructure` selector retains the
  `EmailProvider` runtime choice for the five monitor hosts, but constructs the
  moved Graph adapter from provider-owned Graph options. It directly references
  GraphMail and its compatibility package pins all four internal dependencies
  (Common, Communication, SendGridMail, and MicrosoftGraphMail) exactly.
- Root `Rvt.Mono.slnx` inventory contains the GraphMail source project under
  `/Libraries/RVT Monitor Common/` and its tests under `/Tests/`; the temporary
  compatibility bridge packs exactly seven packages and keeps Infrastructure
  packing at `-m:1`.
- Focused results: GraphMail tests 31/31, Infrastructure tests 31/31,
  package-artifact tests 16/16, solution inventory and source-boundary bridge
  guards pass, and all five monitor hosts build with `--no-restore`. Existing
  MSTest analyzer warnings and unrelated untracked Portal duplicate C# files
  remain outside this extraction.

## Communication Task 8: Legacy Infrastructure Removal - 2026-07-24

- The tracked `Rvt.Monitor.Common.Infrastructure` source and test projects are
  removed. Both solutions now list the five extracted communication projects
  and their five test projects; active monitors deliberately reference all
  three providers, while Portal remains limited to Abstractions and SendGrid.
- Communication ownership guards now confine SendGrid, Microsoft Graph, and
  TransmitSMS implementation markers to their provider projects and reject a
  reintroduced Infrastructure project. The transitional `build-mono.sh` graph
  now packs seven packages and no longer builds or validates Infrastructure.
- Provider projects no longer depend on the broad `Microsoft.AspNetCore.App`
  framework. They declare exact 10.0.9 Configuration, DI, Hosting, and HTTP
  dependencies as required; SendGrid and Azure Identity remain provider-local.
- Verification: ownership 7/7, SendGrid 20/20, Graph 31/31, TransmitSMS 24/24,
  MyAtm boundary 12/12, both shell boundary tests pass, and `rvt-common.sln`
  restores with temporary delegated locks and builds with zero errors.
- Lock regeneration remains delegated to the eleven-package release plan.
  Portal testing remains externally blocked by the preserved untracked
  duplicate `* 2.cs` files. Blob client/service unification and every other
  storage, compatibility, plugin, database, MQTT, scheduling, observability,
  notification, API, and persisted-record item remain future pending work.
