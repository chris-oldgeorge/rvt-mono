# RVT Storage Provider Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the storage implementation embedded in `Rvt.Monitor.Common` with a provider-neutral streaming contract and independently consumable Local, Azure Blob, and S3 adapter packages, then migrate the active Svantek and ReportingMonitor consumers without changing their persisted object references.

**Architecture:** `Rvt.Storage.Abstractions` owns object keys, streaming requests/results, shared failures, and named-client lookup. Each provider package owns its SDK, options binding, validation, client implementation, and explicit DI registration. Application composition roots continue to interpret `RVT__BLOB_PROVIDER`; consumers request a named logical resource through `IObjectStorageClientFactory`.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core dependency injection and hosted startup validation, Azure.Storage.Blobs 12.25.0, Azure.Identity 1.15.0, AWSSDK.S3 4.0.100.3, MSTest 4.0.2, Moq 4.20.72, xUnit for ReportingMonitor tests.

## Global Constraints

- This is a clean major-version split. Do not create a compatibility facade or meta-package.
- `Rvt.Storage.Abstractions` has no provider SDK, filesystem implementation, provider selector, configuration package, or dependency-injection package.
- `StorageWriteResult` returns only a `StorageObjectKey`; it never returns an Azure URL, S3 URL, local file URL, credential, or authorization-bearing URI.
- Provider selection stays in the Svantek and ReportingMonitor composition roots.
- Existing configuration aliases remain accepted: `BlobStorage:*`, `RVT:*`, and literal `RVT__*` keys used by in-memory configuration tests.
- The Local provider defaults to root `/data/rvt/blobs`, container `audiofiles`, and an empty prefix.
- ReportingMonitor defaults to container `pdfreports`, prefix `rvtreports`, and retains `BLOB_REPORT_CONTAINER_NAME` as its legacy container alias.
- Azure connection-string configuration takes precedence over the managed-identity service URI.
- S3 credentials continue to use the AWS SDK credential chain; no credential properties are added to `S3StorageOptions`.
- Adapter exceptions do not expose credentials, connection strings, authorization headers, message bodies, provider response bodies, or provider exception messages.
- Cancellation requested by the caller propagates as `OperationCanceledException` and is not translated to `ObjectStorageException`.
- Existing Local traversal, root containment, reparse-point, overwrite, and atomic-write behavior remains intact.
- Existing ReportingMonitor `report.report_link` values retain their provider-specific absolute URI format; URI construction remains outside the generic storage port.
- Portal blob unification and the independent `services/reporting` Azure adapter are excluded from this implementation and recorded under **Future Pending Work**.

---

## File Structure

### New production projects

- `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/`
  - `Rvt.Storage.Abstractions.csproj` — packable, provider-neutral contract package.
  - `StorageObjectKey.cs` — normalized, traversal-safe object key.
  - `StorageWriteRequest.cs` — key, readable content stream, and optional content type.
  - `StorageWriteResult.cs` — stable object key only.
  - `StorageReadResult.cs` — readable stream, metadata, and provider-response lease.
  - `IObjectStorageClient.cs` — write, open-read, and delete-if-exists port.
  - `IObjectStorageClientFactory.cs` — named logical-resource lookup.
  - `ObjectStorageClientRegistration.cs` — resource/client registration value.
  - `ObjectStorageClientFactory.cs` — deterministic named-client registry.
  - `StorageFailureKind.cs` — provider-neutral failure classification.
  - `ObjectStorageException.cs` — secret-safe shared exception.
- `libs/rvt-monitor-common/src/Rvt.Storage.Local/`
  - `Rvt.Storage.Local.csproj`
  - `LocalStorageOptions.cs`
  - `LocalObjectStorageClient.cs`
  - `LocalStorageServiceCollectionExtensions.cs`
  - `LocalStorageStartupValidationHostedService.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/`
  - `Rvt.Storage.AzureBlob.csproj`
  - `AzureBlobStorageOptions.cs`
  - `AzureBlobObjectStorageClient.cs`
  - `AzureBlobStorageServiceCollectionExtensions.cs`
  - `AzureBlobStorageStartupValidationHostedService.cs`
- `libs/rvt-monitor-common/src/Rvt.Storage.S3/`
  - `Rvt.Storage.S3.csproj`
  - `S3StorageOptions.cs`
  - `S3ObjectStorageClient.cs`
  - `S3StorageServiceCollectionExtensions.cs`
  - `S3StorageStartupValidationHostedService.cs`

### New and migrated tests

- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/AzureBlob/`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/S3/`
- `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Architecture/StorageDependencyBoundaryTests.cs`

### Active consumer changes

- `apps/monitors/svantekmonitor/SvantekMonitor/api/Storage/SvantekStorageComposition.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- `apps/monitors/reportingmonitor/ReportingMonitor/api/Storage/ReportingStorageComposition.cs`
- `apps/monitors/reportingmonitor/ReportingMonitor/api/Storage/ConfiguredReportObjectUriResolver.cs`
- `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/IReportObjectUriResolver.cs`
- `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/MonitorBlobReportStorage.cs`

---

### Task 1: Introduce Provider-Neutral Streaming Storage Contracts

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageObjectKey.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageWriteRequest.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageWriteResult.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageReadResult.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/IObjectStorageClient.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/IObjectStorageClientFactory.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageClientRegistration.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageClientFactory.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/StorageFailureKind.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/ObjectStorageException.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/StorageObjectKeyTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/ObjectStorageClientFactoryTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/StorageReadResultTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Abstractions/ObjectStorageExceptionTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace Rvt.Storage;

public sealed record StorageObjectKey
{
    public string Value { get; }
    public static StorageObjectKey Parse(string value);
    public override string ToString();
}

public sealed record StorageWriteRequest(
    StorageObjectKey Key,
    Stream Content,
    string? ContentType = null);

public sealed record StorageWriteResult(StorageObjectKey Key);

public sealed class StorageReadResult : IAsyncDisposable
{
    public StorageReadResult(
        Stream content,
        string? contentType,
        long? length,
        IDisposable? lease = null);

    public Stream Content { get; }
    public string? ContentType { get; }
    public long? Length { get; }
    public ValueTask DisposeAsync();
}

public interface IObjectStorageClient
{
    Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);
}

public interface IObjectStorageClientFactory
{
    IObjectStorageClient GetRequiredClient(string resourceName);
}

public sealed record ObjectStorageClientRegistration(
    string ResourceName,
    IObjectStorageClient Client);

public sealed class ObjectStorageClientFactory : IObjectStorageClientFactory
{
    public ObjectStorageClientFactory(
        IEnumerable<ObjectStorageClientRegistration> registrations);

    public IObjectStorageClient GetRequiredClient(string resourceName);
}

public enum StorageFailureKind
{
    AccessDenied,
    InvalidRequest,
    Conflict,
    Unavailable,
    Unknown
}

public sealed class ObjectStorageException : Exception
{
    public ObjectStorageException(
        StorageFailureKind kind,
        string resourceName,
        StorageObjectKey? key,
        Exception? innerException = null);

    public StorageFailureKind Kind { get; }
    public string ResourceName { get; }
    public StorageObjectKey? Key { get; }
}
```

