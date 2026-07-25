# Task 4 Report: S3 Storage Adapter

Date: 2026-07-25

## Scope

Task 4 started from `e7e6e5b` (`feat(storage): extract Azure Blob adapter`) in
`.worktrees/release-platform-hardening`.

The change adds the packable `Rvt.Storage.S3` provider, S3-specific options and
composition, strict offline `IAmazonS3` tests, and only the lock changes caused
by that provider and its tests. CodeGraph was consulted first. The complete
legacy S3 implementation and its options/registration tests were then read as
the behavior reference and remain unchanged.

## Behavior preserved

- `S3StorageOptions.Bind` keeps the current provider-neutral, `RVT:`, and
  literal `RVT__` aliases for the S3 bucket, shared prefix, region, service
  URL, and force-path-style flag. A custom default prefix remains supported.
- The bucket is required and trimmed when the client resolves. Prefixes are
  normalized through the shared validated `StorageObjectKey` boundary and
  reject traversal. A configured service URL must be absolute.
- `AmazonS3Config` preserves the exact current branches. Region-only
  configuration sets `RegionEndpoint`. A compatible-S3 service URL is
  normalized without its trailing slash, and a supplied region becomes
  `AuthenticationRegion`. `ForcePathStyle` is always copied.
- The public client constructs `new AmazonS3Client(config)`. There are no
  access-key, secret-key, session-token, or other static credential options, so
  the normal AWS SDK credential chain remains in effect.
- Registration follows the Local and Azure provider pattern: one keyed
  singleton S3 client, one named registration, the shared named-client
  factory, and one startup validator.
- Writes send the original caller stream directly through `PutObjectAsync`,
  keep `AutoCloseStream = false`, prefix the provider key, and copy optional
  content type. Returned keys remain provider-neutral and unprefixed.
- Reads call `GetObjectAsync` and return its response stream, content type, and
  content length without buffering. The successful response is passed as the
  shared read-result lease so disposal closes both the content and provider
  response. `NoSuchKey` and HTTP 404 return `null`.
- Deletes call `GetObjectMetadataAsync` first. Missing metadata returns
  `false`; otherwise `DeleteObjectAsync` receives the same bucket/key and the
  operation returns `true`.
- Status 403 maps to `AccessDenied`, 409 to `Conflict`, 408, 429, and 5xx to
  `Unavailable`, other 4xx to `InvalidRequest`, and remaining failures to
  `Unknown`. Caller cancellation propagates. Shared exception messages do not
  copy S3 response text or inner exception text.
- `GetObjectUri` is an S3-specific concrete API. It emits `s3://` URIs and
  escapes each provider-key path segment separately.

## Strict TDD evidence

### Options and registration RED

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~S3StorageOptionsTests|FullyQualifiedName~S3StorageRegistrationTests' \
  --nologo -v minimal
```

Result: exit 1. Restore skipped the absent
`Rvt.Storage.S3/Rvt.Storage.S3.csproj`, and compilation failed because the
`Rvt.Storage.S3` namespace and AWS SDK types did not exist in the test graph.

### Options and registration GREEN

The same command passed 18/18 tests. The registration fixtures supply a region
because AWSSDK v4 validates that a real client has either `RegionEndpoint` or
`ServiceURL`; separate configuration tests verify the region-only,
compatible-S3, and optional-region branches.

### Streaming operations RED

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~S3ObjectStorageClientTests --nologo -v minimal
```

Result: exit 1, 2 passed and 13 failed. URI construction and SDK-client
disposal already passed; every streaming, missing-object, delete, translation,
and cancellation case failed on the deliberate `NotImplementedException`
operation shells.

### Streaming operations GREEN

The same command passed 15/15 tests. Every AWS operation double uses
`Mock<IAmazonS3>(MockBehavior.Strict)`. No AWS endpoint, emulator, credential
request, or other network access is used.

## Final verification

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~S3 --nologo -v minimal
```

Passed 33/33.

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --nologo -v minimal
```

Passed 109/109.

```bash
dotnet build libs/rvt-monitor-common/src/Rvt.Storage.S3/Rvt.Storage.S3.csproj \
  --no-restore --nologo -v minimal
```

Succeeded with 0 warnings and 0 errors.

Locked restore succeeded for both the S3 provider and storage test project.
`git diff --check` also passed.

## Dependency and lock scope

- Reused central `AWSSDK.S3` 4.0.100.3 and Microsoft.Extensions 10.0.9
  versions without changing central package versions.
- Added the new provider's conventional `packages.lock.json`.
- The storage test lock adds only the S3 provider project graph and
  `AWSSDK.S3`/`AWSSDK.Core` graph required by the provider and strict doubles.

## Exclusions and concerns

No legacy storage deletion, provider parity/architecture work, Svantek or
ReportingMonitor migration, Portal storage work, independent
`services/reporting` Azure work, solution/package integration, or unrelated
package/lock work was performed. Those remain owned by Tasks 5-10 or later
plans.

No implementation concern remains within Task 4. The worktree's unrelated
pre-existing untracked files remain untouched and excluded from the commit.
