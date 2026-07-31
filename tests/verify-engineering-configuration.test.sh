#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
client_release_policy="$root_dir/docs/release/client-release-exclusions.txt"
client_release_policy_boundary_verifier="$root_dir/scripts/verify-client-release-policy-boundary.sh"
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
  grep -Fq '"DiagnosticId": "IDE1006"' "$report" || fail "expected IDE1006 root code-style diagnostic beneath ${project#$temp_dir/}: $output"
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
  [[ ! -f "$report" ]] || ! grep -Fq '"DiagnosticId": "IDE1006"' "$report" || fail "nested-root mutation retained IDE1006 beneath ${project#$temp_dir/}: $output"
}

require_file "$root_dir/.editorconfig"
require_file "$root_dir/Directory.Build.props"

is_hardened_npm_install() {
  local path="$1"
  local expected_command="$2"
  local npm_ci_lines

  grep -Fqx "$expected_command" "$path" || return 1
  npm_ci_lines="$(awk '
    /^[[:space:]]*#/ { next }
    /(^|[^[:alnum:]_])npm[[:space:]]+ci($|[^[:alnum:]_])/ { count++ }
    END { print count + 0 }
  ' "$path")"
  [[ "$npm_ci_lines" == "1" ]]
}

assert_hardened_npm_install() {
  local path="$1"
  local expected_command="$2"
  local description="$3"

  is_hardened_npm_install "$path" "$expected_command" || fail "$description must contain exactly one executable npm ci invocation, using --ignore-scripts"
}

assert_npm_install_mutation_rejected() {
  local source_path="$1"
  local expected_command="$2"
  local replacement_command="$3"
  local description="$4"
  local mutation_path="$temp_dir/$(basename "$source_path").mutation"

  cp "$source_path" "$mutation_path"
  sed -i.bak "s|$expected_command|$replacement_command|" "$mutation_path"
  rm -f "$mutation_path.bak"

  if is_hardened_npm_install "$mutation_path" "$expected_command"; then
    fail "$description mutation bypassed the npm lifecycle-script guard"
  fi

  printf 'Rejected %s mutation.\n' "$description"
}

target_contents() {
  local project="$1"
  local target_name="$2"

  awk -v target_name="$target_name" '
    $0 ~ "<Target Name=\"" target_name "\"" { in_target = 1 }
    in_target { print }
    in_target && /<\/Target>/ { exit }
  ' "$project"
}

has_hardened_msbuild_npm_install() {
  local project="$1"
  local target_name="$2"
  local expected_command='    <Exec Command="npm ci --ignore-scripts" WorkingDirectory="$(SpaRoot)" />'
  local contents
  local npm_ci_count

  contents="$(target_contents "$project" "$target_name")"
  grep -Fqx "$expected_command" <<< "$contents" || return 1
  npm_ci_count="$(grep -oE 'npm[[:space:]]+ci([[:space:]]|&amp;|$)' <<< "$contents" | wc -l | tr -d '[:space:]')"
  [[ "$npm_ci_count" == "1" ]]
}

assert_hardened_msbuild_npm_installs() {
  local project="$1"

  has_hardened_msbuild_npm_install "$project" DebugEnsureNodeEnv || fail "Portal DebugEnsureNodeEnv must use exactly one canonical npm ci --ignore-scripts command"
  has_hardened_msbuild_npm_install "$project" PublishClientApp || fail "Portal PublishClientApp must use exactly one canonical npm ci --ignore-scripts command"
}

assert_msbuild_npm_install_mutation_rejected() {
  local source_path="$1"
  local replacement="$2"
  local description="$3"
  local mutation_path="$temp_dir/$(basename "$source_path").$RANDOM.mutation"

  sed "s|npm ci --ignore-scripts|$replacement|" "$source_path" > "$mutation_path"
  if has_hardened_msbuild_npm_install "$mutation_path" DebugEnsureNodeEnv && has_hardened_msbuild_npm_install "$mutation_path" PublishClientApp; then
    fail "$description mutation bypassed the MSBuild npm lifecycle-script guard"
  fi

  printf 'Rejected %s mutation.\n' "$description"
}