- [ ] **Step 1: Create the contract test project and write failing key tests**

Create a net10 MSTest project with `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`,
`MSTest.TestFramework`, and a project reference to
`../../src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj`.

Test these exact cases:

```csharp
[DataTestMethod]
[DataRow(" clips\\sample.wav ", "clips/sample.wav")]
[DataRow("tenant//audio/sample.wav", "tenant/audio/sample.wav")]
public void Parse_NormalizesSafeObjectNames(string input, string expected)
{
    Assert.AreEqual(expected, StorageObjectKey.Parse(input).Value);
}

[DataTestMethod]
[DataRow("")]
[DataRow("/sample.wav")]
[DataRow("../sample.wav")]
[DataRow("nested/../../sample.wav")]
[DataRow("C:\\sample.wav")]
[DataRow("\\\\server\\share\\sample.wav")]
public void Parse_RejectsUnsafeObjectNames(string input)
{
    Assert.ThrowsExactly<ArgumentException>(() => StorageObjectKey.Parse(input));
}
```

- [ ] **Step 2: Run the key tests and verify RED**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageObjectKeyTests --nologo -v minimal
```

Expected: compilation fails because `Rvt.Storage` and `StorageObjectKey` do not exist.

- [ ] **Step 3: Implement `StorageObjectKey`**

Implement `Parse` by trimming whitespace, converting `\` to `/`, rejecting empty,
rooted, UNC, Windows-drive-rooted, `.` and `..` segments, removing empty segments, and
joining the retained segments with `/`. Keep the constructor private so every instance
is validated.

- [ ] **Step 4: Run the key tests and verify GREEN**

Run the command from Step 2.

Expected: all `StorageObjectKeyTests` pass.

- [ ] **Step 5: Write failing factory, read-result, and failure tests**

Cover all of these behaviors:

```csharp
[TestMethod]
public void GetRequiredClient_ReturnsOrdinalNamedRegistration();

[TestMethod]
public void Constructor_RejectsDuplicateResourceNames();

[TestMethod]
public void GetRequiredClient_RejectsUnknownResourceWithoutListingOtherResources();

[TestMethod]
public async Task DisposeAsync_DisposesContentThenProviderLease();

[TestMethod]
public void ObjectStorageException_MessageDoesNotReflectInnerExceptionText();
```

Use a recording `Stream`, a recording `IDisposable`, and a fake
`IObjectStorageClient`. Assert that an inner exception containing
`AccountKey=not-for-output` does not place that text in `ObjectStorageException.Message`.

- [ ] **Step 6: Run the remaining abstraction tests and verify RED**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~ObjectStorageClientFactoryTests|FullyQualifiedName~StorageReadResultTests|FullyQualifiedName~ObjectStorageExceptionTests' \
  --nologo -v minimal
```

Expected: compilation fails because the remaining contract types do not exist.

- [ ] **Step 7: Implement the remaining contract types**

Implement the exact interfaces above. The factory must use
`StringComparer.Ordinal`, reject blank resource names, reject duplicates during
construction, and return this message for a missing resource:

```text
Object storage resource 'resource-name' is not registered.
```

`StorageReadResult.DisposeAsync()` must first call `Content.DisposeAsync()` and then
dispose the optional lease in a `finally` block. `ObjectStorageException.Message` must
be generated only from `Kind`, `ResourceName`, and `Key`.

- [ ] **Step 8: Run all abstraction tests and verify GREEN**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~Abstractions --nologo -v minimal
```

Expected: every abstraction test passes.

- [ ] **Step 9: Commit the abstraction slice**

```bash
git add libs/rvt-monitor-common/src/Rvt.Storage.Abstractions \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "feat(storage): add provider-neutral streaming contracts"
```

---

### Task 2: Extract the Local Storage Adapter

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Local/Rvt.Storage.Local.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalObjectStorageClient.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageServiceCollectionExtensions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.Local/LocalStorageStartupValidationHostedService.cs`
- Modify: `libs/rvt-monitor-common/Directory.Packages.props`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalStorageOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalObjectStorageClientTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Local/LocalStorageRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`

**Interfaces:**
- Consumes: all Task 1 contracts.
- Produces:

```csharp
namespace Rvt.Storage.Local;

public sealed record LocalStorageOptions
{
    public string RootPath { get; init; } = "/data/rvt/blobs";
    public string Container { get; init; } = "audiofiles";
    public string Prefix { get; init; } = string.Empty;

    public static LocalStorageOptions Bind(
        IConfiguration configuration,
        string defaultContainer = "audiofiles",
        string defaultPrefix = "",
        string? legacyContainerEnvironmentKey = "AUDIO_FOLDER");
}

public sealed class LocalObjectStorageClient : IObjectStorageClient
{
    public LocalObjectStorageClient(string resourceName, LocalStorageOptions options);
    public Uri GetObjectUri(StorageObjectKey key);
}

public static class LocalStorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName);

    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, LocalStorageOptions> optionsFactory);

    public static IServiceCollection AddRvtLocalStorage(
        this IServiceCollection services,
        string resourceName,
        LocalStorageOptions options);
}
```

- [ ] **Step 1: Write failing Local options and registration tests**

Move the Local-only binding expectations from
`Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs` and assert:

- empty configuration produces `/data/rvt/blobs`, `audiofiles`, and empty prefix;
- `RVT:AUDIO_FOLDER` supplies the legacy container;
- `BlobStorage:Container` wins over the legacy container;
- literal `RVT__BLOB_LOCAL_ROOT`, `RVT__BLOB_CONTAINER`, and `RVT__BLOB_PREFIX` bind;
- custom reporting defaults and `BLOB_REPORT_CONTAINER_NAME` bind;
- `AddRvtLocalStorage("recordings", factory)` registers exactly one
  `ObjectStorageClientRegistration`;
- `IObjectStorageClientFactory.GetRequiredClient("recordings")` returns a singleton
  `LocalObjectStorageClient`;
- host startup resolves and validates the named client.

- [ ] **Step 2: Run Local options/registration tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~LocalStorageOptionsTests|FullyQualifiedName~LocalStorageRegistrationTests' \
  --nologo -v minimal
```

