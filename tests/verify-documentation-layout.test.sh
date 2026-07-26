#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
expected_moves=85

output="$("$repo_root/scripts/verify-documentation-layout.sh")"
printf '%s\n' "$output"
grep -Fqx \
  "Documentation layout verification passed ($expected_moves moves, 7 retained entry points)." \
  <<<"$output"
