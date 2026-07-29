#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/export-client-release.sh [options]

Options:
  --source-ref REF  Exact commit or ref to export.
                    Default: origin/main when available, otherwise HEAD.
  --export-dir DIR  Absolute output directory outside the source repository.
                    Default: /private/tmp/rvt-monorepo-client-release
  -h, --help        Show this help.

The exporter reads committed Git objects. Working-tree edits and untracked
files cannot enter the payload.
USAGE
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
git_root="$(git -C "${repo_root}" rev-parse --show-toplevel)"
git_root="$(cd "${git_root}" && pwd -P)"
if [[ "${repo_root}" != "${git_root}" ]]; then
  printf 'Release exporter must live at scripts/export-client-release.sh.\n' >&2
  exit 2
fi

policy_file="${repo_root}/docs/release/client-release-exclusions.txt"
verifier="${repo_root}/scripts/verify-client-release.sh"
source_ref=""
export_dir="/private/tmp/rvt-monorepo-client-release"

while (( $# > 0 )); do
  case "$1" in
    --source-ref)
      if (( $# < 2 )); then
        printf 'Missing value for --source-ref.\n' >&2
        exit 2
      fi
      source_ref="$2"
      shift 2
      ;;
    --export-dir)
      if (( $# < 2 )); then
        printf 'Missing value for --export-dir.\n' >&2
        exit 2
      fi
      export_dir="$2"
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

if [[ -z "${source_ref}" ]]; then
  if git -C "${repo_root}" rev-parse --verify --quiet "origin/main^{commit}" >/dev/null; then
    source_ref="origin/main"
  else
    source_ref="HEAD"
  fi
fi

if [[ "${export_dir}" != /* ]]; then
  printf 'Export directory must be absolute: %s\n' "${export_dir}" >&2
  exit 2
fi

export_parent="$(dirname "${export_dir}")"
if [[ ! -d "${export_parent}" ]]; then
  printf 'Export parent directory does not exist: %s\n' "${export_parent}" >&2
  exit 2
fi
export_parent="$(cd "${export_parent}" && pwd -P)"
export_dir="${export_parent}/$(basename "${export_dir}")"

case "${export_dir}" in
  "/"|"${repo_root}"|"${repo_root}"/*)
    printf 'Refusing unsafe export directory: %s\n' "${export_dir}" >&2
    exit 2
    ;;
esac

if [[ ! -f "${policy_file}" || ! -x "${verifier}" ]]; then
  printf 'Client release policy or verifier is missing.\n' >&2
  exit 2
fi

if ! source_commit="$(git -C "${repo_root}" rev-parse --verify "${source_ref}^{commit}" 2>/dev/null)"; then
  printf 'Source ref does not resolve to a commit: %s\n' "${source_ref}" >&2
  exit 2
fi
source_timestamp="$(git -C "${repo_root}" show -s --format=%cI "${source_commit}")"

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

staging_dir="$(mktemp -d "${export_parent}/.rvt-client-export.XXXXXX")"
cleanup() {
  local status=$?
  if [[ -d "${staging_dir}" ]]; then
    rm -rf "${staging_dir}"
  fi
  exit "${status}"
}
trap cleanup EXIT

git -C "${repo_root}" archive --format=tar "${source_commit}" \
  | tar -xf - -C "${staging_dir}"

while IFS= read -r -d '' path; do
  relative_path="${path#"${staging_dir}/"}"
  if matches_exclusion "${relative_path}"; then
    rm -rf "${path}"
  fi
done < <(find "${staging_dir}" -depth -mindepth 1 -print0)

find "${staging_dir}" -depth -type d -empty -delete

cat > "${staging_dir}/RELEASE_SOURCE.json" <<EOF
{"formatVersion":1,"sourceCommit":"${source_commit}","sourceCommitTimestamp":"${source_timestamp}","targetBranch":"release-candidate"}
EOF

find "${staging_dir}" \( -type f -o -type l \) \
  ! -path "${staging_dir}/RELEASE_MANIFEST.txt" \
  -print | sed "s#^${staging_dir}/##" \
  | LC_ALL=C sort \
  > "${staging_dir}/RELEASE_MANIFEST.txt"

"${verifier}" --payload-dir "${staging_dir}"

if [[ -e "${export_dir}" ]]; then
  rm -rf "${export_dir}"
fi
mv "${staging_dir}" "${export_dir}"
staging_dir=""

file_count="$(wc -l < "${export_dir}/RELEASE_MANIFEST.txt" | tr -d '[:space:]')"
printf 'Created reviewable monorepo export.\n'
printf 'Source commit: %s\n' "${source_commit}"
printf 'Files: %s\n' "${file_count}"
printf 'Export directory: %s\n' "${export_dir}"
