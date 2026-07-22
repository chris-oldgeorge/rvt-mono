#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-testlocal-suite.sh [--dry-run]

Runs the current local testlocal monitor suite against already-running Docker
containers. AirQ requires AIRQ_TESTLOCAL_SERIAL_ID to select one monitor.

Override the local Postgres connection with RVT_TESTLOCAL_CONNECTION_STRING.
USAGE
}

dry_run=false
if [[ "${1:-}" == "--dry-run" ]]; then
  dry_run=true
elif [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
elif [[ $# -gt 0 ]]; then
  usage >&2
  exit 2
fi

connection_string="${RVT_TESTLOCAL_CONNECTION_STRING:-Host=host.docker.internal;Port=5432;Database=rvt;Username=rvt;Password=rvt}"

run_job() {
  local name="$1"
  local container="$2"
  local dll="$3"
  local job="$4"

  printf 'Running %s %s\n' "$name" "$job"
  if [[ "$dry_run" == true ]]; then
    printf '  docker exec -w /app -e testlocal=true -e RVT__MONITOR_JOB=%q ... %s dotnet %s\n' "$job" "$container" "$dll"
    return 0
  fi

  docker exec \
    -w /app \
    -e testlocal=true \
    -e "RVT__MONITOR_JOB=$job" \
    -e RVT__DATABASE_PROVIDER=PostgreSql \
    -e "ConnectionStrings__DefaultConnection=$connection_string" \
    "$container" \
    dotnet "$dll"
}

run_airq_job() {
  local job="$1"
  local serial_id="${AIRQ_TESTLOCAL_SERIAL_ID:?Set AIRQ_TESTLOCAL_SERIAL_ID to one AirQ monitor serial before running the suite.}"

  printf 'Running AirQ %s for serial %s\n' "$job" "$serial_id"
  if [[ "$dry_run" == true ]]; then
    printf '  docker exec -w /app -e testlocal=true -e AirQ__TestLocal__SerialId=%q -e RVT__MONITOR_JOB=%q ... rvt-monitors-airqmonitor-api-1 dotnet AirQMonitor.dll\n' "$serial_id" "$job"
    return 0
  fi

  docker exec \
    -w /app \
    -e testlocal=true \
    -e "AirQ__TestLocal__SerialId=$serial_id" \
    -e "RVT__MONITOR_JOB=$job" \
    -e RVT__DATABASE_PROVIDER=PostgreSql \
    -e "ConnectionStrings__DefaultConnection=$connection_string" \
    "rvt-monitors-airqmonitor-api-1" \
    dotnet AirQMonitor.dll
}

run_airq_job "StoreNoiseLevels"

run_job "MyAtm" "rvt-monitors-myatmmonitor-api-1" "MyAtmMonitor.dll" "StoreMonitors"
run_job "MyAtm" "rvt-monitors-myatmmonitor-api-1" "MyAtmMonitor.dll" "StoreDustLevels"

run_job "Omnidots" "rvt-monitors-omnidotsmonitor-api-1" "OmnidotsMonitor.dll" "StoreMonitors"
run_job "Omnidots" "rvt-monitors-omnidotsmonitor-api-1" "OmnidotsMonitor.dll" "StorePeakRecordsLastDataTime"
run_job "Omnidots" "rvt-monitors-omnidotsmonitor-api-1" "OmnidotsMonitor.dll" "StoreTraces"

run_job "Svantek" "rvt-monitors-svantekmonitor-api-1" "SvantekMonitor.dll" "StoreMonitors"
run_job "Svantek" "rvt-monitors-svantekmonitor-api-1" "SvantekMonitor.dll" "StoreNoiseLevels"
