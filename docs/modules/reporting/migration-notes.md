# PostgreSQL Reporting Deployment Record

The reporting workload was extracted from
`Source Code/rvtreporting/RVTReportingAzureFunction` into the current
PostgreSQL/Timescale reporting components.

## Consolidation — 2026-07-28

The reporting stack previously existed twice: as `services/reporting` and as
`apps/monitors/reportingmonitor`. The two copies had diverged, and only the
monitor copy received the later corrections (fail-closed constant-time internal
API-key authentication, per-rule error isolation, transactional report
persistence, delivery-failure capture, and narrow per-concern ports).

`apps/monitors/reportingmonitor` is now the single authoritative
implementation, and the stale `services/reporting` copy was removed. The
monitor host serves the same `/internal/reports/*` contract the portal calls
(`run-scheduled`, `rules/{reportRuleId}/generate`, `one-time`) plus `/liveness`
and `/readiness`, and its test suite supersedes the deleted one. Component
paths named below under `Rvt.Reporting.*` now resolve inside
`apps/monitors/reportingmonitor`; the former `Rvt.Reporting.Data` Npgsql
repository is replaced by the monitor's Entity Framework
`ReportingMonitor/api/db/ReportingDbClient.cs`.

## Component history

- `ReportScheduler.cs` timer execution is driven by the monitor's scheduled
  job runner (`ReportingMonitor/api/ReportingMonitorJobRunner.cs`).
- Anonymous report functions became authenticated `/internal/reports/*`
  endpoints (`ReportingMonitor/api/ReportingMonitorApi.cs`).
- `PdfGenerator` date-window logic moved to
  `Rvt.Reporting.Core/Scheduling/ReportPeriodCalculator.cs`.
- `PdfGenerator` orchestration moved to
  `Rvt.Reporting.Core/Reports/ReportGenerationService.cs`.
- The former `DBUtil.cs` data access moved to
  `ReportingMonitor/api/db/ReportingDbClient.cs`.
- Blob upload moved to
  `Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`.
- SendGrid delivery moved to
  `Rvt.Reporting.Messaging/SendGrid/SendGridReportMessageSender.cs`.

## Deployment contract

- Reporting data access uses PostgreSQL through Npgsql. Stale provider
  configuration may be omitted or use a recognized PostgreSQL/Timescale alias;
  any other value fails during composition before a database client is built.
- Apply
  `apps/monitors/reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql`
  to enable `gen_random_uuid()` and add the hidden one-time report-rule partial
  unique index.
- Set `RVT__POSTGRES_INTEGRATION_CONNECTION` to a dedicated Timescale test
  database and run:

  ```bash
  dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
    --filter FullyQualifiedName~TestReportingDbClient -v minimal
  ```

  These live-database tests fail loudly when the connection variable is unset
  rather than reporting green, so unconfigured runs cannot be mistaken for
  passing coverage.

- The live schema test confirms the canonical `site_search`, `monitor_report`,
  `report_rule`, `report`, `report_sent`, `report_user`, and `"AspNetUsers"`
  relations.
- Reporting queries join `monitor_report` to `deployment` and `contract`, so
  averages, notifications, and alert-rule counts stay inside the
  report-clamped ownership window.
- The live test also verifies `notification.level`,
  `notification.closed_time`, and `notification.closed_note`, then seeds
  threshold-matched notifications to exercise triggered counts, notification
  hydration, and latest closed-note selection.
- Customer-logo deployments set `RVT__SPA_REPORT_CONTENT_API_KEY` to the same
  secret value as the portal backend's `ReportContent:InternalApiKey`.
