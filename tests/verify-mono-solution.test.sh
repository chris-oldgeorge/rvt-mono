#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$root_dir/scripts/verify-mono-solution.sh"

temp_dir="$(mktemp -d)"
new_tree_dir="$root_dir/verify-mono-solution-probe-tree"
trap 'rm -rf "$temp_dir" "$new_tree_dir"' EXIT

# A project in a top-level tree the verifier has never heard of must be graded,
# not ignored. Discovery used to walk a hand-maintained list of three module
# roots, so a fourth tree would have been invisible: its projects could be
# missing from the solution and nothing would say so.
mkdir -p "$new_tree_dir/Probe"
printf '<Project Sdk="Microsoft.NET.Sdk"></Project>\n' > "$new_tree_dir/Probe/Probe.csproj"
if "$root_dir/scripts/verify-mono-solution.sh" >/dev/null 2>&1; then
  echo "Expected verifier to reject a project in an undeclared top-level tree." >&2
  exit 1
fi
rm -rf "$new_tree_dir"

solution_output="$(dotnet sln "$root_dir/Rvt.Mono.slnx" list)"
fake_solution_output="$(printf '%s\n' "$solution_output" | sed 's#^apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj$#apps/monitors/not-a-real-project.csproj#')"

printf '%s\n' '#!/usr/bin/env bash' 'printf "%s\\n" "$DOTNET_SOLUTION_OUTPUT"' > "$temp_dir/dotnet"
chmod +x "$temp_dir/dotnet"

if PATH="$temp_dir:$PATH" DOTNET_SOLUTION_OUTPUT="$fake_solution_output" "$root_dir/scripts/verify-mono-solution.sh" >/dev/null 2>&1; then
  echo "Expected verifier to reject a same-count solution listing with a substituted project path." >&2
  exit 1
fi

sed 's#/Apps/Monitors/#/apps/monitors/#' "$root_dir/Rvt.Mono.slnx" > "$temp_dir/Rvt.Mono.slnx"

if PATH="$temp_dir:$PATH" DOTNET_SOLUTION_OUTPUT="$solution_output" MONO_SOLUTION_FILE="$temp_dir/Rvt.Mono.slnx" \
  "$root_dir/scripts/verify-mono-solution.sh" >/dev/null 2>&1; then
  echo "Expected verifier to reject a solution with the wrong logical folder organization." >&2
  exit 1
fi
