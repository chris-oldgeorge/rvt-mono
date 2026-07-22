# Reporting Storage and P1 Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make ReportingMonitor use the common provider-neutral blob service and correct vibration counts, scheduled batch isolation, and persisted recipient failure behavior.

**Architecture:** Preserve `IReportStorage` in reporting core and implement it as a thin adapter over common `IBlobStorageService`. Keep EF Core as the atomic metadata boundary, normalize delivery outcomes before persistence, and isolate scheduled failures at the per-rule orchestration boundary.

**Tech Stack:** .NET 10, ASP.NET Core dependency injection, EF Core 10, PostgreSQL/Npgsql, MSTest for common infrastructure, xUnit for reporting, common Local/Azure Blob/S3 storage adapters.

## Global Constraints

- Keep `IReportStorage` as the reporting-domain port.
- Keep `IDBClient` out of new reporting behavior; retain the narrow reporting ports.
- Use the common Local, Azure Blob, and S3 implementations without Azure SDK references in `Rvt.Reporting.Storage`.
- Default reporting storage to container `pdfreports`, prefix `rvtreports`, and local root `/data/rvt/blobs`.
- Preserve `RVT__BLOB_REPORT_CONTAINER_NAME` as a compatibility alias; standardize documentation on common `RVT__BLOB_*` settings.
- Persist delivery failures and advance `LastGenerated`; propagate requested cancellation.
- Do not add an outbox, retry worker, database schema, or unrelated refactor.
- Preserve unrelated untracked files.

---

### Task 1: Configurable Common Blob Registration

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageOptions.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageServiceCollectionExtensions.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs`
- Test: `rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Produces: `BlobStorageOptions.Bind(IConfiguration, string defaultContainer = "audiofiles", string defaultPrefix = "", string? legacyContainerEnvironmentKey = "AUDIO_FOLDER")`.
- Produces: `AddMonitorBlobStorage(IServiceCollection, Func<IConfiguration, BlobStorageOptions>)` while retaining the existing parameterless extension.

- [ ] **Step 1: Add failing option-binding and registration tests**

Add tests proving custom defaults, the reporting legacy alias, and a registration options factory:

```csharp
[TestMethod]
public void Bind_WithCustomDefaults_UsesReportingDefaultsAndLegacyAlias()
{
    var defaults = BlobStorageOptions.Bind(
        new ConfigurationBuilder().Build(),
        defaultContainer: "pdfreports",
        defaultPrefix: "rvtreports",
        legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");
    Assert.AreEqual("pdfreports", defaults.Container);
    Assert.AreEqual("rvtreports", defaults.Prefix);

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RVT:BLOB_REPORT_CONTAINER_NAME"] = "legacy-reports"
        })
        .Build();
    var legacy = BlobStorageOptions.Bind(
        configuration,
        defaultContainer: "pdfreports",
        defaultPrefix: "rvtreports",
        legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME");
    Assert.AreEqual("legacy-reports", legacy.Container);
}
```

- [ ] **Step 2: Verify RED**

Run:

```bash
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --filter "FullyQualifiedName~BlobStorage" --nologo
```

Expected: compilation fails because the overloads do not exist.

- [ ] **Step 3: Implement the option and registration overloads**

Change binding to use caller-supplied defaults:

```csharp
public static BlobStorageOptions Bind(
    IConfiguration configuration,
    string defaultContainer = "audiofiles",
    string defaultPrefix = "",
    string? legacyContainerEnvironmentKey = "AUDIO_FOLDER")
{
    var providerText = FirstConfigured(configuration, "BlobStorage:Provider", "BLOB_PROVIDER");
    return new BlobStorageOptions
    {
        Provider = ResolveProvider(providerText),
        Container = FirstConfigured(
            configuration,
            "BlobStorage:Container",
            "BLOB_CONTAINER",
            legacyContainerEnvironmentKey,
            defaultContainer),
        Prefix = FirstConfigured(
            configuration,
            "BlobStorage:Prefix",
            "BLOB_PREFIX",
            defaultValue: defaultPrefix),
        LocalRoot = FirstConfigured(configuration, "BlobStorage:LocalRoot", "BLOB_LOCAL_ROOT", defaultValue: "/data/rvt/blobs"),
        AzureConnectionString = FirstConfigured(configuration, "BlobStorage:AzureConnectionString", "BLOB_CONNECTION_STRING"),
        AzureServiceUri = FirstConfigured(configuration, "BlobStorage:AzureServiceUri", "BLOB_SERVICE_URI"),
        S3Bucket = FirstConfigured(configuration, "BlobStorage:S3Bucket", "S3_BUCKET"),
        S3Region = FirstConfigured(configuration, "BlobStorage:S3Region", "S3_REGION"),
        S3ServiceUrl = FirstConfigured(configuration, "BlobStorage:S3ServiceUrl", "S3_SERVICE_URL"),
        S3ForcePathStyle = GetBool(configuration, "BlobStorage:S3ForcePathStyle", "S3_FORCE_PATH_STYLE")
    };
}
```

