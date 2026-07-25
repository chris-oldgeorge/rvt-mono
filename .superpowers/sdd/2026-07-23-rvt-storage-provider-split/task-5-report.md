# Task 5 Report: Provider Contract Parity and Dependency Isolation

Date: 2026-07-25

## Scope

Task 5 started from `65608a4` (`feat(storage): extract S3 adapter`) in
`.worktrees/release-platform-hardening`.

The change adds only the shared real-provider contract suite, provider
fixtures, dependency-boundary tests, this report, and the project-state entry.
No provider production source, package version, or lock file changed.

## Shared provider contract

`ObjectStorageClientContractTests` defines one reusable eight-test behavior
contract and runs it unchanged against Local, Azure Blob, and S3:

- a non-seekable write returns the same normalized provider-neutral key;
- a subsequent read returns equal bytes, content type, and content length;
- a missing key returns `null`;
- a second write replaces the prior bytes and metadata;
- delete returns `true` for the existing object and `false` afterward;
- pre-cancelled write, read, and delete each propagate cancellation and a
  subsequent uncancelled read proves the original object did not mutate.

Every test creates and asynchronously disposes an isolated fixture. The Local
fixture uses a unique real temporary filesystem root. Azure and S3 instantiate
the real `AzureBlobObjectStorageClient` and `S3ObjectStorageClient`; only their
external SDK boundaries are doubled.

The Azure double uses strict `BlobContainerClient` and `BlobClient` mocks with
an ordinal in-memory object dictionary behind upload, streaming download, and
delete. The S3 double uses strict `IAmazonS3` mocks with the same stateful
semantics behind put, get, metadata, and delete. Both throw on caller
cancellation before reading or mutating state, and neither contacts a network,
emulator, credential provider, or cloud endpoint.

## Dependency boundaries

`StorageDependencyBoundaryTests` locates the repository root from either the
test working directory or assembly base directory, parses each provider
project with `XDocument`, and reads only production `*.cs` files under that
project. Paths containing exact `obj` or `bin` segments are excluded and
asserted absent from the snapshot.

The guards enforce:

- `Rvt.Storage.Abstractions` has no package or project references and no Azure,
  Amazon, Microsoft.Extensions, or direct filesystem API source coupling;
- `Rvt.Storage.Local` references only the abstraction project and no Azure or
  Amazon package/source dependency;
- `Rvt.Storage.AzureBlob` references the abstraction project, requires
  `Azure.Identity` and `Azure.Storage.Blobs`, and excludes AWS/Amazon;
- `Rvt.Storage.S3` references the abstraction project, requires `AWSSDK.S3`
  and Amazon source usage, and excludes Azure.

The assertions operate on dependency identifiers and broad source dependency
markers rather than mirroring exact implementation lines.

## Test-first evidence

### Contract RED

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~ObjectStorageContractTests --nologo -v minimal
```

The shared contract was first run against deliberate unimplemented fixture
shells. All 24 discovered cases failed with the expected fixture
`NotImplementedException` (8 behaviors across 3 providers).

### Contract GREEN

The same command passed 24/24 after replacing the shells with the real Local
fixture and strict stateful Azure/S3 SDK-boundary fixtures.

### Boundary RED/GREEN

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundaryTests --nologo -v minimal
```

The four boundary cases first failed on the deliberate unimplemented project
snapshot reader, then passed 4/4 after the repository/project/source reader was
implemented.

## Final verification

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --nologo -v minimal
```

Passed 137/137: the prior 109 tests plus 24 provider contract cases and 4
dependency-boundary cases.

`git diff --check` passed. Existing unrelated untracked worktree files were
left untouched and excluded from staging.

## Concerns and exclusions

No real provider inconsistency surfaced, so provider production code remains
unchanged. The repository code index request was policy-blocked because it
could export private source; all implementation inspection therefore remained
local-only.

Consumer migrations, legacy Common storage removal, solution/packaging work,
Portal storage, the independent `services/reporting` Azure adapter, and every
other future-pending item remain excluded for Tasks 6-10 or later plans.
