# Generic Blob Storage Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Svantek-specific `AzureBlobService` usage with a generic `rvt-monitor-common` blob storage port and local/Azure/S3 adapters, defaulting to local container storage.

**Architecture:** Introduce a `Rvt.Monitor.Common.Storage` port (`IBlobStorageService`) plus provider-specific adapters. The common package owns option binding, provider selection, path/key safety, and dependency injection; monitor apps consume only the port. Svantek sound-recording handling stores downloaded WAV bytes through the port and keeps database metadata behavior stable by recording the stored object key against the notification.

**Tech Stack:** .NET 10, ASP.NET Core DI, Azure.Storage.Blobs/Azure.Identity, optional AWSSDK.S3, MSTest, EF-backed Svantek DB client, Docker Compose local volumes.

## Global Constraints

- Default provider is local container storage.
- Azure Blob and S3 are opt-in through configuration.
- Do not write credentials to tracked `appsettings*.json`.
- Preserve existing Svantek behavior where the notification `RecordingLink` stores the generated object name.
- Keep the storage abstraction in `rvt-monitor-common`; monitor apps must not reference Azure/S3 SDK types directly.
- Keep live cloud access out of unit tests; use fakes and local temporary directories.
- Prefer async storage APIs internally and keep any synchronous compatibility wrappers at the existing job boundary only.
- Keep object names path-safe and prevent `..`, rooted paths, and platform path separator escapes.

---

## Current State

- Current service: `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobService.cs`.
- Current namespace: `SvantekMonitor.api.rvt_common.storage`, even though the file now lives in common.
- Current caller: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`.
- Current behavior:
  - Svantek finds unresolved alert notifications.
  - It asks the Svantek API for project files for a monitor/day.
  - It filters `.WAV` files by alert window.
  - It downloads the selected WAV bytes into memory.
  - It creates `AzureBlobService` directly.
  - It uploads `{notificationId}.wav`.
  - It stores `{notificationId}.wav` in `Notification.RecordingLink`.
- Current config:
  - `RVT__BLOB_CONNECTION_STRING`
  - `RVT__BLOB_SERVICE_URI`
  - `RVT__AUDIO_FOLDER`, default `audiofiles`
- Current gaps:
  - Azure-specific name and namespace.
  - No port/interface.
  - No local default.
  - No S3 adapter.
  - Static config access inside the adapter.
  - Direct construction in Svantek handler.
  - Upload blocks on `.Result`.
  - Object names are not centrally validated.

## Proposed Configuration

Keep old names as compatibility aliases where useful, but introduce provider-neutral names:

| Setting | Default | Purpose |
| --- | --- | --- |
| `RVT__BLOB_PROVIDER` | `Local` | `Local`, `AzureBlob`, or `S3`. |
| `RVT__BLOB_CONTAINER` | `audiofiles` | Logical container/bucket/folder grouping. Alias `RVT__AUDIO_FOLDER` for backward compatibility. |
| `RVT__BLOB_PREFIX` | empty | Optional object-key prefix, for example `svantek/audio`. |
| `RVT__BLOB_LOCAL_ROOT` | `/data/rvt/blobs` | Local container storage root. |
| `RVT__BLOB_CONNECTION_STRING` | empty | Azure Blob connection string fallback. |
| `RVT__BLOB_SERVICE_URI` | empty | Azure Blob service URI for managed identity. |
| `RVT__S3_BUCKET` | empty | Required when provider is `S3`. |
| `RVT__S3_REGION` | empty | Optional AWS region for S3. |
| `RVT__S3_SERVICE_URL` | empty | Optional S3-compatible endpoint, such as MinIO. |
| `RVT__S3_FORCE_PATH_STYLE` | `false` | Required for many S3-compatible local endpoints. |

Provider behavior:

- `Local`: writes files under `{RVT__BLOB_LOCAL_ROOT}/{container}/{prefix}/{objectName}`.
- `AzureBlob`: writes to `{container}` in Azure Blob Storage using either connection string or `DefaultAzureCredential` service URI.
- `S3`: writes to `{bucket}` using AWS SDK credential resolution, optionally under `{prefix}`.

## File Structure

Create:

- `rvt-monitor-common/Rvt.Monitor.Common/Storage/IBlobStorageService.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageOptions.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageProvider.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageWriteRequest.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageWriteResult.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobObjectName.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageServiceCollectionExtensions.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/LocalFileBlobStorageService.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobStorageService.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/S3BlobStorageService.cs`
- `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs`
- `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobObjectNameTests.cs`
- `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/LocalFileBlobStorageServiceTests.cs`
- `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`
- `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs`

Modify:

- `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- `rvt-monitor-common/Rvt.Monitor.Common/Configuration/RvtConfig.cs`
- `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobService.cs`
- `svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- `svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- `svantekmonitor/SvantekMonitor/api/SvantekService.cs`
- `svantekmonitor/SvantekMonitor/api/MonitorJobRunner.cs`
- `svantekmonitor/SvantekMonitor/appsettings.json`
- `docker-compose.yml`
- `docs/container-builds.md`
- `README.md`

