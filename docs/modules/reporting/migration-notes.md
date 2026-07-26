# PostgreSQL Reporting Deployment Record

The reporting workload was extracted from
`Source Code/rvtreporting/RVTReportingAzureFunction` into the current
PostgreSQL/Timescale reporting service and monitor components.

## Component history

- `ReportScheduler.cs` timer execution moved to
  `Rvt.Reporting.Service/Scheduling/ScheduledReportsJob.cs`.
- Anonymous report functions became authenticated `/internal/reports/*`
  endpoints.
- `PdfGenerator` date-window logic moved to
  `Rvt.Reporting.Core/Scheduling/ReportPeriodCalculator.cs`.
- `PdfGenerator` orchestration moved to
  `Rvt.Reporting.Core/Reports/ReportGenerationService.cs`.
- The former `DBUtil.cs` data access moved to
  `Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs`.
- Blob upload moved to
  `Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`.
- SendGrid delivery moved to
  `Rvt.Reporting.Messaging/SendGrid/SendGridReportMessageSender.cs`.

## Deployment contract

- Reporting data access uses PostgreSQL through Npgsql. Stale provider
  configuration may be omitted or use a recognized PostgreSQL/Timescale alias;
  any other value fails during composition before a database client is built.
- Apply
  `services/reporting/database/postgres/reporting_service_prerequisites_20260625.sql`
  to enable `gen_random_uuid()` and add the hidden one-time report-rule partial
  unique index.
- Set `RVT_REPORTING_TIMESCALE_TEST_CONNECTION` to a dedicated Timescale test
  database and run:

  ```bash
  dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj \
    --filter FullyQualifiedName~TimescaleSchemaIntegrationTests -v minimal
  ```

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
