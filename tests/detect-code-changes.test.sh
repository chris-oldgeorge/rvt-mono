#!/usr/bin/env bash
set -euo pipefail

# Contract for scripts/detect-code-changes.sh: documentation-only ranges emit
# code=false, everything else — including unresolvable ranges and the markdown
# files whose content .NET tests pin — emits code=true.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
script_path="${repo_root}/scripts/detect-code-changes.sh"

if [[ ! -x "${script_path}" ]]; then
  printf 'FAIL: scripts/detect-code-changes.sh is missing or not executable.\n' >&2
  exit 1
fi

temp_root="$(mktemp -d)"
trap 'rm -rf "${temp_root}"' EXIT

fixture="${temp_root}/fixture"
mkdir -p "${fixture}"
git -C "${fixture}" init --quiet --initial-branch=main

fixture_git() {
  git -C "${fixture}" \
    -c user.name="contract-test" \
    -c user.email="contract-test@example.invalid" \
    "$@"
}

write_file() {
  local relative="$1"
  local content="$2"
  mkdir -p "${fixture}/$(dirname "${relative}")"
  printf '%s\n' "${content}" >"${fixture}/${relative}"
}

commit_all() {
  fixture_git add --all
  fixture_git commit --quiet --message "$1"
}

write_file "src/app.cs" "class App {}"
write_file "docs/reviews/review.md" "review"
write_file "docs/modules/monitors/readme.md" "pinned module doc"
write_file "docs/operations/monitors/runbook.md" "pinned operations doc"
write_file "README.md" "readme"
write_file "apps/monitors/README.md" "in-tree doc"
commit_all "baseline"
base_sha="$(fixture_git rev-parse HEAD)"

failures=0

expect() {
  local label="$1"
  local expected="$2"
  local base="$3"
  local head="$4"
  local output_file="${temp_root}/output-${label// /-}"

  : >"${output_file}"
  if ! (cd "${fixture}" && GITHUB_OUTPUT="${output_file}" "${script_path}" \
    --base "${base}" --head "${head}" >/dev/null); then
    printf 'FAIL: %s: detect-code-changes.sh exited nonzero.\n' "${label}" >&2
    failures=$((failures + 1))
    return
  fi

  local actual
  actual="$(cat "${output_file}")"
  if [[ "${actual}" != "code=${expected}" ]]; then
    printf 'FAIL: %s: expected code=%s, got %s.\n' "${label}" "${expected}" "${actual:-nothing}" >&2
    failures=$((failures + 1))
  fi
}

commit_change() {
  local relative="$1"
  write_file "${relative}" "changed $(date +%s%N)"
  commit_all "change ${relative}"
  fixture_git rev-parse HEAD
}

head_sha="$(commit_change "docs/reviews/review.md")"
expect "docs review markdown is documentation" false "${base_sha}" "${head_sha}"

head_sha="$(commit_change "README.md")"
expect "root markdown is documentation" false "${base_sha}" "${head_sha}"

previous_sha="${head_sha}"
head_sha="$(commit_change "src/app.cs")"
expect "source change is code" true "${previous_sha}" "${head_sha}"
expect "mixed docs and code range is code" true "${base_sha}" "${head_sha}"

previous_sha="${head_sha}"
head_sha="$(commit_change "docs/modules/monitors/readme.md")"
expect "content-pinned module doc is code" true "${previous_sha}" "${head_sha}"

previous_sha="${head_sha}"
head_sha="$(commit_change "docs/operations/monitors/runbook.md")"
expect "content-pinned operations doc is code" true "${previous_sha}" "${head_sha}"

previous_sha="${head_sha}"
head_sha="$(commit_change "apps/monitors/README.md")"
expect "markdown beside code is code" true "${previous_sha}" "${head_sha}"

# Rename detection is the fail-open case this gate must resist: git collapses a
# delete+add of similar content into the destination path alone, so a code file
# renamed into docs/ would otherwise read as documentation-only.
previous_sha="${head_sha}"
write_file "src/renamed.cs" "class Renamed { }"
commit_all "seed rename source"
rename_base_sha="$(fixture_git rev-parse HEAD)"
fixture_git mv "src/renamed.cs" "docs/reviews/renamed.md"
commit_all "rename code into docs"
head_sha="$(fixture_git rev-parse HEAD)"
expect "code renamed into docs is code" true "${rename_base_sha}" "${head_sha}"

previous_sha="${head_sha}"
write_file "docs/modules/monitors/pinned.md" "pinned content asserted by tests"
commit_all "seed pinned module doc"
rename_base_sha="$(fixture_git rev-parse HEAD)"
mkdir -p "${fixture}/docs/architecture"
fixture_git mv "docs/modules/monitors/pinned.md" "docs/architecture/pinned.md"
commit_all "relocate content-pinned module doc"
head_sha="$(fixture_git rev-parse HEAD)"
expect "content-pinned doc relocated out of its tree is code" true \
  "${rename_base_sha}" "${head_sha}"

expect "zero base falls back to code" true \
  "0000000000000000000000000000000000000000" "${head_sha}"
expect "unknown base falls back to code" true \
  "1111111111111111111111111111111111111111" "${head_sha}"
expect "identical revisions fall back to code" true "${head_sha}" "${head_sha}"

if (( failures > 0 )); then
  printf 'detect-code-changes: %d contract violation(s).\n' "${failures}" >&2
  exit 1
fi

printf 'detect-code-changes: PASS\n'
