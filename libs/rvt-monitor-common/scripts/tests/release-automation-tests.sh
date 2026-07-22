#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
preflight="$repository_root/scripts/assert-package-version-available.sh"
builder="$repository_root/scripts/build-release-artifacts.sh"
tool_manifest="$repository_root/.config/dotnet-tools.json"
workflow="$repository_root/.github/workflows/release.yml"
ci_workflow="$repository_root/.github/workflows/ci.yml"
release_documentation="$repository_root/../../docs/release/rvt-monitor-common/releasing.md"
temporary_root="$(mktemp -d)"
runtime_lock="$repository_root/package-validation/RuntimeConsumer/packages.lock.json"
test_lock="$repository_root/package-validation/TestConsumer/packages.lock.json"
artifacts="$repository_root/artifacts"
artifacts_existed=false
production_obj_dirs=(
  "$repository_root/src/Rvt.Monitor.Common/obj"
  "$repository_root/src/Rvt.Monitor.Common.Infrastructure/obj"
  "$repository_root/testing/Rvt.Monitor.IntegrationTesting/obj"
)
production_obj_backup="$temporary_root/production-obj"

cp "$runtime_lock" "$temporary_root/runtime.packages.lock.json"
cp "$test_lock" "$temporary_root/test.packages.lock.json"
if [[ -d "$artifacts" ]]; then
  artifacts_existed=true
  mkdir -p "$temporary_root/artifacts"
  cp -R "$artifacts/." "$temporary_root/artifacts/"
fi
mkdir -p "$production_obj_backup"
for index in "${!production_obj_dirs[@]}"; do
  obj_dir="${production_obj_dirs[$index]}"
  if [[ -d "$obj_dir" ]]; then
    mkdir -p "$production_obj_backup/$index"
    cp -R "$obj_dir/." "$production_obj_backup/$index/"
    touch "$production_obj_backup/$index.existed"
  fi
done

restore_production_obj() {
  for index in "${!production_obj_dirs[@]}"; do
    obj_dir="${production_obj_dirs[$index]}"
    rm -rf "$obj_dir"
    if [[ -f "$production_obj_backup/$index.existed" ]]; then
      mkdir -p "$obj_dir"
      cp -R "$production_obj_backup/$index/." "$obj_dir/"
    fi
  done
}

assert_production_obj_restored() {
  for index in "${!production_obj_dirs[@]}"; do
    obj_dir="${production_obj_dirs[$index]}"
    if [[ -f "$production_obj_backup/$index.existed" ]]; then
      diff -qr "$production_obj_backup/$index" "$obj_dir" >/dev/null \
        || fail "production obj tree was not restored: $obj_dir"
    elif [[ -e "$obj_dir" ]]; then
      fail "previously absent production obj tree was created: $obj_dir"
    fi
  done
}

cleanup() {
  cp "$temporary_root/runtime.packages.lock.json" "$runtime_lock"
  cp "$temporary_root/test.packages.lock.json" "$test_lock"
  restore_production_obj
  rm -rf "$artifacts"
  if [[ "$artifacts_existed" == "true" ]]; then
    mkdir -p "$artifacts"
    cp -R "$temporary_root/artifacts/." "$artifacts/"
  fi
  rm -rf "$temporary_root"
}
trap cleanup EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

assert_succeeds() {
  local description="$1"
  shift
  if ! "$@" >"$temporary_root/output" 2>&1; then
    cat "$temporary_root/output" >&2
    fail "$description"
  fi
}

assert_fails() {
  local description="$1"
  shift
  if "$@" >"$temporary_root/output" 2>&1; then
    fail "$description"
  fi
}

assert_output_contains() {
  local expected="$1"
  grep -Fq "$expected" "$temporary_root/output" || {
    cat "$temporary_root/output" >&2
    fail "output did not contain: $expected"
  }
}