Expected: compilation fails because `Rvt.Storage.Local` does not exist.

- [ ] **Step 3: Implement Local options and registration**

Create `Rvt.Storage.Local.csproj` with a project reference to Abstractions and package
references to:

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
```

Add central versions `10.0.9` for
`Microsoft.Extensions.Configuration.Abstractions`,
`Microsoft.Extensions.DependencyInjection.Abstractions`, and
`Microsoft.Extensions.Hosting.Abstractions` in
`libs/rvt-monitor-common/Directory.Packages.props`.

The registration method must register the concrete client as a keyed singleton so a
host-specific URI resolver can reuse the exact same instance:

```csharp
services.TryAddSingleton<IObjectStorageClientFactory, ObjectStorageClientFactory>();
services.AddKeyedSingleton<LocalObjectStorageClient>(
    resourceName,
    (provider, _) => new LocalObjectStorageClient(
        resourceName,
        optionsFactory(provider.GetRequiredService<IConfiguration>())));
services.AddSingleton(provider => new ObjectStorageClientRegistration(
    resourceName,
    provider.GetRequiredKeyedService<LocalObjectStorageClient>(resourceName)));
services.AddSingleton<IHostedService>(provider =>
    new LocalStorageStartupValidationHostedService(
        provider.GetRequiredService<IObjectStorageClientFactory>(),
        resourceName));
```

Validate nonblank resource name when the extension is called. The hosted service must
call `factory.GetRequiredClient(resourceName)` in `StartAsync`. The options overload
delegates to the factory overload with `_ => options`.

- [ ] **Step 4: Run Local options/registration tests and verify GREEN**

Run the Step 2 command.

Expected: all Local options and registration tests pass.

- [ ] **Step 5: Write failing Local streaming and containment tests**

Port the existing Local test suite to the new stream contract. Assert:

```csharp
var content = new MemoryStream(Encoding.UTF8.GetBytes("recording-data"), writable: false);
var result = await client.WriteAsync(
    new StorageWriteRequest(StorageObjectKey.Parse(" clips\\sample.wav "), content, "audio/wav"));

Assert.AreEqual("clips/sample.wav", result.Key.Value);
await using var read = await client.OpenReadAsync(result.Key);
Assert.IsNotNull(read);
Assert.AreEqual("audio/wav", read.ContentType);
Assert.AreEqual(content.Length, read.Length);
```

Also test missing parent creation, overwrite, no leftover `.*.tmp` file, missing read
returns `null`, first delete returns `true`, second delete returns `false`, unsafe
container/prefix rejection, object-key traversal rejection, directory symlink rejection,
target-file symlink rejection, and cancellation before filesystem mutation.

- [ ] **Step 6: Run Local client tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~LocalObjectStorageClientTests --nologo -v minimal
```

Expected: compilation fails because `LocalObjectStorageClient` is absent or its
streaming operations are unimplemented.

- [ ] **Step 7: Implement the Local client**

Move the proven root-containment and reparse-point checks from
`LocalFileBlobStorageService`. Write to a same-directory
`.filename.{Guid:N}.tmp` file with `FileMode.CreateNew`, copy the request stream with
`CopyToAsync`, flush it, and atomically move it over the target. Store content type in
an adjacent `.filename.content-type` metadata file using the same atomic strategy;
delete the metadata file with the object. `OpenReadAsync` returns a
`FileStream` configured for asynchronous sequential reads and reads the optional
metadata file. `GetObjectUri` returns `new Uri(targetPath)`.

- [ ] **Step 8: Run all Local tests and verify GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~Local --nologo -v minimal
```

Expected: every Local test passes.

- [ ] **Step 9: Commit the Local provider**

```bash
git add libs/rvt-monitor-common/src/Rvt.Storage.Local \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "feat(storage): extract local storage adapter"
```

---

### Task 3: Extract the Azure Blob Storage Adapter

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/AzureBlobStorageOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/AzureBlobObjectStorageClient.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/AzureBlobStorageServiceCollectionExtensions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/AzureBlobStorageStartupValidationHostedService.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/AzureBlob/AzureBlobStorageOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/AzureBlob/AzureBlobObjectStorageClientTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/AzureBlob/AzureBlobStorageRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`

**Interfaces:**
- Consumes: Task 1 contracts and provider-neutral registration pattern from Task 2.
- Produces:

```csharp
namespace Rvt.Storage.AzureBlob;

public sealed record AzureBlobStorageOptions
{
    public string Container { get; init; } = "audiofiles";
    public string Prefix { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public string ServiceUri { get; init; } = string.Empty;

    public static AzureBlobStorageOptions Bind(
        IConfiguration configuration,
        string defaultContainer = "audiofiles",
        string defaultPrefix = "",
        string? legacyContainerEnvironmentKey = "AUDIO_FOLDER");
}

public sealed class AzureBlobObjectStorageClient : IObjectStorageClient
{
    public AzureBlobObjectStorageClient(
        string resourceName,
        AzureBlobStorageOptions options);

    internal AzureBlobObjectStorageClient(
        string resourceName,
        BlobContainerClient containerClient,
        string prefix);

    public Uri GetObjectUri(StorageObjectKey key);
}

public static class AzureBlobStorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtAzureBlobStorage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, AzureBlobStorageOptions> optionsFactory);

    public static IServiceCollection AddRvtAzureBlobStorage(
        this IServiceCollection services,
        string resourceName,
        AzureBlobStorageOptions options);
}
```

- [ ] **Step 1: Write failing Azure options and registration tests**

Assert connection-string precedence over an invalid service URI, absolute service-URI
validation, required nonblank container, prefix traversal rejection, all current
configuration aliases, named-client resolution, and startup failure when both
connection string and service URI are missing. Error messages may name
`RVT__BLOB_CONNECTION_STRING` and `RVT__BLOB_SERVICE_URI`; they must not contain
configured values.

- [ ] **Step 2: Run Azure options/registration tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~AzureBlobStorageOptionsTests|FullyQualifiedName~AzureBlobStorageRegistrationTests' \
  --nologo -v minimal
