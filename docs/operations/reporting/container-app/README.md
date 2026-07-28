# Container Service Deployment Notes

The reporting workload is `apps/monitors/reportingmonitor` (image
`rvt/reportingmonitor`, built from
`apps/monitors/reportingmonitor/ReportingMonitor/Dockerfile` with the monorepo
root as build context). It is packaged as a plain OCI container for Azure
container-service style hosting and does not require Kubernetes APIs, leader
election, CronJobs, ConfigMaps, or AKS manifests.

The duplicate standalone `services/reporting` deployment was removed on
2026-07-28; its settings are superseded by the list below. Note in particular
that the connection string and scheduling keys changed names.

Required settings:

- `ASPNETCORE_URLS=http://+:8080`
- `ConnectionStrings__DefaultConnection`
- `RVT__BLOB_PROVIDER` plus the provider's own settings
  (`RVT__BLOB_SERVICE_URI` or `RVT__BLOB_CONNECTION_STRING`, and
  `RVT__BLOB_REPORT_CONTAINER_NAME` for Azure)
- `RVT__SENDGRID_API_KEY`
- `RVT__INTERNAL_API_KEY`
- `RVT__SPA_BACKEND_BASE_URL`
- `RVT__SPA_REPORT_CONTENT_API_KEY`
- Optional dev AI narrative settings: `RVT__AI_SUMMARY_ENABLED`, `RVT__AI_SUMMARY_BASE_URL`, `RVT__AI_SUMMARY_MODEL`, `RVT__AI_SUMMARY_TIMEOUT_SECONDS`
- `MonitorApi__Enabled=true` to serve the `/internal/reports/*` endpoints
- `MonitorScheduler__Enabled=true` and the `GenerateScheduledReports` job's
  `Cron`/`MonitorScheduler__TimeZoneId` to run scheduled generation in-process

`RVT__INTERNAL_API_KEY` must be set to a non-empty value in every non-Development
deployment: the endpoint filter is fail-closed and rejects all requests when the
configured key is blank.

Customer logo fetch uses an internal SPA endpoint. The deployed reporting
value for `RVT__SPA_REPORT_CONTENT_API_KEY` must exactly match the deployed SPA
backend value for `ReportContent:InternalApiKey`. Store the shared value in the
target secret store and inject it into both services; do not commit the plaintext
secret.

AI summary generation is disabled by default. Only enable `RVT__AI_SUMMARY_ENABLED`
where the container can reach a trusted Ollama endpoint; otherwise the report
uses the deterministic executive-summary paragraph.

Before deploying the reporting workload against a database, apply the idempotent
prerequisite script from the repository root:

```bash
psql "$RVT_REPORTING_PSQL_CONNECTION" -f apps/monitors/reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql
```

Then run the live PostgreSQL/Timescale gate from the repository root:

```bash
RVT__POSTGRES_INTEGRATION_CONNECTION="$ConnectionStrings__DefaultConnection" dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~TestReportingDbClient -v minimal
```

These tests fail when `RVT__POSTGRES_INTEGRATION_CONNECTION` is unset rather
than reporting green, so an unconfigured run cannot be mistaken for a pass.

Run one scheduler instance in phase 1. Quartz is non-clustered and uses `[DisallowConcurrentExecution]`; per-rule Postgres advisory locks add a second layer of overlap protection.
