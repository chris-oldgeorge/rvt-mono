#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
test_root="$(cd -P "${test_root}" && pwd)"

cleanup() {
  rm -rf "${test_root}"
}
trap cleanup EXIT

copy_file() {
  local file="$1"
  mkdir -p "${test_root}/$(dirname "${file}")"
  cp "${repo_root}/${file}" "${test_root}/${file}"
}

for file in \
  apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj \
  apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj \
  apps/monitors/airqmonitor/AirQMonitor/Dockerfile \
  apps/monitors/myatmmonitor/MyAtmMonitor/Dockerfile \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/Dockerfile \
  apps/monitors/svantekmonitor/SvantekMonitor/Dockerfile \
  apps/monitors/reportingmonitor/ReportingMonitor/Dockerfile \
  apps/monitors/docker-compose.yml \
  apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj \
  apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj \
  libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj \
  libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj \
  libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj \
  libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj \
  libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj \
  libs/rvt-monitor-common/src/Rvt.Storage.Local/Rvt.Storage.Local.csproj \
  libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj \
  libs/rvt-monitor-common/src/Rvt.Storage.S3/Rvt.Storage.S3.csproj \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  libs/rvt-monitor-common/NuGet.config \
  scripts/verify-rvt-common-source-boundary.sh; do
  copy_file "${file}"
done

guard="${test_root}/scripts/verify-rvt-common-source-boundary.sh"
if ! "${guard}" >"${test_root}/output" 2>&1; then
  printf 'Expected the direct project-reference fixture to pass.\n' >&2
  cat "${test_root}/output" >&2
  exit 1
fi

airq_project="${test_root}/apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj"
cp "${airq_project}" "${airq_project}.baseline"
sed '$d' "${airq_project}" >"${airq_project}.next"
printf '  <ItemGroup><PackageReference Include="Rvt.Monitor.Common" Version="1.0.0" /></ItemGroup>\n</Project>\n' \
  >>"${airq_project}.next"
mv "${airq_project}.next" "${airq_project}"

if "${guard}" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject an internal RVT PackageReference.\n' >&2
  exit 1
fi
grep -Fq 'must not reference Rvt.Monitor.Common as a package' "${test_root}/output"
mv "${airq_project}.baseline" "${airq_project}"

common_project="${test_root}/libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
cp "${common_project}" "${common_project}.baseline"
sed 's#<IsPackable>false</IsPackable>#<IsPackable>true</IsPackable>#' \
  "${common_project}" >"${common_project}.next"
mv "${common_project}.next" "${common_project}"

if "${guard}" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject a packable internal RVT project.\n' >&2
  exit 1
fi
grep -Fq 'Rvt.Monitor.Common.csproj must declare IsPackable=false' "${test_root}/output"
mv "${common_project}.baseline" "${common_project}"

printf '\n# NuGetPackageSourceCredentials_rvt\n' \
  >>"${test_root}/apps/monitors/airqmonitor/AirQMonitor/Dockerfile"

if "${guard}" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject package-feed credentials in monitor container builds.\n' >&2
  exit 1
fi
grep -Fq 'must not contain internal package-feed credential plumbing' "${test_root}/output"

printf 'RVT source-only boundary regression checks verified.\n'
