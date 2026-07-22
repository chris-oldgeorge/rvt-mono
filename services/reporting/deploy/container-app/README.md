# Container Service Deployment Notes

This service is packaged as a plain OCI container for Azure container-service style hosting. It does not require Kubernetes APIs, leader election, CronJobs, ConfigMaps, or AKS manifests.

Required settings:

- `ASPNETCORE_URLS=http://+:8080`
- `ConnectionStrings__ReportingDatabase`
- `RVT__BLOB_SERVICE_URI` or `RVT__BLOB_CONNECTION_STRING`
- `RVT__BLOB_REPORT_CONTAINER_NAME`
- `RVT__SENDGRID_API_KEY`
- `RVT__INTERNAL_API_KEY`
- `RVT__SPA_BACKEND_BASE_URL`
- `RVT__SPA_REPORT_CONTENT_API_KEY`
- Optional dev AI narrative settings: `RVT__AI_SUMMARY_ENABLED`, `RVT__AI_SUMMARY_BASE_URL`, `RVT__AI_SUMMARY_MODEL`, `RVT__AI_SUMMARY_TIMEOUT_SECONDS`
- `Quartz__ScheduledReportsCron`
- `Quartz__TimeZone`

Customer logo fetch uses an internal SPA endpoint. The deployed reporting-service
value for `RVT__SPA_REPORT_CONTENT_API_KEY` must exactly match the deployed SPA
backend value for `ReportContent:InternalApiKey`. Store the shared value in the
target secret store and inject it into both services; do not commit the plaintext
secret.

AI summary generation is disabled by default. Only enable `RVT__AI_SUMMARY_ENABLED`
where the container can reach a trusted Ollama endpoint; otherwise the report
uses the deterministic executive-summary paragraph.

Before deploying the reporting service against a database, apply the idempotent
prerequisite script:

```bash
psql "$RVT_REPORTING_PSQL_CONNECTION" -f database/postgres/reporting_service_prerequisites_20260625.sql
```

Then run the live Timescale schema gate from the repository root:

```bash
RVT_REPORTING_TIMESCALE_TEST_CONNECTION="$ConnectionStrings__ReportingDatabase" \
  dotnet test tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj \
  --filter FullyQualifiedName~TimescaleSchemaIntegrationTests -v minimal
```

Run one scheduler instance in phase 1. Quartz is non-clustered and uses `[DisallowConcurrentExecution]`; per-rule Postgres advisory locks add a second layer of overlap protection.