Delete only after compatibility is proven:

- `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobService.cs`

If removing it is too disruptive in the first pass, keep it as an `[Obsolete]` compatibility wrapper over `AzureBlobStorageService`.

---

### Task 1: Define The Blob Storage Port

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/IBlobStorageService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageWriteRequest.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageWriteResult.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobObjectName.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobObjectNameTests.cs`

**Interfaces:**
- Produces:
  - `IBlobStorageService.WriteAsync(BlobStorageWriteRequest request, CancellationToken cancellationToken = default)`
  - `IBlobStorageService.DeleteAsync(string objectName, CancellationToken cancellationToken = default)`
  - `BlobObjectName.Normalize(string objectName)`

- [x] **Step 1: Write object-name safety tests**

Create `BlobObjectNameTests` with cases:

```csharp
[TestMethod]
public void Normalize_RejectsTraversalAndRootedNames()
{
    Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("../x.wav"));
    Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("/x.wav"));
    Assert.ThrowsExactly<ArgumentException>(() => BlobObjectName.Normalize("folder/../../x.wav"));
}

[TestMethod]
public void Normalize_AllowsSafeNestedObjectNames()
{
    Assert.AreEqual("svantek/audio/abc.wav", BlobObjectName.Normalize("svantek/audio/abc.wav"));
}
```

- [x] **Step 2: Run focused test and verify red**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter BlobObjectNameTests
```

Expected: compile failure because `BlobObjectName` does not exist.

- [x] **Step 3: Add port and value objects**

Create:

```csharp
namespace Rvt.Monitor.Common.Storage;

public interface IBlobStorageService
{
    Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken = default);
}

public sealed record BlobStorageWriteRequest(
    string ObjectName,
    byte[] Content,
    string? ContentType = null);

public sealed record BlobStorageWriteResult(
    string ObjectName,
    string? Uri = null);
```

`BlobObjectName.Normalize` must:

- trim whitespace;
- replace `\` with `/`;
- reject empty values;
- reject rooted paths;
- reject any path segment equal to `..`;
- return the normalized slash-separated object name.

- [x] **Step 4: Run focused test and verify green**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter BlobObjectNameTests
```

Expected: pass.

### Task 2: Add Provider-Neutral Options

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageOptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageProvider.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Configuration/RvtConfig.cs`

**Interfaces:**
- Consumes: environment/config settings.
- Produces: `BlobStorageOptions.Bind(IConfiguration configuration)`.

- [x] **Step 1: Write option binding tests**

Test cases:

- no settings -> provider `Local`, container `audiofiles`, local root `/data/rvt/blobs`;
- `RVT__AUDIO_FOLDER` still aliases container when `RVT__BLOB_CONTAINER` is absent;
- explicit `RVT__BLOB_PROVIDER=AzureBlob` binds Azure values;
- explicit `RVT__BLOB_PROVIDER=S3` binds S3 values;
- invalid provider throws.

- [x] **Step 2: Add options model**

Add:

```csharp
public enum BlobStorageProvider
{
    Local,
    AzureBlob,
    S3
}

public sealed record BlobStorageOptions
{
    public BlobStorageProvider Provider { get; init; } = BlobStorageProvider.Local;
    public string Container { get; init; } = "audiofiles";
    public string Prefix { get; init; } = string.Empty;
    public string LocalRoot { get; init; } = "/data/rvt/blobs";
    public string AzureConnectionString { get; init; } = string.Empty;
    public string AzureServiceUri { get; init; } = string.Empty;
    public string S3Bucket { get; init; } = string.Empty;
    public string S3Region { get; init; } = string.Empty;
    public string S3ServiceUrl { get; init; } = string.Empty;
    public bool S3ForcePathStyle { get; init; }
}
```

Binding should read both environment variable keys and configuration keys, using the existing app configuration pattern.

- [x] **Step 3: Keep `RvtConfig` compatibility fields**

Leave `RvtConfig.BlobConnectionString`, `BlobServiceUri`, and `AudioFolder` in place for now, but route new storage code through `BlobStorageOptions`.

- [x] **Step 4: Run common tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter BlobStorageOptionsTests
```

Expected: pass.

