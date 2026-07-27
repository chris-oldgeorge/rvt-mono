# Monitor Build Status

Use the monorepo for source builds:

```sh
cd /Users/oldgeorge/Developer/rvt-mono
scripts/build-mono.sh
```

Active .NET monitor projects compile Common, Communication, Storage, and
integration-test support through `ProjectReference` entries into the root
`libs/rvt-monitor-common` tree. Ordinary project and solution restores use the
checked-in per-project `packages.lock.json` files.

`apps/monitors/NuGet.config` has nuget.org as its only source, for third-party
dependencies. Active source consumers do not restore Common packages and must
not fall back to a package feed.

## Container build context

Every service in `apps/monitors/docker-compose.yml` uses `../..` as its build
context, which resolves to the monorepo root. Each Dockerfile publishes its
full `apps/monitors/...` project path, so `libs/rvt-monitor-common` is available
to MSBuild inside the build. No package-feed secret is mounted.

Run the container build from `apps/monitors`:

```sh
docker compose build
```

Do not build from the retired Parallels Windows C: share paths (`/Volumes/[C] Windows 11/...` or `/private/tmp/win11c/...`). Those mounted workspaces generated macOS SMB AppleDouble `._*` sidecars that could break Docker build-context packaging. The native clone avoids that class of failure, so the old filtered tar build workaround is no longer the default process.

The checked-in `docker-compose.yml` describes the intended API-mode runtime
environment after that build-path follow-up:

```sh
Infrastructure=local
MonitorApi__Enabled=true
MonitorScheduler__Enabled=false
```

Local API ports:

- MyAtm: `http://localhost:8082/liveness`
- Omnidots: `http://localhost:8083/liveness`
- Svantek: `http://localhost:8084/liveness`
- Reporting: `http://localhost:8085/liveness` (and `http://localhost:8085/readiness` for database readiness)

The root Compose definition does not create a database container. Provide the shared PostgreSQL connection string and reporting secrets through an untracked override or the deployment secret store. ReportingMonitor requires `ConnectionStrings__DefaultConnection` and a target database with `reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql` already applied. Omit database-provider selection settings: PostgreSQL is unconditional, although transitional validation still rejects unsupported stale legacy values. Its protected `/internal/reports` endpoints use `X-RVT-Internal-Key` when `RVT__INTERNAL_API_KEY` is configured.

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

The ReportingMonitor service definition starts with the API enabled and Quartz
disabled; enable the scheduler only in an explicit deployment configuration
after a supported image is available.

## Object Storage Provider Composition

The monitor hosts use the provider-neutral `IObjectStorageClientFactory`
contract from `Rvt.Storage.Abstractions`. Local filesystem behavior belongs to
`Rvt.Storage.Local`, Azure Blob behavior and SDK dependencies belong to
`Rvt.Storage.AzureBlob`, and S3 behavior and SDK dependencies belong to
`Rvt.Storage.S3`; none is supplied by `Rvt.Monitor.Common`.

Svantek uses the named resource `svantek-sound-recordings`, and
ReportingMonitor uses `reporting-reports`. Both hosts reference all three
provider packages only because an operator can select `Local`, `AzureBlob`, or
`S3` at deployment time. Startup composes exactly one provider for each named
resource.

