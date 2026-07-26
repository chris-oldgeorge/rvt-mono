#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

common_project="libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
removed_infrastructure_project="libs/rvt-monitor-common/src/Rvt.Monitor.Common."\
"Infrastructure/Rvt.Monitor.Common."\
"Infrastructure.csproj"
communication_abstractions_project="libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj"
communication_project="libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj"
sendgrid_mail_project="libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj"
microsoft_graph_mail_project="libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj"
transmit_sms_project="libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj"
storage_abstractions_project="libs/rvt-monitor-common/src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj"
storage_local_project="libs/rvt-monitor-common/src/Rvt.Storage.Local/Rvt.Storage.Local.csproj"
storage_azure_blob_project="libs/rvt-monitor-common/src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj"
storage_s3_project="libs/rvt-monitor-common/src/Rvt.Storage.S3/Rvt.Storage.S3.csproj"
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

reject_project_reference() {
  local project="$1"
  local target="$2"
  require_file "${project}" || return 0
  if has_project_reference "${project}" "${target}"; then
    fail "${project} must not reference ${target}"
  fi
}

has_package_reference() {
  local project="$1"
  local package="$2"
  grep -Eq "<PackageReference[[:space:]][^>]*Include=\"${package}\"" "${repo_root}/${project}"
}

reject_internal_package_references() {
  local package project
  for package in \
    Rvt.Monitor.Common \
    Rvt.Monitor.Common.Infrastructure \
    Rvt.Communication.Abstractions \
    Rvt.Communication \
    Rvt.Communication.SendGridMail \
    Rvt.Communication.MicrosoftGraphMail \
    Rvt.Communication.TransmitSms \
    Rvt.Monitor.IntegrationTesting \
    Rvt.Storage.Abstractions \
    Rvt.Storage.Local \
    Rvt.Storage.AzureBlob \
    Rvt.Storage.S3; do
    while IFS= read -r -d '' project; do
      if grep -Eq "<PackageReference[[:space:]][^>]*Include=\"${package}\"" "${project}"; then
        fail "${project#"${repo_root}/"} must not reference ${package} as a package"
      fi
    done < <(find \
      "${repo_root}/apps/monitors" \
      "${repo_root}/apps/portal" \
      "${repo_root}/libs/rvt-monitor-common" \
      "${repo_root}/services/reporting" \
      \( -path '*/bin' -o -path '*/obj' \) -prune -o \
      -name '*.csproj' -print0)
  done
}

reject_package_reference() {
  local project="$1"
  local package="$2"
  require_file "${project}" || return 0
  if has_package_reference "${project}" "${package}"; then
    fail "${project} must not reference package ${package}"
  fi
}

if [[ -e "${repo_root}/${removed_infrastructure_project}" ]]; then
  fail "Removed communication infrastructure project still exists: ${removed_infrastructure_project}"
fi

for project in \
  apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj \
  apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj \
  apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj \
  apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj; do
  require_project_reference "${project}" "${common_project}"
  require_project_reference "${project}" "${communication_abstractions_project}"
  require_project_reference "${project}" "${communication_project}"
  require_project_reference "${project}" "${sendgrid_mail_project}"
  require_project_reference "${project}" "${microsoft_graph_mail_project}"
  require_project_reference "${project}" "${transmit_sms_project}"
  reject_project_reference "${project}" "${removed_infrastructure_project}"
done

require_project_reference \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  "${communication_abstractions_project}"
reject_project_reference \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  "${common_project}"
reject_package_reference \
  apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  SendGrid

require_project_reference \
  apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj \
  "${storage_abstractions_project}"
reject_project_reference \
  apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj \
  "${common_project}"
for project in \
  "${storage_local_project}" \
  "${storage_azure_blob_project}" \
  "${storage_s3_project}"; do
  reject_project_reference \
    apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj \
    "${project}"
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

require_project_reference apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj "${communication_abstractions_project}"
require_project_reference apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj "${sendgrid_mail_project}"
reject_project_reference apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj "${removed_infrastructure_project}"

require_project_reference \
  services/reporting/src/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  "${communication_abstractions_project}"
reject_package_reference \
  services/reporting/src/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj \
  SendGrid
require_project_reference \
  services/reporting/src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj \
  "${sendgrid_mail_project}"
reject_internal_package_references

if [[ -e "${repo_root}/libs/rvt-monitor-common/package-validation" ]]; then
  fail "Package-validation consumers must be removed; internal RVT projects are source referenced."
fi

for project in \
  "${common_project}" \
  "${communication_abstractions_project}" \
  "${communication_project}" \
  "${sendgrid_mail_project}" \
  "${microsoft_graph_mail_project}" \
  "${transmit_sms_project}" \
  "${storage_abstractions_project}" \
  "${storage_local_project}" \
  "${storage_azure_blob_project}" \
  "${storage_s3_project}" \
  "${integration_testing_project}"; do
  if ! grep -Eq '<IsPackable>false</IsPackable>' "${repo_root}/${project}"; then
    fail "${project} must declare IsPackable=false"
  fi
done

if grep -Eq 'local-rvt|artifacts/packages|Rvt\.\*' "${repo_root}/libs/rvt-monitor-common/NuGet.config"; then
  fail "libs/rvt-monitor-common/NuGet.config must not contain an internal RVT package feed."
fi

for dockerfile in \
  apps/monitors/airqmonitor/AirQMonitor/Dockerfile \
  apps/monitors/myatmmonitor/MyAtmMonitor/Dockerfile \
  apps/monitors/omnidotsmonitor/OmnidotsMonitor/Dockerfile \
  apps/monitors/svantekmonitor/SvantekMonitor/Dockerfile \
  apps/monitors/reportingmonitor/ReportingMonitor/Dockerfile; do
  if grep -Eq 'nuget_credentials|NuGetPackageSourceCredentials_rvt' "${repo_root}/${dockerfile}"; then
    fail "${dockerfile} must not contain internal package-feed credential plumbing"
  fi
done

compose="${repo_root}/apps/monitors/docker-compose.yml"
if grep -Eq 'nuget_credentials|NuGetPackageSourceCredentials_rvt' "${compose}"; then
  fail "apps/monitors/docker-compose.yml must not contain internal package-feed credential plumbing"
fi
if [[ "$(grep -Ec '^[[:space:]]+context: \.\./\.\.$' "${compose}")" -ne 5 ]] ||
   [[ "$(grep -Ec '^[[:space:]]+dockerfile: apps/monitors/' "${compose}")" -ne 5 ]]; then
  fail "apps/monitors/docker-compose.yml must use the monorepo source context for all five images"
fi

if (( failures > 0 )); then
  printf '%d source-boundary violation(s) found.\n' "${failures}" >&2
  exit 1
fi

printf 'RVT common source boundary verified.\n'
