#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scan_root="${1:-$repo_root}"

search_status=0
if command -v rg >/dev/null 2>&1; then
    violations="$(
        rg --line-number \
            --with-filename \
            --glob '*.sh' \
            --glob '!node_modules/**' \
            --glob '!.git/**' \
            --regexp '^[[:space:]]*(if|while|until)[[:space:]]+(test[[:space:]]|\[[[:space:]])' \
            --regexp '^[[:space:]]*(test[[:space:]]|\[[[:space:]])' \
            --regexp '(&&|\|\||;)[[:space:]]*(test[[:space:]]|\[[[:space:]])' \
            "$scan_root"
    )" || search_status=$?
else
    violations="$(
        grep --recursive \
            --line-number \
            --extended-regexp \
            --include='*.sh' \
            --exclude-dir='node_modules' \
            --exclude-dir='.git' \
            '^[[:space:]]*(if|while|until)[[:space:]]+(test[[:space:]]|\[[[:space:]])|^[[:space:]]*(test[[:space:]]|\[[[:space:]])|(&&|\|\||;)[[:space:]]*(test[[:space:]]|\[[[:space:]])' \
            "$scan_root"
    )" || search_status=$?
fi

if [[ "$search_status" -gt 1 ]]; then
    echo "Shell conditional scan failed with status ${search_status}." >&2
    exit "$search_status"
fi

if [[ -n "$violations" ]]; then
    echo "Unsafe shell conditional syntax found; use [[ ... ]] instead of test or [ ... ]." >&2
    printf '%s\n' "$violations" >&2
    exit 1
fi