Existing configuration names are unchanged. Selection checks
`BlobStorage:Provider`, `RVT:BLOB_PROVIDER`, then
`RVT__BLOB_PROVIDER`. Provider settings continue to accept their
`BlobStorage:*`, `RVT:*`, and `RVT__*` forms, including the Local
root/container/prefix keys, Azure connection-string/service-URI keys, and S3
bucket/region/service-URL/path-style keys. Svantek retains the `AUDIO_FOLDER`
container alias, and ReportingMonitor retains the
`BLOB_REPORT_CONTAINER_NAME` alias. The complete alias matrix is in the
[monitor README](../../../apps/monitors/README.md#object-storage-providers).

ReportingMonitor persists absolute provider-specific links without changing
their formats: Local `file:`, Azure HTTPS, and S3 `s3:`. URI resolution stays
outside the generic object-storage contract.

Portal blob-client/service migration and the independent
`services/reporting` Azure storage adapter remain future pending and are not
container rollout prerequisites for this monitor split.

Svantek uses the Local object-storage provider by default in the Compose setup. The named `svantek-audiofiles` volume is mounted at `/data/rvt/blobs` and persists sound recordings across container recreation. Remove the volume explicitly when the stored recordings should be deleted.

To switch Svantek to Azure Blob Storage, provide the generic configuration through the deployment environment:

```sh
RVT__BLOB_PROVIDER=AzureBlob
RVT__BLOB_SERVICE_URI=https://<account>.blob.core.windows.net
RVT__BLOB_CONTAINER=audiofiles
```

To switch Svantek to S3-compatible storage, provide:

```sh
RVT__BLOB_PROVIDER=S3
RVT__S3_BUCKET=<bucket>
RVT__S3_REGION=<region>
RVT__BLOB_PREFIX=svantek/audio
```

Keep credentials out of tracked files and supply them through the deployment secret store or an untracked local environment file.

Local scheduler containers should set `Infrastructure=local`, `MonitorScheduler__Enabled=true`, and normally leave `MonitorApi__Enabled=false` unless a combined API plus scheduler process is intentional. Azure Container Apps Jobs should instead set `Infrastructure=azure` and run a single job with `RVT__MONITOR_JOB=<job-name>` or `--job <job-name>`; they do not initialize Quartz.

Before deploying the current Omnidots image, apply `omnidotsmonitor/OmnidotsMonitor/postgres/2026-07-14-add-import-cursors-and-trace-order.sql`. The application requires the independent import-cursor table and ordered trace-sample key. Keep the matching rollback script available, but stop or roll back the application before removing that schema.

## MQTT Client Certificates

Do not commit MQTT client certificates or private keys. When `RVT__MQTT_ENABLED=true`, mount the certificate and private key into the container from the host secret store and set:

```sh
RVT__MQTT_CERTIFICATE_PATH=/run/secrets/mqtt-client.pem
RVT__MQTT_PRIVATE_KEY_PATH=/run/secrets/mqtt-client.key
RVT__MQTT_HOSTNAME=rvt-mqtt-namespace.westeurope-1.ts.eventgrid.azure.net
RVT__MQTT_CLIENT_ID=client1-session1
RVT__MQTT_USERNAME=client1-authn-ID
```

`RVT__MQTT_HOSTNAME`, `RVT__MQTT_CLIENT_ID`, and `RVT__MQTT_USERNAME` keep the historical defaults when unset. Certificate and private-key paths have no defaults, so an MQTT-enabled container fails fast if the secret mount is missing.

## Testlocal Suite

Run the current local suite against already-running monitor API containers with:

```sh
scripts/run-testlocal-suite.sh
```

AirQ joins this suite through a fail-closed single-monitor `testlocal=true` filter. Set `AIRQ_TESTLOCAL_SERIAL_ID` in the calling shell to the one intended AirQ serial before running the script. The script passes it to the monitor as `AirQ__TestLocal__SerialId`; AirQ refuses to start a testlocal run without that value, preventing a fleet-wide Turnkey poll.

The current suite includes:

- AirQ `StoreNoiseLevels` for `AIRQ_TESTLOCAL_SERIAL_ID`
- MyAtm `StoreMonitors` and `StoreDustLevels`
- Omnidots `StoreMonitors`, `StorePeakRecordsLastDataTime`, and `StoreTraces`
- Svantek `StoreMonitors` and `StoreNoiseLevels`

Use `scripts/run-testlocal-suite.sh --dry-run` to print the commands without executing them.

## Svantek Local Demo Secrets

The Svantek monitor reads its API key from `RVT__SVANTEK_API_KEY`. For local development, keep the value out of tracked files and store it as a project-level .NET user secret on `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`:

```sh
dotnet user-secrets set RVT__SVANTEK_API_KEY <redacted> --project svantekmonitor/SvantekMonitor/SvantekMonitor.csproj
```

For the single-monitor local demo container, pass the key through a local, untracked env file together with `testlocal=true`, `RVT__MONITOR_JOB=StoreNoiseLevels`, and `ConnectionStrings__DefaultConnection`. Standalone `docker run` reads these values from `--env-file`; Docker swarm secrets require the Docker daemon to be initialized as a swarm manager first.

## Omnidots Vibration Local Demo

The Omnidots vibration monitor also honors `testlocal=true`. When enabled, the app intentionally narrows monitor catalog writes and monitor read loops to the demo monitor `Vibration - R17222V-QUCILO - 14768`.

Implementation notes:

- `RvtConfig.TESTLOCAL` reads the `testlocal` environment variable.
- `OmnidotsApi` captures that flag at construction time.
- `OmnidotsTestLocalMonitorFilter` filters Omnidots API catalog results by serial `14768`.
- The same filter narrows database monitor reads by serial `14768` and fleet `R17222V-QUCILO`.
- StorePeak, StoreVeff, StoreVdv, battery, traces, monitoring, and offline checks all use the filtered `ReadMonitors` helper.

Do not set `testlocal=true` for normal Omnidots runs; it deliberately excludes every vibration monitor except `14768` / `R17222V-QUCILO`.

Normal trace rollout is configured separately through `Omnidots__TraceCollection__Enabled`, `Omnidots__TraceCollection__AllowedSerialIds__<index>`, and `Omnidots__TraceCollection__MaxMonitorsPerRun`. The checked-in configuration enables only serial `23423` and processes at most one eligible monitor per five-minute run. An empty allowed-serial list means the filtered fleet is eligible; increase the per-run maximum gradually after validating vendor and database capacity.

## MyAtm Dust Local Demo

The MyAtm dust monitor also honors `testlocal=true`. When enabled, the app intentionally narrows monitor catalog writes and monitor read loops to the demo monitor `Dust - R6025V - 21972`.

Implementation notes:

- `RvtConfig.TESTLOCAL` reads the `testlocal` environment variable.
- `MyAtmApi` captures that flag at construction time.
- `MyAtmTestLocalMonitorFilter` filters MyAtmosphere catalog results by serial `21972`.
- The same filter narrows database monitor reads by serial `21972` and fleet `R6025V`.
- StoreDust, StoreAccessoryInfo, offline checks, clear-offline, and serial-specific ProcessDustLevels rules are constrained to the demo monitor when `testlocal=true`.

For an authenticated one-shot local demo container, pass an untracked env file containing `RVT__MYATM_TOKEN`, `testlocal=true`, `RVT__MONITOR_JOB=StoreMonitors`, and `ConnectionStrings__DefaultConnection`. The local Timescale smoke run can share the database container network with `--network container:rvt-timescaledb` and use `Host=127.0.0.1` in the connection string.

Do not set `testlocal=true` for normal MyAtm runs; it deliberately excludes every dust monitor except `21972` / `R6025V`.
