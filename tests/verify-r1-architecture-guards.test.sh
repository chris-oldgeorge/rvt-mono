#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-architecture-guards.XXXXXX")"
temp_root="$(cd "${temp_root}" && pwd -P)"
mutation_root="${temp_root}/worktree"
test_output="${temp_root}/test-output.log"

cleanup() {
  local status=$?
  if git -C "${repo_root}" worktree list --porcelain |
      grep -Fqx "worktree ${mutation_root}"; then
    git -C "${repo_root}" worktree remove --force "${mutation_root}" >/dev/null
  fi
  rm -rf "${temp_root}"
  exit "${status}"
}
trap cleanup EXIT

git -C "${repo_root}" worktree add --detach "${mutation_root}" HEAD >/dev/null

test_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"
mapperly_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"
consumer_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"
mapperly_filter='FullyQualifiedName=MyAtmMonitorTests.Architecture.MyAtmDependencyBoundaryTests.MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy'
source_filter='FullyQualifiedName=MyAtmMonitorTests.Architecture.CommonPackageBoundaryTests.ActiveConsumers_MatchApprovedRvtSourceReferenceMatrix'
baseline_filter="${mapperly_filter}|${source_filter}"

dotnet restore \
  "${test_project}" \
  --locked-mode \
  --disable-parallel \
  --nologo
dotnet build \
  "${test_project}" \
  --no-restore \
  --nologo \
  -m:1
dotnet test \
  "${test_project}" \
  --no-build \
  --no-restore \
  --nologo \
  -m:1 \
  --filter "${baseline_filter}"

assert_mutation_rejected() {
  local label="$1"
  local filter="$2"
  local expected_diagnostic="$3"
  local status

  set +e
  dotnet test \
    "${test_project}" \
    --no-build \
    --no-restore \
    --nologo \
    -m:1 \
    --filter "${filter}" >"${test_output}" 2>&1
  status=$?
  set -e

  if (( status == 0 )); then
    printf 'FAIL: %s mutation was accepted.\n' "${label}" >&2
    cat "${test_output}" >&2
    exit 1
  fi

  if ! grep -Fq "${expected_diagnostic}" "${test_output}"; then
    printf 'FAIL: %s failed without the expected architecture diagnostic.\n' \
      "${label}" >&2
    cat "${test_output}" >&2
    exit 1
  fi

  printf 'Rejected %s mutation.\n' "${label}"
}

cp "${mapperly_project}" "${mapperly_project}.baseline"
ruby - "${mapperly_project}" <<'RUBY'
path = ARGV.fetch(0)
source = File.read(path, encoding: "utf-8")
closing = "</Project>"
mutation = <<~XML
    <ItemGroup>
      <PackageReference Include="Riok.Mapperly" PrivateAssets="all" OutputItemType="Analyzer" />
    </ItemGroup>
  </Project>
XML
abort "Mapperly mutation anchor not found in #{path}" unless source.include?(closing)
File.write(path, source.sub(closing, mutation), mode: "w", encoding: "utf-8")
RUBY

assert_mutation_rejected \
  "Mapperly test-project shape" \
  "${mapperly_filter}" \
  "Mapperly is restricted to direct, non-test monitor application projects."
mv "${mapperly_project}.baseline" "${mapperly_project}"

cp "${consumer_project}" "${consumer_project}.baseline"
ruby - "${consumer_project}" <<'RUBY'
path = ARGV.fetch(0)
source = File.read(path, encoding: "utf-8")
closing = "</Project>"
mutation = <<~XML
    <ItemGroup>
      <PackageReference Include="Rvt.Monitor.Common" />
    </ItemGroup>
  </Project>
XML
abort "source-dependency mutation anchor not found in #{path}" unless source.include?(closing)
File.write(path, source.sub(closing, mutation), mode: "w", encoding: "utf-8")
RUBY

assert_mutation_rejected \
  "forbidden internal package dependency" \
  "${source_filter}" \
  "active consumer must not PackageReference Rvt.Monitor.Common."
mv "${consumer_project}.baseline" "${consumer_project}"

dotnet test \
  "${test_project}" \
  --no-build \
  --no-restore \
  --nologo \
  -m:1 \
  --filter "${baseline_filter}"

printf 'R1 architecture guard mutations rejected and baseline restored.\n'
