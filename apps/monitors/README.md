# RVT Monitors

RVT Monitors contains the container-ready monitor services used to import environmental monitoring data into the RVT PostgreSQL/Timescale database and generate customer reports. The codebase includes four vendor monitor applications, a reporting monitor, shared monitor infrastructure, local Docker orchestration, and an OpenTelemetry/Grafana observability stack.

Detailed monitor documentation is centralized in the
[repository documentation index](../../docs/index.md#monitors).

## Contents

| Path | Contents |
| --- | --- |
| `airqmonitor/` | AirQ noise monitor application, tests, Dockerfile, PostgreSQL scripts, and CI pipeline definitions. |
| `myatmmonitor/` | MyAtm dust monitor application, tests, Dockerfile, PostgreSQL scripts, and CI pipeline definitions. |
| `omnidotsmonitor/` | Omnidots vibration monitor application, tests, Dockerfile, PostgreSQL scripts, and CI pipeline definitions. |
| `svantekmonitor/` | Svantek noise monitor application, tests, Dockerfile, PostgreSQL scripts, and CI pipeline definitions. |
| `reportingmonitor/` | Reporting monitor host, reporting domain/adapters, PostgreSQL prerequisite script, and tests. |
| `observability/` | Local OpenTelemetry Collector, Grafana, Prometheus, Tempo, and Loki configuration plus provisioned RVT dashboards. |
| [`../../docs/index.md#monitors`](../../docs/index.md#monitors) | Central monitor architecture, development, operations, release, database, module, and history documentation. |
| `scripts/` | Operational scripts for local testlocal monitor runs and SonarQube/SonarCloud analysis. |
| `docker-compose.yml` | Local PostgreSQL/Timescale and monitor API container composition. |
| `rvt-monitors.sln` | Root .NET solution containing 14 private-package consumer projects across the monitor applications and tests. |

This release package intentionally excludes agent memory, internal planning notes, and local development state files such as `AGENTS.md`, `project_state.md`, `docs/superpowers/**`, `docs/database/monitors/monitor-data-access-migration.md`, `docs/release/**`, `.codegraph/**`, and release-export tooling.

Shared runtime, infrastructure, and test support come from the private `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` packages at the exact version `0.2.0-rc.1`. `NuGet.config` maps `Rvt.Monitor.*` exclusively to GitHub Packages, and `Directory.Packages.props` centrally pins the version. Authentication must be supplied only to the running restore or container-build process; never write a package credential into source, NuGet configuration, build arguments, image layers, or committed environment files.

## Architecture Summary

The monitor applications are ASP.NET Core/.NET services that can run in one-shot job mode, local always-on container mode, or Azure Container Apps Job mode. PostgreSQL/Timescale is the default database target, with SQL Server compatibility retained for legacy tests and selected migration paths.

Each monitor app uses an EF Core-backed data access layer and narrow query/command interfaces over the legacy `IDBClient` compatibility facade. Simple DTO/entity mapping is handled inside monitor app projects with Mapperly, while shared database and runtime infrastructure is consumed from the exact private packages owned by `RVT-Group-LTD/rvt-reporting`.

OpenTelemetry is wired for traces, metrics, and logs. The local observability stack receives OTLP from the monitor containers through the collector and exposes dashboards in Grafana.

## Monitor Applications

### AirQ

AirQ imports noise measurements and monitor status data. The AirQ application lives under `airqmonitor/AirQMonitor`, with tests under `airqmonitor/AirQMonitorTests`.

### MyAtm

MyAtm imports dust monitor catalogue data, dust levels, accessory information, and offline status. The MyAtm application lives under `myatmmonitor/MyAtmMonitor`, with tests under `myatmmonitor/MyAtmMonitorTests`.

### Omnidots

Omnidots imports vibration monitor catalogue data, peak records, Veff, VDV, traces, battery, sensor, and offline data. The Omnidots application lives under `omnidotsmonitor/OmnidotsMonitor`, with tests under `omnidotsmonitor/OmnidotsMonitorTests`.

### Svantek

Svantek imports noise monitor catalogue data, noise levels, status, notification state, and sound-file metadata. The Svantek application lives under `svantekmonitor/SvantekMonitor`, with tests under `svantekmonitor/SvantekMonitorTests`.

### Reporting

ReportingMonitor generates scheduled, rule-triggered, and one-time customer reports from the shared PostgreSQL data model. The host lives in `reportingmonitor/ReportingMonitor`; its retained domain, PDF, storage, and messaging boundaries are sibling projects under `reportingmonitor/`. Apply `reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql` to the target database before enabling report persistence.

## Configuration

Common runtime configuration is supplied through environment variables or app settings:

| Setting | Purpose |
| --- | --- |
| `RVT__DATABASE_PROVIDER` | Database provider. Defaults to `PostgreSql`; `SqlServer` is still supported for tests and legacy compatibility. |
| `ConnectionStrings__DefaultConnection` | Monitor database connection string. |
| `Infrastructure` | `local` for always-on local containers, `azure` for Azure Container Apps Job execution without Quartz startup. |
| `MonitorScheduler__Enabled` | Enables Quartz scheduling when `Infrastructure=local`. |
| `RVT__MONITOR_JOB` | Runs a single monitor job in one-shot mode. |
| `testlocal` | Enables local demo filters where implemented so only known test monitors are processed. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry collector endpoint. |

ReportingMonitor additionally uses `RVT__INTERNAL_API_KEY` for its protected `/internal/reports` routes and the shared blob-storage settings (`RVT__BLOB_PROVIDER`, `RVT__BLOB_CONTAINER`, `RVT__BLOB_PREFIX`, and the selected provider's settings). Its defaults are container `pdfreports`, prefix `rvtreports`, and the Local provider. The legacy `RVT__BLOB_REPORT_CONTAINER_NAME` setting remains a fallback when `RVT__BLOB_CONTAINER` is absent. Email, SendGrid, SPA, AI-summary, and complete storage settings are documented in the [ReportingMonitor guide](../../docs/modules/monitors/reportingmonitor/README.md). It uses the standard `ConnectionStrings__DefaultConnection` setting and PostgreSQL provider.

### Shared email and SMS delivery

All monitor hosts and ReportingMonitor compose the provider-neutral communication ports from `Rvt.Monitor.Common.Infrastructure`. Provider SDKs do not leak into monitor or reporting application code. Delivery configuration is validated when the host starts; setting a channel to `false` disables its credential requirements and makes local/API-only deployments explicit.

| Setting | Purpose |
| --- | --- |
| `RVT__EMAIL_ENABLED` | Enables email delivery. Docker Compose defaults this to `false`; application configuration otherwise defaults to `true`. |
| `RVT__EMAIL_PROVIDER` | `SendGrid` (default) or `MicrosoftGraph`. |
| `RVT__EMAIL_ALERT_FROM_EMAIL` | SendGrid sender address. |
| `RVT__EMAIL_ALERT_FROM_NAME` | SendGrid display name. Graph uses the configured mailbox display name instead. |
| `RVT__SENDGRID_API_KEY` | SendGrid credential, required only when SendGrid email is enabled. |
| `RVT__MICROSOFT_TENANT_ID` | Microsoft Entra tenant for app-only Graph authentication. |
| `RVT__MICROSOFT_CLIENT_ID` | Microsoft Entra application/client ID. |
| `RVT__MICROSOFT_CLIENT_SECRET` | Microsoft Entra client secret. |
| `RVT__MICROSOFT_SENDER_ADDRESS` | Mailbox used in `/users/{sender}` Graph requests. |
| `RVT__SMS_ENABLED` | Enables TransmitSMS delivery; defaults to `false`. |
| `RVT__SMS_API_KEY` / `RVT__SMS_API_SECRET` | TransmitSMS credentials. |
| `RVT__SMS_SENDER` | TransmitSMS sender label; defaults to `KrakenAlert`. |
| `RVT__EMAIL_TEST_MODE` / `RVT__EMAIL_TEST_REPORT_TO_EMAIL` | ReportingMonitor-only recipient override for staged report sends. |
| `RVT__OMNIDOTS_MONITORING_ALERT_TO` | Omnidots fleet-watchdog recipient; preferred over `Omnidots:Monitoring:Recipient`. |

Microsoft Graph uses application permissions, not POP, IMAP, or delegated user login. Grant admin-approved `Mail.Send`. Reports with attachments from exactly 3 MiB through 150 MiB use Graph draft/upload sessions and also require `Mail.ReadWrite`; smaller attachments use one `sendMail` request. Restrict the Entra application's mailbox access to the designated sender using the tenant's supported application-access controls. Graph ignores `RVT__EMAIL_ALERT_FROM_NAME`; manage the visible name on the sender mailbox.

Durable alert/outbox delivery is at least once. Transient network, timeout, throttling, and server failures retain bounded retry (including bounded `Retry-After`); a process failure after provider acceptance can therefore produce a duplicate. Permanent and configuration failures dead-letter immediately. Stored/logged errors are bounded and omit credentials, tokens, destinations, message bodies, attachment data, raw provider responses, and Graph upload-session URLs.

For rollout, deploy first with SendGrid selected, verify the existing path, then configure Graph in staging and enable ReportingMonitor test-recipient mode. Switch one workload to `RVT__EMAIL_PROVIDER=MicrosoftGraph`, verify acceptance, persisted report outcomes, notification audits, retry scheduling, and dead letters, then expand the rollout. Keep both credentials during migration. Rollback requires no database change: restore `RVT__EMAIL_PROVIDER=SendGrid` and restart/redeploy. Disabling `RVT__EMAIL_ENABLED` or `RVT__SMS_ENABLED` is the channel-level emergency stop.

Svantek uses local blob storage by default for sound recordings. In the local Compose setup, the `svantek-audiofiles` named volume is mounted at `/data/rvt/blobs`, so recordings persist across container recreation. Remove the volume explicitly when the stored recordings should be deleted.

To switch Svantek to Azure Blob Storage, provide:

```bash
RVT__BLOB_PROVIDER=AzureBlob
RVT__BLOB_SERVICE_URI=https://<account>.blob.core.windows.net
RVT__BLOB_CONTAINER=audiofiles
```

To switch Svantek to S3-compatible storage, provide:

```bash
RVT__BLOB_PROVIDER=S3
RVT__S3_BUCKET=<bucket>
RVT__S3_REGION=<region>
RVT__BLOB_PREFIX=svantek/audio
```

Vendor credentials are intentionally not included in this repository. Provide them through user secrets, environment variables, platform secrets, or local ignored files as appropriate for the deployment target.

## Build And Test

Restore and build the full solution:

```bash
dotnet restore rvt-monitors.sln
dotnet build rvt-monitors.sln
```

PostgreSQL integration tests can read local settings from the ignored
`.rvt/rvt-integration.appsettings.Development.json` file at the repository root. Create
that file locally when the runtime environment variable is not used; do not commit database
credentials or copy an existing settings file into the repository.

Run the full test suite:

```bash
dotnet test rvt-monitors.sln
```

Run a single monitor test project:

```bash
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj
```

ReportingMonitor's database fixture creates an isolated generated schema. Supply its admin connection only to the test process; do not put it in app settings or source control:

```bash
RVT__POSTGRES_INTEGRATION_CONNECTION='<runtime-only connection string>' \
  dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo
```

## Local Containers

The container build restores private `Rvt.Monitor.*` packages through an ephemeral
BuildKit secret. Export the NuGet credential for the current shell, then build or start
the local monitor API containers:

```bash
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
docker compose build
```

```bash
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
docker compose up -d --build
```

BuildKit reads this variable only for the package restore/publish step. It is not a
runtime application secret and is not stored in the Dockerfiles, image environment,
image layers, build arguments, repository files, or a committed `.env` file. CI uses
`github.actor` with the repository's authorized `GITHUB_TOKEN` supplied by GitHub Actions.

Start the local observability stack:

```bash
docker compose --project-directory . \
  -f observability/docker-compose.observability.yml \
  up -d
```

Start monitor containers with the observability overrides:

```bash
docker compose --project-directory . \
  -f docker-compose.yml \
  -f observability/docker-compose.observability.yml \
  -f observability/docker-compose.monitors-observed.yml \
  up -d --build
```

Grafana is available at `http://localhost:3000` when the observability stack is running.

ReportingMonitor is available at `http://localhost:8085/liveness`; its database-backed readiness endpoint is `http://localhost:8085/readiness`. Its `/internal/reports` endpoints require `X-RVT-Internal-Key` when `RVT__INTERNAL_API_KEY` is configured (and always outside Development when it is absent). With the default Local blob provider, generated PDFs persist in the `reporting-reportfiles` named volume under `/data/rvt/blobs/pdfreports/rvtreports/`. Supply the database connection and all reporting vendor credentials through an untracked Compose override or deployment secret store; the base Compose file deliberately declares no database service.

AirQ's import API is not published to the host by the base Compose file. Set
`RVT__MONITOR_API_KEY` through a secret mechanism before enabling `MonitorApi`.
Call `POST /store-noise-levels-for-date` with `X-Api-Key` and
`{"date":"YYYY-MM-DD"}`. Other Compose services reach AirQ at
`http://airqmonitor-api:8080`.

For temporary local host access, use an untracked override file with:

```yaml
services:
  airqmonitor-api:
    ports: ["127.0.0.1:8081:8080"]
```

## Testlocal Runs

The repository includes `scripts/run-testlocal-suite.sh` for already-running local monitor containers. The suite targets known demo monitors for MyAtm, Omnidots, Svantek, and one explicitly selected AirQ monitor.

Run:

```bash
export AIRQ_TESTLOCAL_SERIAL_ID=<one-airq-monitor-serial>
scripts/run-testlocal-suite.sh
```

`AIRQ_TESTLOCAL_SERIAL_ID` is required. The script passes it to AirQ as `AirQ__TestLocal__SerialId`; when `testlocal=true`, AirQ fails closed if that setting is absent. The script also expects local containers, database access, and the required vendor credentials to already be configured outside the repository.

## Observability

The `observability/` folder provides:

- OpenTelemetry Collector configuration.
- Prometheus metrics storage.
- Loki log storage.
- Tempo trace storage.
- Grafana datasource and dashboard provisioning.

The provisioned Grafana dashboards cover monitor overview, monitor jobs, and service/database telemetry. Monitor logs, traces, and metrics are correlated through OpenTelemetry attributes where available.

## Release Package Notes

This repository can be exported as a curated client/audit package using the release process documented in the source repository. The curated package contains source code, tests, Docker and observability configuration, operational documentation, and this README. It excludes internal agent instructions, planning notes, local memory files, generated build output, local secrets, and release-export tooling.
