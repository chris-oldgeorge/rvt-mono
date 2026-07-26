#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"${repo_root}/scripts/verify-rvt-common-source-boundary.sh"

temp_dir="$(mktemp -d)"
fake_bin="${temp_dir}/bin"
dotnet_call_log="${temp_dir}/dotnet-calls.log"
mkdir -p "${fake_bin}"

cleanup() {
  rm -rf "${temp_dir}"
}
trap cleanup EXIT

cat > "${fake_bin}/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${DOTNET_CALL_LOG}"
EOF
chmod +x "${fake_bin}/dotnet"

PATH="${fake_bin}:${PATH}" \
  DOTNET_CALL_LOG="${dotnet_call_log}" \
  "${repo_root}/scripts/build-mono.sh"

if grep -Eq '(^|[[:space:]])pack([[:space:]]|$)' "${dotnet_call_log}"; then
  printf 'FAIL: build-mono.sh must not pack internal RVT projects.\n' >&2
  exit 1
fi

if grep -Fq 'package-validation' "${dotnet_call_log}"; then
  printf 'FAIL: build-mono.sh must not restore or build package-validation consumers.\n' >&2
  exit 1
fi

expected_calls="${temp_dir}/expected-calls.log"
cat > "${expected_calls}" <<EOF
restore ${repo_root}/Rvt.Mono.slnx --disable-parallel
build ${repo_root}/Rvt.Mono.slnx --no-restore --nologo -m:1
test ${repo_root}/Rvt.Mono.slnx --no-build --nologo
EOF

if ! diff -u "${expected_calls}" "${dotnet_call_log}"; then
  printf 'FAIL: build-mono.sh must use the direct project-reference solution sequence.\n' >&2
  exit 1
fi

printf 'RVT direct project-reference build sequencing verified.\n'
