#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: build-release-artifacts.sh VERSION}"
core='(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
prerelease_identifier='(0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)'
semver_regex="^${core}(-${prerelease_identifier}(\.${prerelease_identifier})*)?$"

if [[ ! "$version" =~ $semver_regex ]]; then
  echo "Invalid package version: $version" >&2
  exit 1
fi

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"
sbom_dotnet="${SBOM_DOTNET:-dotnet}"

runtime_lock="package-validation/RuntimeConsumer/packages.lock.json"
test_lock="package-validation/TestConsumer/packages.lock.json"
lock_backup_dir="$(mktemp -d)"
sbom_component_root="$(mktemp -d)"

restore_consumer_locks() {
  local status=$?
  if [[ -f "$lock_backup_dir/runtime.packages.lock.json" ]]; then
    cp "$lock_backup_dir/runtime.packages.lock.json" "$runtime_lock"
  fi
  if [[ -f "$lock_backup_dir/test.packages.lock.json" ]]; then
    cp "$lock_backup_dir/test.packages.lock.json" "$test_lock"
  fi
  rm -rf "$lock_backup_dir"
  rm -rf "$sbom_component_root"
  return "$status"
}
trap restore_consumer_locks EXIT

cp "$runtime_lock" "$lock_backup_dir/runtime.packages.lock.json"
cp "$test_lock" "$lock_backup_dir/test.packages.lock.json"

rm -rf artifacts/release artifacts/packages
mkdir -p artifacts/release artifacts/packages

dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion="$version"
dotnet clean rvt-common.sln -c Release --nologo
dotnet build rvt-common.sln -c Release --no-restore --nologo -p:PackageVersion="$version"
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  -c Release --no-build --nologo
dotnet test tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj \
  -c Release --no-build --nologo
dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  -c Release --no-build --nologo

for project in \
  src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj; do
  dotnet pack "$project" -c Release --no-build -p:PackageVersion="$version"
done

cp Directory.Build.props Directory.Build.targets Directory.Packages.props "$sbom_component_root/"
stage_sbom_project() {
  local project_dir="$1"
  local project_file="$2"
  local package_id="$3"
  local staged_project_dir="$sbom_component_root/$project_dir"

  python3 - "$project_dir/obj/project.assets.json" "$version" <<'PY'
import json
import sys

assets_path, expected_version = sys.argv[1:]
with open(assets_path, encoding="utf-8") as assets_file:
    assets = json.load(assets_file)

actual_version = assets.get("project", {}).get("version")
if actual_version != expected_version:
    raise SystemExit(
        f"{assets_path} project.assets.json project.version is "
        f"{actual_version!r}, expected {expected_version!r}"
    )
PY

  mkdir -p "$staged_project_dir/obj/Release"
  cp "$project_dir/$project_file" "$project_dir/packages.lock.json" "$staged_project_dir/"
  cp "$project_dir/obj/project.assets.json" "$staged_project_dir/obj/"
  cp "$project_dir/obj/Release/$package_id.$version.nuspec" "$staged_project_dir/obj/Release/"
}
stage_sbom_project src/Rvt.Monitor.Common Rvt.Monitor.Common.csproj Rvt.Monitor.Common
stage_sbom_project \
  src/Rvt.Monitor.Common.Infrastructure \
  Rvt.Monitor.Common.Infrastructure.csproj \
  Rvt.Monitor.Common.Infrastructure
stage_sbom_project \
  testing/Rvt.Monitor.IntegrationTesting \
  Rvt.Monitor.IntegrationTesting.csproj \
  Rvt.Monitor.IntegrationTesting

tar -czf "artifacts/release/rvt-common-migrations-$version.tar.gz" database
"$sbom_dotnet" tool restore
"$sbom_dotnet" tool run sbom-tool -- generate \
  -b artifacts/packages \
  -bc "$sbom_component_root" \
  -pn rvt-common \
  -pv "$version" \
  -ps "RVT Group" \
  -nsb https://github.com/RVT-Group-LTD/rvt-reporting

"$sbom_dotnet" tool run sbom-tool -- validate \
  -b artifacts/packages \
  -m artifacts/packages/_manifest \
  -o artifacts/release/sbom-validation.json \
  -n \
  -mi SPDX:2.2

python3 - \
  "artifacts/packages/_manifest/spdx_2.2/manifest.spdx.json" \
  "$version" \
  "$sbom_component_root" <<'PY'
import json
import sys
from pathlib import Path

manifest_path, expected_version, component_root = sys.argv[1:]
with open(manifest_path, encoding="utf-8") as manifest_file:
    manifest = json.load(manifest_file)

if manifest.get("spdxVersion") != "SPDX-2.2":
    raise SystemExit("SPDX manifest version is not SPDX-2.2")

packages = manifest.get("packages")
if not isinstance(packages, list):
    raise SystemExit("SPDX manifest does not contain a packages array")

root_packages = [package for package in packages if package.get("name") == "rvt-common"]
if len(root_packages) != 1 or root_packages[0].get("versionInfo") != expected_version:
    raise SystemExit("SPDX manifest does not contain one unique requested rvt-common root")

root_id = root_packages[0].get("SPDXID")
if not root_id or manifest.get("documentDescribes") != [root_id]:
    raise SystemExit("SPDX documentDescribes does not identify the requested root")

