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

## Review hardening round 1

The first review round identified three test-quality gaps. This follow-up
changes tests and documentation only:

- Azure contract coverage now increments and asserts the strict raw
  `Response.Dispose` callback. S3 uses a disposal-counting response stream and
  asserts exactly two disposals: the first from `StorageReadResult` content
  ownership and the second from the retained `GetObjectResponse` lease. This
  makes response disposal distinct from stream disposal alone.
- Project dependency parsing now processes every matching MSBuild item in
  document order. `Include` and `Update` activate each semicolon-separated
  identity; `Remove` deactivates it. The same reader handles both package and
  project references.
- Raw source substring checks were replaced with a syntax-aware lexical
  dependency analyzer. It tokenizes identifiers and qualified names while
  skipping line/block comments and regular, verbatim, raw, interpolated, and
  character literals; understands `global::`, global using directives, and
  namespace/type aliases; and resolves unqualified filesystem boundary types
  to their canonical `System.IO` names.

### Review RED

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~ProviderResponseLease|FullyQualifiedName~StorageDependencyBoundaryRegressionTests' \
  --nologo -v minimal
```

Failed 5/5 for the intended gaps: Azure lease count 0 rather than 1, S3
response-stream disposal count 0 rather than 2, omitted `Update` plus retained
`Remove`, comment/string false positives, and unresolved `System.IO` alias
usage.

### Review GREEN

The same focused command passed 5/5 after the helper changes. The complete
contract filter passed 26/26, including the two provider response-lease cases.
The complete boundary filter passed 7/7, including the three regression
fixtures. The complete storage suite passed 142/142. `git diff --check` also
passed for the review-fix scope.

## Review hardening round 2

Round 2 supersedes the round-1 custom lexical source analyzer with Roslyn
syntax and semantic analysis. The storage test project alone references
`Microsoft.CodeAnalysis.CSharp` 5.0.0 with `PrivateAssets="all"`; its centrally
managed version is conditioned to `Rvt.Storage.Tests`. Only the storage test
lock receives the direct compiler package and its analyzer/common
dependencies. Provider source, provider projects, and provider locks remain
unchanged.

All production source files for one storage project are parsed as separate
syntax trees in one `CSharpCompilation`. Framework implicit usings and the
test output's managed reference graph are supplied as metadata references.
The analyzer records resolved namespace, type, alias-target, and containing
type symbols per source file. Consequently:

- executable expressions inside interpolated strings participate normally;
- global aliases resolve across source-file boundaries;
- comments and literal text have no semantic symbols;
- user-defined `File`, `Directory`, and `FileStream` types retain their own
  namespaces and cannot masquerade as `System.IO` dependencies.

The regression class and file are both named
`StorageDependencyBoundaryRegressionTests`, matching the focused filter.

### Round 2 RED

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundaryRegressionTests \
  --nologo -v minimal
```

Selected exactly six tests: three prior guards passed and three new semantic
regressions failed. The failures were the skipped `System.IO.File` call inside
an interpolation hole, the unresolved cross-file global `System.IO` alias,
and the false classification of a user-defined `File` type.

### Round 2 GREEN and final verification

The same focused command passed 6/6.

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundary --nologo -v minimal
```

Passed 10/10 (four project boundaries plus six focused regressions).

The provider contract filter passed 26/26 and the complete storage suite
passed 145/145. Locked restore and `git diff --check` passed. No provider
production code or lock changed.