[[ -x "$preflight" ]] || fail "immutable-version preflight is missing or not executable"
[[ -x "$builder" ]] || fail "release artifact builder is missing or not executable"
[[ -f "$tool_manifest" ]] || fail "local tool manifest is missing"
grep -Fq '"version": "4.1.5"' "$tool_manifest" || fail "SBOM tool is not pinned to 4.1.5"
[[ -f "$workflow" ]] || fail "release workflow is missing"
[[ -f "$ci_workflow" ]] || fail "CI workflow is missing"
[[ -f "$release_documentation" ]] || fail "release documentation is missing"

grep -Fq 'actions/checkout@v6' "$workflow" || fail "checkout action major is incorrect"
grep -Fq 'actions/setup-dotnet@v5' "$workflow" || fail "setup-dotnet action major is incorrect"
grep -Fq '8.0.x' "$workflow" || fail "release runner does not install the SBOM tool runtime"
grep -Fq '10.0.x' "$workflow" || fail "release runner does not install the build SDK"
grep -Fq 'actions/upload-artifact@v6' "$workflow" || fail "upload-artifact action major is incorrect"
grep -Fq 'concurrency:' "$workflow" || fail "release concurrency is missing"
grep -Fq 'cancel-in-progress: false' "$workflow" || fail "same-version releases must serialize"
grep -Fq "github.event_name == 'workflow_dispatch'" "$workflow" || fail "concurrency does not distinguish manual releases"
grep -Fq 'inputs.package_version' "$workflow" || fail "manual release concurrency omits the version"
grep -Fq 'github.ref_name' "$workflow" || fail "tag release concurrency omits the tag"
[[ "$(grep -Fc 'artifacts/release/assets/*' "$workflow")" == "2" ]] || fail "workflow does not consume the exact flat assets twice"
if grep -Fq 'artifacts/release/**' "$workflow"; then
  fail "workflow still uploads a recursive release tree"
fi
grep -Fq 'refs/heads/main' "$workflow" || fail "manual release is not restricted to main"
if grep -Fq -- '--skip-duplicate' "$workflow"; then
  fail "release workflow weakens immutability with --skip-duplicate"
fi
if grep -Fq 'secrets.' "$workflow"; then
  fail "release workflow must not depend on repository secrets"
fi
if grep -Fq -- '--allow-roll-forward' "$builder"; then
  fail "SBOM generation must not roll a net8 tool onto net10"
fi
grep -Fq 'SBOM_DOTNET' "$builder" || fail "SBOM runtime override is missing"
grep -Fq 'SBOM_DOTNET' "$release_documentation" || fail "SBOM runtime override is undocumented"
grep -Fq 'dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion="$version"' "$builder" \
  || fail "solution restore does not receive PackageVersion"
grep -Fq 'dotnet clean rvt-common.sln -c Release' "$builder" || fail "Release output is not cleaned before the versioned build"
grep -Fq 'dotnet build rvt-common.sln -c Release --no-restore --nologo -p:PackageVersion="$version"' "$builder" \
  || fail "Release build does not receive PackageVersion"
grep -Fq 'sbom_component_root="$(mktemp -d)"' "$builder" || fail "isolated SBOM component root is missing"
grep -Fq 'rm -rf "$sbom_component_root"' "$builder" || fail "isolated SBOM component root is not cleaned"
grep -Fq -- '-bc "$sbom_component_root"' "$builder" || fail "SBOM generation does not use the isolated component root"
if grep -Fq -- '-bc .' "$builder"; then
  fail "SBOM generation still scans the repository worktree"
fi
grep -Fq 'tool run sbom-tool -- validate' "$builder" || fail "official SBOM validation is missing"
grep -Fq -- '-mi SPDX:2.2' "$builder" || fail "official SBOM validation does not select SPDX 2.2"
grep -Fq 'dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion="$RVT_PACKAGE_VERSION"' "$ci_workflow" \
  || fail "CI restore does not receive the requested package version"
grep -Fq 'dotnet build rvt-common.sln -c Release --no-restore --nologo -p:PackageVersion="$RVT_PACKAGE_VERSION"' "$ci_workflow" \
  || fail "CI build does not compile the requested package version"