assert_second_msbuild_npm_install_rejected() {
  local source_path="$1"
  local mutation_path="$temp_dir/$(basename "$source_path").second-install.mutation"

  awk '
    /<Target Name="DebugEnsureNodeEnv"/ { in_target = 1 }
    in_target && /<\/Target>/ {
      print "    <Exec Command=\"npm ci --ignore-scripts\" WorkingDirectory=\"$(SpaRoot)\" />"
      in_target = 0
    }
    { print }
  ' "$source_path" > "$mutation_path"
  if has_hardened_msbuild_npm_install "$mutation_path" DebugEnsureNodeEnv; then
    fail "Portal DebugEnsureNodeEnv second npm ci mutation bypassed the MSBuild npm lifecycle-script guard"
  fi

  printf 'Rejected Portal DebugEnsureNodeEnv second npm ci mutation.\n'
}

portal_frontend_verifier="$root_dir/apps/portal/scripts/verify-frontend.sh"
portal_frontend_container_verifier="$root_dir/apps/portal/scripts/verify-frontend-container.sh"
portal_client_dockerfile="$root_dir/apps/portal/RvtPortal.Client/Dockerfile"
portal_spa_project="$root_dir/apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj"

final_dockerfile_stage() {
  awk '
    /^[[:space:]]*FROM[[:space:]]/ { stage = $0 "\n"; next }
    { stage = stage $0 "\n" }
    END { printf "%s", stage }
  ' "$1"
}

