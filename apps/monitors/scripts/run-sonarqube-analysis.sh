#!/usr/bin/env bash
set -euo pipefail

SONAR_HOST_URL="${SONAR_HOST_URL:-http://localhost:9000}"
SONAR_ORGANIZATION="${SONAR_ORGANIZATION:-}"
SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY:-rvt-monitors}"
SONAR_PROJECT_NAME="${SONAR_PROJECT_NAME:-RVT Monitors}"
SOLUTION="${SOLUTION:-rvt-monitors.sln}"
RUN_TESTS="${RUN_TESTS:-false}"
COLLECT_COVERAGE="${COLLECT_COVERAGE:-true}"
COVERAGE_RESULTS_DIR="${COVERAGE_RESULTS_DIR:-TestResults/coverage/$(date +%Y%m%d%H%M%S)}"
COVERAGE_FORMAT="${COVERAGE_FORMAT:-opencover}"
SONAR_SCANNER_SCAN_ALL="${SONAR_SCANNER_SCAN_ALL:-false}"
SONAR_DISABLE_WEB_ANALYSIS="${SONAR_DISABLE_WEB_ANALYSIS:-false}"

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "SONAR_TOKEN is required. Export a SonarQube token before running this script." >&2
  exit 2
fi

if ! dotnet tool list --global | grep -Eq '^dotnet-sonarscanner[[:space:]]'; then
  echo "dotnet-sonarscanner is not installed." >&2
  echo "Install it with: dotnet tool install --global dotnet-sonarscanner" >&2
  exit 2
fi

begin_args=(
  "/k:${SONAR_PROJECT_KEY}"
  "/n:${SONAR_PROJECT_NAME}"
  "/d:sonar.host.url=${SONAR_HOST_URL}"
  "/d:sonar.token=${SONAR_TOKEN}"
  "/d:sonar.scanner.scanAll=${SONAR_SCANNER_SCAN_ALL}"
)

if [[ "$RUN_TESTS" == "true" && "$COLLECT_COVERAGE" == "true" ]]; then
  begin_args+=("/d:sonar.cs.opencover.reportsPaths=${COVERAGE_RESULTS_DIR}/**/coverage.opencover.xml")
fi

if [[ -n "$SONAR_ORGANIZATION" ]]; then
  begin_args=("/o:${SONAR_ORGANIZATION}" "${begin_args[@]}")
fi

if [[ "$SONAR_DISABLE_WEB_ANALYSIS" == "true" ]]; then
  begin_args+=(
    "/d:sonar.javascript.file.suffixes=-"
    "/d:sonar.javascript.yaml.file.suffixes=-"
    "/d:sonar.typescript.file.suffixes=-"
    "/d:sonar.css.file.suffixes=-"
  )
fi

dotnet sonarscanner begin \
  "${begin_args[@]}"

dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" --no-restore --no-incremental

if [[ "$RUN_TESTS" == "true" ]]; then
  if [[ "$COLLECT_COVERAGE" == "true" ]]; then
    if [[ "$COVERAGE_FORMAT" != "opencover" ]]; then
      echo "SonarQube coverage import requires COVERAGE_FORMAT=opencover." >&2
      exit 2
    fi

    dotnet test "$SOLUTION" --no-build --no-restore \
      --results-directory "$COVERAGE_RESULTS_DIR" \
      --collect "XPlat Code Coverage;Format=${COVERAGE_FORMAT}"

    if ! find "$COVERAGE_RESULTS_DIR" -name 'coverage.opencover.xml' -type f -print -quit | grep -q .; then
      echo "No OpenCover coverage reports were generated under $COVERAGE_RESULTS_DIR." >&2
      exit 2
    fi
  else
    dotnet test "$SOLUTION" --no-build --no-restore
  fi
fi

dotnet sonarscanner end "/d:sonar.token=${SONAR_TOKEN}"
