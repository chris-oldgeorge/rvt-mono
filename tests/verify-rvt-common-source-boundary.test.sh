#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"${repo_root}/scripts/verify-rvt-common-source-boundary.sh"

temp_dir="$(mktemp -d)"
trap 'rm -rf "${temp_dir}"' EXIT

fake_bin="${temp_dir}/bin"
empty_feed="${temp_dir}/packages"
dotnet_call_log="${temp_dir}/dotnet-calls.log"
mkdir -p "${fake_bin}" "${empty_feed}"

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
