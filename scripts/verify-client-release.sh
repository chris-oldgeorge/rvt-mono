#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-client-release.sh --payload-dir DIR

Verifies that a prepared client payload contains the required monorepo files
and no internal-development, local-state, generated, secret-bearing, special,
or unsafe linked paths.
USAGE
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
policy_file="${RVT_CLIENT_RELEASE_POLICY:-${repo_root}/docs/release/client-release-exclusions.txt}"
payload_dir=""

while (( $# > 0 )); do
  case "$1" in
    --payload-dir)
      if (( $# < 2 )); then
        printf 'Missing value for --payload-dir.\n' >&2
        exit 2
      fi
      payload_dir="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      printf 'Unknown option: %s\n' "$1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "${payload_dir}" || ! -d "${payload_dir}" ]]; then
  printf 'Payload directory does not exist: %s\n' "${payload_dir:-<missing>}" >&2
  exit 2
fi

if [[ ! -f "${policy_file}" ]]; then
  printf 'Client release exclusion policy does not exist: %s\n' "${policy_file}" >&2
  exit 2
fi

payload_dir="$(cd "${payload_dir}" && pwd -P)"
if [[ "${payload_dir}" == "/" || "${payload_dir}" == "${repo_root}" ]]; then
  printf 'Refusing unsafe payload directory: %s\n' "${payload_dir}" >&2
  exit 2
fi

patterns=()
while IFS= read -r pattern || [[ -n "${pattern}" ]]; do
  if [[ "${pattern}" =~ ^[[:space:]]*(#|$) ]]; then
    continue
  fi
  patterns+=("${pattern}")
done < "${policy_file}"

matches_exclusion() {
  local relative_path="$1"
  local pattern

  for pattern in "${patterns[@]}"; do
    if [[ "${relative_path}" == ${pattern} ]]; then
      return 0
    fi
  done
  return 1
}

failed=0
report_finding() {
  local rule="$1"
  local relative_path="$2"

  printf '%s [rule: %s]\n' "${relative_path}" "${rule}" >&2
  failed=1
}

trim_value() {
  local value="$1"

  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  value="${value%,}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "${value}"
}

value_is_placeholder() {
  local value
  local lower_value

  value="$(trim_value "$1")"
  if [[ "${value}" == \"*\" && "${value}" == *\" ]]; then
    value="${value:1:${#value}-2}"
  elif [[ "${value}" == \'*\' && "${value}" == *\' ]]; then
    value="${value:1:${#value}-2}"
  fi
  value="$(trim_value "${value}")"
  lower_value="$(printf '%s' "${value}" | tr '[:upper:]' '[:lower:]')"

  case "${lower_value}" in
    ""|null|false|true|changeme|change-me|password|postgres|example|sample|placeholder|redacted|not-set|unset|"***")
      return 0
      ;;
  esac

  case "${lower_value}" in
    *'${'*|*'${{'*|*'<placeholder>'*|*'<secret>'*|*'your-'*|*'your_'*|*'example.invalid'*)
      return 0
      ;;
  esac

  return 1
}

scan_secret_assignments() {
  local path="$1"
  local line
  local lower_line
  local value
  local assignment_regex
  local connection_regex
  local url_regex

  assignment_regex='^[[:space:]]*(export[[:space:]]+)?"?[a-z_][a-z0-9_.:-]*(api[_-]?key|apikey|token|secret|password|passwd|pwd|connection[_-]?string|private[_-]?key)[a-z0-9_.:-]*"?[[:space:]]*[:=][[:space:]]*(.*)$'
  connection_regex=';[[:space:]]*(password|pwd)[[:space:]]*=[[:space:]]*([^;[:space:]",]+)'
  url_regex='https?://[^/@[:space:]]+:([^/@[:space:]]+)@'

  while IFS= read -r line || [[ -n "${line}" ]]; do
    lower_line="$(printf '%s' "${line}" | tr '[:upper:]' '[:lower:]')"

    if [[ "${lower_line}" =~ ${connection_regex} ]]; then
      value="${BASH_REMATCH[2]}"
      if ! value_is_placeholder "${value}"; then
        printf 'connection-string password'
        return 0
      fi
    fi

    if [[ "${line}" =~ ${url_regex} ]]; then
      value="${BASH_REMATCH[1]}"
      if ! value_is_placeholder "${value}"; then
        printf 'credentialed URL'
        return 0
      fi
    fi

    if [[ "${lower_line}" =~ ${assignment_regex} ]]; then
      value="${BASH_REMATCH[3]}"
      if ! value_is_placeholder "${value}"; then
        printf 'secret assignment'
        return 0
      fi
    fi
  done < "${path}"

  return 1
}

is_weak_secret_scan_candidate() {
  local relative_path="$1"

  case "${relative_path}" in
    tests/*|*/tests/*|*/test/*|*Tests/*|*/testdata/*|*/fixtures/*)
      return 1
      ;;
    *package-lock.json|*packages.lock.json|*.lock)
      return 1
      ;;
    *.json|*.yml|*.yaml|*.toml|*.ini|*.config|*.props|*.targets|*.xml|*.env.example|*.env.sample|*Dockerfile|config/*|*/config/*|configuration/*|*/configuration/*|deploy/*|*/deploy/*)
      return 0
      ;;
  esac
  return 1
}

report_pattern_matches() {
  local rule="$1"
  local pattern="$2"
  local path
  local relative_path

  while IFS= read -r path; do
    relative_path="${path#"${payload_dir}/"}"
    report_finding "${rule}" "${relative_path}"
  done < <(find "${payload_dir}" -type f \
    -exec grep -IlE -- "${pattern}" {} + 2>/dev/null)
}

required_files=(
  README.md
  Rvt.Mono.slnx
  Directory.Build.props
  global.json
  RELEASE_SOURCE.json
  RELEASE_MANIFEST.txt
)

for required_file in "${required_files[@]}"; do
  if [[ ! -f "${payload_dir}/${required_file}" ]]; then
    report_finding "required release file" "${required_file}"
  fi
done

while IFS= read -r -d '' path; do
  relative_path="${path#"${payload_dir}/"}"

  if matches_exclusion "${relative_path}"; then
    report_finding "excluded client path" "${relative_path}"
    continue
  fi

  if [[ -L "${path}" ]]; then
    link_target="$(readlink "${path}")"
    if [[ "${link_target}" == /* ]]; then
      report_finding "absolute symlink" "${relative_path}"
      continue
    fi

    link_parent="$(cd "$(dirname "${path}")" && pwd -P)"
    if [[ ! -e "${path}" ]]; then
      report_finding "broken symlink" "${relative_path}"
      continue
    fi

    resolved_target="$(cd "${link_parent}" && cd "$(dirname "${link_target}")" && pwd -P)/$(basename "${link_target}")"
    case "${resolved_target}" in
      "${payload_dir}"|"${payload_dir}"/*)
        ;;
      *)
        report_finding "escaping symlink" "${relative_path}"
        ;;
    esac
    continue
  fi

  if [[ ! -f "${path}" && ! -d "${path}" ]]; then
    report_finding "special file" "${relative_path}"
  fi
done < <(find "${payload_dir}" -mindepth 1 -print0)

report_pattern_matches \
  "PEM private key" \
  '-----BEGIN ([A-Z0-9]+ )*PRIVATE KEY-----'
report_pattern_matches \
  "GitHub token" \
  '(^|[^A-Za-z0-9])(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})'
report_pattern_matches \
  "AWS access key" \
  '(^|[^A-Z0-9])AKIA[0-9A-Z]{16}([^A-Z0-9]|$)'
report_pattern_matches \
  "Slack token" \
  '(^|[^A-Za-z0-9])xox[baprs]-[A-Za-z0-9-]{20,}'

while IFS= read -r -d '' path; do
  relative_path="${path#"${payload_dir}/"}"
  if ! is_weak_secret_scan_candidate "${relative_path}"; then
    continue
  fi

  assignment_rule=""
  if assignment_rule="$(scan_secret_assignments "${path}")"; then
    report_finding "${assignment_rule}" "${relative_path}"
  fi
done < <(find "${payload_dir}" -type f -print0)

if (( failed != 0 )); then
  printf 'Client release payload verification failed.\n' >&2
  exit 1
fi

printf 'Client release payload boundary verified: %s\n' "${payload_dir}"
