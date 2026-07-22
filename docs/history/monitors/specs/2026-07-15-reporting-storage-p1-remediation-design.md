# Reporting Storage and P1 Remediation Design

## Purpose

Refactor ReportingMonitor to use the shared monitor blob-storage service and correct the three P1 behaviors identified during review: incorrect vibration notification counts, fail-fast scheduled batches, and duplicate-prone partial email delivery.

## Scope

- Keep `IReportStorage` as the reporting-domain port.
- Replace the reporting-specific Azure Blob implementation with an adapter over `Rvt.Monitor.Common.Storage.IBlobStorageService`.
- Preserve the reporting defaults `pdfreports` for the container and `rvtreports` for the object prefix.
- Support the common Local, Azure Blob, and S3 providers.
- Preserve `RVT__BLOB_REPORT_CONTAINER_NAME` as a compatibility alias while documenting the standard common-storage settings.
- Persist local Compose reports in a named volume mounted at `/data/rvt/blobs`.
- Correct vibration alert-rule notification matching.
- Isolate scheduled report failures per rule.
- Persist every recipient delivery failure and advance `LastGenerated` after the report and all delivery attempts are recorded.

No full transactional email outbox, delivery retry worker, or database schema expansion is included.

## Storage Architecture

`Rvt.Reporting.Core` continues to depend only on `IReportStorage`. The `Rvt.Reporting.Storage` project will provide a thin `MonitorBlobReportStorage` adapter that translates a `RenderedReport` into `BlobStorageWriteRequest`, delegates to `IBlobStorageService`, and converts the returned provider URI into the report URI persisted in PostgreSQL.

The host will call the common `AddMonitorBlobStorage` registration with reporting-specific option defaults. The common registration and binding APIs will gain an overload that accepts an options factory; their current parameterless behavior and audio-storage defaults remain unchanged. Reporting will no longer reference Azure SDK packages directly.

Provider behavior:

- Local writes beneath `/data/rvt/blobs/pdfreports/rvtreports/` and returns a `file:` URI.
- Azure Blob writes to container `pdfreports` under prefix `rvtreports/` and returns the blob URI.
- S3 writes to the configured bucket under prefix `rvtreports/` and returns an `s3:` URI.

The standard configuration is `RVT__BLOB_PROVIDER`, `RVT__BLOB_CONTAINER`, `RVT__BLOB_PREFIX`, `RVT__BLOB_LOCAL_ROOT`, `RVT__BLOB_CONNECTION_STRING`, `RVT__BLOB_SERVICE_URI`, and the existing `RVT__S3_*` settings. The legacy reporting container setting remains a fallback only.

## Vibration Alert Matching

Notification matching will always use the raw alert-rule averaging period from PostgreSQL. After matching and computing `TriggeredCount` and `LatestClosedNote`, vibration rules will expose a null averaging period only in `AlertRuleData`, preserving the intended peak-only display. An EF Core PostgreSQL integration test will cover a vibration rule and notification with a non-null averaging period.

## Scheduled Failure Isolation

`GenerateScheduledReportsAsync` will handle failures around each rule independently. A failure in data loading, narrative generation, rendering, storage, delivery, or persistence will be logged with the report-rule identifier and will not prevent later due rules from running. Successful reports remain in the returned collection. Cancellation requested through the supplied token will continue to propagate immediately and will not be converted into a rule failure.

The direct `GenerateRuleAsync` and one-time API paths will continue to surface non-delivery generation failures to their caller.

## Delivery Failure Persistence

Each recipient is attempted independently. A non-success `ReportSendResult` or a non-cancellation exception from the sender becomes a delivery record with a non-null error message. Processing continues with the remaining recipients. Successful delivery rows use a null `error_message`.

After all attempts, `SaveGeneratedReportAsync` persists the report, all `report_sent` rows, and the scheduled rule's `LastGenerated` value in its existing EF Core transaction. This prevents a later recipient failure from discarding earlier delivery outcomes and causing duplicate sends on the next scheduled run.

A process crash after an external provider accepts an email but before PostgreSQL commits remains possible. Eliminating that residual window requires a durable outbox and is explicitly outside this remediation.

## Logging and Error Safety

`ReportGenerationService` will use `ILogger<ReportGenerationService>` for structured rule and recipient failure logs. Logs include rule IDs or recipient addresses and exception details through the logging framework. Persisted error text will be bounded and will not include configuration values, API keys, connection strings, or full exception serialization.

## Testing

Implementation will follow red-green-refactor cycles:

1. Adapter tests prove common blob write requests, provider URIs, and missing-URI handling.
2. Common-storage registration tests prove reporting-specific defaults and legacy container compatibility without changing existing defaults.
3. PostgreSQL integration coverage proves vibration matching uses the raw averaging period while displaying no averaging label.
4. Core service tests prove a failed scheduled rule does not block a later rule and cancellation still propagates.
5. Core and EF persistence tests prove thrown and returned delivery failures are recorded, later recipients are attempted, successful rows have no error, and `LastGenerated` advances.
6. The complete ReportingMonitor test project and solution build provide final verification.

## Documentation and State

The root and reporting READMEs will describe the common storage provider settings and local report path. `project_state.md` will record the new adapter, failure semantics, configuration aliases, tests, and verification commands.
