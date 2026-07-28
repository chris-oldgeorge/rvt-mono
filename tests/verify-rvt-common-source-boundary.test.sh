#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"${repo_root}/scripts/verify-rvt-common-source-boundary.sh"

temp_dir="$(mktemp -d)"
fake_bin="${temp_dir}/bin"
build_call_log="${temp_dir}/build-calls.log"
mkdir -p "${fake_bin}"

cleanup() {
  rm -rf "${temp_dir}"
}
trap cleanup EXIT

cat > "${fake_bin}/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'dotnet %s\n' "$*" >> "${BUILD_CALL_LOG}"
EOF
chmod +x "${fake_bin}/dotnet"

cat > "${fake_bin}/node" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'node %s\n' "$*" >> "${BUILD_CALL_LOG}"
EOF
chmod +x "${fake_bin}/node"

PATH="${fake_bin}:${PATH}" \
  BUILD_CALL_LOG="${build_call_log}" \
  "${repo_root}/scripts/build-mono.sh"

if grep -Eq '(^|[[:space:]])pack([[:space:]]|$)' "${build_call_log}"; then
  printf 'FAIL: build-mono.sh must not pack internal RVT projects.\n' >&2
  exit 1
fi

if grep -Fq 'package-validation' "${build_call_log}"; then
  printf 'FAIL: build-mono.sh must not restore or build package-validation consumers.\n' >&2
  exit 1
fi

expected_calls="${temp_dir}/expected-calls.log"
cat > "${expected_calls}" <<EOF
dotnet restore ${repo_root}/Rvt.Mono.slnx --locked-mode --disable-parallel
node ${repo_root}/scripts/engineering-standards/verify.mjs --working-tree
dotnet build ${repo_root}/Rvt.Mono.slnx --no-restore --nologo -m:1
dotnet test ${repo_root}/Rvt.Mono.slnx --no-build --nologo
EOF

if ! diff -u "${expected_calls}" "${build_call_log}"; then
  printf 'FAIL: build-mono.sh must use the direct project-reference solution sequence.\n' >&2
  exit 1
fi

printf 'RVT direct project-reference build sequencing verified.\n'
