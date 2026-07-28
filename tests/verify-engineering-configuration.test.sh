#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/rvt-engineering-configuration.XXXXXX")"
temp_dir="$(cd "$temp_dir" && pwd -P)"
nuget_config="$temp_dir/NuGet.Config"
export NUGET_PACKAGES="$temp_dir/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$temp_dir/nuget-http-cache"

cat > "$nuget_config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
EOF

cleanup() {
  rm -rf "$temp_dir"
}
trap cleanup EXIT

fail() {
  printf 'FAIL: %s\n' "$*" >&2
  exit 1
}

require_file() {
  [[ -f "$1" ]] || fail "required configuration file is missing: ${1#$root_dir/}"
}

assert_property() {
  local project="$1"
  local property="$2"
  local expected="$3"
  local actual

  actual="$(dotnet msbuild "$project" --nologo "-getProperty:$property" "${@:4}")"
  [[ "$actual" == "$expected" ]] || fail "${project#$root_dir/}: expected $property=$expected, got ${actual:-<empty>}"
}

write_probe_project() {
  local project_dir="$1"

  mkdir -p "$project_dir"
  cat > "$project_dir/ConfigurationProbe.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
EOF
  cat > "$project_dir/RootOnlyProbe.cs" <<'EOF'
namespace Probe;

public sealed class RootOnlyProbe
{
    private int _rootProbe;

    public int Value
    {
        get
        {
            System.Collections.Generic.IEnumerable<int> values = GetValues();
            foreach (int value in values)
            {
                return value;
            }

            return 0;
        }
        set => _rootProbe = value;
    }

    private static System.Collections.Generic.IEnumerable<int> GetValues() => new[] { 1 };
}
EOF
}

assert_root_probe_diagnostic() {
  local project="$1"
  local output
  local report="$project.format-report.json"
  local status

  rm -f "$report"
  set +e
  output="$(dotnet format style "$project" --verify-no-changes --severity info --no-restore --diagnostics IDE1006 --report "$report" 2>&1)"
  status=$?
  set -e

  [[ $status -ne 0 ]] || fail "expected root code-style diagnostic beneath ${project#$temp_dir/}: $output"
  [[ -f "$report" ]] || fail "dotnet format did not write a diagnostic report beneath ${project#$temp_dir/}: $output"
  rg -q '"DiagnosticId": "IDE1006"' "$report" || fail "expected IDE1006 root code-style diagnostic beneath ${project#$temp_dir/}: $output"
}

assert_root_probe_diagnostic_absent() {
  local project="$1"
  local output
  local report="$project.format-report.json"
  local status

  rm -f "$report"
  set +e
  output="$(dotnet format style "$project" --verify-no-changes --severity info --no-restore --diagnostics IDE1006 --report "$report" 2>&1)"
  status=$?
  set -e

  [[ $status -eq 0 ]] || fail "nested-root mutation retained root code-style diagnostic beneath ${project#$temp_dir/}: $output"
  [[ ! -f "$report" ]] || ! rg -q '"DiagnosticId": "IDE1006"' "$report" || fail "nested-root mutation retained IDE1006 beneath ${project#$temp_dir/}: $output"
}

require_file "$root_dir/.editorconfig"
require_file "$root_dir/Directory.Build.props"

declare -a representative_projects=(
  "apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj|latest|true"
  "apps/portal/RVT.Entities/RVT.Entities.csproj|latest-recommended|false"
  "libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj|latest|false"
  "services/reporting/src/Rvt.Reporting.Core/Rvt.Reporting.Core.csproj|latest-recommended|false"
)

for representative_project in "${representative_projects[@]}"; do
  IFS='|' read -r project_relative_path expected_analysis_level expected_code_style_enforcement <<< "$representative_project"
  project="$root_dir/$project_relative_path"
  assert_property "$project" Nullable enable
  assert_property "$project" ImplicitUsings enable
  assert_property "$project" AnalysisLevel "$expected_analysis_level"
  assert_property "$project" RvtEngineeringStandardsMode Ratchet
  assert_property "$project" EnforceCodeStyleInBuild "$expected_code_style_enforcement"
  assert_property "$project" Deterministic true
done

strict_project="$root_dir/apps/portal/RVT.Entities/RVT.Entities.csproj"
assert_property "$strict_project" RvtEngineeringStandardsMode Strict -p:RvtEngineeringStandardsMode=Strict
assert_property "$strict_project" EnforceCodeStyleInBuild true -p:RvtEngineeringStandardsMode=Strict

editorconfig_root="$temp_dir/editorconfig"
mkdir -p "$editorconfig_root"
cp "$root_dir/.editorconfig" "$editorconfig_root/.editorconfig"
cat >> "$editorconfig_root/.editorconfig" <<'EOF'

# Test-only root policy: module configs intentionally contain no equivalent rule.
[*.cs]
dotnet_naming_symbols.root_probe_types.applicable_kinds = class
dotnet_naming_symbols.root_probe_types.applicable_accessibilities = *
dotnet_naming_style.root_probe_type_style.required_prefix = RootProbe
dotnet_naming_style.root_probe_type_style.capitalization = pascal_case
dotnet_naming_rule.root_probe_types_require_root_prefix.symbols = root_probe_types
dotnet_naming_rule.root_probe_types_require_root_prefix.style = root_probe_type_style
dotnet_naming_rule.root_probe_types_require_root_prefix.severity = warning
EOF

declare -a editorconfig_modules=(
  "apps/monitors"
  "apps/portal"
  "services/reporting"
)

for module_relative_path in "${editorconfig_modules[@]}"; do
  module_dir="$editorconfig_root/$module_relative_path"
  mkdir -p "$module_dir"
  cp "$root_dir/$module_relative_path/.editorconfig" "$module_dir/.editorconfig"
  cp "$module_dir/.editorconfig" "$module_dir/.editorconfig.original"
  write_probe_project "$module_dir/probe"
  dotnet restore "$module_dir/probe/ConfigurationProbe.csproj" --configfile "$nuget_config" --packages "$NUGET_PACKAGES" --nologo >/dev/null

  assert_root_probe_diagnostic "$module_dir/probe/ConfigurationProbe.csproj"

  # EditorConfig recognizes root only as a top-level declaration. Insert it
  # before the copied module settings to simulate a nested inheritance stop.
  { printf 'root = true\n\n'; cat "$module_dir/.editorconfig.original"; } > "$module_dir/.editorconfig"
  assert_root_probe_diagnostic_absent "$module_dir/probe/ConfigurationProbe.csproj"
  cp "$module_dir/.editorconfig.original" "$module_dir/.editorconfig"
done

msbuild_root="$temp_dir/msbuild"
mkdir -p "$msbuild_root/apps/monitors"
cp "$root_dir/Directory.Build.props" "$msbuild_root/Directory.Build.props"
cp "$root_dir/apps/monitors/Directory.Build.props" "$msbuild_root/apps/monitors/Directory.Build.props"
write_probe_project "$msbuild_root/apps/monitors/probe"

assert_property "$msbuild_root/apps/monitors/probe/ConfigurationProbe.csproj" Nullable enable
sed -i.bak '/<Import Project="\.\.\/\.\.\/Directory\.Build\.props" \/>/d' "$msbuild_root/apps/monitors/Directory.Build.props"
assert_property "$msbuild_root/apps/monitors/probe/ConfigurationProbe.csproj" Nullable ''

printf 'Engineering configuration hierarchy verified.\n'
