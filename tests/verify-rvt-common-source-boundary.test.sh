#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"${repo_root}/scripts/verify-rvt-common-source-boundary.sh"

temp_dir="$(mktemp -d)"

fake_bin="${temp_dir}/bin"
empty_feed="${temp_dir}/packages"
replacement_feed="${temp_dir}/replacement-packages"
dotnet_call_log="${temp_dir}/dotnet-calls.log"
validation_package_feed="${repo_root}/libs/rvt-monitor-common/artifacts/packages"
validation_package_feed_backup="${temp_dir}/compatibility-feed-backup"
had_validation_package_feed=0
mkdir -p "${fake_bin}" "${empty_feed}"

if [[ -e "${validation_package_feed}" || -L "${validation_package_feed}" ]]; then
  mv "${validation_package_feed}" "${validation_package_feed_backup}"
  had_validation_package_feed=1
fi

cleanup() {
  rm -rf "${validation_package_feed}"
  if (( had_validation_package_feed )); then
    mv "${validation_package_feed_backup}" "${validation_package_feed}"
  fi
  rm -rf "${temp_dir}"
}
trap cleanup EXIT

cat > "${fake_bin}/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$*" >> "${DOTNET_CALL_LOG}"

if [[ "${FAKE_DOTNET_CREATE_PACKAGES:-0}" == "1" && "${1:-}" == "pack" ]]; then
  project_path="${2}"
  package_output=""
  previous=""
  for argument in "$@"; do
    if [[ "${previous}" == "--output" ]]; then
      package_output="${argument}"
      break
    fi
    previous="${argument}"
  done
  package_id="$(basename "${project_path}" .csproj)"
  mkdir -p "${package_output}"
  touch "${package_output}/${package_id}.0.2.0-rc.1.nupkg"
fi
EOF
chmod +x "${fake_bin}/dotnet"

missing_artifact="${empty_feed}/Rvt.Monitor.Common.0.2.0-rc.1.nupkg"
abstractions_artifact="${empty_feed}/Rvt.Communication.Abstractions.0.2.0-rc.1.nupkg"
communication_artifact="${empty_feed}/Rvt.Communication.0.2.0-rc.1.nupkg"
sendgrid_mail_artifact="${empty_feed}/Rvt.Communication.SendGridMail.0.2.0-rc.1.nupkg"
graph_mail_artifact="${empty_feed}/Rvt.Communication.MicrosoftGraphMail.0.2.0-rc.1.nupkg"
transmit_sms_artifact="${empty_feed}/Rvt.Communication.TransmitSms.0.2.0-rc.1.nupkg"
integration_testing_artifact="${empty_feed}/Rvt.Monitor.IntegrationTesting.0.2.0-rc.1.nupkg"
if output="$(
  PATH="${fake_bin}:${PATH}" \
    DOTNET_CALL_LOG="${dotnet_call_log}" \
    RVT_PACKAGE_FEED_DIR="${empty_feed}" \
    "${repo_root}/scripts/build-mono.sh" 2>&1
)"; then
  printf 'FAIL: build-mono.sh must reject a missing local package artifact.\n' >&2
  exit 1
fi

if [[ "${output}" != *"Missing package artifact: ${missing_artifact}"* ]]; then
  printf 'FAIL: expected missing package diagnostic for %s, got:\n%s\n' \
    "${missing_artifact}" "${output}" >&2
  exit 1
fi

for forbidden_restore in \
  'package-validation/RuntimeConsumer/RuntimeConsumer.csproj' \
  'package-validation/TestConsumer/TestConsumer.csproj' \
  'Rvt.Mono.slnx'; do
  if [[ -f "${dotnet_call_log}" ]] && grep -Fq "${forbidden_restore}" "${dotnet_call_log}"; then
    printf 'FAIL: restore of %s was attempted before all local package artifacts existed.\n' \
      "${forbidden_restore}" >&2
    exit 1
  fi
done

> "${dotnet_call_log}"
FAKE_DOTNET_CREATE_PACKAGES=1 \
  PATH="${fake_bin}:${PATH}" \
  DOTNET_CALL_LOG="${dotnet_call_log}" \
  RVT_PACKAGE_FEED_DIR="${empty_feed}" \
  "${repo_root}/scripts/build-mono.sh"