```

Expected: compilation fails because the Azure provider project does not exist.

- [ ] **Step 3: Implement Azure options, client construction, and registration**

Reference Abstractions, `Azure.Identity`, `Azure.Storage.Blobs`, and the three
Microsoft extension abstraction packages used by Task 2. Construct a
`BlobServiceClient` from `ConnectionString` when nonblank; otherwise require an
absolute `ServiceUri` and construct it with `DefaultAzureCredential`. Bind the trimmed
container with `GetBlobContainerClient`. Register one named client and one startup
validator using the same pattern as Local.

- [ ] **Step 4: Run Azure options/registration tests and verify GREEN**

Run the Step 2 command.

Expected: all Azure option and registration tests pass.

- [ ] **Step 5: Write failing Azure streaming-operation tests**

Use strict Moq instances of `BlobContainerClient` and `BlobClient`. Use
`BlobsModelFactory.BlobDownloadStreamingResult` to return a `MemoryStream`,
`ContentType = "audio/wav"`, and a known content length.

Test:

- `WriteAsync` creates the container, uploads the original request stream with
  overwrite enabled, applies `BlobHttpHeaders.ContentType`, prefixes the provider key,
  and returns only the unprefixed `StorageObjectKey`;
- `OpenReadAsync` returns the streaming response and metadata;
- Azure status 404 returns `null`;
- `DeleteIfExistsAsync` returns the SDK boolean;
- 403 becomes `StorageFailureKind.AccessDenied`;
- 409 becomes `StorageFailureKind.Conflict`;
- 408, 429, and 5xx become `StorageFailureKind.Unavailable`;
- a caller-cancelled operation remains `OperationCanceledException`;
- no translated message contains the Azure response body or inner exception text.

- [ ] **Step 6: Run Azure client tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~AzureBlobObjectStorageClientTests --nologo -v minimal
```

Expected: tests fail because Azure operations are not implemented.

- [ ] **Step 7: Implement Azure streaming operations and translation**

For writes, call `CreateIfNotExistsAsync`, then:

```csharp
await blobClient.UploadAsync(
    request.Content,
    new BlobUploadOptions
    {
        HttpHeaders = string.IsNullOrWhiteSpace(request.ContentType)
            ? null
            : new BlobHttpHeaders { ContentType = request.ContentType }
    },
    cancellationToken);
```

For reads, call `DownloadStreamingAsync`; return its content stream and details without
buffering. For deletes, call `DeleteIfExistsAsync`. Catch `RequestFailedException`
only after allowing caller cancellation to propagate. `GetObjectUri` uses the bound
`BlobClient.Uri`; the method is concrete-provider API and is not part of
`IObjectStorageClient`.

- [ ] **Step 8: Run all Azure tests and verify GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~AzureBlob --nologo -v minimal
```

Expected: every Azure test passes without network access.

- [ ] **Step 9: Commit the Azure provider**

```bash
git add libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "feat(storage): extract Azure Blob adapter"
```

---

### Task 4: Extract the S3 Storage Adapter

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.S3/Rvt.Storage.S3.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.S3/S3StorageOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.S3/S3ObjectStorageClient.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.S3/S3StorageServiceCollectionExtensions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Storage.S3/S3StorageStartupValidationHostedService.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/S3/S3StorageOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/S3/S3ObjectStorageClientTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/S3/S3StorageRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj`

**Interfaces:**
- Produces:

```csharp
namespace Rvt.Storage.S3;

public sealed record S3StorageOptions
{
    public string Bucket { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; }

    public static S3StorageOptions Bind(
        IConfiguration configuration,
        string defaultPrefix = "");
}

public sealed class S3ObjectStorageClient : IObjectStorageClient, IDisposable
{
    public S3ObjectStorageClient(string resourceName, S3StorageOptions options);

    internal S3ObjectStorageClient(
        string resourceName,
        IAmazonS3 client,
        string bucket,
        string prefix);

    public Uri GetObjectUri(StorageObjectKey key);
    public void Dispose();
}

public static class S3StorageServiceCollectionExtensions
{
    public static IServiceCollection AddRvtS3Storage(
        this IServiceCollection services,
        string resourceName,
        Func<IConfiguration, S3StorageOptions> optionsFactory);

    public static IServiceCollection AddRvtS3Storage(
        this IServiceCollection services,
        string resourceName,
        S3StorageOptions options);
}
```

- [ ] **Step 1: Write failing S3 options and registration tests**

Assert required `RVT__S3_BUCKET`, prefix traversal rejection, optional region, absolute
service URL, force-path-style parsing, region-only `RegionEndpoint`, compatible-S3
`ServiceURL` plus `AuthenticationRegion`, named registration, and startup validation.
Assert that no options property accepts an access key or secret key.

- [ ] **Step 2: Run S3 options/registration tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter 'FullyQualifiedName~S3StorageOptionsTests|FullyQualifiedName~S3StorageRegistrationTests' \
  --nologo -v minimal
```

Expected: compilation fails because the S3 provider project does not exist.

- [ ] **Step 3: Implement S3 options, client construction, and registration**

Reference Abstractions, `AWSSDK.S3`, and the Microsoft extension abstraction packages.
Construct `AmazonS3Config` with the exact current behaviors:

```csharp
var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };
if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
{
    config.ServiceURL = new Uri(options.ServiceUrl).AbsoluteUri.TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(options.Region))
    {
        config.AuthenticationRegion = options.Region.Trim();
    }
}
else if (!string.IsNullOrWhiteSpace(options.Region))
{
    config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region.Trim());
}
```

Use `new AmazonS3Client(config)` so the normal SDK credential chain remains in effect.

- [ ] **Step 4: Run S3 options/registration tests and verify GREEN**

Run the Step 2 command.

Expected: all S3 options and registration tests pass.

- [ ] **Step 5: Write failing S3 streaming-operation tests**

Use strict `Mock<IAmazonS3>` behavior. Assert:

- `PutObjectRequest.InputStream` is the original request stream,
  `AutoCloseStream = false`, the key includes the prefix, and content type is copied;
- `GetObjectResponse.ResponseStream`, `Headers.ContentType`, and `ContentLength` are
  returned without buffering;
- disposing `StorageReadResult` disposes the `GetObjectResponse`;
- `NoSuchKey` and HTTP 404 return `null`;
- delete checks metadata, returns `false` for a missing key, and otherwise sends the
  expected bucket/key and returns `true`;
- access denied, invalid request, conflict, and unavailable statuses map to the shared
  failure kinds;
- cancellation remains `OperationCanceledException`;
- provider response text and inner exception text are absent from the shared message;
- `GetObjectUri` returns `s3://bucket/prefix/escaped%20name.pdf`.

