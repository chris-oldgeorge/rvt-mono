#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/export-client-release.sh [export-dir]

Creates a curated client/audit release package from Git-tracked files only.

Default export-dir: /private/tmp/rvt-monitors-client-release
USAGE
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

repo_root="$(git rev-parse --show-toplevel)"
export_dir="${1:-/private/tmp/rvt-monitors-client-release}"
exclusions_file="$repo_root/docs/release/client-release-exclusions.txt"

if [[ ! -f "$exclusions_file" ]]; then
  echo "Missing exclusion policy: $exclusions_file" >&2
  exit 1
fi

case "$export_dir" in
  ""|"/"|"$repo_root"|"$repo_root"/*)
    echo "Refusing unsafe export directory: $export_dir" >&2
    exit 1
    ;;
esac

exclusions=()
while IFS= read -r pattern; do
  if [[ "$pattern" =~ ^[[:space:]]*(#|$) ]]; then
    continue
  fi

  exclusions+=("$pattern")
done < "$exclusions_file"

is_excluded() {
  local path="$1"
  local pattern
  for pattern in "${exclusions[@]}"; do
    if [[ "$path" == $pattern ]]; then
      return 0
    fi
  done
  return 1
}

rm -rf "$export_dir"
mkdir -p "$export_dir"

copied=0
while IFS= read -r path; do
  if is_excluded "$path"; then
    continue
  fi

  mkdir -p "$export_dir/$(dirname "$path")"
  cp -p "$repo_root/$path" "$export_dir/$path"
  copied=$((copied + 1))
done < <(git -C "$repo_root" ls-files)

find "$export_dir" -type f \
  | sed "s#^$export_dir/##" \
  | sort > "$export_dir/RELEASE_MANIFEST.txt"

blocked_output="$(find "$export_dir" -type f \( \
  -name 'AGENTS.md' -o \
  -name 'project_state.md' -o \
  -path '*/rvt-monitor-common/*' -o \
  -path '*/docs/superpowers/*' -o \
  -path '*/docs/database/monitors/monitor-data-access-migration.md' -o \
  -path '*/docs/release/*' -o \
  -path '*/.codegraph/*' -o \
  -iname 'appsettings.Development.json' -o \
  -iname 'appsettings.development.json' -o \
  -iname 'local.settings.json' -o \
  -iname '.env' -o \
  -iname '*.key' -o \
  -iname '*.pem' -o \
  -iname '*.p12' -o \
  -iname '*.pfx' \
\) -print)"

if [[ -n "$blocked_output" ]]; then
  echo "Blocked files were found in the curated export:" >&2
  echo "$blocked_output" >&2
  exit 1
fi

for required in README.md rvt-monitors.sln docker-compose.yml NuGet.config Directory.Packages.props; do
  if [[ ! -f "$export_dir/$required" ]]; then
    echo "Required release file missing: $required" >&2
    exit 1
  fi
done

echo "Created curated export at $export_dir"
echo "Copied $copied tracked files"
echo "Manifest: $export_dir/RELEASE_MANIFEST.txt"