### Task 3: Implement Local File Adapter As Default

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/LocalFileBlobStorageService.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/LocalFileBlobStorageServiceTests.cs`

**Interfaces:**
- Consumes: `BlobStorageOptions`, `BlobObjectName`.
- Produces: local write/delete behavior behind `IBlobStorageService`.

- [x] **Step 1: Write local adapter tests**

Test:

- writes bytes to `{LocalRoot}/{Container}/{Prefix}/{ObjectName}`;
- creates missing directories;
- overwrites existing files;
- returns normalized object name and a `file://` URI or absolute local path URI;
- deletes existing file;
- rejects traversal object names.

- [x] **Step 2: Implement local adapter**

Use `FileStream` async writes. Write to a temporary file in the target directory and move/replace it to reduce partial-write risk.

- [x] **Step 3: Run local adapter tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter LocalFileBlobStorageServiceTests
```

Expected: pass.

### Task 4: Implement Azure Blob Adapter

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobStorageService.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Storage/AzureBlobService.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `BlobStorageOptions.Provider=AzureBlob`.
- Produces: Azure-backed `IBlobStorageService`.

- [x] **Step 1: Move existing Azure logic into the adapter**

Use existing behavior:

- connection string wins when present;
- otherwise use `BlobServiceUri` with `DefaultAzureCredential`;
- throw when Azure provider is selected without either Azure setting;
- create container if missing;
- upload with overwrite;
- return object name and blob URI.

- [x] **Step 2: Keep compatibility wrapper**

Either delete `AzureBlobService` after all callers move, or keep:

```csharp
[Obsolete("Use Rvt.Monitor.Common.Storage.IBlobStorageService instead.")]
public sealed class AzureBlobService
{
    // Thin wrapper over AzureBlobStorageService for legacy callers only.
}
```

Prefer deletion if no caller remains.

- [x] **Step 3: Add adapter selection test**

Do not hit Azure. Test that selecting `AzureBlob` requires Azure settings and registers `AzureBlobStorageService`.

- [x] **Step 4: Run common tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter BlobStorage
```

Expected: pass without live cloud credentials.

### Task 5: Implement S3 Adapter

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/S3BlobStorageService.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `BlobStorageOptions.Provider=S3`.
- Produces: S3-backed `IBlobStorageService`.

- [x] **Step 1: Add S3 package**

Add `AWSSDK.S3` to `Rvt.Monitor.Common.csproj` using the current compatible package version available during implementation.

- [x] **Step 2: Implement S3 adapter**

Use AWS SDK credential resolution. Do not add raw access keys to appsettings.

Rules:

- require `RVT__S3_BUCKET`;
- apply `RVT__BLOB_PREFIX` to object keys;
- support `RVT__S3_REGION`;
- support `RVT__S3_SERVICE_URL` and `RVT__S3_FORCE_PATH_STYLE` for MinIO/S3-compatible stores;
- set content type when provided;
- return object name and provider URI when available.

- [x] **Step 3: Add registration tests**

Test S3 option validation and adapter selection without live AWS calls.

- [x] **Step 4: Restore/build**

Run:

```bash
dotnet restore rvt-monitors.sln
dotnet build rvt-monitors.sln
```

Expected: build passes.

### Task 6: Add DI Registration

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageServiceCollectionExtensions.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`

**Interfaces:**
- Produces: `services.AddMonitorBlobStorage(configuration)` or `services.AddMonitorBlobStorage()` aligned with existing host configuration style.

- [x] **Step 1: Add extension tests**

Cover:

- no config -> `LocalFileBlobStorageService`;
- `RVT__BLOB_PROVIDER=AzureBlob` -> Azure adapter;
- `RVT__BLOB_PROVIDER=S3` -> S3 adapter;
- invalid provider -> startup exception.

- [x] **Step 2: Implement service registration**

Register `BlobStorageOptions` and `IBlobStorageService` as singleton. For local storage, singleton is safe. For cloud adapters, SDK clients are designed for reuse.

- [x] **Step 3: Wire Svantek composition root**

In `SvantekMonitorServices.AddSvantekMonitor`, register the storage service before constructing `SvantekApi`.

### Task 7: Refactor Svantek Sound Recording Use Case

**Files:**
- Modify: `svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekService.cs`
- Modify: `svantekmonitor/SvantekMonitor/api/MonitorJobRunner.cs`
- Test: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs`
- Test: `svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs`

**Interfaces:**
- Consumes: `IBlobStorageService`.
- Produces: sound recording upload through storage port.

- [x] **Step 1: Add fake-storage tests**

Test that a matching WAV:

- downloads bytes from the Svantek gateway;
- calls `IBlobStorageService.WriteAsync` with object name `{notificationId}.wav`;
- passes content type `audio/wav`;
- calls `WriteSoundFile(notificationId, "{notificationId}.wav")`.