- [ ] **Step 6: Run S3 client tests and verify RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~S3ObjectStorageClientTests --nologo -v minimal
```

Expected: tests fail because S3 operations are unimplemented.

- [ ] **Step 7: Implement S3 streaming operations and translation**

Use `PutObjectAsync`, `GetObjectAsync`, `GetObjectMetadataAsync`, and
`DeleteObjectAsync`. `DeleteIfExistsAsync` first calls metadata; return `false` for
`NoSuchKey` or HTTP 404, otherwise delete and return `true`. Wrap a successful
`GetObjectResponse` in `StorageReadResult` and pass the response as its lease. Build the
provider key from normalized prefix plus key. Escape each URI path segment separately
in `GetObjectUri`.

- [ ] **Step 8: Run all S3 tests and verify GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~S3 --nologo -v minimal
```

Expected: every S3 test passes without network access.

- [ ] **Step 9: Commit the S3 provider**

```bash
git add libs/rvt-monitor-common/src/Rvt.Storage.S3 \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "feat(storage): extract S3 adapter"
```

---

### Task 5: Enforce Provider Contract Parity and Dependency Isolation

**Files:**
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Contracts/ObjectStorageClientContractTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Contracts/LocalObjectStorageContractTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Contracts/AzureBlobObjectStorageContractTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Contracts/S3ObjectStorageContractTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Architecture/StorageDependencyBoundaryTests.cs`

**Interfaces:**
- Consumes: all four storage projects.
- Produces: one reusable behavior contract exercised by each concrete provider fixture.

- [ ] **Step 1: Write the shared provider contract**

Define an abstract test base with:

```csharp
protected abstract Task<IObjectStorageClientFixture> CreateFixtureAsync();

public interface IObjectStorageClientFixture : IAsyncDisposable
{
    IObjectStorageClient Client { get; }
}
```

Run the same assertions for every provider:

- writing a non-seekable stream returns the same normalized key;
- reading returns equal bytes, content type, and content length;
- opening a missing key returns `null`;
- overwriting replaces content;
- delete returns `true` for the existing key and `false` for the missing key;
- caller cancellation aborts write, read, and delete.

The Local fixture uses a temporary directory. Azure and S3 fixtures use the strict SDK
test doubles from Tasks 3 and 4 with an in-memory object dictionary behind the mocked
operations.

- [ ] **Step 2: Run the contract suite**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~ObjectStorageContractTests --nologo -v minimal
```

Expected: all three provider fixtures pass the same behavior suite.

- [ ] **Step 3: Write dependency-boundary tests**

Read the project and source files from the repository root and assert:

```text
Rvt.Storage.Abstractions:
  no PackageReference
  no Azure., Amazon., Microsoft.Extensions., System.IO.File, Directory, or FileStream

Rvt.Storage.Local:
  no Azure. or Amazon.

Rvt.Storage.AzureBlob:
  Azure.Storage.Blobs and Azure.Identity present
  no AWSSDK.S3 or Amazon.

Rvt.Storage.S3:
  AWSSDK.S3 and Amazon. present
  no Azure.

```

Task 8 adds the corresponding Common assertions immediately before removing its legacy
storage implementation.

- [ ] **Step 4: Run the dependency-boundary tests**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundaryTests --nologo -v minimal
```

Expected: all provider project isolation assertions pass.

- [ ] **Step 5: Commit the shared contract and architecture guards**

```bash
git add libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "test(storage): enforce provider contract parity"
```

---

### Task 6: Migrate Svantek Sound Recordings

**Files:**
- Create: `apps/monitors/svantekmonitor/SvantekMonitor/api/Storage/SvantekStorageComposition.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/Program.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Hosting/MonitorHost.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/Program.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/Program.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/Program.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/UseCases/CheckForSoundRecordingsHandler.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordingStorage.cs`
- Create: `apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/StorageCompositionTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekImportOptionsTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs`

**Interfaces:**
- Produces:

```csharp
internal static class SvantekStorageComposition
{
    internal const string SoundRecordingsResource = "svantek-sound-recordings";

    internal static IServiceCollection AddSvantekStorage(
        this IServiceCollection services,
        IConfiguration configuration);
}

public static IServiceCollection AddSvantekMonitor(
    this IServiceCollection services,
    IConfiguration configuration);

public static Task<int> MonitorHost.RunAsync<TDispatcher>(
    string[] args,
    string monitorName,
    Func<string[], string?> getJobName,
    Func<string, IServiceProvider, Task<int>> runJobAsync,
    Action<WebApplication> mapApi,
    Action<ILoggingBuilder>? configureLogging = null,
    Action<IServiceCollection, IConfiguration>? configureServices = null);
```

- [ ] **Step 1: Write failing composition tests**

For Local, AzureBlob, and S3 configuration, build a service provider and assert the
factory returns the selected concrete client for
`SvantekStorageComposition.SoundRecordingsResource`. Assert missing provider defaults
to Local. Assert `GoogleCloud` throws:

```text
Unsupported blob storage provider 'GoogleCloud'. Allowed values are 'Local', 'AzureBlob', and 'S3'.
```

Assert the Local defaults are `/data/rvt/blobs/audiofiles`, with empty prefix.

- [ ] **Step 2: Run Svantek composition tests and verify RED**

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter FullyQualifiedName~StorageCompositionTests --nologo -v minimal
```

Expected: compilation fails because `SvantekStorageComposition` and new project
references do not exist.

- [ ] **Step 3: Pass host configuration into composition callbacks**

Change `MonitorHost.RunAsync`'s final callback type from
`Action<IServiceCollection>?` to
`Action<IServiceCollection, IConfiguration>?`. Pass the existing `configuration`
argument in the one-shot host, `apiBuilder.Configuration` in API mode, and
`context.Configuration` in scheduler mode.

Update the Svantek Program lambda to:

```csharp
(services, configuration) => services.AddSvantekMonitor(configuration)
```

AirQ, MyAtm, and Omnidots accept but ignore the second lambda argument:

```csharp
configureServices: (services, _) => services.AddAirQMonitor()
configureServices: (services, _) => services.AddMyAtmMonitor()
configureServices: (services, _) => services.AddOmnidotsMonitor()
```

Update the two callbacks in
`OmnidotsMonitorTests/TestMonitorJobScheduling.cs` to `(services, _) =>`. Preserve one
service provider per application mode.

