#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$root_dir/scripts/verify-mono-solution.sh"

temp_dir="$(mktemp -d)"
trap 'rm -rf "$temp_dir"' EXIT

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