expected_rvt_packages = {
    "Rvt.Monitor.Common",
    "Rvt.Monitor.Common.Infrastructure",
    "Rvt.Monitor.IntegrationTesting",
}
rvt_packages = [
    package
    for package in packages
    if isinstance(package.get("name"), str)
    and package["name"].startswith("Rvt.")
]
if (
    len(rvt_packages) != len(expected_rvt_packages)
    or {package.get("name") for package in rvt_packages} != expected_rvt_packages
    or any(package.get("versionInfo") != expected_version for package in rvt_packages)
):
    raise SystemExit("SPDX manifest Rvt package components are missing, duplicated, or stale")

lock_paths = sorted(Path(component_root).rglob("packages.lock.json"))
if len(lock_paths) != 3:
    raise SystemExit("SBOM component input does not contain exactly three package lock files")

expected_dependencies = set()
for lock_path in lock_paths:
    with lock_path.open(encoding="utf-8") as lock_file:
        lock = json.load(lock_file)
    for framework_dependencies in lock.get("dependencies", {}).values():
        for package_name, details in framework_dependencies.items():
            if "resolved" in details:
                expected_dependencies.add((package_name, details["resolved"]))

if not expected_dependencies:
    raise SystemExit("staged package locks do not contain resolved dependencies")

external_packages = [
    package
    for package in packages
    if package not in root_packages
    and package not in rvt_packages
]
observed_dependencies = [
    (package.get("name"), package.get("versionInfo"))
    for package in external_packages
]
if (
    len(observed_dependencies) != len(set(observed_dependencies))
    or set(observed_dependencies) != expected_dependencies
):
    raise SystemExit(
        "SPDX dependency packages do not exactly match resolved production package locks"
    )

expected_files = {
    f"./{package_id}.{expected_version}.{extension}"
    for package_id in expected_rvt_packages
    for extension in ("nupkg", "snupkg")
}
files = manifest.get("files")
if (
    not isinstance(files, list)
    or len(files) != len(expected_files)
    or {file.get("fileName") for file in files} != expected_files
):
    raise SystemExit("SPDX manifest does not describe exactly the six requested package files")

file_ids = [file.get("SPDXID") for file in files]
root_file_ids = root_packages[0].get("hasFiles")
if (
    any(not isinstance(file_id, str) or not file_id for file_id in file_ids)
    or len(file_ids) != len(set(file_ids))
    or not isinstance(root_file_ids, list)
    or len(root_file_ids) != len(set(root_file_ids))
    or set(root_file_ids) != set(file_ids)
):
    raise SystemExit("SPDX root hasFiles does not identify exactly the six package files")

print(
    f"SPDX manifest contains {len(packages)} packages, "
    f"including {len(expected_dependencies)} resolved dependencies "
    f"and the three synchronized Rvt packages"
)
PY

mv artifacts/packages/_manifest artifacts/release/sbom

RVT_PACKAGE_VERSION="$version" dotnet test \
  tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  -c Release --no-build --nologo
dotnet restore package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  --configfile package-validation/NuGet.local.config --force-evaluate \
  -p:RestoreLockedMode=false \
  -p:RvtPackageVersion="$version"
dotnet build package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  --no-restore --nologo \
  -p:RvtPackageVersion="$version"
dotnet restore package-validation/TestConsumer/TestConsumer.csproj \
  --configfile package-validation/NuGet.local.config --force-evaluate \
  -p:RestoreLockedMode=false \
  -p:RvtPackageVersion="$version"
dotnet test package-validation/TestConsumer/TestConsumer.csproj \
  --no-restore --nologo \
  -p:RvtPackageVersion="$version"

if command -v sha256sum >/dev/null 2>&1; then
  checksum_command=(sha256sum)
  checksum_verify_command=(sha256sum -c)
elif command -v shasum >/dev/null 2>&1; then
  checksum_command=(shasum -a 256)
  checksum_verify_command=(shasum -a 256 -c)
else
  echo "No SHA-256 utility found" >&2
  exit 1
fi

assets_dir="artifacts/release/assets"
mkdir -p "$assets_dir"
for package_id in \
  Rvt.Monitor.Common \
  Rvt.Monitor.Common.Infrastructure \
  Rvt.Monitor.IntegrationTesting; do
  cp "artifacts/packages/$package_id.$version.nupkg" "$assets_dir/"
  cp "artifacts/packages/$package_id.$version.snupkg" "$assets_dir/"
done
cp "artifacts/release/rvt-common-migrations-$version.tar.gz" "$assets_dir/"
cp artifacts/release/sbom/spdx_2.2/manifest.spdx.json "$assets_dir/"
cp artifacts/release/sbom/spdx_2.2/manifest.spdx.json.sha256 "$assets_dir/"

expected_asset_count=9
actual_asset_count="$(find "$assets_dir" -maxdepth 1 -type f ! -name SHA256SUMS | wc -l | tr -d ' ')"
if [[ "$actual_asset_count" != "$expected_asset_count" ]]; then
  echo "Expected $expected_asset_count release assets, found $actual_asset_count" >&2
  exit 1
fi

(
  cd "$assets_dir"
  find . -maxdepth 1 -type f ! -name SHA256SUMS -print \
    | sed 's#^\./##' \
    | LC_ALL=C sort \
    | while IFS= read -r file; do
        "${checksum_command[@]}" "$file"
      done >SHA256SUMS
  "${checksum_verify_command[@]}" SHA256SUMS
)