if [[ ! -f "${abstractions_artifact}" ]]; then
  printf 'FAIL: build-mono.sh must pack %s for Common package validation.\n' \
    "${abstractions_artifact}" >&2
  exit 1
fi

if [[ ! -f "${communication_artifact}" ]]; then
  printf 'FAIL: build-mono.sh must pack %s for communication package validation.\n' \
    "${communication_artifact}" >&2
  exit 1
fi

for package_artifact in \
  "${missing_artifact}" \
  "${abstractions_artifact}" \
  "${communication_artifact}" \
  "${graph_mail_artifact}" \
  "${sendgrid_mail_artifact}" \
  "${transmit_sms_artifact}" \
  "${integration_testing_artifact}"; do
  if [[ ! -f "${package_artifact}" ]]; then
    printf 'FAIL: build-mono.sh must pack the temporary seven-package graph; missing %s.\n' \
      "${package_artifact}" >&2
    exit 1
  fi
done

sendgrid_restore_call="$(grep -F 'restore '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj' "${dotnet_call_log}" | head -n 1)"
sendgrid_pack_call="$(grep -F 'pack '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj' "${dotnet_call_log}" | head -n 1)"
if [[ -z "${sendgrid_restore_call}" || -z "${sendgrid_pack_call}" ]]; then
  printf 'FAIL: build-mono.sh must restore and pack Rvt.Communication.SendGridMail for the temporary six-package graph.\n' >&2
  exit 1
fi

graph_restore_call="$(grep -F 'restore '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj' "${dotnet_call_log}" | head -n 1)"
graph_pack_call="$(grep -F 'pack '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj' "${dotnet_call_log}" | head -n 1)"
if [[ -z "${graph_restore_call}" || -z "${graph_pack_call}" ]]; then
  printf 'FAIL: build-mono.sh must restore and pack Rvt.Communication.MicrosoftGraphMail for the temporary seven-package graph.\n' >&2
  exit 1
fi

transmit_sms_restore_call="$(grep -F 'restore '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj' "${dotnet_call_log}" | head -n 1)"
transmit_sms_pack_call="$(grep -F 'pack '"${repo_root}"'/libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj' "${dotnet_call_log}" | head -n 1)"
if [[ -z "${transmit_sms_restore_call}" || -z "${transmit_sms_pack_call}" ]]; then
  printf 'FAIL: build-mono.sh must restore and pack Rvt.Communication.TransmitSms for the temporary seven-package graph.\n' >&2
  exit 1
fi

removed_infrastructure_identity='Rvt.Monitor.Common.'"Infrastructure"
if grep -Fq "${removed_infrastructure_identity}" "${repo_root}/scripts/build-mono.sh"; then
  printf 'FAIL: build-mono.sh must not retain the removed Infrastructure package.\n' >&2
  exit 1
fi

if [[ ! -L "${validation_package_feed}" ]] || \
  [[ "$(readlink "${validation_package_feed}")" != "${empty_feed}" ]]; then
  printf 'FAIL: build-mono.sh must create the compatibility feed link for the temporary test feed.\n' >&2
  exit 1
fi

rm -rf "${empty_feed}"
FAKE_DOTNET_CREATE_PACKAGES=1 \
  PATH="${fake_bin}:${PATH}" \
  DOTNET_CALL_LOG="${dotnet_call_log}" \
  RVT_PACKAGE_FEED_DIR="${replacement_feed}" \
  "${repo_root}/scripts/build-mono.sh"

if [[ ! -L "${validation_package_feed}" ]] || \
  [[ "$(readlink "${validation_package_feed}")" != "${replacement_feed}" ]]; then
  printf 'FAIL: build-mono.sh must replace a stale compatibility feed link.\n' >&2
  exit 1
fi

for validation_restore in \
  'package-validation/RuntimeConsumer/RuntimeConsumer.csproj' \
  'package-validation/TestConsumer/TestConsumer.csproj' \
  'Rvt.Mono.slnx'; do
  restore_call="$(grep -F "${validation_restore}" "${dotnet_call_log}" | head -n 1)"
  if [[ "${restore_call}" != *'-p:RvtUseArtifactValidationLocks=true'* ]]; then
    printf 'FAIL: restore of %s must use artifact-scoped validation locks, got:\n%s\n' \
      "${validation_restore}" "${restore_call}" >&2
    exit 1
  fi
done

printf 'Local RVT package prerequisite sequencing verified.\n'
