# Reporting monitor

This folder contains the `ReportingMonitor` host plus retained reporting domain, PDF, storage, and messaging projects. The retired standalone reporting Data and Service projects are intentionally not included; their monitor-specific persistence and API behavior belongs in the host.

## Build and test

```bash
dotnet build reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo
```

The database fixture creates a random `rvt_integration_*` PostgreSQL schema, executes its unqualified create/reset scripts through that schema's search path, and drops the schema during cleanup. Provide `RVT__POSTGRES_INTEGRATION_CONNECTION` only to the test process; its role must be able to create and drop schemas. The connection is never stored in this repository.

```bash
RVT__POSTGRES_INTEGRATION_CONNECTION='<runtime-only connection string>' \
  dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo
```

Run the container API at `http://localhost:8085` with `docker compose up reportingmonitor-api`. `/liveness` is process health and `/readiness` verifies PostgreSQL connectivity. The internal report-generation routes under `/internal/reports` require `X-RVT-Internal-Key` when `RVT__INTERNAL_API_KEY` is set; outside Development they reject requests when the key is missing.

## Runtime configuration

The host binds these environment-variable names without storing values in source control:

- `ConnectionStrings__DefaultConnection`
- `RVT__DATABASE_PROVIDER` (must be `PostgreSql`)
- `RVT__INTERNAL_API_KEY`
- `RVT__BLOB_PROVIDER` (`Local`, `AzureBlob`, or `S3`; defaults to `Local`)
- `RVT__BLOB_CONTAINER` (defaults to `pdfreports`)
- `RVT__BLOB_PREFIX` (defaults to `rvtreports`)
- `RVT__BLOB_LOCAL_ROOT` (Local provider; defaults to `/data/rvt/blobs`)
- `RVT__BLOB_CONNECTION_STRING` or `RVT__BLOB_SERVICE_URI` (Azure Blob provider)
- `RVT__S3_BUCKET`, `RVT__S3_REGION`, and optional `RVT__S3_SERVICE_URL` (S3 provider)
- `RVT__EMAIL_ENABLED`
- `RVT__EMAIL_PROVIDER` (`SendGrid` or `MicrosoftGraph`; defaults to `SendGrid`)
- `RVT__EMAIL_TEST_MODE`
- `RVT__EMAIL_TEST_REPORT_TO_EMAIL`
- `RVT__EMAIL_ALERT_FROM_EMAIL`
- `RVT__EMAIL_ALERT_FROM_NAME`
- `RVT__SENDGRID_API_KEY`
- `RVT__MICROSOFT_TENANT_ID`
- `RVT__MICROSOFT_CLIENT_ID`
- `RVT__MICROSOFT_CLIENT_SECRET`
- `RVT__MICROSOFT_SENDER_ADDRESS`
- `RVT__SPA_BACKEND_BASE_URL`
- `RVT__SPA_REPORT_CONTENT_API_KEY`
- `RVT__AI_SUMMARY_ENABLED`
- `RVT__AI_SUMMARY_BASE_URL`
- `RVT__AI_SUMMARY_MODEL`
- `RVT__AI_SUMMARY_TIMEOUT_SECONDS`

`RVT__BLOB_REPORT_CONTAINER_NAME` remains a legacy fallback for the report container when `RVT__BLOB_CONTAINER` is not configured. New deployments should use the common `RVT__BLOB_*` settings. The default Local provider writes PDFs below `/data/rvt/blobs/pdfreports/rvtreports/`; Docker Compose mounts the `reporting-reportfiles` named volume at `/data/rvt/blobs`. The storage adapter returns the provider URI, which is persisted in `report.report_link`.

Azure Blob storage can use either a connection string or a service URI with the Azure credential chain. S3 credentials use the AWS SDK credential chain and are not application settings. See the root README for provider examples.

Each report recipient has an independent persisted delivery outcome. Provider-returned failures and non-cancellation provider exceptions are saved in `report_delivery.error_message`, bounded to 1,024 characters, and do not prevent later recipients from being attempted. Scheduled generation similarly isolates failures by rule; explicit cancellation still stops processing.

Reporting retains `IReportMessageSender` as its application boundary and sends the generated PDF through the shared email port. SendGrid remains the default provider. Microsoft Graph uses app-only `Mail.Send`; PDFs from exactly 3 MiB through 150 MiB use draft/upload sessions and additionally require `Mail.ReadWrite`. Restrict the Entra application to `RVT__MICROSOFT_SENDER_ADDRESS`. Graph gets the visible sender name from that mailbox, while `RVT__EMAIL_ALERT_FROM_NAME` applies to SendGrid. No POP or IMAP configuration is used.

Use `RVT__EMAIL_TEST_MODE=true` with `RVT__EMAIL_TEST_REPORT_TO_EMAIL` during staging so every report is redirected to the approved test address. Verify the persisted per-recipient result before disabling test mode. Provider failures are sanitized before persistence; credentials, recipients, content, attachments, raw responses, and upload URLs are not included. Roll back Graph by restoring `RVT__EMAIL_PROVIDER=SendGrid` and restarting the host; no database rollback is required. Set `RVT__EMAIL_ENABLED=false` for an emergency delivery stop.

Apply `database/postgres/reporting_service_prerequisites_20260625.sql` to the target PostgreSQL database before enabling report persistence.
