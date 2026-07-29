#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
verifier="${repo_root}/scripts/verify-shell-conditionals.sh"
fixture_root="$(mktemp -d)"
trap 'rm -rf "$fixture_root"' EXIT

printf '%s\n' \
    '#!/usr/bin/env bash' \
    'if [ "$mode" = "safe" ]; then' \
    '    exit 0' \
    'fi' \
    > "${fixture_root}/unsafe.sh"

if "$verifier" "$fixture_root" >"${fixture_root}/unsafe.out" 2>&1; then
    echo "expected unsafe single-bracket conditional to fail" >&2
    exit 1
fi

grep -F 'unsafe.sh:2' "${fixture_root}/unsafe.out" >/dev/null

fallback_bin="${fixture_root}/bin"
mkdir "$fallback_bin"
ln -s "$(command -v dirname)" "${fallback_bin}/dirname"
ln -s "$(command -v grep)" "${fallback_bin}/grep"

if PATH="$fallback_bin" /bin/bash "$verifier" "$fixture_root" >"${fixture_root}/fallback.out" 2>&1; then
    echo "expected grep fallback to reject unsafe single-bracket conditional" >&2
    exit 1
fi

grep -F 'unsafe.sh:2' "${fixture_root}/fallback.out" >/dev/null

printf '%s\n' \
    '#!/usr/bin/env bash' \
    'if [[ "$mode" == "safe" ]]; then' \
    '    exit 0' \
    'fi' \
    > "${fixture_root}/safe.sh"
rm "${fixture_root}/unsafe.sh"

"$verifier" "$fixture_root"
PATH="$fallback_bin" /bin/bash "$verifier" "$fixture_root"
