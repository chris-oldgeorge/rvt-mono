#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

required_paths=(
  "apps/monitors"
  "apps/portal"
  "libs/rvt-monitor-common"
  "services/reporting"
  "docs/imports/source-manifest.md"
  "Rvt.Mono.slnx"
)

for required_path in "${required_paths[@]}"; do
  if [[ ! -e "$root_dir/$required_path" ]]; then
    echo "Missing required mono-repository path: $required_path" >&2
    exit 1
  fi
done

nested_git_dirs="$(cd "$root_dir" && find apps libs services -type d -name .git)"
if [[ -n "$nested_git_dirs" ]]; then
  echo "Nested .git metadata is not allowed:" >&2
  echo "$nested_git_dirs" >&2
  exit 1
fi

manifest="$root_dir/docs/imports/source-manifest.md"
pinned_revisions=(
  "5935f40614073afa6c4ef954db1308a72a5f8f2b"
  "8355070f094a591297c9f8468057f44a6c876986"
  "f00d5b8a320945ed08e248da8641ca0c3f7e3b82"
  "e602e8317e35bd94a1eb4dd017759b91713ea111"
)

for revision in "${pinned_revisions[@]}"; do
  if ! grep -Fq "$revision" "$manifest"; then
    echo "Missing pinned source revision in manifest: $revision" >&2
    exit 1
  fi
done
