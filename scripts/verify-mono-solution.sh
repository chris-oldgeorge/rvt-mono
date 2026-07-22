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
listed_projects="$(printf '%s\n' "$solution_output" | grep -E '\.csproj$' || true)"
listed_count="$(printf '%s\n' "$listed_projects" | grep -c . || true)"
source_count="$(cd "$root_dir" && find "${module_roots[@]}" -name '*.csproj' -print | wc -l | tr -d '[:space:]')"

if [[ "$listed_count" -ne "$source_count" ]]; then
  echo "Solution project count ($listed_count) does not match module project count ($source_count)." >&2
  exit 1
fi

for module_root in "${module_roots[@]}"; do
  if ! printf '%s\n' "$listed_projects" | grep -q "^${module_root}/.*\.csproj$"; then
    echo "Solution has no project from module: $module_root" >&2
    exit 1
  fi
done