for requirement in \
  'protected tag' \
  'workflow_dispatch' \
  'main/default branch' \
  'exact version' \
  'designated RVT deployment operator' \
  'container startup' \
  'forward merge' \
  'credential revocation' \
  'already-published prior version'; do
  grep -Fiq "$requirement" "$release_documentation" || fail "release documentation omits: $requirement"
done

version_step="$temporary_root/resolve-and-validate-version.sh"
ruby -ryaml -e '
  workflow = YAML.load_file(ARGV.fetch(0))
  step = workflow.fetch("jobs").fetch("release").fetch("steps")
    .find { |candidate| candidate["name"] == "Resolve and validate version" }
  abort "version step not found" unless step
  print step.fetch("run")
' "$workflow" >"$version_step"

assert_fails "manual release dispatch from a feature branch must fail" \
  env GITHUB_REF_TYPE=branch GITHUB_REF_NAME=feature GITHUB_REF=refs/heads/feature \
  REQUESTED_PACKAGE_VERSION=1.2.3-rc.1 GITHUB_ENV="$temporary_root/github.env" \
  bash "$version_step"
assert_succeeds "manual release dispatch from main must accept a prerelease" \
  env GITHUB_REF_TYPE=branch GITHUB_REF_NAME=main GITHUB_REF=refs/heads/main \
  REQUESTED_PACKAGE_VERSION=1.2.3-rc.1 GITHUB_ENV="$temporary_root/github.env" \
  bash "$version_step"
assert_fails "manual release dispatch must reject a stable version" \
  env GITHUB_REF_TYPE=branch GITHUB_REF_NAME=main GITHUB_REF=refs/heads/main \
  REQUESTED_PACKAGE_VERSION=1.2.3 GITHUB_ENV="$temporary_root/github.env" \
  bash "$version_step"
assert_succeeds "stable tags must accept strict stable SemVer" \
  env GITHUB_REF_TYPE=tag GITHUB_REF_NAME=v1.2.3 GITHUB_REF=refs/tags/v1.2.3 \
  REQUESTED_PACKAGE_VERSION= GITHUB_ENV="$temporary_root/github.env" \
  bash "$version_step"
assert_fails "stable tags must reject prerelease versions" \
  env GITHUB_REF_TYPE=tag GITHUB_REF_NAME=v1.2.3-rc.1 GITHUB_REF=refs/tags/v1.2.3-rc.1 \
  REQUESTED_PACKAGE_VERSION= GITHUB_ENV="$temporary_root/github.env" \
  bash "$version_step"

fake_gh_dir="$temporary_root/fake-gh"
mkdir -p "$fake_gh_dir"
cat >"$fake_gh_dir/gh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$*" >>"$GH_FAKE_LOG"
case " $* " in
  *" --paginate "*) ;;
  *)
    echo "pagination was not requested" >&2
    exit 91
    ;;
esac

case "$GH_FAKE_MODE" in
  existing)
    printf '%s\n' "0.1.0" "$GH_FAKE_TARGET_VERSION"
    ;;
  absent)
    printf '%s\n' "0.1.0" "0.1.1"
    ;;
  package-404)
    echo "gh: package not found (HTTP 404)" >&2
    exit 1
    ;;
  api-error)
    echo "gh: service unavailable (HTTP 503)" >&2
    exit 1
    ;;
  *)
    echo "unknown fake mode" >&2
    exit 92
    ;;
esac
EOF
chmod +x "$fake_gh_dir/gh"

gh_log="$temporary_root/gh.log"
: >"$gh_log"

assert_fails "invalid versions must fail before GitHub is queried" \
  env PATH="$fake_gh_dir:$PATH" GH_FAKE_LOG="$gh_log" GH_FAKE_MODE=absent \
  GH_FAKE_TARGET_VERSION=unused "$preflight" "01.2.3-rc.1"
assert_output_contains "Invalid package version"
[[ ! -s "$gh_log" ]] || fail "invalid version reached gh"

