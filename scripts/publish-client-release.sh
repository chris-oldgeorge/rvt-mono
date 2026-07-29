#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/publish-client-release.sh [options]

Options:
  --target-repo URL  Target Git repository.
                     Default: https://github.com/RVT-Group-LTD/rvt-monitors.git
  --branch NAME      Target release branch.
                     Default: release-candidate
  --source-ref REF   Exact source commit or ref.
                     Default: origin/main when available, otherwise HEAD.
  --export-dir DIR   Absolute temporary export directory.
                     Default: /private/tmp/rvt-monorepo-client-release
  --work-dir DIR     Absolute temporary publication clone.
                     Default: /private/tmp/rvt-monorepo-client-publish
  --verify-dir DIR   Absolute temporary post-push verification checkout.
                     Default: /private/tmp/rvt-monorepo-client-verify
  --prepare-only     Prepare and summarize the orphan commit without pushing.
  -h, --help         Show this help.

Publication uses an explicit force-with-lease against the remote branch SHA
observed before export preparation.
USAGE
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
exporter="${repo_root}/scripts/export-client-release.sh"
verifier="${repo_root}/scripts/verify-client-release.sh"

target_repo="https://github.com/RVT-Group-LTD/rvt-monitors.git"
branch="release-candidate"
source_ref=""
export_dir="/private/tmp/rvt-monorepo-client-release"
work_dir="/private/tmp/rvt-monorepo-client-publish"
verify_dir="/private/tmp/rvt-monorepo-client-verify"
prepare_only=0

while (( $# > 0 )); do
  case "$1" in
    --target-repo)
      target_repo="$2"
      shift 2
      ;;
    --branch)
      branch="$2"
      shift 2
      ;;
    --source-ref)
      source_ref="$2"
      shift 2
      ;;
    --export-dir)
      export_dir="$2"
      shift 2
      ;;
    --work-dir)
      work_dir="$2"
      shift 2
      ;;
    --verify-dir)
      verify_dir="$2"
      shift 2
      ;;
    --prepare-only)
      prepare_only=1
      shift
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

if [[ -z "${target_repo}" || -z "${branch}" ]]; then
  printf 'Target repository and branch must be non-empty.\n' >&2
  exit 2
fi
if [[ "${branch}" == -* || "${branch}" == *".."* || "${branch}" == *" "* ]]; then
  printf 'Unsafe target branch: %s\n' "${branch}" >&2
  exit 2
fi

