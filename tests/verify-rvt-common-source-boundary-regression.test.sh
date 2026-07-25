#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_root="${repo_root}/tests/fixtures/rvt-common-source-boundary"
test_root="$(mktemp -d)"
test_root="$(cd -P "${test_root}" && pwd)"
boundary_root=""

cleanup() {
  rm -rf "${test_root}"
  if [[ -n "${boundary_root}" ]]; then
    rm -rf "${boundary_root}"
  fi
}
trap cleanup EXIT

cp -R "${fixture_root}/." "${test_root}"
mkdir -p "${test_root}/scripts"
cp "${repo_root}/scripts/verify-rvt-common-source-boundary.sh" "${test_root}/scripts/"

if "${test_root}/scripts/verify-rvt-common-source-boundary.sh" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject the package-validation fixture.\n' >&2
  exit 1
fi

grep -Fq \
  'libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj must not source-reference libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj' \
  "${test_root}/output"

sed \
  '/ProjectReference/d' \
  "${test_root}/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj" \
  > "${test_root}/RuntimeConsumer.csproj"
mv \
  "${test_root}/RuntimeConsumer.csproj" \
  "${test_root}/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj"
removed_project="libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj"
mkdir -p "${test_root}/$(dirname "${removed_project}")"
touch "${test_root}/${removed_project}"

if "${test_root}/scripts/verify-rvt-common-source-boundary.sh" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject the removed Infrastructure project.\n' >&2
  exit 1
fi

grep -Fq \
  "Removed communication infrastructure project still exists: ${removed_project}" \
  "${test_root}/output"

boundary_root="$(mktemp -d /private/tmp/rvt-common-source-boundary.XXXXXX)"

copy_project() {
  local project="$1"
  mkdir -p "${boundary_root}/$(dirname "${project}")"
  cp "${repo_root}/${project}" "${boundary_root}/${project}"
}

for project in \
  apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj \
  apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj \
  apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj \
  apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj \
  apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  services/reporting/src/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  services/reporting/src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj \
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
  libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj; do
  copy_project "${project}"
done

mkdir -p "${boundary_root}/scripts"
cp "${repo_root}/scripts/verify-rvt-common-source-boundary.sh" "${boundary_root}/scripts/"

if ! "${boundary_root}/scripts/verify-rvt-common-source-boundary.sh" >"${boundary_root}/output" 2>&1; then
  printf 'Expected the guard to accept Reporting Storage with only Rvt.Storage.Abstractions.\n' >&2
  cat "${boundary_root}/output" >&2
  exit 1
fi

storage_project="${boundary_root}/apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"
storage_project_baseline="${storage_project}.baseline"
cp "${storage_project}" "${storage_project_baseline}"

add_storage_provider_reference() {
  local provider_project="$1"
  sed '$d' "${storage_project}" > "${storage_project}.next"
  printf '  <ItemGroup>\n    <ProjectReference Include="../../../../libs/rvt-monitor-common/src/%s/%s.csproj" />\n  </ItemGroup>\n</Project>\n' \
    "${provider_project}" "${provider_project}" >> "${storage_project}.next"
  mv "${storage_project}.next" "${storage_project}"
}

for provider_project in Rvt.Storage.Local Rvt.Storage.AzureBlob Rvt.Storage.S3; do
  cp "${storage_project_baseline}" "${storage_project}"
  add_storage_provider_reference "${provider_project}"

  if "${boundary_root}/scripts/verify-rvt-common-source-boundary.sh" >"${boundary_root}/output" 2>&1; then
    printf 'Expected the guard to reject Reporting Storage reference to %s.\n' "${provider_project}" >&2
    exit 1
  fi

  grep -Fq \
    "apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj must not reference libs/rvt-monitor-common/src/${provider_project}/${provider_project}.csproj" \
    "${boundary_root}/output"
done

cp "${storage_project_baseline}" "${storage_project}"
rm "${storage_project_baseline}"

sed -i.bak \
  's#Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj#Rvt.Monitor.Common/Rvt.Monitor.Common.csproj#' \
  "${boundary_root}/apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"
rm "${boundary_root}/apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj.bak"

if "${boundary_root}/scripts/verify-rvt-common-source-boundary.sh" >"${boundary_root}/output" 2>&1; then
  printf 'Expected the guard to reject a Reporting Storage reference to Rvt.Monitor.Common.\n' >&2
  exit 1
fi

grep -Fq \
  'apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj must not reference libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj' \
  "${boundary_root}/output"
