#!/usr/bin/env bash
set -euo pipefail

control_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_dir="$(mktemp -d)"

cleanup() {
  local status=$?
  rm -rf "${temp_dir}"
  exit "${status}"
}
trap cleanup EXIT

copy_release_tools() {
  local fixture="$1"

  mkdir -p "${fixture}/scripts" "${fixture}/docs/release"
  cp "${control_root}/scripts/export-client-release.sh" \
    "${fixture}/scripts/export-client-release.sh"
  cp "${control_root}/scripts/verify-client-release.sh" \
    "${fixture}/scripts/verify-client-release.sh"
  cp "${control_root}/scripts/publish-client-release.sh" \
    "${fixture}/scripts/publish-client-release.sh"
  cp "${control_root}/docs/release/client-release-exclusions.txt" \
    "${fixture}/docs/release/client-release-exclusions.txt"
  chmod +x "${fixture}/scripts/"*.sh
}

create_source_repo() {
  local fixture="$1"

  git init -q "${fixture}"
  git -C "${fixture}" config user.name "Release Test"
  git -C "${fixture}" config user.email "release-test@example.invalid"

  copy_release_tools "${fixture}"
  mkdir -p \
    "${fixture}/.github/workflows" \
    "${fixture}/apps/portal" \
    "${fixture}/libs/rvt-monitor-common" \
    "${fixture}/eng" \
    "${fixture}/tests" \
    "${fixture}/docs/operations"

  printf '# Reviewable monorepo\n' > "${fixture}/README.md"
  printf '<Solution />\n' > "${fixture}/Rvt.Mono.slnx"
  printf '<Project />\n' > "${fixture}/Directory.Build.props"
  printf '{"sdk":{"version":"10.0.302"}}\n' > "${fixture}/global.json"
  printf 'name: verify\n' > "${fixture}/.github/workflows/verify.yml"
  printf 'version one\n' > "${fixture}/apps/portal/value.txt"
  printf 'shared\n' > "${fixture}/libs/rvt-monitor-common/value.txt"
  printf 'engineering\n' > "${fixture}/eng/policy.txt"
  printf 'test\n' > "${fixture}/tests/example.test"
  printf '# Operations\n' > "${fixture}/docs/operations/runbook.md"

  git -C "${fixture}" add -A
  git -C "${fixture}" commit -q -m "source version one"
}

create_target_remote() {
  local remote="$1"
  local seed="${temp_dir}/target-seed"

  git init -q --bare "${remote}"
  git init -q "${seed}"
  git -C "${seed}" config user.name "Target Test"
  git -C "${seed}" config user.email "target-test@example.invalid"
  printf '# Old monitor-only release\n' > "${seed}/README.md"
  git -C "${seed}" add README.md
  git -C "${seed}" commit -q -m "old client release"
  git -C "${seed}" branch -M release-candidate
  git -C "${seed}" remote add origin "${remote}"
  git -C "${seed}" push -q -u origin release-candidate
  git --git-dir="${remote}" symbolic-ref HEAD refs/heads/release-candidate
}

publisher_help="$("${control_root}/scripts/publish-client-release.sh" --help)"
if [[ "${publisher_help}" != *"https://github.com/RVT-Group-LTD/rvt-monitors.git"* \
  || "${publisher_help}" != *"release-candidate"* ]]; then
  printf 'FAIL: publisher help does not document the default target and branch.\n' >&2
  exit 1
fi

source_repo="${temp_dir}/source"
target_remote="${temp_dir}/target.git"
create_source_repo "${source_repo}"
create_target_remote "${target_remote}"
source_commit="$(git -C "${source_repo}" rev-parse HEAD)"
initial_remote_sha="$(git --git-dir="${target_remote}" rev-parse refs/heads/release-candidate)"

prepare_export="${temp_dir}/prepare-export"
prepare_work="${temp_dir}/prepare-work"
prepare_verify="${temp_dir}/prepare-verify"
(
  cd "${source_repo}"
  scripts/publish-client-release.sh \
    --target-repo "${target_remote}" \
    --branch release-candidate \
    --source-ref "${source_commit}" \
    --export-dir "${prepare_export}" \
    --work-dir "${prepare_work}" \
    --verify-dir "${prepare_verify}" \
    --prepare-only
)

after_prepare_sha="$(git --git-dir="${target_remote}" rev-parse refs/heads/release-candidate)"
if [[ "${after_prepare_sha}" != "${initial_remote_sha}" ]]; then
  printf 'FAIL: prepare-only changed the target branch.\n' >&2
  exit 1
fi
if ! grep -Fq "\"sourceCommit\":\"${source_commit}\"" \
  "${prepare_work}/RELEASE_SOURCE.json"; then
  printf 'FAIL: prepare-only did not forward the selected source commit.\n' >&2
  exit 1
