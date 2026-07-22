#!/usr/bin/env bash
set -euo pipefail

if (( $# > 1 )); then
  printf 'usage: %s [EXPECTED_RVT_VERSION]\n' "$0" >&2
  exit 2
fi

if (( $# == 1 )); then
  expected_version="$1"
else
  repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
  central_versions=()
  for property in RvtCommonVersion RvtCommonInfrastructureVersion RvtIntegrationTestingVersion; do
    version="$(awk -F '[<>]' -v property="$property" '$2 == property { print $3; exit }' \
      "$repository_root/Directory.Packages.props")"
    if [[ -z "$version" ]]; then
      printf 'Unable to read %s from Directory.Packages.props.\n' "$property" >&2
      exit 1
    fi
    central_versions+=("$version")
  done
  if [[ "${central_versions[0]}" != "${central_versions[1]}" || \
        "${central_versions[0]}" != "${central_versions[2]}" ]]; then
    printf 'Central RVT package versions are not synchronized.\n' >&2
    exit 1
  fi
  expected_version="${central_versions[0]}"
fi

if [[ -z "$expected_version" ]]; then
  printf 'Expected RVT package version must be an exact non-empty value.\n' >&2
  exit 2
fi

images=(
  rvt/airqmonitor:local
  rvt/myatmmonitor:local
  rvt/omnidotsmonitor:local
  rvt/svantekmonitor:local
  rvt/reportingmonitor:local
)

container=""
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/rvt-package-inventory.XXXXXX")"

cleanup() {
  if [[ -n "$container" ]]; then
    docker rm -f "$container" >/dev/null 2>&1 || true
  fi

  rm -rf "$temporary_directory"
}
trap cleanup EXIT

package_version() {
  local package="$1"
  local dependency_file="$2"

  awk -v package="$package" '
    {
      marker = package "/"
      marker_start = index($0, marker)
      if (marker_start > 0) {
        version_start = marker_start + length(marker)
        remainder = substr($0, version_start)
        quote = index(remainder, "\"")
        if (quote > 1) {
          print substr(remainder, 1, quote - 1)
          exit
        }
      }
    }
  ' "$dependency_file"
}

for image in "${images[@]}"; do
  container="$(docker create "$image")"
  dependency_file="$temporary_directory/${image//[\/:]/_}.deps.json"
  entrypoint="$(docker image inspect "$image" --format '{{json .Config.Entrypoint}}')"
  app="$(printf '%s\n' "$entrypoint" | sed -nE 's/.*"([^"]+)\.dll".*/\1/p')"

  if [[ -z "$app" ]]; then
    printf 'Unable to determine the application assembly for %s.\n' "$image" >&2
    exit 1
  fi

  docker cp "$container:/app/$app.deps.json" "$dependency_file"
  docker rm "$container" >/dev/null
  container=""

  if grep -Fq 'Rvt.Monitor.IntegrationTesting/' "$dependency_file"; then
    printf 'Test-only Rvt.Monitor.IntegrationTesting is present in %s.\n' "$image" >&2
    exit 1
  fi

  common="$(package_version 'Rvt.Monitor.Common' "$dependency_file")"
  infrastructure="$(package_version 'Rvt.Monitor.Common.Infrastructure' "$dependency_file")"

  if [[ -z "$common" || -z "$infrastructure" ]]; then
    printf 'Required RVT runtime packages are missing from %s.\n' "$image" >&2
    exit 1
  fi

  if [[ "$common" != "$infrastructure" ]]; then
    printf 'RVT runtime package versions are not synchronized in %s.\n' "$image" >&2
    exit 1
  fi

  if [[ "$common" != "$expected_version" || "$infrastructure" != "$expected_version" ]]; then
    printf 'RVT runtime packages in %s resolve to %s/%s, expected exact central version %s.\n' \
      "$image" "$common" "$infrastructure" "$expected_version" >&2
    exit 1
  fi

  printf '%s\t%s\t%s\n' "$image" "$common" "$infrastructure"
done
