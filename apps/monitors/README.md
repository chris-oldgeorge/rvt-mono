# RVT Monitors

RVT Monitors contains the monitor services used to import environmental monitoring data into the RVT PostgreSQL/Timescale database and generate customer reports. The codebase includes four vendor monitor applications, a reporting monitor, shared runtime and provider projects, monorepo-aware Docker definitions, and an OpenTelemetry/Grafana observability stack.

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
| `docker-compose.yml` | Monitor API runtime composition using the monorepo root as its source build context. |
| `rvt-monitors.sln` | Root .NET solution for the monitor applications, shared-source references, and tests. |

The reviewable client release publishes the complete monorepo while excluding agent/session state, internal development history and reviews, release mechanics, generated output, local settings, and saved secrets.

The active mono-repository graph source-references `Rvt.Monitor.Common`,
`Rvt.Monitor.IntegrationTesting`, `Rvt.Communication.Abstractions`,
`Rvt.Communication`, and the three explicit provider projects.
`Rvt.Monitor.Common.Infrastructure` has been removed and is not a facade.
Storage is likewise split into the provider-neutral
`Rvt.Storage.Abstractions` project and the explicit `Rvt.Storage.Local`,
`Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3` adapter projects.

All internal RVT dependencies are direct project references. Internal package
production, package-consumer validation, local RVT feeds, and package-feed
credentials are intentionally absent from the build chain.

## Architecture Summary

The monitor applications are ASP.NET Core/.NET services that can run in one-shot job mode, local always-on container mode, or Azure Container Apps Job mode. PostgreSQL/TimescaleDB is the sole database target for runtime, migrations, and tests.

Each monitor app uses an EF Core-backed data access layer and narrow
query/command interfaces over the retained `IDBClient` facade. Simple
DTO/entity mapping is handled inside monitor app projects with Mapperly, while
shared database and runtime infrastructure is compiled from the in-repository
Common projects.

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
| `ConnectionStrings__DefaultConnection` | Monitor database connection string. |
| `Infrastructure` | `local` for always-on local containers, `azure` for Azure Container Apps Job execution without Quartz startup. |
| `MonitorScheduler__Enabled` | Enables Quartz scheduling when `Infrastructure=local`. |
| `RVT__MONITOR_JOB` | Runs a single monitor job in one-shot mode. |
| `testlocal` | Enables local demo filters where implemented so only known test monitors are processed. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry collector endpoint. |

Database-provider selection settings should be omitted. PostgreSQL is
unconditional; transitional validation may still reject an unsupported value
if a stale legacy setting is supplied, but no provider key is required.

