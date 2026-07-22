#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

test ! -d rvt-monitor-common

while IFS= read -r -d '' file; do
  if grep -EnH '<ProjectReference[^>]+rvt-monitor-common|UseLocalRvtCommon' "$file"; then
    echo "A local RVT Common source reference or switch is present." >&2
    exit 1
  fi
done < <(find . \
  \( -path './.git' -o -path './.worktrees' -o -path '*/bin' -o -path '*/obj' \) -prune -o \
  -type f \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -print0)

is_retired_common_identity() {
  local identity="$1"
  local normalized
  normalized="$(printf '%s' "$identity" | tr '[:upper:]' '[:lower:]')"
  case "$normalized" in
    rvt.monitor.common*|rvt.monitor.integrationtesting*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

while IFS= read -r -d '' project; do
  relative="${project#./}"
  filename="$(basename "$project" .csproj)"
  if is_retired_common_identity "$filename"; then
    printf '%s: project filename uses retired local identity %s.\n' "$relative" "$filename" >&2
    exit 1
  fi

  evaluated_properties="$(dotnet msbuild "$project" \
    -getProperty:AssemblyName \
    -getProperty:PackageId \
    -getProperty:RootNamespace)"
  while IFS='=' read -r property identity; do
    if [[ -n "$identity" ]] && is_retired_common_identity "$identity"; then
      printf '%s: %s uses retired local identity %s.\n' \
        "$relative" "$property" "$identity" >&2
      exit 1
    fi
  done < <(printf '%s\n' "$evaluated_properties" | sed -nE \
    's/^[[:space:]]*"(AssemblyName|PackageId|RootNamespace)": "([^"]*)",?$/\1=\2/p')

  evaluated_references="$(dotnet msbuild "$project" -getItem:ProjectReference)"
  while IFS= read -r identity; do
    if [[ -n "$identity" ]] && is_retired_common_identity "$identity"; then
      printf '%s: ProjectReference resolves to retired local identity %s.\n' \
        "$relative" "$identity" >&2
      exit 1
    fi
  done < <(printf '%s\n' "$evaluated_references" | sed -nE \
    's/^[[:space:]]*"Filename": "([^"]*)",?$/\1/p')
done < <(find . \
  \( -path './.git' -o -path './.worktrees' -o -path '*/bin' -o -path '*/obj' \) -prune -o \
  -type f -name '*.csproj' -print0)

solutions=(
  rvt-monitors.sln
  airqmonitor/airqmonitor.sln
  myatmmonitor/myatmmonitor.sln
  omnidotsmonitor/omnidotsmonitor.sln
  svantekmonitor/svantekmonitor.sln
)

for solution in "${solutions[@]}"; do
  if dotnet sln "$solution" list | grep -Eq 'Rvt\.Monitor\.(Common|IntegrationTesting)'; then
    echo "Retired Common project remains in $solution" >&2
    exit 1
  fi
done

dockerfiles=(
  airqmonitor/AirQMonitor/Dockerfile
  myatmmonitor/MyAtmMonitor/Dockerfile
  omnidotsmonitor/OmnidotsMonitor/Dockerfile
  svantekmonitor/SvantekMonitor/Dockerfile
  reportingmonitor/ReportingMonitor/Dockerfile
)

for file in "${dockerfiles[@]}"; do
  grep -Fq '# syntax=docker/dockerfile:1.10' "$file"
  grep -Fq -- '--mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_rvt' "$file"
  if grep -Eqi 'github_packages_token|read:packages|Password=[^$]' "$file"; then
    echo "Potential credential material in $file" >&2
    exit 1
  fi
done

grep -Fq 'nuget_credentials:' docker-compose.yml
grep -Fq 'environment: NuGetPackageSourceCredentials_rvt' docker-compose.yml
