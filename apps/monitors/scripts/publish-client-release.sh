#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/publish-client-release.sh [options]

Options:
  --target-repo URL   Target Git repository.
                      Default: https://github.com/RVT-Group-LTD/rvt-monitors.git
  --branch NAME      Release branch to create/update.
                      Default: release-candidate
  --base NAME        Target repository base branch.
                      Default: main
  --preserve-history Update from an existing branch/base history instead of
                     creating a fresh orphan release commit.
  --export-dir DIR   Local curated export directory.
                      Default: /private/tmp/rvt-monitors-client-release
  --work-dir DIR     Temporary target clone directory.
                      Default: /private/tmp/rvt-monitors-client-publish
  -h, --help         Show this help.

The script regenerates the curated export before publishing.
USAGE
}

target_repo="https://github.com/RVT-Group-LTD/rvt-monitors.git"
branch="release-candidate"
base_branch="main"
export_dir="/private/tmp/rvt-monitors-client-release"
work_dir="/private/tmp/rvt-monitors-client-publish"
fresh_history="true"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target-repo)
      target_repo="$2"
      shift 2
      ;;
    --branch)
      branch="$2"
      shift 2
      ;;
    --base)
      base_branch="$2"
      shift 2
      ;;
    --preserve-history)
      fresh_history="false"
      shift
      ;;
    --export-dir)
      export_dir="$2"
      shift 2
      ;;
    --work-dir)
      work_dir="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

repo_root="$(git rev-parse --show-toplevel)"

case "$work_dir" in
  ""|"/"|"$repo_root"|"$repo_root"/*)
    echo "Refusing unsafe work directory: $work_dir" >&2
    exit 1
    ;;
esac

"$repo_root/scripts/export-client-release.sh" "$export_dir"

rm -rf "$work_dir"
git clone "$target_repo" "$work_dir"

if [[ "$fresh_history" == "true" ]]; then
  if git -C "$work_dir" show-ref --verify --quiet "refs/heads/$branch"; then
    git -C "$work_dir" checkout --detach
    git -C "$work_dir" branch -D "$branch"
  fi

  git -C "$work_dir" checkout --orphan "$branch"
elif git -C "$work_dir" ls-remote --exit-code --heads origin "$branch" >/dev/null 2>&1; then
  git -C "$work_dir" fetch origin "$branch"
  git -C "$work_dir" checkout -B "$branch" "origin/$branch"
elif git -C "$work_dir" ls-remote --exit-code --heads origin "$base_branch" >/dev/null 2>&1; then
  git -C "$work_dir" fetch origin "$base_branch"
  git -C "$work_dir" checkout -B "$branch" "origin/$base_branch"
else
  git -C "$work_dir" checkout --orphan "$branch"
fi

git -C "$work_dir" rm -rf --ignore-unmatch .

find "$work_dir" -mindepth 1 -maxdepth 1 ! -name '.git' -exec rm -rf {} +
cp -R "$export_dir"/. "$work_dir"/

git -C "$work_dir" add -A

if git -C "$work_dir" diff --cached --quiet; then
  echo "No release changes to commit for $branch"
else
  git -C "$work_dir" commit -m "Deploy RVT monitors release candidate"
fi

if [[ "$fresh_history" == "true" ]]; then
  git -C "$work_dir" push --force-with-lease -u origin "$branch"
else
  git -C "$work_dir" push -u origin "$branch"
fi

echo "Published curated release to $target_repo branch $branch"