normalize_temporary_dir() {
  local candidate="$1"
  local label="$2"
  local parent
  local normalized

  if [[ "${candidate}" != /* ]]; then
    printf '%s must be an absolute path: %s\n' "${label}" "${candidate}" >&2
    return 1
  fi

  parent="$(dirname "${candidate}")"
  if [[ ! -d "${parent}" ]]; then
    printf '%s parent does not exist: %s\n' "${label}" "${parent}" >&2
    return 1
  fi
  parent="$(cd "${parent}" && pwd -P)"
  normalized="${parent}/$(basename "${candidate}")"

  case "${normalized}" in
    "/"|"${repo_root}"|"${repo_root}"/*)
      printf 'Refusing unsafe %s: %s\n' "${label}" "${normalized}" >&2
      return 1
      ;;
  esac

  printf '%s' "${normalized}"
}

export_dir="$(normalize_temporary_dir "${export_dir}" "export directory")"
work_dir="$(normalize_temporary_dir "${work_dir}" "work directory")"
verify_dir="$(normalize_temporary_dir "${verify_dir}" "verification directory")"

if [[ "${export_dir}" == "${work_dir}" || "${export_dir}" == "${verify_dir}" \
  || "${work_dir}" == "${verify_dir}" ]]; then
  printf 'Export, work, and verification directories must be distinct.\n' >&2
  exit 2
fi

if [[ ! -x "${exporter}" || ! -x "${verifier}" ]]; then
  printf 'Client release exporter or verifier is missing.\n' >&2
  exit 2
fi

remote_ref="refs/heads/${branch}"
remote_sha="$(
  git ls-remote --heads "${target_repo}" "${remote_ref}" \
    | awk 'NR == 1 { print $1 }'
)"

export_args=(
  --export-dir "${export_dir}"
)
if [[ -n "${source_ref}" ]]; then
  export_args+=(--source-ref "${source_ref}")
fi
"${exporter}" "${export_args[@]}"

source_commit="$(
  sed -n 's/.*"sourceCommit":"\([0-9a-f][0-9a-f]*\)".*/\1/p' \
    "${export_dir}/RELEASE_SOURCE.json"
)"
if [[ -z "${source_commit}" ]]; then
  printf 'Export metadata does not contain a source commit.\n' >&2
  exit 1
fi

if [[ -e "${work_dir}" ]]; then
  rm -rf "${work_dir}"
fi
git clone --no-checkout "${target_repo}" "${work_dir}"
git -C "${work_dir}" config user.name \
  "$(git -C "${repo_root}" config user.name || printf 'RVT Release Automation')"
git -C "${work_dir}" config user.email \
  "$(git -C "${repo_root}" config user.email || printf 'release@rvt.invalid')"

git -C "${work_dir}" checkout --orphan prepared-release
git -C "${work_dir}" rm -rf --ignore-unmatch . >/dev/null 2>&1 || true
find "${work_dir}" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -R "${export_dir}/." "${work_dir}/"
git -C "${work_dir}" add -A

prepared_tree="$(git -C "${work_dir}" write-tree)"
remote_tree=""
if [[ -n "${remote_sha}" ]]; then
  remote_tree="$(git -C "${work_dir}" rev-parse "${remote_sha}^{tree}")"
fi

verify_remote_payload() {
  local actual_manifest

  if [[ -e "${verify_dir}" ]]; then
    rm -rf "${verify_dir}"
  fi
  git clone --depth 1 --branch "${branch}" "${target_repo}" "${verify_dir}"
  rm -rf "${verify_dir}/.git"

  "${verifier}" --payload-dir "${verify_dir}"

  actual_manifest="$(mktemp "$(dirname "${verify_dir}")/.rvt-manifest.XXXXXX")"
  find "${verify_dir}" \( -type f -o -type l \) \
    ! -path "${verify_dir}/RELEASE_MANIFEST.txt" \
    -print | sed "s#^${verify_dir}/##" \
    | LC_ALL=C sort > "${actual_manifest}"
  if ! diff -u "${verify_dir}/RELEASE_MANIFEST.txt" \
    "${actual_manifest}"; then
    rm -f "${actual_manifest}"
    printf 'Published release manifest does not match the remote payload.\n' >&2
    return 1
  fi
  rm -f "${actual_manifest}"

  if ! grep -Fq "\"sourceCommit\":\"${source_commit}\"" \
    "${verify_dir}/RELEASE_SOURCE.json"; then
    printf 'Published release source metadata does not match the selected commit.\n' >&2
    return 1
  fi
}

if [[ -n "${remote_tree}" && "${prepared_tree}" == "${remote_tree}" ]]; then
  printf 'Remote %s already matches source %s; no push required.\n' \
    "${branch}" "${source_commit}"
  if (( prepare_only == 0 )); then
    verify_remote_payload
    printf 'Published and verified unchanged client release: %s\n' "${remote_sha}"
  fi
  exit 0
fi

file_count="$(wc -l < "${export_dir}/RELEASE_MANIFEST.txt" | tr -d '[:space:]')"
printf 'Prepared reviewable client release.\n'
printf 'Source commit: %s\n' "${source_commit}"
printf 'Observed remote SHA: %s\n' "${remote_sha:-<branch absent>}"
printf 'Files: %s\n' "${file_count}"
printf 'Changed paths:\n'
if [[ -n "${remote_sha}" ]]; then
  git -C "${work_dir}" diff --name-status "${remote_sha}" "${prepared_tree}" \
    | sed -n '1,200p'
else
  git -C "${work_dir}" ls-tree -r --name-only "${prepared_tree}" \
    | sed -n '1,200p'
fi

git -C "${work_dir}" commit -m \
  "Deploy reviewable monorepo ${source_commit:0:12}" >/dev/null
prepared_commit="$(git -C "${work_dir}" rev-parse HEAD)"
printf 'Prepared release commit: %s\n' "${prepared_commit}"

if (( prepare_only != 0 )); then
  printf 'Prepare-only mode: no remote changes were made.\n'
  exit 0
fi

if [[ -n "${RVT_CLIENT_RELEASE_BEFORE_PUSH_HOOK:-}" ]]; then
  /bin/sh -c "${RVT_CLIENT_RELEASE_BEFORE_PUSH_HOOK}"
fi

lease="--force-with-lease=${remote_ref}:${remote_sha}"
git -C "${work_dir}" push "${lease}" origin "HEAD:${remote_ref}"

published_sha="$(
  git ls-remote --heads "${target_repo}" "${remote_ref}" \
    | awk 'NR == 1 { print $1 }'
)"
if [[ "${published_sha}" != "${prepared_commit}" ]]; then
  printf 'Published SHA does not match the prepared commit.\n' >&2
  exit 1
fi

verify_remote_payload
printf 'Published and verified client release: %s\n' "${published_sha}"
