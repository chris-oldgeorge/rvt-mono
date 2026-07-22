#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
module_roots=(
  "apps/monitors"
  "apps/portal"
  "libs/rvt-monitor-common"
  "services/reporting"
)
solution_file="${MONO_SOLUTION_FILE:-$root_dir/Rvt.Mono.slnx}"

solution_output="$(dotnet sln "$solution_file" list)"
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

expected_folders="$(printf '%s\n' \
  '/Apps/' \
  '/Apps/Monitors/' \
  '/Apps/Monitors/Tests/' \
  '/Apps/Portal/' \
  '/Apps/Portal/Tests/' \
  '/Libraries/' \
  '/Libraries/RVT Monitor Common/' \
  '/Libraries/RVT Monitor Common/Tests/' \
  '/Services/' \
  '/Services/Reporting/' \
  '/Services/Reporting/Tests/' | LC_ALL=C sort)"
listed_folders="$(awk '
  /<Folder Name="/ {
    folder = $0
    sub(/^.*<Folder Name="/, "", folder)
    sub(/".*$/, "", folder)
    print folder
  }
' "$solution_file" | LC_ALL=C sort)"

if ! diff -u <(printf '%s\n' "$expected_folders") <(printf '%s\n' "$listed_folders"); then
  echo "Solution folders do not exactly match the approved logical organization." >&2
  exit 1
fi

expected_structure=''
while IFS= read -r project_path; do
  case "$project_path" in
    apps/monitors/*) logical_folder='/Apps/Monitors/' ;;
    apps/portal/*) logical_folder='/Apps/Portal/' ;;
    libs/rvt-monitor-common/*) logical_folder='/Libraries/RVT Monitor Common/' ;;
    services/reporting/*) logical_folder='/Services/Reporting/' ;;
  esac

  if grep -Eq '<IsTestProject>true</IsTestProject>|Include="Microsoft\.NET\.Test\.Sdk"' "$root_dir/$project_path"; then
    logical_folder="${logical_folder}Tests/"
  fi

  expected_structure+="$(printf '%s\t%s\n' "$project_path" "$logical_folder")"$'\n'
done <<< "$discovered_projects"
expected_structure="$(printf '%s' "$expected_structure" | LC_ALL=C sort)"
listed_structure="$(awk '
  /<Folder Name="/ {
    folder = $0
    sub(/^.*<Folder Name="/, "", folder)
    sub(/".*$/, "", folder)
  }
  /<Project Path="/ {
    project = $0
    sub(/^.*<Project Path="/, "", project)
    sub(/".*$/, "", project)
    print project "\t" folder
  }
  /<\/Folder>/ { folder = "" }
' "$solution_file" | LC_ALL=C sort)"

if ! diff -u <(printf '%s\n' "$expected_structure") <(printf '%s\n' "$listed_structure"); then
  echo "Solution projects are not assigned to the approved logical folders." >&2
  exit 1
fi