fi
if [[ "$(git -C "${prepare_work}" rev-list --parents -n 1 HEAD | wc -w | tr -d ' ')" != "1" ]]; then
  printf 'FAIL: prepared release commit is not an orphan root commit.\n' >&2
  exit 1
fi

publish_export="${temp_dir}/publish-export"
publish_work="${temp_dir}/publish-work"
publish_verify="${temp_dir}/publish-verify"
publish_output="$(
  cd "${source_repo}"
  scripts/publish-client-release.sh \
    --target-repo "${target_remote}" \
    --branch release-candidate \
    --source-ref "${source_commit}" \
    --export-dir "${publish_export}" \
    --work-dir "${publish_work}" \
    --verify-dir "${publish_verify}"
)"

published_sha="$(git --git-dir="${target_remote}" rev-parse refs/heads/release-candidate)"
if [[ "${published_sha}" == "${initial_remote_sha}" ]]; then
  printf 'FAIL: release publication did not update the target branch.\n' >&2
  exit 1
fi
if [[ "$(git --git-dir="${target_remote}" rev-list --parents -n 1 "${published_sha}" | wc -w | tr -d ' ')" != "1" ]]; then
  printf 'FAIL: published release commit is not an orphan root commit.\n' >&2
  exit 1
fi
if [[ "${publish_output}" != *"Published and verified"* ]]; then
  printf 'FAIL: publisher did not report post-push verification.\n' >&2
  exit 1
fi
if ! grep -Fq "\"sourceCommit\":\"${source_commit}\"" \
  "${publish_verify}/RELEASE_SOURCE.json"; then
  printf 'FAIL: post-push verification checkout has wrong source metadata.\n' >&2
  exit 1
fi

no_change_output="$(
  cd "${source_repo}"
  scripts/publish-client-release.sh \
    --target-repo "${target_remote}" \
    --branch release-candidate \
    --source-ref "${source_commit}" \
    --export-dir "${temp_dir}/no-change-export" \
    --work-dir "${temp_dir}/no-change-work" \
    --verify-dir "${temp_dir}/no-change-verify"
)"
after_no_change_sha="$(git --git-dir="${target_remote}" rev-parse refs/heads/release-candidate)"
if [[ "${after_no_change_sha}" != "${published_sha}" ]]; then
  printf 'FAIL: identical payload publication changed the remote commit.\n' >&2
  exit 1
fi
if [[ "${no_change_output}" != *"already matches"* ]]; then
  printf 'FAIL: identical payload did not report no-change behavior.\n' >&2
  exit 1
fi

printf 'version two\n' > "${source_repo}/apps/portal/value.txt"
git -C "${source_repo}" add apps/portal/value.txt
git -C "${source_repo}" commit -q -m "source version two"
source_commit_two="$(git -C "${source_repo}" rev-parse HEAD)"

hook_script="${temp_dir}/advance-remote.sh"
cat > "${hook_script}" <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
remote="$1"
work="$2"
git clone -q --branch release-candidate "${remote}" "${work}"
git -C "${work}" config user.name "Concurrent Test"
git -C "${work}" config user.email "concurrent-test@example.invalid"
printf 'concurrent update\n' > "${work}/CONCURRENT.txt"
git -C "${work}" add CONCURRENT.txt
git -C "${work}" commit -q -m "concurrent target update"
git -C "${work}" push -q origin release-candidate
HOOK
chmod +x "${hook_script}"

set +e
concurrent_output="$(
  cd "${source_repo}"
  RVT_CLIENT_RELEASE_BEFORE_PUSH_HOOK="${hook_script} ${target_remote} ${temp_dir}/concurrent-work" \
    scripts/publish-client-release.sh \
      --target-repo "${target_remote}" \
      --branch release-candidate \
      --source-ref "${source_commit_two}" \
      --export-dir "${temp_dir}/race-export" \
      --work-dir "${temp_dir}/race-work" \
      --verify-dir "${temp_dir}/race-verify" 2>&1
)"
concurrent_status=$?
set -e

if (( concurrent_status == 0 )); then
  printf 'FAIL: publisher overwrote a concurrent remote update.\n' >&2
  exit 1
fi
race_remote_sha="$(git --git-dir="${target_remote}" rev-parse refs/heads/release-candidate)"
if ! git --git-dir="${target_remote}" show "${race_remote_sha}:CONCURRENT.txt" \
  >/dev/null 2>&1; then
  printf 'FAIL: concurrent remote update was not preserved.\n' >&2
  exit 1
fi
if [[ "${concurrent_output}" != *"stale info"* \
  && "${concurrent_output}" != *"force-with-lease"* \
  && "${concurrent_output}" != *"rejected"* ]]; then
  printf 'FAIL: lease rejection was not visible in publisher output.\n' >&2
  exit 1
fi

printf 'Lease-protected client release publication fixtures verified.\n'
