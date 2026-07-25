# Task 3 Report: Azure Blob Storage Adapter

Date: 2026-07-25

## Scope

Task 3 started from `406f057` (`feat(storage): extract local storage adapter`)
in `.worktrees/release-platform-hardening`.

The change adds the packable `Rvt.Storage.AzureBlob` provider, Azure-specific
options and composition, strict offline SDK-double tests, and only the lock
changes caused by that provider and its tests. The legacy
`Rvt.Monitor.Common` storage sources and tests were read as the behavior
reference and remain unchanged.

CodeGraph was consulted before local inspection as required, but the request
was rejected because the tool destination was not approved to receive private
repository source. All subsequent comparison used read-only local files.

## Behavior preserved

- `AzureBlobStorageOptions.Bind` keeps the current provider-neutral,
  `RVT:`, literal `RVT__`, legacy audio-folder, and custom reporting-container
  aliases and precedence.
- A nonblank connection string takes precedence over the service URI, including
  an invalid URI. Otherwise, the provider requires an absolute service URI and
  uses `DefaultAzureCredential`.
- The container is required and trimmed before binding the
  `BlobContainerClient`. Prefixes are normalized through the shared validated
  `StorageObjectKey` boundary and reject traversal.
- Registration follows the Local provider pattern: one keyed singleton Azure
  client, one named registration, the shared named-client factory, and one
  startup validator.
- Writes create the container if needed and pass the original request stream
  directly to Azure upload with overwrite semantics and optional
  `BlobHttpHeaders.ContentType`. Returned keys remain provider-neutral and
  unprefixed.
- Reads use `DownloadStreamingAsync` without buffering, return SDK content type
  and length, return `null` for status 404, and transfer the raw Azure response
  into the shared read-result disposal lease.
- Deletes return the Azure SDK boolean. Status 403 maps to `AccessDenied`, 409
  to `Conflict`, and 408, 429, and 5xx to `Unavailable`. Other client failures
  map to `InvalidRequest`, and unclassified failures map to `Unknown`.
- Caller cancellation propagates as `OperationCanceledException`. Shared
  exception messages contain only the logical resource, safe object key, and
  failure kind; Azure response text and inner exception text are not copied
  into the message.
- `GetObjectUri` is an Azure-specific concrete API and returns the URI of the
  prefix-bound `BlobClient`; it is not added to `IObjectStorageClient`.

## Strict TDD evidence

### Options and registration RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~AzureBlobStorageOptionsTests|FullyQualifiedName~AzureBlobStorageRegistrationTests' \
  --nologo -v minimal
```

Result: exit 1. Restore skipped the absent
`Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj`, and compilation failed
because `Rvt.Storage.AzureBlob` and `AzureBlobStorageOptions` did not exist.

### Options and registration GREEN

The same command passed 18/18 tests.

### Streaming operations RED

Command:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~AzureBlobObjectStorageClientTests --nologo -v minimal
```

Result: exit 1, 0 passed and 14 failed. Every failure was caused by the
deliberate `NotImplementedException` operation shell.

### Streaming operations GREEN

The same command passed 14/14 tests. All `BlobContainerClient` and `BlobClient`
doubles use `MockBehavior.Strict`; no network endpoint, emulator, or Azure
credential call is used.

## Final verification

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~AzureBlob --nologo -v minimal
```

Passed 32/32.

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --nologo -v minimal
```

Passed 76/76.

```bash
dotnet build libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj \
  --no-restore --nologo -v minimal
```

Succeeded with 0 warnings and 0 errors.

`git diff --check` also passed.

## Dependency and lock scope

- Reused central `Azure.Identity` 1.15.0, `Azure.Storage.Blobs` 12.25.0, and
  Microsoft.Extensions 10.0.9 versions without modifying central package
  versions.
- Added the new provider's conventional `packages.lock.json`.
- The storage test lock adds only the Azure provider graph and Moq/Castle.Core
  required for the strict SDK doubles.

## Exclusions and concerns

No legacy storage deletion, Portal storage work, Svantek or other consumer
migration, independent `services/reporting` Azure work, solution/package
integration, or unrelated package/lock work was performed. Those remain owned
by later tasks.

The only process limitation was the rejected CodeGraph source query described
above; it did not block local behavior comparison, implementation, or test
coverage.
