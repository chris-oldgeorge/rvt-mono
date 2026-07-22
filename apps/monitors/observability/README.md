# RVT Monitor Observability

This subfolder contains the recovered local observability container stack from the
Windows VM project. It starts and provisions the collector, Grafana, Prometheus,
Tempo, and Loki stack from the macOS clone. The monitor apps share their
OpenTelemetry setup through `Rvt.Monitor.Common` `0.2.0-rc.1`, and emit OTLP logs, traces,
and metrics when `OpenTelemetry__Enabled=true`.

This runbook starts a local OpenTelemetry collector with Grafana, Prometheus, Tempo, and Loki for the RVT monitor apps:

- `airqmonitor`
- `myatmmonitor`
- `omnidotsmonitor`
- `svantekmonitor`

The stack is intended for local verification and development. The observability images are pinned by digest so local verification is reproducible.

## Start The Stack

From the repository root:

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml up -d
```

If the stack has already been created and you are validating config changes, recreate the affected service or the full stack:

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml up -d --force-recreate
```

If you also want to run the monitor API containers from the project compose file on the same Docker network, start both compose files together:

```bash
docker compose --project-directory . \
  -f docker-compose.yml \
  -f observability/docker-compose.observability.yml \
  -f observability/docker-compose.monitors-observed.yml \
  up -d
```

`docker-compose.yml` remains safe to run by itself: it does not enable OpenTelemetry unless the `observability/docker-compose.monitors-observed.yml` override is included.

Do not put secrets in the observability compose file. Continue to provide monitor database/API credentials through the existing local secret flow or private environment files.

## Monitor OTEL Settings

Monitor containers should send OTLP telemetry to the collector over gRPC:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_SERVICE_NAME=airqmonitor
```

Use the matching service name for each container:

- `OTEL_SERVICE_NAME=airqmonitor`
- `OTEL_SERVICE_NAME=myatmmonitor`
- `OTEL_SERVICE_NAME=omnidotsmonitor`
- `OTEL_SERVICE_NAME=svantekmonitor`

When running a monitor from the host instead of inside Docker, use:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

The collector also accepts OTLP HTTP on `http://localhost:4318`.

## Log Verbosity

Every one-shot and Quartz monitor job emits OpenTelemetry log entries for job start, completion, failure, duration, exit code, monitor name, job name, and execution mode. These logs are useful even when the monitor-specific import code does not write its own messages.

For local observed compose runs, `observability/docker-compose.monitors-observed.yml` sets monitor logging to `Debug` and keeps Microsoft/System categories at `Warning`:

```text
OpenTelemetry__LogLevel=Debug
Logging__LogLevel__Default=Debug
Logging__LogLevel__Microsoft=Warning
Logging__LogLevel__System=Warning
```

For production, start with `Information` unless actively troubleshooting:

```text
OpenTelemetry__LogLevel=Information
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
Logging__LogLevel__System=Warning
```

Use Grafana Explore with the Loki datasource for logs. Useful local queries:

```logql
{service_name=~"airqmonitor|myatmmonitor|omnidotsmonitor|svantekmonitor"} |= "Monitor job"
{service_name=~"airqmonitor|myatmmonitor|omnidotsmonitor|svantekmonitor"} | severity_text =~ "Warning|Error"
{service_name="myatmmonitor"} |= "StoreMonitors"
```

For labelled local smoke runs, `smoke_run` is exported as OTLP structured metadata rather than an indexed stream label. Filter it after the stream selector:

```logql
{service_name=~"airqmonitor|myatmmonitor|omnidotsmonitor|svantekmonitor"} | smoke_run = "verbose-normalized"
```

Legacy monitor code used Serilog-style bare `{}` placeholders in many Microsoft `ILogger` message templates. Those produce empty OpenTelemetry attribute names and can make Loki reject an entire batch with `symbolizer lookup: label name is empty`. Monitor templates have been normalised to named placeholders, and the collector logs pipeline also removes empty resource/scope/log attributes before export as a defensive guard.

## .NET Package Versions

The monitor apps get OpenTelemetry through `Rvt.Monitor.Common` `0.2.0-rc.1` with these package versions:

- `OpenTelemetry.Exporter.OpenTelemetryProtocol` `1.16.0`
- `OpenTelemetry.Extensions.Hosting` `1.16.0`
- `OpenTelemetry.Instrumentation.AspNetCore` `1.16.0`
- `OpenTelemetry.Instrumentation.Http` `1.16.0`
- `OpenTelemetry.Instrumentation.Runtime` `1.15.1`

The resolved package dependency versions remain observable in each published container's `.deps.json` files.

Set `OpenTelemetry:ServiceVersion` or `RVT__SERVICE_VERSION` to override the default resource service version of `v0.1.0`.

## Exposed Ports

| Port | Service | Purpose |
| --- | --- | --- |
| `3000` | Grafana | Dashboards and Explore |
| `3100` | Loki | Log API and OTLP log ingest target behind the collector |
| `3200` | Tempo | Tempo query API |
| `4317` | OpenTelemetry Collector | OTLP gRPC receiver |
| `4318` | OpenTelemetry Collector | OTLP HTTP receiver |
| `9090` | Prometheus | Prometheus UI and API |
| `9464` | OpenTelemetry Collector | Prometheus scrape endpoint |