- [ ] **Step 4: Implement explicit Svantek provider composition**

Add project references to Abstractions, Local, AzureBlob, and S3.

`SvantekStorageComposition` must read keys in this order:

```text
BlobStorage:Provider
RVT:BLOB_PROVIDER
RVT__BLOB_PROVIDER
Local
```

It must call exactly one of `AddRvtLocalStorage`, `AddRvtAzureBlobStorage`, or
`AddRvtS3Storage` for `svantek-sound-recordings`.

- [ ] **Step 5: Run composition and host tests and verify GREEN**

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter FullyQualifiedName~StorageCompositionTests --nologo -v minimal
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  --filter FullyQualifiedName~TestMonitorJobScheduling --nologo -v minimal
```

Expected: all storage composition and shared host scheduling tests pass.

- [ ] **Step 6: Rewrite the sound-recording test for streaming**

Replace `RecordingBlobStorageService` with:

```csharp
internal sealed class RecordingObjectStorageClient : IObjectStorageClient
{
    public List<StorageWrite> Writes { get; } = [];

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        Writes.Add(new(request.Key, buffer.ToArray(), request.ContentType, cancellationToken));
        return new StorageWriteResult(request.Key);
    }

    public Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StorageReadResult?>(null);

    public Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
```

Supply it through a one-resource `ObjectStorageClientFactory`. Assert key, bytes,
content type, and cancellation token.

- [ ] **Step 7: Run the rewritten sound-recording test and verify RED**

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --filter FullyQualifiedName~TestCheckForSoundRecordingStorage --nologo -v minimal
```

Expected: compilation fails while production code still expects `IBlobStorageService`.

- [ ] **Step 8: Migrate Svantek production code**

Inject `IObjectStorageClientFactory`, resolve
`svantek-sound-recordings` once in `CheckForSoundRecordingsHandler`, and write:

```csharp
await using var stream = new MemoryStream(content, writable: false);
await storage.WriteAsync(
    new StorageWriteRequest(
        StorageObjectKey.Parse(fileName),
        stream,
        "audio/wav"),
    cancellationToken).ConfigureAwait(false);
```

Replace `MissingBlobStorageService` with a missing factory/client implementing the new
contracts and retaining the existing explicit error semantics.

- [ ] **Step 9: Run all Svantek tests and verify GREEN**

```bash
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --nologo -v minimal
```

Expected: all enabled Svantek tests pass.

- [ ] **Step 10: Commit the Svantek and host-callback migration**

```bash
git add libs/rvt-monitor-common/src/Rvt.Monitor.Common/Hosting/MonitorHost.cs \
  apps/monitors/airqmonitor/AirQMonitor/Program.cs \
  apps/monitors/myatmmonitor/MyAtmMonitor/Program.cs \
  apps/monitors/omnidotsmonitor \
  apps/monitors/svantekmonitor
git commit -m "refactor(storage): migrate Svantek to object storage"
```

---

### Task 7: Migrate ReportingMonitor Without Changing Persisted Report Links

**Files:**
- Create: `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/IReportObjectUriResolver.cs`
- Create: `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/ReportingStorageResourceNames.cs`
- Create: `apps/monitors/reportingmonitor/ReportingMonitor/api/Storage/ReportingStorageComposition.cs`
- Create: `apps/monitors/reportingmonitor/ReportingMonitor/api/Storage/ConfiguredReportObjectUriResolver.cs`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/MonitorBlobReportStorage.cs`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/Program.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/Storage/MonitorBlobReportStorageTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`

**Interfaces:**
- Produces:

```csharp
namespace Rvt.Reporting.Storage;

public interface IReportObjectUriResolver
{
    Uri Resolve(StorageObjectKey key);
}

public static class ReportingStorageResourceNames
{
    public const string Reports = "reporting-reports";
}

public sealed class ConfiguredReportObjectUriResolver : IReportObjectUriResolver
{
    public ConfiguredReportObjectUriResolver(
        Func<StorageObjectKey, Uri> resolveUri);

    public Uri Resolve(StorageObjectKey key);
}

internal static class ReportingStorageComposition
{
    internal static IServiceCollection AddReportingStorage(
        this IServiceCollection services,
        IConfiguration configuration);
}

public static IServiceCollection AddReportingMonitor(
    this IServiceCollection services,
    IConfiguration configuration);

public sealed class MonitorBlobReportStorage(
    IObjectStorageClientFactory storageClients,
    IReportObjectUriResolver uriResolver) : IReportStorage;
```

- [ ] **Step 1: Write failing ReportingMonitor storage tests**

Rewrite `MonitorBlobReportStorageTests` with a recording streaming client and resolver.
Assert:

- the report bytes are read from the stream;
- key is `report.FileName`;
- content type is retained;
- the returned URI is the resolver's URI;
- a provider result cannot supply or override a URI because `StorageWriteResult` has
  only a key.

Add composition tests for the three providers and Local default. Assert ReportingMonitor
uses `pdfreports`, `rvtreports`, and the legacy `BLOB_REPORT_CONTAINER_NAME` alias.

- [ ] **Step 2: Run ReportingMonitor storage tests and verify RED**

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --filter 'FullyQualifiedName~MonitorBlobReportStorageTests|FullyQualifiedName~ReportingDependencyBoundaryTests' \
  --nologo -v minimal
```

Expected: compilation fails because the reporting storage adapter still uses
`IBlobStorageService`.

- [ ] **Step 3: Implement provider-neutral report storage**

`Rvt.Reporting.Storage.csproj` references only `Rvt.Storage.Abstractions` for storage.
Write:

```csharp
var client = storageClients.GetRequiredClient(
    ReportingStorageResourceNames.Reports);
await using var stream = new MemoryStream(report.Content, writable: false);
var result = await client.WriteAsync(
    new StorageWriteRequest(
        StorageObjectKey.Parse(report.FileName),
        stream,
        report.ContentType),
    cancellationToken).ConfigureAwait(false);
return uriResolver.Resolve(result.Key);
```

Define `ReportingStorageResourceNames.Reports = "reporting-reports"` in
`Rvt.Reporting.Storage` so both the adapter and host use the same public constant.

- [ ] **Step 4: Implement explicit provider composition and URI resolution**

The ReportingMonitor host deliberately references Local, AzureBlob, and S3. Select
exactly one provider using the same key order and invalid-provider message as Svantek.
Bind provider options with ReportingMonitor defaults.

Update Program to:

```csharp
configureServices: (services, configuration) =>
    services.AddReportingMonitor(configuration)
