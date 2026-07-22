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

if [[ -f "${dotnet_call_log}" ]] && grep -Fq 'Rvt.Mono.slnx' "${dotnet_call_log}"; then
  printf 'FAIL: aggregate solution restore was attempted before package verification.\n' >&2
  exit 1
fi

printf 'Local RVT package prerequisite sequencing verified.\n'