Refactor registration so both entry points share one implementation:

```csharp
public static IServiceCollection AddMonitorBlobStorage(this IServiceCollection services) =>
    services.AddMonitorBlobStorage(static configuration => BlobStorageOptions.Bind(configuration));

public static IServiceCollection AddMonitorBlobStorage(
    this IServiceCollection services,
    Func<IConfiguration, BlobStorageOptions> optionsFactory)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(optionsFactory);
    services.AddSingleton(provider => optionsFactory(provider.GetRequiredService<IConfiguration>()));
    services.AddSingleton<IBlobStorageService>(CreateStorageService);
    services.AddSingleton<IHostedService, BlobStorageStartupValidationHostedService>();
    return services;
}
```

`CreateStorageService` retains the existing provider switch.

- [ ] **Step 4: Verify GREEN and commit**

Run the filtered test command. Expected: all blob-storage tests pass, including the existing `audiofiles` defaults.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageOptions.cs rvt-monitor-common/Rvt.Monitor.Common/Storage/BlobStorageServiceCollectionExtensions.cs rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageOptionsTests.cs rvt-monitor-common/Rvt.Monitor.CommonTests/Storage/BlobStorageServiceCollectionExtensionsTests.cs
git commit -m "feat: support monitor-specific blob defaults"
```

### Task 2: Reporting Adapter over Common Blob Storage

**Files:**
- Delete: `reportingmonitor/Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`
- Create: `reportingmonitor/Rvt.Reporting.Storage/MonitorBlobReportStorage.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj`
- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorOptions.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingOptions.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Create: `reportingmonitor/ReportingMonitorTests/Storage/MonitorBlobReportStorageTests.cs`

**Interfaces:**
- Consumes: `IBlobStorageService.WriteAsync(BlobStorageWriteRequest, CancellationToken)`.
- Produces: `MonitorBlobReportStorage : IReportStorage`.

- [ ] **Step 1: Add failing adapter and composition tests**

Create a recording `IBlobStorageService` test double. Assert `StoreAsync` sends object name `report.pdf`, content type `application/pdf`, and the original bytes, then returns `https://storage.example.test/rvtreports/report.pdf`. Add a missing-URI case expecting `InvalidOperationException`.

Extend the architecture test:

```csharp
Assert.IsType<LocalFileBlobStorageService>(provider.GetRequiredService<IBlobStorageService>());
Assert.IsType<MonitorBlobReportStorage>(provider.GetRequiredService<IReportStorage>());
var blobOptions = provider.GetRequiredService<BlobStorageOptions>();
Assert.Equal("pdfreports", blobOptions.Container);
Assert.Equal("rvtreports", blobOptions.Prefix);
```

- [ ] **Step 2: Verify RED**

```bash
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --filter "FullyQualifiedName~MonitorBlobReportStorageTests|FullyQualifiedName~ReportingDependencyBoundaryTests" --nologo
```

Expected: compilation fails because `MonitorBlobReportStorage` is absent.

- [ ] **Step 3: Implement the thin adapter and host registration**

```csharp
public sealed class MonitorBlobReportStorage(IBlobStorageService blobStorage) : IReportStorage
{
    public async Task<Uri> StoreAsync(RenderedReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        var result = await blobStorage.WriteAsync(
            new BlobStorageWriteRequest(report.FileName, report.Content, report.ContentType),
            cancellationToken).ConfigureAwait(false);
        return Uri.TryCreate(result.Uri, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException("The blob storage provider did not return an absolute report URI.");
    }
}
```

Replace Azure packages in `Rvt.Reporting.Storage.csproj` with a reference to common. Register:

```csharp
services.AddMonitorBlobStorage(configuration => BlobStorageOptions.Bind(
    configuration,
    defaultContainer: "pdfreports",
    defaultPrefix: "rvtreports",
    legacyContainerEnvironmentKey: "BLOB_REPORT_CONTAINER_NAME"));
services.AddSingleton<IReportStorage, MonitorBlobReportStorage>();
```

Remove blob fields, binding, and validation from `ReportingMonitorOptions`; common storage now owns them.

- [ ] **Step 4: Verify GREEN and commit**