```

Update `ReportingServiceProviderFactory` to pass its existing in-memory
`IConfiguration` to `AddReportingMonitor(configuration)`.

`ConfiguredReportObjectUriResolver` receives one of these delegates:

```csharp
Func<StorageObjectKey, Uri> resolveUri
```

For Local, resolve the keyed `LocalObjectStorageClient`; for Azure, resolve the keyed
`AzureBlobObjectStorageClient`; for S3, resolve the keyed `S3ObjectStorageClient`.
Register the resolver with the selected concrete client's `GetObjectUri` method. The
Local branch is:

```csharp
services.AddSingleton<IReportObjectUriResolver>(provider =>
    new ConfiguredReportObjectUriResolver(
        provider
            .GetRequiredKeyedService<LocalObjectStorageClient>(
                ReportingStorageResourceNames.Reports)
            .GetObjectUri));
```

The Azure branch resolves:

```csharp
provider
    .GetRequiredKeyedService<AzureBlobObjectStorageClient>(
        ReportingStorageResourceNames.Reports)
    .GetObjectUri
```

The S3 branch resolves:

```csharp
provider
    .GetRequiredKeyedService<S3ObjectStorageClient>(
        ReportingStorageResourceNames.Reports)
    .GetObjectUri
```

These three explicit branches preserve the existing Local `file:`, Azure HTTPS, and S3
`s3:` URI formats without adding URI behavior to `IObjectStorageClient`.

- [ ] **Step 5: Run the focused ReportingMonitor tests and verify GREEN**

Run the Step 2 command.

Expected: all focused storage and architecture tests pass.

- [ ] **Step 6: Run all ReportingMonitor tests**

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  --nologo -v minimal
```

Expected: every enabled ReportingMonitor test passes.

- [ ] **Step 7: Commit the ReportingMonitor migration**

```bash
git add apps/monitors/reportingmonitor
git commit -m "refactor(storage): migrate report storage consumer"
```

---

### Task 8: Remove Legacy Storage From `Rvt.Monitor.Common`

**Files:**
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/AzureBlobService.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/AzureBlobStorageService.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobObjectName.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageOptions.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageProvider.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageServiceCollectionExtensions.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageStartupValidationHostedService.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageWriteRequest.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/BlobStorageWriteResult.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/IBlobStorageService.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/LocalFileBlobStorageService.cs`
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/S3BlobStorageService.cs`
- Delete: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Storage/BlobObjectNameTests.cs`
- Delete: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs`
- Delete: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`
- Delete: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Storage/LocalFileBlobStorageServiceTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Architecture/StorageDependencyBoundaryTests.cs`

- [ ] **Step 1: Add Common dependency assertions and make them fail**

Add assertions that `Rvt.Monitor.Common` has no `AWSSDK.S3`, `Azure.Identity`, or
`Azure.Storage.Blobs` package reference and no `Amazon.` or `Azure.Storage` namespace
in production source. Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj \
  --filter FullyQualifiedName~StorageDependencyBoundaryTests --nologo -v minimal
```

Expected: FAIL because Common still contains Azure/Amazon source and package references.

- [ ] **Step 2: Verify all active consumers have migrated**

Run:

```bash
rg -n \
  'IBlobStorageService|BlobStorageWriteRequest|BlobStorageWriteResult|BlobStorageOptions|BlobStorageProvider|AddMonitorBlobStorage|LocalFileBlobStorageService|AzureBlobStorageService|S3BlobStorageService' \
  apps libs services \
  --glob '*.cs' --glob '*.csproj'
```

Expected: matches exist only in the legacy Common source/tests scheduled for deletion,
historical documentation, and explicitly excluded future-pending code that does not
reference these Common types.

- [ ] **Step 3: Delete the legacy files and remove SDK package references**

Delete the listed production and test files. Remove these entries from
`Rvt.Monitor.Common.csproj`:

```xml
<PackageReference Include="AWSSDK.S3" />
<PackageReference Include="Azure.Identity" />
<PackageReference Include="Azure.Storage.Blobs" />
```

Do not remove their central versions; the new provider projects use them.

- [ ] **Step 4: Run the Common dependency guard and verify GREEN**

Run the Step 1 command.

Expected: all dependency-boundary tests pass with no inconclusive result.

- [ ] **Step 5: Run shared, Common, and consumer tests**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj --nologo -v minimal
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --nologo -v minimal
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --nologo -v minimal
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo -v minimal
```

Expected: all four test projects pass.

- [ ] **Step 6: Run the legacy-symbol and vendor-boundary searches**

```bash
rg -n \
  'IBlobStorageService|BlobStorageWriteRequest|BlobStorageWriteResult|BlobStorageOptions|BlobStorageProvider|AddMonitorBlobStorage|LocalFileBlobStorageService|AzureBlobStorageService|S3BlobStorageService' \
  apps libs services \
  --glob '*.cs' --glob '*.csproj'

rg -n 'AWSSDK.S3|Azure.Identity|Azure.Storage.Blobs|using Amazon|using Azure.Storage' \
  libs/rvt-monitor-common/src/Rvt.Monitor.Common \
  --glob '*.cs' --glob '*.csproj'
```

Expected: both commands return exit code 1 with no matches.

- [ ] **Step 7: Commit legacy removal**

```bash
git add libs/rvt-monitor-common/src/Rvt.Monitor.Common \
  libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests \
  libs/rvt-monitor-common/tests/Rvt.Storage.Tests
git commit -m "refactor(storage): remove common storage implementations"
```

---

### Task 9: Wire Storage Projects Into Source Solutions and Hand Off Packaging

**Files:**
- Modify: `libs/rvt-monitor-common/rvt-common.sln`
- Modify: `Rvt.Mono.slnx`
- Modify: `apps/monitors/rvt-monitors.sln`
- Verify only: `tests/verify-mono-solution.test.sh`
- Delegated: every `packages.lock.json` change is owned by
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`,
  Task 5, **Regenerate the complete locked dependency graph**.
- Delegated: package catalogs and artifact assertions are owned by Tasks 1–2;
  package-only consumers by Task 3; source-boundary guards and fixtures by Task 4;
  build/CI by Tasks 6–7; release tests, preflight, SBOM, assets, and checksums by
  Tasks 8–9; and release documentation by Tasks 10–11 of
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`,
  which executes after this source split.

**Interfaces:**
- Produces four storage source projects ready for the release plan to pack at the same
  exact version:
  - `Rvt.Storage.Abstractions`
  - `Rvt.Storage.Local`
  - `Rvt.Storage.AzureBlob`
  - `Rvt.Storage.S3`

