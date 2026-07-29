#!/usr/bin/env bash
set -euo pipefail

control_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
control_exporter="${control_root}/scripts/export-client-release.sh"
control_verifier="${control_root}/scripts/verify-client-release.sh"
control_policy="${control_root}/docs/release/client-release-exclusions.txt"
temp_dir="$(mktemp -d)"

cleanup() {
  local status=$?
  rm -rf "${temp_dir}"
  exit "${status}"
}
trap cleanup EXIT

assert_file_content() {
  local path="$1"
  local expected="$2"
  local actual

  actual="$(cat "${path}")"
  if [[ "${actual}" != "${expected}" ]]; then
    printf 'FAIL: %s expected %q, got %q.\n' \
      "${path}" "${expected}" "${actual}" >&2
    exit 1
  fi
}

create_source_repo() {
  local fixture="$1"

  git init -q "${fixture}"
  git -C "${fixture}" config user.name "Release Test"
  git -C "${fixture}" config user.email "release-test@example.invalid"

  mkdir -p \
    "${fixture}/.github/workflows" \
    "${fixture}/apps/portal" \
    "${fixture}/libs/rvt-monitor-common" \
    "${fixture}/eng" \
    "${fixture}/scripts" \
    "${fixture}/tests" \
    "${fixture}/docs/operations" \
    "${fixture}/docs/release" \
    "${fixture}/docs/superpowers/plans" \
    "${fixture}/docs/history/portal" \
    "${fixture}/docs/reviews" \
    "${fixture}/.codex"

  cp "${control_exporter}" "${fixture}/scripts/export-client-release.sh"
  cp "${control_verifier}" "${fixture}/scripts/verify-client-release.sh"
  cp "${control_policy}" "${fixture}/docs/release/client-release-exclusions.txt"
  chmod +x \
    "${fixture}/scripts/export-client-release.sh" \
    "${fixture}/scripts/verify-client-release.sh"

  printf '# Reviewable monorepo\n' > "${fixture}/README.md"
  printf '<Solution />\n' > "${fixture}/Rvt.Mono.slnx"
  printf '<Project />\n' > "${fixture}/Directory.Build.props"
  printf '{"sdk":{"version":"10.0.302"}}\n' > "${fixture}/global.json"
  printf 'name: verify\n' > "${fixture}/.github/workflows/verify.yml"
  printf 'committed\n' > "${fixture}/apps/portal/value.txt"
  printf 'shared\n' > "${fixture}/libs/rvt-monitor-common/value.txt"
  printf 'engineering\n' > "${fixture}/eng/policy.txt"
  printf 'test\n' > "${fixture}/tests/example.test"
  printf '# Operations\n' > "${fixture}/docs/operations/runbook.md"

  printf 'internal\n' > "${fixture}/AGENTS.md"
  printf 'internal\n' > "${fixture}/project_state.md"
  printf 'internal\n' > "${fixture}/.codex/settings.json"
  printf 'internal\n' > "${fixture}/docs/superpowers/plans/internal.md"
  printf 'internal\n' > "${fixture}/docs/history/portal/internal.md"
  printf 'internal\n' > "${fixture}/docs/reviews/internal.md"

  git -C "${fixture}" add -A
  GIT_AUTHOR_DATE="2026-07-29T12:34:56+03:00" \
    GIT_COMMITTER_DATE="2026-07-29T12:34:56+03:00" \
    git -C "${fixture}" commit -q -m "fixture source"
}

source_repo="${temp_dir}/source"
create_source_repo "${source_repo}"
source_commit="$(git -C "${source_repo}" rev-parse HEAD)"
source_timestamp="$(git -C "${source_repo}" show -s --format=%cI "${source_commit}")"

printf 'dirty\n' > "${source_repo}/apps/portal/value.txt"
printf 'untracked\n' > "${source_repo}/apps/portal/local-only.txt"

export_one="${temp_dir}/export-one"
(
  cd "${source_repo}"
  scripts/export-client-release.sh \
    --source-ref "${source_commit}" \
    --export-dir "${export_one}"
)

assert_file_content "${export_one}/apps/portal/value.txt" "committed"
if [[ -e "${export_one}/apps/portal/local-only.txt" ]]; then
  printf 'FAIL: untracked working-tree content was exported.\n' >&2
  exit 1
fi

required_paths=(
  .github/workflows/verify.yml
  apps/portal/value.txt
  libs/rvt-monitor-common/value.txt
  eng/policy.txt
  tests/example.test
  docs/operations/runbook.md
)
for required_path in "${required_paths[@]}"; do
  if [[ ! -f "${export_one}/${required_path}" ]]; then
    printf 'FAIL: expected monorepo path is missing: %s\n' "${required_path}" >&2
    exit 1
  fi
done

blocked_paths=(
  AGENTS.md
  project_state.md
  .codex/settings.json
  docs/superpowers/plans/internal.md
  docs/history/portal/internal.md
  docs/reviews/internal.md
  docs/release/client-release-exclusions.txt
  scripts/export-client-release.sh
  scripts/verify-client-release.sh
)
for blocked_path in "${blocked_paths[@]}"; do
  if [[ -e "${export_one}/${blocked_path}" ]]; then
    printf 'FAIL: excluded path was exported: %s\n' "${blocked_path}" >&2
    exit 1
  fi
done

if ! grep -Fq "\"sourceCommit\":\"${source_commit}\"" \
  "${export_one}/RELEASE_SOURCE.json"; then
  printf 'FAIL: release metadata does not contain the resolved source commit.\n' >&2
  exit 1
fi
if ! grep -Fq "\"sourceCommitTimestamp\":\"${source_timestamp}\"" \
  "${export_one}/RELEASE_SOURCE.json"; then
  printf 'FAIL: release metadata does not contain the source timestamp.\n' >&2
  exit 1
fi

if ! LC_ALL=C sort -c "${export_one}/RELEASE_MANIFEST.txt"; then
  printf 'FAIL: release manifest is not sorted.\n' >&2
  exit 1
fi
if grep -Fxq RELEASE_MANIFEST.txt "${export_one}/RELEASE_MANIFEST.txt"; then
  printf 'FAIL: release manifest must not list itself.\n' >&2
  exit 1
fi

find "${export_one}" -type f \
  | sed "s#^${export_one}/##" \
  | grep -Fvx RELEASE_MANIFEST.txt \
  | LC_ALL=C sort > "${temp_dir}/expected-manifest.txt"
if ! diff -u "${temp_dir}/expected-manifest.txt" \
  "${export_one}/RELEASE_MANIFEST.txt"; then
  printf 'FAIL: release manifest does not match exported files.\n' >&2
  exit 1
fi

export_two="${temp_dir}/export-two"
(
  cd "${source_repo}"
  scripts/export-client-release.sh \
    --source-ref "${source_commit}" \
    --export-dir "${export_two}"
)
if ! diff -qr "${export_one}" "${export_two}"; then
  printf 'FAIL: repeated exports of one commit are not deterministic.\n' >&2
  exit 1
fi

if (
  cd "${source_repo}"
  scripts/export-client-release.sh \
    --source-ref "${source_commit}" \
    --export-dir "${source_repo}/unsafe-output"
) >/dev/null 2>&1; then
  printf 'FAIL: exporter accepted an output directory inside the source repository.\n' >&2
  exit 1
fi

printf 'Revision-pinned client release export fixtures verified.\n'
