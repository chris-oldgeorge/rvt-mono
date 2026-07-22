#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_root="${repo_root}/tests/fixtures/rvt-common-source-boundary"
test_root="$(mktemp -d)"
test_root="$(cd -P "${test_root}" && pwd)"
trap 'rm -rf "${test_root}"' EXIT

cp -R "${fixture_root}/." "${test_root}"
mkdir -p "${test_root}/scripts"
cp "${repo_root}/scripts/verify-rvt-common-source-boundary.sh" "${test_root}/scripts/"

if "${test_root}/scripts/verify-rvt-common-source-boundary.sh" >"${test_root}/output" 2>&1; then
  printf 'Expected the guard to reject the package-validation fixture.\n' >&2
  exit 1
fi

grep -Fq \
  'libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj must not source-reference libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj' \
  "${test_root}/output"