: >"$gh_log"
assert_fails "an existing version on a paginated response must fail" \
  env PATH="$fake_gh_dir:$PATH" GH_FAKE_LOG="$gh_log" GH_FAKE_MODE=existing \
  GH_FAKE_TARGET_VERSION=1.2.3-rc.1 "$preflight" "1.2.3-rc.1"
assert_output_contains "already exists"

: >"$gh_log"
assert_succeeds "an absent version must be available" \
  env PATH="$fake_gh_dir:$PATH" GH_FAKE_LOG="$gh_log" GH_FAKE_MODE=absent \
  GH_FAKE_TARGET_VERSION=1.2.3-rc.1 "$preflight" "1.2.3-rc.1"
[[ "$(wc -l <"$gh_log" | tr -d ' ')" == "3" ]] || fail "all three packages were not queried"

: >"$gh_log"
assert_succeeds "an actual package 404 must be treated as available" \
  env PATH="$fake_gh_dir:$PATH" GH_FAKE_LOG="$gh_log" GH_FAKE_MODE=package-404 \
  GH_FAKE_TARGET_VERSION=1.2.3-rc.1 "$preflight" "1.2.3-rc.1"

: >"$gh_log"
assert_fails "non-404 API errors must fail closed" \
  env PATH="$fake_gh_dir:$PATH" GH_FAKE_LOG="$gh_log" GH_FAKE_MODE=api-error \
  GH_FAKE_TARGET_VERSION=1.2.3-rc.1 "$preflight" "1.2.3-rc.1"
assert_output_contains "HTTP 503"

fake_tool_dir="$temporary_root/fake-tools"
mkdir -p "$fake_tool_dir"
cat >"$fake_tool_dir/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

printf 'dotnet %s\n' "$*" >>"$FAKE_TOOL_LOG"
if [[ "${1:-}" == "restore" && "${2:-}" == "rvt-common.sln" ]]; then
  version=""
  for argument in "$@"; do
    case "$argument" in
      -p:PackageVersion=*) version="${argument#-p:PackageVersion=}" ;;
    esac
  done
  assets_version="${FAKE_ASSETS_VERSION:-$version}"
  for project_dir in \
    src/Rvt.Monitor.Common \
    src/Rvt.Monitor.Common.Infrastructure \
    testing/Rvt.Monitor.IntegrationTesting; do
    mkdir -p "$project_dir/obj"
    python3 - "$project_dir/obj/project.assets.json" "$assets_version" <<'PY'
import json
import sys

path, version = sys.argv[1:]
with open(path, "w", encoding="utf-8") as assets_file:
    json.dump({"project": {"version": version}}, assets_file)
PY
  done
elif [[ "${1:-}" == "pack" ]]; then
  project="${2:?}"
  version=""
  for argument in "$@"; do
    case "$argument" in
      -p:PackageVersion=*) version="${argument#-p:PackageVersion=}" ;;
    esac
  done
  case "$project" in
    src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj) package=Rvt.Monitor.Common ;;
    src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj) package=Rvt.Monitor.Common.Infrastructure ;;
    testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj) package=Rvt.Monitor.IntegrationTesting ;;
    *) exit 93 ;;
  esac
  touch "artifacts/packages/$package.$version.nupkg"
  touch "artifacts/packages/$package.$version.snupkg"
  project_dir="${project%/*}"
  mkdir -p "$project_dir/obj/Release"
  touch "$project_dir/obj/Release/$package.$version.nuspec"
elif [[ "${1:-}" == "restore" && "${2:-}" == "package-validation/RuntimeConsumer/RuntimeConsumer.csproj" ]]; then
  printf 'runtime lock changed by restore\n' >package-validation/RuntimeConsumer/packages.lock.json
elif [[ "${1:-}" == "restore" && "${2:-}" == "package-validation/TestConsumer/TestConsumer.csproj" ]]; then
  printf 'test lock changed by restore\n' >package-validation/TestConsumer/packages.lock.json
elif [[ "${1:-}" == "test" && "${2:-}" == "package-validation/TestConsumer/TestConsumer.csproj" && "${FAKE_DOTNET_FAIL_TEST_CONSUMER:-false}" == "true" ]]; then
  exit 42