has_portal_runtime_user() {
  local dockerfile="$1"
  local stage
  local effective_user

  stage="$(final_dockerfile_stage "$dockerfile")"
  effective_user="$(printf '%s\n' "$stage" | awk '
    /^[[:space:]]*USER[[:space:]]+/ { user = $0 }
    END { print user }
  ')"

  [[ "$effective_user" =~ ^[[:space:]]*USER[[:space:]]+101:101[[:space:]]*(#.*)?$ ]]
}

has_portal_container_inspection_user() {
  local verifier="$1"

  grep -Fqx "docker image inspect --format '{{.Config.User}}' \"\$image\" | grep -Fx '101:101'" "$verifier"
}

assert_portal_runtime_user_mutation_rejected() {
  local mutation_path="$temp_dir/$(basename "$portal_client_dockerfile").runtime-user-comment.mutation"

  sed 's/^[[:space:]]*USER 101:101$/# USER 101:101/' "$portal_client_dockerfile" > "$mutation_path"
  has_portal_runtime_user "$mutation_path" && fail "Portal client commented runtime-user mutation bypassed the Dockerfile USER guard"
  printf 'Rejected Portal client commented runtime-user mutation.\n'
}

assert_portal_runtime_user_later_root_mutation_rejected() {
  local mutation_path="$temp_dir/$(basename "$portal_client_dockerfile").runtime-user-later-root.mutation"

  awk '
    /^[[:space:]]*HEALTHCHECK/ { print "USER 0:0" }
    { print }
  ' "$portal_client_dockerfile" > "$mutation_path"
  has_portal_runtime_user "$mutation_path" && fail "Portal client later-root runtime-user mutation bypassed the Dockerfile USER guard"
  printf 'Rejected Portal client later-root runtime-user mutation.\n'
}

assert_portal_runtime_user_post_runtime_root_mutation_rejected() {
  local mutation_path="$temp_dir/$(basename "$portal_client_dockerfile").runtime-user-post-runtime-root.mutation"

  awk '
    { print }
    /^[[:space:]]*HEALTHCHECK/ { print "USER 0:0" }
  ' "$portal_client_dockerfile" > "$mutation_path"
  has_portal_runtime_user "$mutation_path" && fail "Portal client post-runtime root-user mutation bypassed the Dockerfile USER guard"
  printf 'Rejected Portal client post-runtime root-user mutation.\n'
}

assert_portal_container_inspection_user_mutation_rejected() {
  local mutation_path="$temp_dir/$(basename "$portal_frontend_container_verifier").user-inspection.mutation"

  sed "s/'101:101'/'101'/" "$portal_frontend_container_verifier" > "$mutation_path"
  has_portal_container_inspection_user "$mutation_path" && fail "Portal frontend container bare UID inspection mutation bypassed the user guard"
  printf 'Rejected Portal frontend container bare UID inspection mutation.\n'
}

has_portal_runtime_user "$portal_client_dockerfile" || fail "Portal client Dockerfile final runtime stage must declare USER 101:101 before runtime execution"
assert_portal_runtime_user_mutation_rejected
assert_portal_runtime_user_later_root_mutation_rejected
assert_portal_runtime_user_post_runtime_root_mutation_rejected
has_portal_container_inspection_user "$portal_frontend_container_verifier" || fail "Portal frontend container verifier must require Config.User 101:101"
assert_portal_container_inspection_user_mutation_rejected

assert_hardened_npm_install "$portal_frontend_verifier" 'npm ci --ignore-scripts' "Portal frontend verifier"
assert_hardened_npm_install "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' "Portal client Dockerfile"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'npm ci # comment' "frontend verifier inline-comment bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'npm ci ' "frontend verifier whitespace bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'command npm ci' "frontend verifier command-wrapped bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'env CI=true npm ci' "frontend verifier env-wrapped bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'CI=true npm ci' "frontend verifier assignment-prefixed bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'command env CI=true npm ci' "frontend verifier nested-wrapper bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' "sh -c 'npm ci'" "frontend verifier single-quoted shell bare install"
assert_npm_install_mutation_rejected "$portal_frontend_verifier" 'npm ci --ignore-scripts' 'bash -c "npm ci"' "frontend verifier double-quoted shell bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN npm ci # comment' "Dockerfile inline-comment bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN npm ci ' "Dockerfile whitespace bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN command npm ci' "Dockerfile command-wrapped bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN env CI=true npm ci' "Dockerfile env-wrapped bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN CI=true npm ci' "Dockerfile assignment-prefixed bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN command env CI=true npm ci' "Dockerfile nested-wrapper bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' "RUN sh -c 'npm ci'" "Dockerfile single-quoted shell bare install"
assert_npm_install_mutation_rejected "$portal_client_dockerfile" 'RUN npm ci --ignore-scripts' 'RUN bash -c "npm ci"' "Dockerfile double-quoted shell bare install"

assert_hardened_msbuild_npm_installs "$portal_spa_project"
assert_msbuild_npm_install_mutation_rejected "$portal_spa_project" 'npm ci --ignore-scripts --no-audit' "MSBuild arbitrary npm ci option"
assert_msbuild_npm_install_mutation_rejected "$portal_spa_project" 'command npm ci --ignore-scripts' "MSBuild command-wrapped npm ci"
assert_msbuild_npm_install_mutation_rejected "$portal_spa_project" 'npm ci' "MSBuild bare npm ci"
assert_second_msbuild_npm_install_rejected "$portal_spa_project"

declare -a representative_projects=(
  "apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj|latest|true"
  "apps/portal/RVT.Entities/RVT.Entities.csproj|latest-recommended|false"
  "libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj|latest|false"
  "apps/monitors/reportingmonitor/Rvt.Reporting.Core/Rvt.Reporting.Core.csproj|latest|true"
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

"$client_release_policy_boundary_verifier" "$client_release_policy" "$root_dir"

if [[ -f "$client_release_policy" ]]; then
  # The monitor Dockerfiles COPY the whole context, so anything the build context
  # carries lands in a build layer and in the BuildKit cache even though only
  # /app/publish is copied into the final image. The classes the client-release
  # policy treats as secret are therefore withheld from the build context too, and
  # the list is read from the policy so the two cannot drift.
  policy_secret_patterns() {
    awk '
      /^# Local environment and settings$/ { section = 1; next }
      /^# Secret-bearing key and certificate material$/ { section = 1; next }
      /^$/ { section = 0; next }
      /^#/ { section = 0; next }
      section { print }
    ' "$client_release_policy"
  }

  assert_dockerignore_withholds_secrets() {
    local dockerignore="$1"
    local pattern

    require_file "$dockerignore"
    while IFS= read -r pattern; do
      [[ -n "$pattern" ]] || continue
      grep -Fqx -- "$pattern" "$dockerignore" ||
        fail "${dockerignore#$root_dir/} does not withhold the secret-bearing pattern '$pattern' that docs/release/client-release-exclusions.txt classifies as secret"
    done < <(policy_secret_patterns)
  }

  secret_pattern_count="$(policy_secret_patterns | grep -c .)"
  [[ "$secret_pattern_count" -ge 20 ]] ||
    fail "expected the client-release policy to classify at least 20 secret-bearing patterns, found $secret_pattern_count"

  assert_dockerignore_withholds_secrets "$root_dir/.dockerignore"
  assert_dockerignore_withholds_secrets "$root_dir/apps/monitors/.dockerignore"
  assert_dockerignore_withholds_secrets "$root_dir/apps/portal/.dockerignore"
fi

printf 'Engineering configuration hierarchy verified.\n'
