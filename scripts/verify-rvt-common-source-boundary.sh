#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

common_project="libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
infrastructure_project="libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj"
integration_testing_project="libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj"

failures=0

fail() {
  printf 'FAIL: %s\n' "$*" >&2
  failures=$((failures + 1))
}

require_file() {
  local project="$1"
  if [[ ! -f "${repo_root}/${project}" ]]; then
    fail "Missing project: ${project}"
    return 1
  fi
}

has_project_reference() {
  local project="$1"
  local expected_target="$2"
  local project_dir include resolved

  project_dir="$(cd "$(dirname "${repo_root}/${project}")" && pwd -P)"
  while IFS= read -r include; do
    resolved="$(cd "${project_dir}/$(dirname "${include}")" && pwd -P)/$(basename "${include}")"
    if [[ "${resolved}" == "${repo_root}/${expected_target}" ]]; then
      return 0
    fi
  done < <(sed -nE 's/.*<ProjectReference[[:space:]][^>]*Include="([^"]+)".*/\1/p' "${repo_root}/${project}")

  return 1
}

require_project_reference() {
  local project="$1"
  local target="$2"
  require_file "${project}" || return 0
  if ! has_project_reference "${project}" "${target}"; then
    fail "${project} must reference ${target}"
  fi
}

has_package_reference() {
  local project="$1"
  local package="$2"
  grep -Eq "<PackageReference[[:space:]][^>]*Include=\"${package}\"" "${repo_root}/${project}"
}

reject_active_package_references() {
  local package project
  for package in Rvt.Monitor.Common Rvt.Monitor.Common.Infrastructure Rvt.Monitor.IntegrationTesting; do
    while IFS= read -r -d '' project; do
      if grep -Eq "<PackageReference[[:space:]][^>]*Include=\"${package}\"" "${project}"; then
        fail "${project#"${repo_root}/"} must not reference ${package} as a package"
      fi
    done < <(find "${repo_root}/apps/monitors" "${repo_root}/apps/portal" -name '*.csproj' -print0)
  done
}

require_package_reference() {
  local project="$1"
  local package="$2"
  require_file "${project}" || return 0
  if ! has_package_reference "${project}" "${package}"; then
    fail "${project} must retain PackageReference to ${package}"
  fi
}

reject_package_validation_source_references() {
  local project="$1"
  local target="$2"
  require_file "${project}" || return 0
  if has_project_reference "${project}" "${target}"; then
    fail "${project} must not source-reference ${target}"
  fi
}

for project in \
  apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj \
  apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj \
  apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj; do
  require_project_reference "${project}" "${common_project}"
  require_project_reference "${project}" "${infrastructure_project}"
done

for project in \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj; do
  require_project_reference "${project}" "${common_project}"
done

require_project_reference apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj "${common_project}"
require_project_reference apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj "${integration_testing_project}"

for project in \
  apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj; do
  require_project_reference "${project}" "${integration_testing_project}"
done

require_project_reference apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj "${infrastructure_project}"
reject_active_package_references

runtime_consumer=libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj
test_consumer=libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj

require_package_reference "${runtime_consumer}" Rvt.Monitor.Common
require_package_reference "${runtime_consumer}" Rvt.Monitor.Common.Infrastructure
require_package_reference "${test_consumer}" Rvt.Monitor.IntegrationTesting

for project in "${runtime_consumer}" "${test_consumer}"; do
  for target in "${common_project}" "${infrastructure_project}" "${integration_testing_project}"; do
    reject_package_validation_source_references "${project}" "${target}"
  done
done

if (( failures > 0 )); then
  printf '%d source-boundary violation(s) found.\n' "${failures}" >&2
  exit 1
fi

printf 'RVT common source boundary verified.\n'
