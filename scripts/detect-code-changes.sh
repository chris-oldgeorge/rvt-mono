#!/usr/bin/env bash
set -euo pipefail

# Emits `code=true` when the base..head range touches anything beyond pure
# documentation, `code=false` when it is documentation-only, so workflows can
# skip expensive jobs on docs-only pull requests. Skipped jobs still report a
# (successful) conclusion, which keeps required checks satisfied.
#
# Documentation means markdown that nothing executes or content-pins:
# `docs/**/*.md` — except `docs/modules/**` and `docs/operations/**`, whose
# content is asserted by .NET tests — plus repository-root markdown. Anything
# else counts as code, and so does any range this script cannot resolve:
# when in doubt, run everything.

usage() {
  printf 'Usage: %s --base <revision> --head <revision>\n' "$0" >&2
  exit 2
}

base=""
head=""
while (($# > 0)); do
  case "$1" in
    --base)
      [[ $# -ge 2 ]] || usage
      base="$2"
      shift 2
      ;;
    --head)
      [[ $# -ge 2 ]] || usage
      head="$2"
      shift 2
      ;;
    *)
      usage
      ;;
  esac
done

emit() {
  local value="$1"
  local reason="$2"
  printf 'detect-code-changes: code=%s (%s)\n' "${value}" "${reason}"
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    printf 'code=%s\n' "${value}" >>"${GITHUB_OUTPUT}"
  fi
}

is_documentation_path() {
  local path="$1"
  if [[ "${path}" != *.md ]]; then
    return 1
  fi
  # Module and operations docs are content-pinned by .NET tests
  # (e.g. CommonPackageBoundaryTests, TestReportingFixture): treat as code.
  if [[ "${path}" == docs/modules/* || "${path}" == docs/operations/* ]]; then
    return 1
  fi
  if [[ "${path}" == docs/* ]]; then
    return 0
  fi
  # Repository-root markdown feeds no build, test, or guard.
  if [[ "${path}" != */* ]]; then
    return 0
  fi
  return 1
}

if [[ -z "${base}" || -z "${head}" || "${base}" =~ ^0+$ ]]; then
  emit true "change range unavailable"
  exit 0
fi

if ! git cat-file -e "${base}^{commit}" 2>/dev/null ||
  ! git cat-file -e "${head}^{commit}" 2>/dev/null; then
  emit true "revision ${base} or ${head} is not available"
  exit 0
fi

changed_paths="$(git diff --name-only "${base}" "${head}" --)"
if [[ -z "${changed_paths}" ]]; then
  emit true "no changed paths resolved"
  exit 0
fi

while IFS= read -r changed_path; do
  if ! is_documentation_path "${changed_path}"; then
    emit true "non-documentation change: ${changed_path}"
    exit 0
  fi
done <<<"${changed_paths}"

emit false "documentation-only change"
