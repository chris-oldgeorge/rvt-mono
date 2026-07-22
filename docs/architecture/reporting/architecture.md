# RVT Reporting New Architecture

The new reporting service is a .NET 10 container application intended for Azure container-service style hosting. It deliberately avoids AKS-only constructs. Scheduling is handled inside the singleton service process by Quartz.NET.

## Runtime Shape

- One long-running reporting container.
- HTTP port `8080`.
- Health endpoints:
  - `GET /health/live`
  - `GET /health/ready`
- Internal report endpoints:
  - `POST /internal/reports/run-scheduled`
  - `POST /internal/reports/rules/{reportRuleId}/generate`
  - `POST /internal/reports/one-time`

## Overlap Protection

Quartz uses `[DisallowConcurrentExecution]` to prevent overlapping executions within the process. The Postgres repository also attempts a `pg_try_advisory_lock` per report rule, report period, and frequency so manual and scheduled paths do not process the same report period at the same time.

## One-Time Reports

One-time reports accept an explicit UTC date range and recipient list. The service rejects ranges longer than 31 days. Generated reports are persisted with a hidden per-site one-time `report_rule_id`, keeping existing report-list joins compatible while excluding the hidden rule from scheduled generation.

## Data Access

All new reporting SQL is isolated in `Rvt.Reporting.Data`. Application table/view names use canonical lowercase singular snake_case names and existing reporting projections such as `site_search` and `monitor_report`. ASP.NET Identity remains physically named by the framework, so direct reads use `"AspNetUsers"` with quoted identity columns.

Report insights use `notification` rows from the requested report period. `level`, `closed_time`, and `closed_note` are loaded alongside threshold-matching fields so the PDF can show executive breach counts, latest closure notes, and day/hour alert heatmaps without new database objects.

The service expects `pgcrypto` so `gen_random_uuid()` is available, and it expects a partial unique index named `ux_report_rule_hidden_one_time_per_site` on hidden one-time report rules. Apply `database/postgres/reporting_service_prerequisites_20260625.sql` before enabling one-time report generation in a target environment.

## Report Insights

The PDF renderer builds a front-of-report executive summary before the graph section. Traffic-light status is Red when a monitor type has any alert notifications in the period, Amber when it has only caution notifications, and Green when it has no alert/caution notifications.

Narrative text comes from `IReportNarrativeProvider`. The default deployment wiring uses the Ollama provider only when `RVT__AI_SUMMARY_ENABLED=true`; otherwise, or if Ollama times out or returns an error, the service renders a deterministic summary from the same compact metrics.

## Internal Service Keys

`RVT__INTERNAL_API_KEY` protects the reporting service's `/internal/reports/*` endpoints. Customer-logo fetches use a separate key: `RVT__SPA_REPORT_CONTENT_API_KEY` in this service must match `ReportContent:InternalApiKey` in the SPA backend. `RVT__SPA_BACKEND_BASE_URL` points to the deployed SPA backend base URL.
