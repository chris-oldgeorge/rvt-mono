# Migration Notes

Legacy source: `Source Code/rvtreporting/RVTReportingAzureFunction`.

Key legacy-to-new moves:

- `ReportScheduler.cs` timer trigger -> `Rvt.Reporting.Service/Scheduling/ScheduledReportsJob.cs`.
- `ReportScheduler.cs` anonymous HTTP functions -> authenticated `/internal/reports/*` endpoints.
- `PdfGenerator` date-window logic -> `Rvt.Reporting.Core/Scheduling/ReportPeriodCalculator.cs`.
- `PdfGenerator` orchestration -> `Rvt.Reporting.Core/Reports/ReportGenerationService.cs`.
- `DBUtil.cs` SQL Server calls -> `Rvt.Reporting.Data/Postgres/PostgresReportingRepository.cs`.
- Blob upload -> `Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`.
- SendGrid delivery -> `Rvt.Reporting.Messaging/SendGrid/SendGridReportMessageSender.cs`.

Production validation:

- Apply `database/postgres/reporting_service_prerequisites_20260625.sql` to enable `gen_random_uuid()` and add the hidden one-time report-rule partial unique index.
- Set `RVT_REPORTING_TIMESCALE_TEST_CONNECTION` to the real Timescale connection string and run `dotnet test tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --filter FullyQualifiedName~TimescaleSchemaIntegrationTests -v minimal`.
- The live test confirms canonical view/table availability for `site_search`, `monitor_report`, `report_rule`, `report`, `report_sent`, `report_user`, and `"AspNetUsers"`.
- Reporting SQL now joins `monitor_report` to `deployment` and `contract` so monitor-bound averages, notifications, and alert-rule counts are scoped to the report-clamped effective ownership window.
- The live test also confirms `notification.level`, `notification.closed_time`, and `notification.closed_note`, then seeds threshold-matched notifications to verify triggered counts, notification hydration, and latest closed-note selection.
- Customer-logo deployments must set `RVT__SPA_REPORT_CONTENT_API_KEY` to the same secret value as the SPA backend's `ReportContent:InternalApiKey`.
