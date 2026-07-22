# RVT Reporting New

`rvt-reporting-new` is the containerized replacement for the legacy RVT reporting Azure Function. It targets .NET 10 and runs as a long-lived ACS-compatible service with Quartz.NET scheduling.

## What This Port Changes

- Replaces Azure Function timer triggers with an in-process Quartz scheduler.
- Exposes internal APIs for scheduled, rule-specific, and one-time report generation.
- Uses Postgres/Timescale via `Npgsql` and canonical database names.
- Keeps ASP.NET Identity table names unchanged when direct identity reads are needed.
- Stores generated PDFs in Azure Blob Storage.
- Sends report emails through SendGrid.
- Supports one-time report generation for a maximum 31-day period by creating or reusing a hidden per-site one-time report rule.
- Fetches optional customer logos from the SPA backend internal report-content API and renders them in generated reports.
- Adds executive-summary report insights, closed-alert notes, and report-period alert heatmaps from Timescale notification data.
- Can generate a dev-only AI narrative through a local Ollama service, with deterministic fallback when disabled or unavailable.

## Important Paths

- `src/Rvt.Reporting.Service`: HTTP host, health checks, internal endpoints, Quartz job.
- `src/Rvt.Reporting.Core`: report models, date-window logic, validation, orchestration contracts.
- `src/Rvt.Reporting.Data`: Postgres repository and SQL mapping.
- `src/Rvt.Reporting.Pdf`: QuestPDF rendering.
- `src/Rvt.Reporting.Storage`: Azure Blob storage adapter.
- `src/Rvt.Reporting.Messaging`: SendGrid delivery adapter.
- `tests/Rvt.Reporting.Core.Tests`: scheduling, validation, and orchestration tests.

## Local Commands

```bash
dotnet build Rvt.Reporting.New.slnx
dotnet test Rvt.Reporting.New.slnx
docker compose -f deploy/docker-compose.yml up --build
```

Internal endpoints require `X-RVT-Internal-Key` unless running in development with no key configured.

Set `RVT__SPA_BACKEND_BASE_URL` and `RVT__SPA_REPORT_CONTENT_API_KEY` when customer logos should be fetched from the portal backend for report branding.
`RVT__SPA_REPORT_CONTENT_API_KEY` must match the SPA backend `ReportContent:InternalApiKey` value in the deployed environment.

For local AI summary experiments, set `RVT__AI_SUMMARY_ENABLED=true`, `RVT__AI_SUMMARY_BASE_URL=http://localhost:11434`, and `RVT__AI_SUMMARY_MODEL` to an installed Ollama model. Leave the flag disabled in environments where Ollama is not available; reports still include a deterministic summary paragraph.

Before using the service with a target Timescale database, apply `database/postgres/reporting_service_prerequisites_20260625.sql` and run the gated schema validation test with `RVT_REPORTING_TIMESCALE_TEST_CONNECTION` set.