elif [[ "${1:-}" == "tool" && "${2:-}" == "run" && "${3:-}" == "sbom-tool" && "${5:-}" == "generate" ]]; then
  base=""
  component_root=""
  version=""
  while (($#)); do
    if [[ "$1" == "-b" ]]; then
      base="${2:?}"
      shift 2
    elif [[ "$1" == "-bc" ]]; then
      component_root="${2:?}"
      shift 2
    elif [[ "$1" == "-pv" ]]; then
      version="${2:?}"
      shift 2
    else
      shift
    fi
  done
  [[ -n "$component_root" && "$component_root" != "." ]]
  [[ -f "$component_root/Directory.Build.props" ]]
  [[ -f "$component_root/Directory.Build.targets" ]]
  [[ -f "$component_root/Directory.Packages.props" ]]
  [[ "$(find "$component_root" -type f -name '*.csproj' | wc -l | tr -d ' ')" == "3" ]]
  [[ "$(find "$component_root" -type f -name 'packages.lock.json' | wc -l | tr -d ' ')" == "3" ]]
  [[ "$(find "$component_root" -type f -name 'project.assets.json' | wc -l | tr -d ' ')" == "3" ]]
  [[ "$(find "$component_root" -type f -name '*.nuspec' | wc -l | tr -d ' ')" == "3" ]]
  if find "$component_root" -type f | grep -Eq '/(tests|package-validation)/'; then
    exit 94
  fi
  mkdir -p "$base/_manifest/spdx_2.2"
  if [[ "${FAKE_SBOM_ROOT_ONLY:-false}" == "true" ]]; then
    printf '{"spdxVersion":"SPDX-2.2","documentDescribes":["SPDXRef-RootPackage"],"packages":[{"SPDXID":"SPDXRef-RootPackage","name":"rvt-common","versionInfo":"%s"}],"files":[]}\n' "$version" \
      >"$base/_manifest/spdx_2.2/manifest.spdx.json"
  else
    python3 - \
      "$base/_manifest/spdx_2.2/manifest.spdx.json" \
      "$version" \
      "${FAKE_SBOM_STALE_RVT:-false}" \
      "${FAKE_SBOM_MUTATION:-none}" \
      "$component_root" <<'PY'
import json
import sys
from pathlib import Path

path, version, stale, mutation, component_root = sys.argv[1:]
package_ids = [
    "Rvt.Monitor.Common",
    "Rvt.Monitor.Common.Infrastructure",
    "Rvt.Monitor.IntegrationTesting",
]
dependency_pairs = set()
for lock_path in Path(component_root).rglob("packages.lock.json"):
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    for framework_dependencies in lock.get("dependencies", {}).values():
        for name, details in framework_dependencies.items():
            resolved = details.get("resolved")
            if resolved is not None:
                dependency_pairs.add((name, resolved))

files = [
    {"SPDXID": f"SPDXRef-File-{index}", "fileName": f"./{name}.{version}.{extension}"}
    for index, (name, extension) in enumerate(
        (name, extension)
        for name in package_ids
        for extension in ("nupkg", "snupkg")
    )
]
root = {
    "SPDXID": "SPDXRef-RootPackage",
    "name": "rvt-common",
    "versionInfo": version,
    "hasFiles": [file["SPDXID"] for file in files],
}
dependencies = [
    {
        "SPDXID": f"SPDXRef-Dependency-{index}",
        "name": name,
        "versionInfo": resolved,
    }
    for index, (name, resolved) in enumerate(sorted(dependency_pairs))
]
packages = [root, *dependencies]
packages.extend(
    {"SPDXID": f"SPDXRef-{index}", "name": name, "versionInfo": version}
    for index, name in enumerate(package_ids)
)
if stale == "true":
    packages.append({"SPDXID": "SPDXRef-Stale", "name": package_ids[0], "versionInfo": "0.2.0-ci.5"})
if mutation == "extra-dependency":
    packages.append({"SPDXID": "SPDXRef-Dummy", "name": "Dummy.External", "versionInfo": "1.0.0"})
elif mutation == "missing-dependency":
    packages.remove(dependencies[0])
elif mutation == "wrong-dependency-version":
    dependencies[0]["versionInfo"] = "999.0.0"
elif mutation == "duplicate-dependency":
    duplicate = dict(dependencies[0])
    duplicate["SPDXID"] = "SPDXRef-Duplicate"
    packages.append(duplicate)
elif mutation == "wrong-root-link":
    root["hasFiles"] = root["hasFiles"][:-1]
with open(path, "w", encoding="utf-8") as manifest_file:
    json.dump(
        {
            "spdxVersion": "SPDX-2.2",
            "documentDescribes": ["SPDXRef-RootPackage"],
            "packages": packages,
            "files": files,
        },
        manifest_file,
    )
PY
  fi
  printf 'fake tool manifest checksum\n' >"$base/_manifest/spdx_2.2/manifest.spdx.json.sha256"
elif [[ "${1:-}" == "tool" && "${2:-}" == "run" && "${3:-}" == "sbom-tool" && "${5:-}" == "validate" ]]; then
  [[ "${FAKE_SBOM_VALIDATE_FAIL:-false}" != "true" ]] || exit 43
  output=""
  while (($#)); do
    if [[ "$1" == "-o" ]]; then
      output="${2:?}"
      shift 2
    else
      shift
    fi
  done
  printf '{"result":"success"}\n' >"$output"
fi
EOF
chmod +x "$fake_tool_dir/dotnet"

cat >"$fake_tool_dir/tar" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
[[ "${1:-}" == "-czf" ]]
touch "${2:?}"
EOF
chmod +x "$fake_tool_dir/tar"

assert_fails "unsafe release versions must be rejected before build tools run" \
  env PATH="$fake_tool_dir:$PATH" FAKE_TOOL_LOG="$temporary_root/tools.log" \
  "$builder" "1.2.3-01"
assert_output_contains "Invalid package version"
[[ ! -e "$temporary_root/tools.log" ]] || fail "invalid version reached build tools"

run_fake_build() {
  local version="$1"
  local fail_test_consumer="$2"
  local root_only_sbom="${3:-false}"
  local stale_rvt_sbom="${4:-false}"
  local fail_sbom_validation="${5:-false}"
  local assets_version="${6:-}"
  local sbom_mutation="${7:-none}"
  (
    cd "$repository_root"
    env PATH="$fake_tool_dir:$PATH" FAKE_TOOL_LOG="$temporary_root/tools.log" \
      FAKE_DOTNET_FAIL_TEST_CONSUMER="$fail_test_consumer" \
      FAKE_SBOM_ROOT_ONLY="$root_only_sbom" \
      FAKE_SBOM_STALE_RVT="$stale_rvt_sbom" \
      FAKE_SBOM_VALIDATE_FAIL="$fail_sbom_validation" \
      FAKE_ASSETS_VERSION="$assets_version" \
      FAKE_SBOM_MUTATION="$sbom_mutation" "$builder" "$version"
  )
}

assert_succeeds "non-default release builds must complete with the fake toolchain" \
  run_fake_build "9.8.7-rc.2" false
cmp -s "$temporary_root/runtime.packages.lock.json" "$runtime_lock" || fail "runtime lock was not restored after success"
cmp -s "$temporary_root/test.packages.lock.json" "$test_lock" || fail "test lock was not restored after success"
[[ -f "$repository_root/artifacts/release/rvt-common-migrations-9.8.7-rc.2.tar.gz" ]] || fail "migration archive missing"
[[ -f "$repository_root/artifacts/release/sbom/spdx_2.2/manifest.spdx.json" ]] || fail "SPDX manifest missing"
assets="$repository_root/artifacts/release/assets"
[[ -f "$assets/SHA256SUMS" ]] || fail "flat asset checksum manifest missing"
[[ "$(find "$assets" -maxdepth 1 -type f | wc -l | tr -d ' ')" == "10" ]] || fail "flat release asset count is not exact"
[[ "$(wc -l <"$assets/SHA256SUMS" | tr -d ' ')" == "9" ]] || fail "not every published asset has a checksum entry"
if grep -Eq '  .*[/]' "$assets/SHA256SUMS"; then
  fail "release checksums are not basename-only"
fi
while read -r _ asset_name; do
  [[ -f "$assets/$asset_name" ]] || fail "checksum entry is not a published flat asset: $asset_name"
done <"$assets/SHA256SUMS"
grep -Fq 'dotnet clean rvt-common.sln -c Release' "$temporary_root/tools.log" || fail "fake build did not clean Release outputs"
grep -Fq 'dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion=9.8.7-rc.2' \
  "$temporary_root/tools.log" || fail "fake build did not restore the requested version"
grep -Fq 'dotnet build rvt-common.sln -c Release --no-restore --nologo -p:PackageVersion=9.8.7-rc.2' \
  "$temporary_root/tools.log" || fail "fake build did not compile the requested version"
grep -Fq 'dotnet tool run sbom-tool -- validate' "$temporary_root/tools.log" || fail "fake build did not run official SBOM validation"

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$assets" && sha256sum -c SHA256SUMS) >/dev/null
else
  (cd "$assets" && shasum -a 256 -c SHA256SUMS) >/dev/null
fi

assert_fails "consumer locks must be restored when a release build fails" \
  run_fake_build "9.8.7-rc.3" true
cmp -s "$temporary_root/runtime.packages.lock.json" "$runtime_lock" || fail "runtime lock was not restored after failure"
cmp -s "$temporary_root/test.packages.lock.json" "$test_lock" || fail "test lock was not restored after failure"

assert_fails "a root-only SBOM must fail the release build" \
  run_fake_build "9.8.7-rc.4" false true
assert_output_contains "Rvt package components"
cmp -s "$temporary_root/runtime.packages.lock.json" "$runtime_lock" || fail "runtime lock was not restored after SBOM rejection"
cmp -s "$temporary_root/test.packages.lock.json" "$test_lock" || fail "test lock was not restored after SBOM rejection"

assert_fails "an SBOM with a stale Rvt package version must fail the release build" \
  run_fake_build "9.8.7-rc.5" false false true
assert_output_contains "Rvt package components"

assert_fails "official SBOM validation failures must fail the release build" \
  run_fake_build "9.8.7-rc.6" false false false true
cmp -s "$temporary_root/runtime.packages.lock.json" "$runtime_lock" || fail "runtime lock was not restored after official SBOM failure"
cmp -s "$temporary_root/test.packages.lock.json" "$test_lock" || fail "test lock was not restored after official SBOM failure"

assert_fails "staged project assets with the wrong project version must fail the release build" \
  run_fake_build "9.8.7-rc.7" false false false false "0.2.0-rc.1"
assert_output_contains "project.assets.json project.version"

assert_fails "an SBOM with an extra external dependency must fail the release build" \
  run_fake_build "9.8.7-rc.8" false false false false "" extra-dependency
assert_output_contains "SPDX dependency packages"

assert_fails "an SBOM missing a resolved dependency must fail the release build" \
  run_fake_build "9.8.7-rc.9" false false false false "" missing-dependency
assert_output_contains "SPDX dependency packages"

assert_fails "an SBOM with a wrong dependency version must fail the release build" \
  run_fake_build "9.8.7-rc.10" false false false false "" wrong-dependency-version
assert_output_contains "SPDX dependency packages"

assert_fails "an SBOM with a duplicate dependency object must fail the release build" \
  run_fake_build "9.8.7-rc.11" false false false false "" duplicate-dependency
assert_output_contains "SPDX dependency packages"

assert_fails "an SBOM whose root omits a package file link must fail the release build" \
  run_fake_build "9.8.7-rc.12" false false false false "" wrong-root-link
assert_output_contains "root hasFiles"

restore_production_obj
assert_production_obj_restored

echo "release automation tests passed"
