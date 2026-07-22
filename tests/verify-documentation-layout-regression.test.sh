#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_root="$repo_root/tests/fixtures/documentation-layout-stale-source-reference"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

mkdir -p "$test_root/docs" "$test_root/scripts"
cp "$repo_root/docs/documentation-move-manifest.md" "$test_root/docs/"
cp "$repo_root/docs/index.md" "$test_root/docs/"
cp "$repo_root/scripts/verify-documentation-layout.sh" "$test_root/scripts/"
cp -R "$fixture_root/." "$test_root/"

stale_document_path="apps/monitors/myatmmonitor/"
stale_document_path+="README.md"
STALE_DOCUMENT_PATH="$stale_document_path" perl -pi -e \
  's/__STALE_DOCUMENT_PATH__/$ENV{STALE_DOCUMENT_PATH}/g' \
  "$test_root/apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs"

stale_module_relative_path="docs/"
stale_module_relative_path+="releasing.md"
STALE_MODULE_RELATIVE_PATH="$stale_module_relative_path" perl -pi -e \
  's/__STALE_MODULE_RELATIVE_DOCUMENT_PATH__/$ENV{STALE_MODULE_RELATIVE_PATH}/g' \
  "$test_root/libs/rvt-monitor-common/scripts/release-documentation.txt"

while IFS=$'\t' read -r source destination; do
  [[ -n "$source" && -n "$destination" ]] || continue
  mkdir -p "$(dirname "$test_root/$destination")"
  touch "$test_root/$destination"
done < <(awk -F '`' '/^\| `/ { print $2 "\t" $4 }' "$test_root/docs/documentation-move-manifest.md")

for retained_path in \
  README.md \
  apps/monitors/README.md \
  apps/monitors/AGENTS.md \
  apps/portal/README.md \
  apps/portal/AGENTS.md \
  libs/rvt-monitor-common/README.md \
  services/reporting/README.md; do
  mkdir -p "$(dirname "$test_root/$retained_path")"
  touch "$test_root/$retained_path"
done

git -C "$test_root" init --quiet
git -C "$test_root" add .

if "$test_root/scripts/verify-documentation-layout.sh" >"$test_root/output" 2>&1; then
  printf 'Expected the guard to reject the stale source-code reference.\n' >&2
  exit 1
fi

grep -Fq \
  "ERROR: stale reference uses old document path: $stale_document_path" \
  "$test_root/output"
grep -Fq \
  "ERROR: stale module-relative reference uses old document path: $stale_module_relative_path" \
  "$test_root/output"
grep -Fq 'ERROR: 2 stale old-document reference(s) remain' "$test_root/output"