Run the filtered tests, then verify no reporting-specific Azure dependency remains:

```bash
rg -n "AzureBlobReportStorage|Azure\.Storage|Azure\.Identity" reportingmonitor/Rvt.Reporting.Storage reportingmonitor/ReportingMonitor
```

Expected: tests pass and the search has no matches.

```bash
git add reportingmonitor/Rvt.Reporting.Storage reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs reportingmonitor/ReportingMonitor/api/ReportingMonitorOptions.cs reportingmonitor/ReportingMonitorTests/TestReportingOptions.cs reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs reportingmonitor/ReportingMonitorTests/Storage/MonitorBlobReportStorageTests.cs
git commit -m "refactor: use common blob storage for reports"
```

### Task 3: Correct Vibration Alert Matching

**Files:**
- Modify: `reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`

**Interfaces:**
- Preserves: `IReportingDataQueries.LoadSiteReportDataAsync`.
- Changes: vibration notification matching uses the raw database averaging period; returned `AlertRuleData.AveragingPeriodSeconds` remains null.

- [ ] **Step 1: Add a failing PostgreSQL integration test**

Add `SeedSiteWithVibrationRuleAsync` with a vibration monitor (`type_of_monitor = 2`), rule averaging period `60`, and matching notification averaging period `60` with closed note. Assert:

```csharp
var rule = Assert.Single(Assert.Single(site.Monitors).AlertRules);
Assert.Null(rule.AveragingPeriodSeconds);
Assert.Equal(1, rule.TriggeredCount);
Assert.Equal("Vibration reviewed", rule.LatestClosedNote);
```

- [ ] **Step 2: Verify RED**

```bash
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --filter "FullyQualifiedName~LoadSiteReportDataAsync_MatchesVibrationNotificationsBeforeClearingDisplayPeriod" --nologo
```

Expected: `TriggeredCount` is `0` and the note is null.

- [ ] **Step 3: Implement the minimal mapping fix**

```csharp
var matchingAveragingPeriod = row.AveragingPeriod;
var displayAveragingPeriod = prototype.TypeOfMonitor == MonitorType.Vibration
    ? null
    : matchingAveragingPeriod;
var matchingNotifications = notifications.Where(notification =>
    notification.AlertType == (AlertType)row.AlertType &&
    string.Equals(notification.Field, row.AlertField, StringComparison.Ordinal) &&
    notification.Threshold == threshold &&
    notification.AveragingPeriodSeconds == matchingAveragingPeriod).ToArray();
```

Pass `displayAveragingPeriod` to `AlertRuleData`.

- [ ] **Step 4: Verify GREEN and commit**

Run the filtered test. Expected: one trigger, null display period, and the closed note.

```bash
git add reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs
git commit -m "fix: preserve vibration alert matches"
```

### Task 4: Persist Recipient Delivery Failures

