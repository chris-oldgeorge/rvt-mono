# Reporting Monitor Integration Design

**Date:** 2026-07-14
**Status:** Design approved; awaiting written-spec review before implementation planning

## Goal

Bring the complete `rvt-reporting-new` service into this repository as a first-class monitor application. Refactor it to the shared host, PostgreSQL-first EF Core, configuration, observability, deployment, and test conventions used by the existing monitors. Omnidots is the style reference wherever the reporting domain does not require a documented difference.

The imported application retains its report-generation capabilities: scheduled, rule-specific, and one-time reports; QuestPDF rendering; Azure Blob storage; SendGrid delivery; optional SPA customer logos; optional Ollama narratives; and reporting metadata persistence.

## Scope and Source

- Copy the clean local reporting source, including its two local commits ahead of `origin/master`, into `reportingmonitor/` in this repository.
- Add all reporting production and test projects to `rvt-monitors.sln` under a `reportingmonitor` solution folder.
- Retain source documents, schema prerequisites, Docker assets, and report-rendering assets when they are needed by the integrated application. Do not copy source-repository `.git` metadata, `bin`, `obj`, `.DS_Store`, sample PDFs, or other generated output.
- Use .NET 10, matching both the imported service and the current monitors solution.

## Project Layout and Style

The integrated host project is `reportingmonitor/ReportingMonitor`. It follows Omnidots conventions:

- A short top-level `Program.cs` delegates runtime selection to `MonitorHost.RunAsync`.
- Composition lives in an explicit `AddReportingMonitor` service-registration extension.
- API routing is an extension in `api/`; individual application operations live in focused `api/UseCases/` handlers.
- Database code lives in `api/db/`, with `EntityFramework/`, `Queries/`, and `Commands/` subfolders.
- Types use file-scoped namespaces, nullable reference types, implicit usings, `async`/`CancellationToken` signatures, `ConfigureAwait(false)` in library/application code, XML summary comments on public boundary types, and concise `LoggerMessage` definitions where operational logging is needed.
- Tests are in `reportingmonitor/ReportingMonitorTests` and follow Omnidots naming and fixture conventions: `Test…` behavior tests, architecture tests, mapping tests, endpoint tests, and PostgreSQL integration fixtures.

Reporting-specific renderers and external adapters may remain focused projects below `reportingmonitor/` when that keeps package-heavy vendor code separate:

- `Rvt.Reporting.Core` contains domain models, date-window calculation, orchestration contracts, validators, and report insight construction.
- `Rvt.Reporting.Pdf`, `Rvt.Reporting.Storage`, and `Rvt.Reporting.Messaging` remain adapter projects that implement core ports.
- `ReportingMonitor` owns the host, API, use cases, EF Core data access, and dependency injection.

This is a deliberate reporting-specific deviation from a single-project monitor: QuestPDF, Azure Blob, and SendGrid packages remain outside the host and domain packages. It preserves the existing clean boundaries without putting vendor policy into `rvt-monitor-common`.

## Shared Host, API, and Scheduling

`ReportingMonitor` runs through `MonitorHost` in the same three modes as the other monitors:

1. `--job GenerateScheduledReports` (or `RVT__MONITOR_JOB`) builds a one-shot host and dispatches scheduled report generation.
2. `MonitorApi:Enabled=true` starts the minimal API, including shared OpenTelemetry configuration and the reporting internal API surface.
3. `MonitorScheduler:Enabled=true` registers `GenerateScheduledReports` through the shared `MonitorScheduler` Quartz integration. Scheduler jobs, their UTC time zone, and cron expression are declared in `appsettings.json`, not hard-coded in the host.

`ReportingMonitorJobDispatcher` is parameterless for schedule validation and exposes only `GenerateScheduledReports`. The dispatcher delegates to a focused scheduled-generation handler. Quartz runs one reporting job at a time, and the per-rule/per-period database lock prevents duplicate work across API, one-shot, and scheduler processes.

The existing protected endpoints remain compatible under `/internal/reports`:

- `POST /run-scheduled`
- `POST /rules/{reportRuleId}/generate`
- `POST /one-time`

The internal API-key filter validates `X-RVT-Internal-Key` using fixed-time comparison whenever a configured key is present. It never logs the presented key. Missing or invalid keys return an authorization failure. One-time validation failures return RFC-compatible validation responses.

The host maps `/liveness` and `/readiness` using the monitors’ standard response behavior; readiness verifies the reporting database through a narrow health query port.

## EF Core Data Access

The direct `NpgsqlDataSource`/`PostgresReportingRepository` implementation is replaced by a PostgreSQL-first `ReportingMonitorContext` derived from the shared monitor EF Core base/context options conventions.

### Entity and read-model mapping

- Persisted EF entities map `report_rule`, `report`, and `report_sent`, including the hidden one-time rule marker and reporting timestamps.
- Keyless query models map the canonical reporting views/tables needed to compose a report: `site_search`, `monitor_report`, `monitor_windows`, `notification`, `report_user`, `AspNetUsers`, `deployment`, `contract`, and `rvt_alert_rule`.
- The model uses explicit PostgreSQL names and column mappings. ASP.NET Identity names remain quoted/mapped exactly as the physical schema requires.
- Read queries use `AsNoTracking`, parameterized LINQ/EF translation, and effective deployment/contract ownership-window filtering equivalent to the imported SQL.