- [ ] **Step 1: Run the solution guard first**

The guard derives its expected project set from the repository and therefore sees the
new projects before they are added to the solutions. Run:

```bash
./tests/verify-mono-solution.test.sh
```

Expected: FAIL because the four production projects and storage test project are not
yet represented in the solutions.

- [ ] **Step 2: Add the projects to all required solutions**

Add the four production projects and the test project to
`libs/rvt-monitor-common/rvt-common.sln` and `Rvt.Mono.slnx`. Add the provider projects
reached by active monitors to `apps/monitors/rvt-monitors.sln`.

Run:

```bash
./tests/verify-mono-solution.test.sh
dotnet sln libs/rvt-monitor-common/rvt-common.sln list
dotnet sln apps/monitors/rvt-monitors.sln list
dotnet sln Rvt.Mono.slnx list
```

Expected: the guard passes and each solution lists the intended storage projects once.

- [ ] **Step 3: Restore and build the source graph without rewriting release locks**

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  -p:RestorePackagesWithLockFile=false -p:RestoreLockedMode=false --nologo
dotnet restore Rvt.Mono.slnx \
  -p:RestorePackagesWithLockFile=false -p:RestoreLockedMode=false --nologo
dotnet build libs/rvt-monitor-common/rvt-common.sln --no-restore --nologo -v minimal
dotnet build Rvt.Mono.slnx --no-restore --nologo -v minimal
```

Expected: both solution graphs restore and build. No tracked lock file changes are
staged; release-plan Task 5 regenerates the complete lock graph atomically.

- [ ] **Step 4: Commit source-solution wiring**

```bash
git add Rvt.Mono.slnx apps/monitors/rvt-monitors.sln \
  libs/rvt-monitor-common/rvt-common.sln
git commit -m "build(storage): add provider projects to source solutions"
```

---

### Task 10: Final Verification and Documentation

**Files:**
- Modify: `apps/monitors/README.md`
- Modify: `docs/modules/monitors/reportingmonitor/README.md`
- Modify: `docs/operations/monitors/container-builds.md`
- Modify: `docs/development/rvt-monitor-common/dependency-license-review.md`
- Modify: `project_state.md`

- [ ] **Step 1: Update documentation assertions before documentation**

Add or extend a repository documentation test that requires:

- explicit provider package names;
- unchanged Local, Azure, and S3 configuration keys;
- named resources `svantek-sound-recordings` and `reporting-reports`;
- reporting link formats remain Local `file:`, Azure HTTPS, and S3 `s3:`;
- Portal and independent reporting-service migrations are labelled future pending.

Run the relevant documentation test.

Expected: FAIL because the documents still describe `IBlobStorageService`.

- [ ] **Step 2: Update operator and dependency documentation**

Replace Common-storage wording with explicit adapter composition. Preserve every current
configuration example and explain that hosts reference all three provider packages only
because they deliberately retain deployment-time provider selection.

Update the dependency license review so Azure and AWS dependencies are attributed to
their provider packages rather than `Rvt.Monitor.Common`.

- [ ] **Step 3: Update `project_state.md`**

Record:

- the four storage projects and named-client contract;
- migrated Svantek and ReportingMonitor resources;
- removal of Common SDK dependencies;
- test/build/package verification results;
- every item in **Future Pending Work**, preserving its pending status.

- [ ] **Step 4: Run the documentation test and full verification**

```bash
./tests/verify-mono-layout.test.sh
./tests/verify-mono-solution.test.sh
./tests/verify-rvt-common-source-boundary.test.sh
dotnet test libs/rvt-monitor-common/tests/Rvt.Storage.Tests/Rvt.Storage.Tests.csproj --nologo -v minimal
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --nologo -v minimal
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --nologo -v minimal
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo -v minimal
dotnet build Rvt.Mono.slnx --no-restore --nologo -v minimal
git diff --check
```

Expected: every command passes; `git diff --check` has no output.

- [ ] **Step 5: Run the final boundary searches**

```bash
rg -n \
  'IBlobStorageService|BlobStorageWriteRequest|BlobStorageWriteResult|BlobStorageOptions|BlobStorageProvider|AddMonitorBlobStorage' \
  apps libs services \
  --glob '*.cs' --glob '*.csproj'

rg -n 'AWSSDK.S3|Azure.Identity|Azure.Storage.Blobs' \
  libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
```

Expected: no matches.

- [ ] **Step 6: Commit documentation and state**

```bash
git add apps/monitors/README.md \
  docs/modules/monitors/reportingmonitor/README.md \
  docs/operations/monitors/container-builds.md \
  docs/development/rvt-monitor-common/dependency-license-review.md \
  project_state.md tests
git commit -m "docs(storage): document provider package split"
```

---

## Future Pending Work

These items remain outside this implementation. They require separate review before
code changes:

1. Migrate Portal `MonitorPictureStorage` and `SiteArchiveService` from
   `BlobStorageClientFactory` to `IObjectStorageClientFactory`, preserving protected
   streaming, Local fallback, atomic writes, existing `blob://` monitor references,
   persisted archive URLs, and report/archive container boundaries.
2. Decide whether Portal customer-logo storage should use the shared named-client
   contract.
3. Decide whether
   `services/reporting/src/Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`
   should adopt `Rvt.Storage.AzureBlob`; it remains an independent adapter in this
   split.
4. Make an independent deprecation/removal decision for
   `apps/portal/RVT.Utilities/AzureBlobService.cs`.
5. Consider dynamic provider discovery only if deployments require installing a
   provider without rebuilding a host.
6. Consider external-consumer migration tooling only if coordinated major-version
   adoption proves insufficient.
7. Review database, MQTT, scheduling, and observability dependencies as separate
   boundary projects after the communication and storage splits are complete.

## Self-Review Checklist

- [ ] Every storage requirement in the approved design maps to a task.
- [ ] Every new production type has an exact file and public signature.
- [ ] Every behavior change follows RED, GREEN, and commit steps.
- [ ] No provider SDK appears in Abstractions or Common.
- [ ] Provider selection appears only in application composition roots.
- [ ] ReportingMonitor preserves its absolute persisted report links outside the
  generic storage port.
- [ ] Portal and independent reporting-service storage remain future pending.
- [ ] Release wiring uses the complete eleven-package graph rather than a temporary
  storage-only package count.
- [ ] All commands name exact projects or repository scripts and state expected results.
