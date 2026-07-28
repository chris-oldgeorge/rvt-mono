#!/usr/bin/env bash
set -euo pipefail

SONAR_HOST_URL="${SONAR_HOST_URL:-http://localhost:9000}"
SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY:-rvt-monitors}"
SONAR_PROJECT_NAME="${SONAR_PROJECT_NAME:-RVT Monitors}"

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "SONAR_TOKEN is required. Export a SonarQube token before running this script." >&2
  exit 2
fi

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

status_code="$(
  curl -sS \
    -o "$response_file" \
    -w "%{http_code}" \
    -u "${SONAR_TOKEN}:" \
    -X POST "${SONAR_HOST_URL%/}/api/projects/create" \
    --data-urlencode "project=${SONAR_PROJECT_KEY}" \
    --data-urlencode "name=${SONAR_PROJECT_NAME}"
)"

case "$status_code" in
  200|201|204)
    echo "SonarQube project '${SONAR_PROJECT_KEY}' is ready at ${SONAR_HOST_URL%/}."
    ;;
  400)
    if grep -qi "already" "$response_file"; then
      echo "SonarQube project '${SONAR_PROJECT_KEY}' already exists at ${SONAR_HOST_URL%/}."
    else
      echo "SonarQube rejected the project create request:" >&2
      cat "$response_file" >&2
      exit 1
    fi
    ;;
  *)
    echo "SonarQube project create request failed with HTTP ${status_code}:" >&2
    cat "$response_file" >&2
    exit 1
    ;;
esac