Open Grafana at `http://localhost:3000`. Anonymous local admin access is enabled for this development stack only.

Tempo and Loki are API backends, not standalone browser UIs. If Docker Desktop opens `http://localhost:3200/` or `http://localhost:3100/` when you click their published ports, a `404` response from `/` is expected. Use Grafana Explore for traces and logs, or use the direct health/API paths:

- Tempo readiness: `http://localhost:3200/ready`
- Tempo search API example: `http://localhost:3200/api/search?tags=service.name%3Dmyatmmonitor&limit=5`
- Loki readiness: `http://localhost:3100/ready`
- Loki labels API: `http://localhost:3100/loki/api/v1/labels`

## Data Flow

The monitor apps emit traces, metrics, and logs to `otel-collector`.

- Traces: collector exports OTLP gRPC to `tempo:4317`.
- Logs: collector exports OTLP HTTP to `http://loki:3100/otlp`.
- Metrics: collector exposes Prometheus-format metrics on `otel-collector:9464`.
- Prometheus scrapes itself and `otel-collector:9464`.
- Grafana provisions Prometheus, Loki, and Tempo datasources plus the RVT dashboards.

Loki is configured for OTLP structured metadata. Tempo is configured as a local single-binary instance with OTLP gRPC and HTTP receivers enabled.

## Dashboards

Grafana provisions these dashboards into the `RVT Monitors` folder:

- `RVT Monitor Overview`: cross-service job throughput, failure ratio, job duration, HTTP server rate/duration, process memory, logs, and recent traces.
- `RVT Monitor Jobs`: job starts, completions, failures, failure ratio, job duration, job totals, and job-filtered logs. It has `service` and `job` variables; the Prometheus job variable reads the sanitized OTEL attribute label `rvt_monitor_job_name`.
- `RVT Monitor Dependencies`: outbound HTTP request rate/duration, outbound 5xx ratio, process CPU, .NET GC collections, HTTP traces, and HTTP logs.

The dashboards expect the .NET instrumentation to emit these custom metric names:

- `rvt_monitor_job_started_total`
- `rvt_monitor_job_completed_total`
- `rvt_monitor_job_failed_total`
- `rvt_monitor_job_duration_seconds`

After the first Prometheus scrape, verify the exact metric names at `http://localhost:9090/graph` or `http://localhost:9464/metrics`. If the collector or exporter adds a namespace and the metrics appear as `rvt_rvt_monitor_job_started_total`, `rvt_rvt_monitor_job_completed_total`, `rvt_rvt_monitor_job_failed_total`, and `rvt_rvt_monitor_job_duration_seconds`, update the dashboard queries and job variable query to use that prefixed form.

The dashboards use `service_name` as the Prometheus and Loki label produced from the OpenTelemetry `service.name` resource attribute. If the first scrape shows a different label shape, update the dashboard selectors after confirming the actual exported labels.

## Startup Troubleshooting

The local stack was verified against the pinned Grafana 13.1.0, Tempo 3.0.0, Loki 3.7.3, Prometheus, and collector images. Keep these compatibility notes in mind when changing config or image digests:

- Docker Desktop port links open the service root path. Grafana and Prometheus have browser UIs there; Tempo and Loki return `404` at `/` because they are API backends. Check `/ready`, the API endpoints, or Grafana Explore instead.
- Tempo 3.0.0 rejects older top-level `ingester` and `compactor` config blocks in this single-binary setup. Retention is configured through `overrides.defaults.compaction.block_retention`.
- The Tempo image does not include `wget`, so the compose file does not define a container healthcheck. Use `curl http://localhost:3200/ready` from the host instead.
- Loki retention requires `compactor.delete_request_store: filesystem`; without it Loki exits during config validation.
- Grafana provisions dashboard files from individual bind mounts rather than a whole dashboard directory. This avoids macOS SMB AppleDouble `._*.json` sidecar files being scanned as dashboards and failing provisioning.

## First Green Verification

1. Start the observability stack:

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml up -d
```

2. Confirm containers are running:

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml ps
```

3. Open Grafana at `http://localhost:3000` and confirm the `RVT Monitors` folder contains all three dashboards.

4. Open Prometheus at `http://localhost:9090/targets` and confirm both targets are up:

   - `prometheus`
   - `otel-collector`

5. Start at least one monitor container with:

   ```text
   OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
   OTEL_EXPORTER_OTLP_PROTOCOL=grpc
   OTEL_SERVICE_NAME=<monitor-service-name>
   ```

6. In Prometheus, check for:

   ```text
   rvt_monitor_job_started_total
   rvt_monitor_job_completed_total
   rvt_monitor_job_failed_total
   rvt_monitor_job_duration_seconds_count
   ```

7. In Grafana Explore:

   - Prometheus: query `rvt_monitor_job_started_total`.
   - Loki: query `{service_name="<monitor-service-name>"} |= "Monitor job"`.
   - Tempo: run a TraceQL query such as `{ resource.service.name = "<monitor-service-name>" }`.

8. Open the dashboards and confirm the selected service shows job metrics, logs, and traces after a monitor job runs.

## Stop The Stack

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml down
```

To remove local telemetry data as well:

```bash
docker compose --project-directory . -f observability/docker-compose.observability.yml down -v
```
