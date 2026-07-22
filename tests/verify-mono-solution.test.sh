#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$root_dir/scripts/verify-mono-solution.sh"

temp_dir="$(mktemp -d)"
trap 'rm -rf "$temp_dir"' EXIT

fake_solution_output="$(dotnet sln "$root_dir/Rvt.Mono.slnx" list | sed 's#^apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj$#apps/monitors/not-a-real-project.csproj#')"

printf '%s\n' '#!/usr/bin/env bash' 'printf "%s\\n" "$DOTNET_SOLUTION_OUTPUT"' > "$temp_dir/dotnet"
chmod +x "$temp_dir/dotnet"

if PATH="$temp_dir:$PATH" DOTNET_SOLUTION_OUTPUT="$fake_solution_output" "$root_dir/scripts/verify-mono-solution.sh" >/dev/null 2>&1; then
  echo "Expected verifier to reject a same-count solution listing with a substituted project path." >&2
  exit 1
fi