### Narrow ports

Application code depends on narrow reporting interfaces rather than a broad database client:

- `IReportingRuleQueries` loads due and explicitly requested report rules.
- `IReportingDataQueries` loads the complete site report projection and report data for a bounded UTC window.
- `IReportingGenerationLocks` acquires and releases the per-rule/per-period PostgreSQL advisory lock.
- `IReportingGenerationCommands` atomically persists a generated report, delivery attempts, one-time hidden rule creation/reuse, and scheduled-rule `LastGenerated` update.
- `IReportingHealthQueries` checks database readiness.

If a compatibility `IDBClient` is needed while moving source code, it is an internal facade that only composes these narrow interfaces; new API and use-case code must not depend on it.

`IReportingGenerationCommands` opens an EF Core database transaction for all metadata mutations. A one-time request performs hidden-rule upsert/reuse, report insert, report-sent inserts, and any rule timestamp update in that transaction. Blob upload and email remain external side effects and occur before metadata persistence; failures propagate to the caller and are logged through the operational boundary rather than being silently converted to success.

PostgreSQL advisory locking is a vendor-specific data-boundary operation. It is implemented inside the EF Core data adapter with a parameterized database command and returned through `IReportingGenerationLocks`; no Npgsql connection/data-source API appears in handlers or controllers.

## Report Generation Flow

`ReportGenerationService` remains the domain orchestrator but consumes the narrow query, lock, and command ports. The flows are:

1. Scheduled generation loads due enabled rules, calculates report periods in UTC, skips hidden/off/one-time rules, obtains a per-period lock, and generates each unlocked period.
2. Rule-specific generation loads the requested rule, returns an empty result when it does not exist, then follows the same period flow.
3. One-time generation validates its request, including the existing maximum 31-day UTC window, loads report data, renders/stores/delivers the report, then persists the hidden one-time rule and report metadata atomically.
4. For each generated report, the service loads the report data and optional logo, builds deterministic report insights, obtains an optional narrative with deterministic fallback, renders PDF, uploads it, sends to each recipient, and records delivery results.

The adapter ports preserve cancellation from endpoint, Quartz, and one-shot callers through every I/O operation. No report action emits success after a database, storage, or delivery exception.

## Configuration, Observability, and Deployment

Configuration follows the monitors flat environment-variable convention:

- `ConnectionStrings__DefaultConnection` is the reporting PostgreSQL/Timescale connection.
- `RVT__DATABASE_PROVIDER=PostgreSql` selects the expected provider.
- `MonitorApi`, `MonitorScheduler`, and `MonitorInfrastructure` configure the shared runtime modes.
- A typed `ReportingMonitorOptions` owns reporting-only configuration: Blob settings, SendGrid options, internal API key, SPA content API settings, and optional Ollama settings.

No secret values are committed. Logs use the shared redaction policy and include service/job/rule/period identifiers but never API keys, connection strings, SendGrid keys, or recipient content.

`Dockerfile`, root `docker-compose.yml`, `README.md`, and `docs/container-builds.md` gain a reporting service entry consistent with other monitor containers. The local compose service uses the common host image/build pattern and an explicit development-only port. It depends only on externally configured PostgreSQL/Timescale rather than adding a competing database service to the root compose file.

The existing schema prerequisite is retained as a versioned PostgreSQL migration/preflight document. It requires `pgcrypto`, `report_rule.is_hidden_system_rule`, and the one-time-rule partial unique index before one-time reports are enabled.

## Verification

Implementation is test-first and preserves imported behavior through focused tests before each refactor step:

- Host/dispatcher tests prove one-shot parsing, supported-job validation, scheduler configuration, API registration, and readiness/liveness behavior.
- Architecture tests prevent API/use-case code from referencing `ReportingMonitorContext`, EF Core entities, Npgsql APIs, or vendor adapter implementations directly.
- EF model tests assert PostgreSQL table, column, keyless view/read-model, and identity mappings.
- Query/command tests cover due-rule selection, ownership-window filtering, notification/insight hydration, advisory locking, one-time hidden-rule reuse, and atomic report/report-sent/rule updates.
- Endpoint tests cover API-key authentication, request validation, error status behavior, and cancellation propagation.
- Existing core tests continue covering date periods, one-time validation, insight rendering, PDF output, and orchestration.
- PostgreSQL integration tests use the repository’s established ephemeral schema fixture and require an explicitly supplied integration connection. They apply the reporting prerequisite and create/reset SQL only within the generated test schema.

The final verification gate runs `dotnet build rvt-monitors.sln`, the reporting test project, applicable shared tests, `docker compose config`, and `git diff --check`.

## Non-Goals

- Do not move reporting-specific PDF, storage, messaging, or AI packages into `rvt-monitor-common`.
- Do not change the public reporting API paths or request/response contracts without a separate compatibility decision.
- Do not deploy, send live emails, upload production PDFs, or apply schema changes to a live database as part of this integration.
- Do not rewrite the reporting domain’s schedule/date-period rules beyond the changes required for shared host dispatch and testable UTC configuration.
