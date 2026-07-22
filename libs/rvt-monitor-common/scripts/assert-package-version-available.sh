#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: assert-package-version-available.sh VERSION}"
core='(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
prerelease_identifier='(0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)'
semver_regex="^${core}(-${prerelease_identifier}(\.${prerelease_identifier})*)?$"

if [[ ! "$version" =~ $semver_regex ]]; then
  echo "Invalid package version: $version" >&2
  exit 1
fi

for package in \
  Rvt.Monitor.Common \
  Rvt.Monitor.Common.Infrastructure \
  Rvt.Monitor.IntegrationTesting; do
  error_file="$(mktemp)"
  versions=""
  if versions="$(gh api --paginate \
      "/orgs/RVT-Group-LTD/packages/nuget/$package/versions?per_page=100" \
      --jq '.[].name' 2>"$error_file")"; then
    if grep -Fxq "$version" <<<"$versions"; then
      rm -f "$error_file"
      echo "Package $package version $version already exists" >&2
      exit 1
    fi
  elif grep -q 'HTTP 404' "$error_file"; then
    : # A package that has never been published has no conflicting version.
  else
    cat "$error_file" >&2
    rm -f "$error_file"
    exit 1
  fi
  rm -f "$error_file"
done