ReportingMonitor additionally uses `RVT__INTERNAL_API_KEY` for its protected `/internal/reports` routes and the provider-neutral object-storage settings (`RVT__BLOB_PROVIDER`, `RVT__BLOB_CONTAINER`, `RVT__BLOB_PREFIX`, and the selected provider's settings). Its defaults are container `pdfreports`, prefix `rvtreports`, and the Local provider. The legacy `RVT__BLOB_REPORT_CONTAINER_NAME` setting remains a fallback when `RVT__BLOB_CONTAINER` is absent. Email, SendGrid, SPA, AI-summary, and complete storage settings are documented in the [ReportingMonitor guide](../../docs/modules/monitors/reportingmonitor/README.md). It uses the standard `ConnectionStrings__DefaultConnection` setting and PostgreSQL provider.

### Object storage providers

Storage consumers depend on the `IObjectStorageClientFactory` named-client
contract from `Rvt.Storage.Abstractions`; provider SDKs and filesystem
implementation details belong only to `Rvt.Storage.AzureBlob`,
`Rvt.Storage.S3`, and `Rvt.Storage.Local`. `Rvt.Monitor.Common` owns no storage
implementation and has no Azure or AWS SDK dependency. Svantek resolves the
named resource `svantek-sound-recordings`, while ReportingMonitor resolves
`reporting-reports`.

The Svantek and ReportingMonitor hosts reference all three adapter packages
only because they deliberately retain deployment-time selection among
`Local`, `AzureBlob`, and `S3`. Each process composes exactly one adapter for
its named resource; these references are not dynamic provider discovery.

The split preserves the existing configuration aliases and their
first-nonblank precedence:

| Purpose | Accepted keys |
| --- | --- |
| Provider | `BlobStorage:Provider`, `RVT:BLOB_PROVIDER`, `RVT__BLOB_PROVIDER` |
| Local root | `BlobStorage:LocalRoot`, `RVT:BLOB_LOCAL_ROOT`, `RVT__BLOB_LOCAL_ROOT` |
| Container | `BlobStorage:Container`, `RVT:BLOB_CONTAINER`, `RVT__BLOB_CONTAINER`; Svantek also retains `RVT:AUDIO_FOLDER` / `RVT__AUDIO_FOLDER`, and ReportingMonitor retains `RVT:BLOB_REPORT_CONTAINER_NAME` / `RVT__BLOB_REPORT_CONTAINER_NAME` |
| Prefix | `BlobStorage:Prefix`, `RVT:BLOB_PREFIX`, `RVT__BLOB_PREFIX` |
| Azure connection | `BlobStorage:AzureConnectionString`, `RVT:BLOB_CONNECTION_STRING`, `RVT__BLOB_CONNECTION_STRING` |
| Azure service URI | `BlobStorage:AzureServiceUri`, `RVT:BLOB_SERVICE_URI`, `RVT__BLOB_SERVICE_URI` |
| S3 bucket | `BlobStorage:S3Bucket`, `RVT:S3_BUCKET`, `RVT__S3_BUCKET` |
| S3 region | `BlobStorage:S3Region`, `RVT:S3_REGION`, `RVT__S3_REGION` |
| S3-compatible endpoint | `BlobStorage:S3ServiceUrl`, `RVT:S3_SERVICE_URL`, `RVT__S3_SERVICE_URL` |
| S3 path style | `BlobStorage:S3ForcePathStyle`, `RVT:S3_FORCE_PATH_STYLE`, `RVT__S3_FORCE_PATH_STYLE` |

ReportingMonitor keeps URI construction outside the generic storage port so
persisted `report.report_link` values retain their established absolute
formats: Local `file:`, Azure HTTPS, and S3 `s3:`.

Portal blob-client/service unification is explicitly future pending work and is
not part of the monitor-host migration. The formerly independent
`services/reporting` Azure report-storage adapter was removed on 2026-07-28
when reporting consolidated into `apps/monitors/reportingmonitor`.

### Shared email and SMS delivery

Each of the five monitor hosts directly composes
`Rvt.Communication.Abstractions`, the `Rvt.Communication` workflow, one selected
mail provider, and `Rvt.Communication.TransmitSms`. The host reads
`RVT:EMAIL_PROVIDER` before the literal `RVT__EMAIL_PROVIDER` fallback, selects
SendGrid by default or Microsoft Graph by a case-insensitive exact match, and
rejects any other value. Both mail provider projects are source-referenced so
the choice is explicit at build time; this is not dynamic provider discovery.
Provider SDKs do not leak into monitor or reporting messaging projects.
Delivery configuration is validated when the host starts; setting a channel to
`false` disables its credential requirements and makes local/API-only
deployments explicit.

The Portal is intentionally different: its host composes only
`Rvt.Communication.Abstractions` and
`Rvt.Communication.SendGridMail`. The monitor Reporting messaging project and
the containerized reporting-service messaging project each depend only on
communication abstractions. ReportingMonitor selects SendGrid or Microsoft
Graph in its host like the other monitors, while the containerized reporting
service explicitly selects SendGrid in its host.

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

Svantek uses the Local object-storage adapter by default for sound recordings. In the local Compose setup, the `svantek-audiofiles` named volume is mounted at `/data/rvt/blobs`, so recordings persist across container recreation. Remove the volume explicitly when the stored recordings should be deleted.

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

The solution projects use their checked-in per-project `packages.lock.json`
files. All RVT dependencies are direct project references. To validate the
complete source graph, run `scripts/build-mono.sh` from the monorepo root; it
restores, checks engineering standards, builds, and tests `Rvt.Mono.slnx`.

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

The complete monitor suites require
`RVT__POSTGRES_INTEGRATION_CONNECTION`. Several retained architecture tests
also resolve paths from the modules' former monorepo locations. Without that
database setting and path-baseline migration, full-suite failures are expected
and must not be reported as communication regressions. The focused
`CommunicationsCompositionTests` are the provider-choice gate.

Package lock files describe third-party dependencies only. Internal RVT
projects are resolved by MSBuild through the source graph.

ReportingMonitor's database fixture creates an isolated generated schema. Supply its admin connection only to the test process; do not put it in app settings or source control:

```bash
RVT__POSTGRES_INTEGRATION_CONNECTION='<runtime-only connection string>' \
  dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo
```

## Local Containers

Compose uses the monorepo root as each monitor's build context. The Dockerfiles
publish their `apps/monitors/...` project paths and resolve shared projects from
`libs/rvt-monitor-common` directly. No internal package feed or NuGet
credential mount is used.

Continue to keep runtime database and vendor credentials in deployment secrets
or ignored local files.

Start the local observability stack:

```bash
docker compose --project-directory . \
  -f observability/docker-compose.observability.yml \
  up -d
```

The monitor-container observability override remains blocked by the same build
context issue. Do not use it as release evidence until the follow-up is merged
and a clean root-context image build is verified.

Grafana is available at `http://localhost:3000` when the observability stack is running.

Once a supported image build path exists, ReportingMonitor's intended local
routes are `http://localhost:8085/liveness` and
`http://localhost:8085/readiness`. Its `/internal/reports` endpoints require
`X-RVT-Internal-Key` when `RVT__INTERNAL_API_KEY` is configured (and always
outside Development when it is absent). With the default Local object-storage
provider,
generated PDFs use the `reporting-reportfiles` named volume under
`/data/rvt/blobs/pdfreports/rvtreports/`. Supply the database connection and
all reporting vendor credentials through an untracked Compose override or
deployment secret store; the base Compose file deliberately declares no
database service.

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

The source repository's private release tooling exports a revision-pinned, reviewable snapshot of the entire monorepo. The payload retains application and library source, tests, CI, Docker and observability configuration, database assets, build tooling, and operational documentation. It omits internal development/session files, release tooling, generated artifacts, local settings, and saved secrets.
