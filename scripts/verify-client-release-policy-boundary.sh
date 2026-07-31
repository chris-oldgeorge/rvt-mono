#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  printf 'Usage: %s POLICY_PATH RELEASE_ROOT\n' "${0##*/}" >&2
  exit 64
fi

policy_path="$1"
release_root="$2"
release_source_marker="$release_root/RELEASE_SOURCE.json"

if [[ -f "$policy_path" ]]; then
  exit 0
fi

if [[ ! -f "$release_source_marker" ]] ||
  ! grep -Eq '"sourceCommit"[[:space:]]*:[[:space:]]*"[0-9a-f]{40}"' "$release_source_marker" ||
  ! grep -Eq '"sourceCommitTimestamp"[[:space:]]*:[[:space:]]*"[0-9]{4}-[0-9]{2}-[0-9]{2}T' "$release_source_marker"; then
  printf 'FAIL: client-release policy is missing and RELEASE_SOURCE.json does not identify a prepared client release.\n' >&2
  exit 1
fi

printf 'Prepared client release marker identified; skipped source-only Dockerignore correlation.\n'
