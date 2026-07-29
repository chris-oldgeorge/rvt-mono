#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scan_root="${1:-$repo_root}"

violations="$(
    rg --line-number \
        --with-filename \
        --glob '*.sh' \
        --glob '!node_modules/**' \
        --glob '!.git/**' \
        --regexp '^[[:space:]]*(if|while|until)[[:space:]]+(test[[:space:]]|\[[[:space:]])' \
        --regexp '^[[:space:]]*(test[[:space:]]|\[[[:space:]])' \
        --regexp '(&&|\|\||;)[[:space:]]*(test[[:space:]]|\[[[:space:]])' \
        "$scan_root" \
        || true
)"

if [[ -n "$violations" ]]; then
    echo "Unsafe shell conditional syntax found; use [[ ... ]] instead of test or [ ... ]." >&2
    printf '%s\n' "$violations" >&2
    exit 1
fi
