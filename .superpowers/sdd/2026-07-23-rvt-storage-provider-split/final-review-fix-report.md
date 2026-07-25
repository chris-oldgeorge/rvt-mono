# Storage Provider Split — Final Review Fix Report

Date: 2026-07-26

Base: `0539691` (`docs(storage): correct report URI ownership`)

## Scope

This corrective task addresses the two whole-plan blocking findings and the
stale dependency inventory:

1. Azure Blob and S3 leaked provider-triggered, non-caller
   `OperationCanceledException` values instead of the neutral storage failure
   contract.
2. Five storage locks and a storage-test-only CodeAnalysis package/catalog
   addition escaped the approved source-split scope.
3. The dependency license review described seven projects rather than the
   current 20-project `rvt-common.sln` graph.

Unrelated tracked and untracked files, existing Task 10 documentation/state,
future-pending storage migrations, and provider-package release work were
preserved. No push was performed.

## Root cause and provider repair

All three operations in both `AzureBlobObjectStorageClient` and
`S3ObjectStorageClient` had a filtered catch that correctly rethrows when the
supplied caller token is cancelled. They then caught only their provider SDK
exception type. A provider-internal timeout represented as
`OperationCanceledException` therefore escaped unchanged whenever the caller
token remained active.

Six focused real-client-fixture tests cover Azure and S3
`WriteAsync`, `OpenReadAsync`, and `DeleteIfExistsAsync`. Each uses an active
caller token, makes the real adapter call its SDK-boundary double, and asserts:

- exact `ObjectStorageException`;
- `StorageFailureKind.Unavailable`;
- the configured resource name;
- the same requested `StorageObjectKey`;
- the provider cancellation retained as `InnerException`;
- provider text absent from the neutral exception message.

The production change is limited to a second cancellation catch after the
existing caller-cancellation filter in each operation, plus one small
translation helper per provider. Status/missing/disposal behavior is
unchanged.

### Strict RED/GREEN evidence

RED command:

```bash
dotnet test \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --no-restore -m:1 \
  --filter 'FullyQualifiedName~WhenProviderCancelsWithoutCallerCancellation' \
  --nologo --verbosity minimal
```

Result: exit 1; 0 passed, 6 failed, 0 skipped. Every failure expected exact
`ObjectStorageException` and received raw `OperationCanceledException`.

Focused GREEN and caller-cancellation preservation:

```bash
dotnet test \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --no-restore -m:1 \
  --filter \
  'FullyQualifiedName~WhenProviderCancelsWithoutCallerCancellation|FullyQualifiedName~WhenCallerCancels' \
  --nologo --verbosity minimal
```

Result: exit 0; 8 passed, 0 failed, 0 skipped.

Test variables are `providerMessage`, `cancellation`, and `expectedKey`.

## Lock, catalog, and dependency-boundary correction

The following source-split locks are deleted:

- `src/Rvt.Storage.Abstractions/packages.lock.json`;
- `src/Rvt.Storage.Local/packages.lock.json`;
- `src/Rvt.Storage.AzureBlob/packages.lock.json`;
- `src/Rvt.Storage.S3/packages.lock.json`;
- `tests/Rvt.Storage.Tests/packages.lock.json`.

No other tracked lock changes. Atomic lock regeneration remains delegated to
the subsequent provider-package release migration.

The conditional `Microsoft.CodeAnalysis.CSharp` central version and the
storage-test project reference are removed. The dependency boundary now uses
only framework/repository facilities:

- a BCL lexical sanitizer excludes line/block comments, character literals,
  ordinary/verbatim/raw literal text, and retains executable interpolation
  holes;
- BCL regular expressions recognize namespace imports, local/global aliases,
  qualified names, and the guarded implicit `System.IO` type names;
- aliases resolve across source files and user-defined filesystem lookalikes
  covered by the regressions remain excluded;
- dependency matches retain their production source paths.

All existing boundary regression tests remain. Focused boundary result:
13 passed, 0 failed, 0 skipped.

Analyzer variables are `AliasUsingPattern`, `NamespaceUsingPattern`,
`QualifiedNamePattern`, `DeclaredTypePattern`, `ImplicitSystemIoTypes`,
`sanitizedSources`, `aliases`, and `filesByDependency`.

## Temporary restore classification

The first bounded restore followed the plan's
`RestorePackagesWithLockFile=false`, `RestoreLockedMode=false` override. It
failed immediately with `NU1005`: .NET 10 rejects lock generation disabled
when other solution projects still have checked-in lock files.

The successful bounded retry was serial and redirected lock output outside
the repository:

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  -p:RestorePackagesWithLockFile=true \
  -p:RestoreLockedMode=false \
  -p:NuGetLockFilePath='/private/tmp/rvt-storage-final-fix-locks.ks9vR8/$(MSBuildProjectName).packages.lock.json' \
  --nologo --verbosity minimal -m:1
```

Result: exit 0; all 20 projects restored. The literal redirected temporary
lock output is outside the repository, and no tracked lock was created or
modified.

The refreshed `dotnet list ... --include-transitive --format json
--no-restore` result contains 20 projects and exactly 101 distinct
package/version pairs. A mechanical comparison against
`dependency-license-review.md` reports 101 actual, 101 documented, no missing
pair, and no stale pair. Neither the refreshed graph nor project/package
catalog source contains `Microsoft.CodeAnalysis.CSharp`.

The bounded current vulnerability-audit attempt exited before producing a
result because sandbox DNS could not resolve `api.nuget.org`. The dependency
document records that limitation and makes no refreshed audit-clean claim.

## Verification

- Focused provider/caller cancellation: 8 passed, 0 failed, 0 skipped.
- Storage dependency boundary: 13 passed, 0 failed, 0 skipped.
- Full storage suite: 154 passed, 0 failed, 0 skipped.
- `./tests/verify-mono-layout.test.sh`: exit 0.
- `./tests/verify-mono-solution.test.sh`: exit 0.
- `./tests/verify-rvt-common-source-boundary.test.sh`: exit 0.
- `./tests/verify-rvt-common-source-boundary-regression.test.sh`: exit 0.
- Storage lock working-tree search: no lock exists in any of the five storage
  project directories.
- Project/package catalog search: no
  `Microsoft.CodeAnalysis.CSharp` occurrence.
- Dependency inventory/document comparison: 101/101, no missing/stale pair.
- `git diff --check`: no output.

The full storage compilation still reports existing MSTest analyzer warnings
for repository test-style configuration and obsolete `DataTestMethod`
attributes; no new warning class was introduced by this fix.

## Remaining blocker

Microsoft Graph large-attachment upload-chunk non-caller timeout translation
remains outside the storage task and is still the carry-forward merge blocker.
This correction does not claim the overall provider branch is ready to merge,
release, or deploy.
