#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
module_roots=(
  "apps/monitors"
  "apps/portal"
  "libs/rvt-monitor-common"
  "services/reporting"
)

solution_output="$(dotnet sln "$root_dir/Rvt.Mono.slnx" list)"
listed_projects="$(printf '%s\n' "$solution_output" | grep -E '\.csproj$' | sed -e 's/\r$//' -e 's#^\./##' | LC_ALL=C sort || true)"
listed_count="$(printf '%s\n' "$listed_projects" | grep -c . || true)"
discovered_projects="$(cd "$root_dir" && find "${module_roots[@]}" -name '*.csproj' -print | sed -e 's/\r$//' -e 's#^\./##' | LC_ALL=C sort)"
source_count="$(printf '%s\n' "$discovered_projects" | grep -c . || true)"

if [[ "$listed_count" -ne "$source_count" ]]; then
  echo "Solution project count ($listed_count) does not match module project count ($source_count)." >&2
  exit 1
fi

if ! diff -u <(printf '%s\n' "$discovered_projects") <(printf '%s\n' "$listed_projects"); then
  echo "Solution projects do not exactly match the discovered module projects." >&2
  exit 1
fi

for module_root in "${module_roots[@]}"; do
  if ! printf '%s\n' "$listed_projects" | grep -q "^${module_root}/.*\.csproj$"; then
    echo "Solution has no project from module: $module_root" >&2
    exit 1
  fi
done