Keep existing cache test and adjust it to use a fake storage service.

- [x] **Step 2: Inject storage port**

Change handler constructor:

```csharp
public CheckForSoundRecordingsHandler(
    ISvantekNotificationQueries notificationQueries,
    ISvantekOperationalCommands operationalCommands,
    SvantekHttpGateway gateway,
    IBlobStorageService blobStorage)
```

- [x] **Step 3: Prefer async use case**

Add:

```csharp
public Task RunAsync(CancellationToken cancellationToken = default)
```

Keep `Run()` as a compatibility wrapper only if required by existing scheduler code:

```csharp
public void Run() => RunAsync().GetAwaiter().GetResult();
```

- [x] **Step 4: Replace direct `AzureBlobService` construction**

Use:

```csharp
await blobStorage.WriteAsync(
    new BlobStorageWriteRequest(fileName, content, "audio/wav"),
    cancellationToken);
```

- [x] **Step 5: Run Svantek sound tests**

Run:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~CheckForSound"
```

Expected: pass.

### Task 8: Configure Local Container Storage

**Files:**
- Modify: `svantekmonitor/SvantekMonitor/appsettings.json`
- Modify: `docker-compose.yml`
- Modify: `docs/container-builds.md`
- Modify: `README.md`

**Interfaces:**
- Produces: local container storage default documented and mounted.

- [x] **Step 1: Add appsettings documentation defaults**

Add non-secret defaults:

```json
{
  "BlobStorage": {
    "Provider": "Local",
    "Container": "audiofiles",
    "LocalRoot": "/data/rvt/blobs"
  }
}
```

Environment variables still override these settings.

- [x] **Step 2: Add Docker volume**

In `docker-compose.yml`, mount a named volume for Svantek:

```yaml
svantek-audiofiles:
```

and mount it to `/data/rvt/blobs`.

- [x] **Step 3: Document cloud provider switches**

Document examples:

```bash
RVT__BLOB_PROVIDER=AzureBlob
RVT__BLOB_SERVICE_URI=https://<account>.blob.core.windows.net
RVT__BLOB_CONTAINER=audiofiles
```

```bash
RVT__BLOB_PROVIDER=S3
RVT__S3_BUCKET=<bucket>
RVT__S3_REGION=<region>
RVT__BLOB_PREFIX=svantek/audio
```

Do not document real credentials.

### Task 9: Full Verification

**Files:**
- All modified source/tests/docs.

**Interfaces:**
- Produces: evidence the refactor is complete and behavior is preserved.

- [x] **Step 1: Run focused tests**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter BlobStorage
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter "FullyQualifiedName~CheckForSound"
```

- [ ] **Step 2: Run full solution tests**

Run:

```bash
dotnet test rvt-monitors.sln
```

- [x] **Step 3: Run Docker config validation**

Run:

```bash
docker compose config
docker compose --project-directory . -f docker-compose.yml -f observability/docker-compose.monitors-observed.yml config
```

- [ ] **Step 4: Optional local storage smoke**

Run Svantek `CheckForSoundRecordings` with local storage configured and a fake/no-op data state. If real Svantek credentials are unavailable, verify startup and configuration resolution only.

- [ ] **Step 5: Static checks**

Run:

```bash
git diff --check
rg -n "new AzureBlobService|SvantekMonitor.api.rvt_common.storage" .
```

Expected:

- no direct construction in monitor app code;
- no old Svantek-specific storage namespace remains except possibly an obsolete compatibility wrapper;
- no whitespace errors.

## Rollout Notes

- Default changes from “Azure required when using sound upload” to “local container storage”. Azure deployments must explicitly set `RVT__BLOB_PROVIDER=AzureBlob`.
- Local container storage is not durable unless the container path is backed by a volume. Docker Compose should mount a named volume.
- S3 support should rely on the AWS SDK credential chain or platform-provided secrets, not tracked config files.
- Existing DB rows with `RecordingLink={guid}.wav` remain valid because the object key shape is preserved.
- If any portal/reporting code assumes Azure Blob public URLs, keep storing object keys and resolve URLs at the serving layer rather than storing provider-specific URLs in the notification row.

## Self-Review

- Spec coverage: plan covers moving the implementation to common, generic service naming, ports/adapters, local default, Azure optional, S3 optional, DI, Svantek migration, Docker/config/docs, and tests.
- Placeholder scan: no `TBD`/`TODO` placeholders are required for implementation.
- Type consistency: `IBlobStorageService`, `BlobStorageOptions`, `BlobStorageWriteRequest`, and `BlobStorageWriteResult` are consistently named across tasks.