**Files:**
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationContracts.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationService.cs`
- Modify: `reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Core/Reports/ReportGenerationServiceTests.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs`

**Interfaces:**
- Changes: `ReportDeliverySaveRequest(..., string? ErrorMessage)` replaces ambiguous `StatusMessage` persistence.
- Adds: `ILogger<ReportGenerationService>` constructor dependency.

- [ ] **Step 1: Add failing persistence and continuation tests**

Replace the throwing sender expectation with a test expecting a saved report and delivery error. Add a two-recipient sender that throws for the first and succeeds for the second. Assert both recipients were attempted, the first delivery has a non-null bounded error, the second has null error, and the report is saved. Update successful-delivery assertions to expect null `ErrorMessage`. Add an EF assertion that supplied failure text is persisted and successful `report_sent.error_message` is null.

- [ ] **Step 2: Verify RED**

```bash
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --filter "FullyQualifiedName~ReportGenerationServiceTests|FullyQualifiedName~SaveGeneratedReportAsync_PersistsDeliveryErrors" --nologo
```

Expected: the throwing sender escapes and successful deliveries still persist `"Sent ok"`.

- [ ] **Step 3: Implement delivery normalization**

Inject `ILogger<ReportGenerationService>`, rename the save-contract property to `ErrorMessage`, and implement:

```csharp
try
{
    var result = await _messageSender.SendAsync(
        recipientEmail,
        sitePostcode ?? string.Empty,
        rendered,
        cancellationToken).ConfigureAwait(false);
    deliveries.Add(new ReportDeliverySaveRequest(
        sentAtUtc,
        recipientEmail,
        result.Success ? null : BoundedError(result.StatusMessage)));
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    throw;
}
catch (Exception exception)
{
    _logger.LogWarning(exception, "Report delivery failed for {RecipientEmail}.", recipientEmail);
    deliveries.Add(new ReportDeliverySaveRequest(
        sentAtUtc,
        recipientEmail,
        $"Delivery provider threw {exception.GetType().Name}."));
}
```

Bound provider-returned error text to 1024 characters. Map `delivery.ErrorMessage` into `ReportSentEntity.ErrorMessage`. Supply `NullLogger<ReportGenerationService>.Instance` in tests.

- [ ] **Step 4: Verify GREEN and commit**

Run the filtered command. Expected: failures persist, later recipients are attempted, success errors are null, and cancellation propagates.

```bash
git add reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationContracts.cs reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationService.cs reportingmonitor/ReportingMonitor/api/db/ReportingDbClient.cs reportingmonitor/ReportingMonitorTests/Core/Reports/ReportGenerationServiceTests.cs reportingmonitor/ReportingMonitorTests/TestReportingDbClient.cs
git commit -m "fix: persist recipient delivery failures"
```

### Task 5: Isolate Scheduled Rule Failures

**Files:**
- Modify: `reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationService.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Core/Reports/ReportGenerationServiceTests.cs`

**Interfaces:**
- Preserves: `GenerateScheduledReportsAsync` returns successful reports.
- Changes: non-cancellation exceptions are logged per rule and processing continues.

- [ ] **Step 1: Add failing batch and cancellation tests**

Allow `FakeRuleQueries` to return multiple due rules. Configure data loading to fail for the first rule's site and succeed for the second. Assert only the second report is returned and saved. Add a cancellation case asserting `OperationCanceledException` stops the batch immediately.

- [ ] **Step 2: Verify RED**

```bash
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --filter "FullyQualifiedName~GenerateScheduledReportsAsync" --nologo
```

Expected: the first exception aborts the batch.

- [ ] **Step 3: Add the per-rule exception boundary**

```csharp
foreach (var rule in dueRules)
{
    try
    {
        generatedReports.AddRange(await GeneratePeriodsForRuleAsync(
            rule,
            triggerUtc,
            updateLastGenerated: true,
            cancellationToken).ConfigureAwait(false));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        _logger.LogError(exception, "Scheduled report generation failed for rule {ReportRuleId}.", rule.Id);
    }
}
```

Do not catch generation failures in direct rule or one-time paths.

- [ ] **Step 4: Verify GREEN and commit**

Run the filtered command. Expected: a later rule succeeds after the first fails; cancellation stops immediately.

```bash
git add reportingmonitor/Rvt.Reporting.Core/Reports/ReportGenerationService.cs reportingmonitor/ReportingMonitorTests/Core/Reports/ReportGenerationServiceTests.cs
git commit -m "fix: isolate scheduled reporting failures"
```

### Task 6: Local Persistence, Documentation, State, and Verification

**Files:**
- Modify: `docker-compose.yml`
- Modify: `README.md`
- Modify: `reportingmonitor/README.md`
- Modify: `reportingmonitor/ReportingMonitor/appsettings.json`
- Modify: `project_state.md`

**Interfaces:**
- Produces: named Compose volume `reporting-reportfiles` mounted at `/data/rvt/blobs`.
- Documents: common storage configuration and local report path.

- [ ] **Step 1: Add the persistent volume and documentation**

Add:

```yaml
    volumes:
      - reporting-reportfiles:/data/rvt/blobs
```

Declare:

```yaml
volumes:
  svantek-audiofiles:
  reporting-reportfiles:
```

Remove the reporting-specific blob container setting from `appsettings.json`. Document common provider, container, prefix, local/Azure/S3 settings, the legacy alias, and `/data/rvt/blobs/pdfreports/rvtreports/`.

- [ ] **Step 2: Update `project_state.md`**

Record the adapter, defaults/providers, Compose volume, three corrected behaviors, tests, commits, and final verification counts.

- [ ] **Step 3: Format and verify**

```bash
dotnet format reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore
git diff --check
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-restore --nologo
dotnet build rvt-monitors.sln --no-restore --nologo
docker compose config --quiet
codegraph sync .
codegraph status .
```

Expected: formatting and diff checks exit zero; all tests pass; the solution builds; Compose validates; CodeGraph is current.

- [ ] **Step 4: Commit and audit**

```bash
git add docker-compose.yml README.md reportingmonitor/README.md reportingmonitor/ReportingMonitor/appsettings.json project_state.md
git commit -m "docs: document common report storage"
git status --short
git log -7 --oneline
```

Expected: only the three pre-existing unrelated untracked files remain and all remediation commits are on `main`.

