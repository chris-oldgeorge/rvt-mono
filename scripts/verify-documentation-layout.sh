#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest_path="$repo_root/docs/documentation-move-manifest.md"
expected_manifest_entries=122
failures=0

report_failure() {
  printf 'ERROR: %s\n' "$1" >&2
  failures=$((failures + 1))
}

contains_path() {
  local wanted="$1"
  shift
  local candidate

  for candidate in "$@"; do
    if [[ "$candidate" == "$wanted" ]]; then
      return 0
    fi
  done

  return 1
}

normalize_path() {
  local raw_path="$1"
  local old_ifs="$IFS"
  local part
  local normalized_path=""
  local -a parts=()
  local -a normalized_parts=()

  IFS='/'
  read -r -a parts <<< "$raw_path"
  IFS="$old_ifs"

  for part in "${parts[@]}"; do
    case "$part" in
      ''|.) ;;
      ..)
        if ((${#normalized_parts[@]} > 0)); then
          unset 'normalized_parts[${#normalized_parts[@]}-1]'
        fi
        ;;
      *) normalized_parts+=("$part") ;;
    esac
  done

  old_ifs="$IFS"
  IFS='/'
  normalized_path="${normalized_parts[*]}"
  IFS="$old_ifs"
  printf '%s\n' "$normalized_path"
}

if [[ ! -f "$manifest_path" ]]; then
  report_failure "missing move manifest: docs/documentation-move-manifest.md"
  exit 1
fi

sources=("__manifest_source_sentinel__")
destinations=("__manifest_destination_sentinel__")
while IFS=$'\t' read -r source destination; do
  [[ -n "$source" && -n "$destination" ]] || continue

  if [[ "$source" != apps/* && "$source" != libs/* && "$source" != services/* ]]; then
    report_failure "manifest source is outside a module root: $source"
  fi
  if [[ "$destination" != docs/* ]]; then
    report_failure "manifest destination is outside docs/: $destination"
  fi
  if contains_path "$source" "${sources[@]}"; then
    report_failure "duplicate manifest source: $source"
  fi
  if contains_path "$destination" "${destinations[@]}"; then
    report_failure "duplicate manifest destination: $destination"
  fi

  sources+=("$source")
  destinations+=("$destination")
done < <(sed -n 's/^| `\([^`]*\)` | `\([^`]*\)` |$/\1\	\2/p' "$manifest_path")

unset 'sources[0]'
unset 'destinations[0]'

if ((${#sources[@]} != expected_manifest_entries)); then
  report_failure "manifest has ${#sources[@]} move entries; expected $expected_manifest_entries"
fi

retained_paths=(
  "README.md"
  "apps/monitors/README.md"
  "apps/monitors/AGENTS.md"
  "apps/portal/README.md"
  "apps/portal/AGENTS.md"
  "libs/rvt-monitor-common/README.md"
  "services/reporting/README.md"
)

documentation_index="docs/index.md"
index_targets=(
  "architecture/reporting/architecture.md"
  "development/portal/development-guidelines.md"
  "operations/monitors/container-builds.md"
  "release/monitors/client-release-runbook.md"
  "database/monitors/monitor-data-access-migration.md"
  "modules/monitors/monitor-timer-triggers.md"
  "history/monitors/project_state.md"
  "imports/source-manifest.md"
)

if [[ ! -f "$repo_root/$documentation_index" ]]; then
  report_failure "missing documentation index: $documentation_index"
else
  for index_target in "${index_targets[@]}"; do
    if ! rg --quiet --fixed-strings -- "]($index_target)" "$repo_root/$documentation_index"; then
      report_failure "documentation index is missing link: $index_target"
    fi
  done
fi

for retained_path in "${retained_paths[@]}"; do
  if [[ ! -f "$repo_root/$retained_path" ]]; then
    report_failure "missing retained entry point: $retained_path"
  fi
  if contains_path "$retained_path" "${sources[@]}"; then
    report_failure "retained entry point must not appear in the move manifest: $retained_path"
  fi
done

legacy_count=0
unmapped_count=0
while IFS= read -r markdown_path; do
  markdown_path="${markdown_path#./}"

  if contains_path "$markdown_path" "${retained_paths[@]}"; then
    continue
  fi

  legacy_count=$((legacy_count + 1))
  if ! contains_path "$markdown_path" "${sources[@]}"; then
    printf 'ERROR: non-entry Markdown is absent from the move manifest: %s\n' "$markdown_path" >&2
    unmapped_count=$((unmapped_count + 1))
  fi
done < <(
  cd "$repo_root"
  git ls-files --cached --others --exclude-standard |
    awk 'tolower($0) ~ /\.md$/ && $0 ~ /^(apps|libs|services)\// { print }' |
    sort
)

if ((legacy_count > 0)); then
  report_failure "$legacy_count non-entry Markdown file(s) remain below module roots"
fi
if ((unmapped_count > 0)); then
  report_failure "$unmapped_count non-entry Markdown file(s) have no manifest mapping"
fi

missing_destination_count=0
duplicate_copy_count=0
missing_sources=("__missing_source_sentinel__")
missing_module_relative_sources=("__missing_module_relative_source_sentinel__")
for index in "${!sources[@]}"; do
  source="${sources[$index]}"
  destination="${destinations[$index]}"

  if [[ ! -f "$repo_root/$destination" ]]; then
    missing_destination_count=$((missing_destination_count + 1))
  fi
  if [[ -f "$repo_root/$source" && -f "$repo_root/$destination" ]]; then
    duplicate_copy_count=$((duplicate_copy_count + 1))
  fi
  if [[ ! -f "$repo_root/$source" ]]; then
    missing_sources+=("$source")

    module_relative_source="$source"
    case "$module_relative_source" in
      apps/monitors/*) module_relative_source="${module_relative_source#apps/monitors/}" ;;
      apps/portal/*) module_relative_source="${module_relative_source#apps/portal/}" ;;
      libs/rvt-monitor-common/*) module_relative_source="${module_relative_source#libs/rvt-monitor-common/}" ;;
      services/reporting/*) module_relative_source="${module_relative_source#services/reporting/}" ;;
    esac
    if [[ "$module_relative_source" == docs/* ]]; then
      missing_module_relative_sources+=("$module_relative_source")
    fi
  fi
done

unset 'missing_module_relative_sources[0]'

if ((missing_destination_count > 0)); then
  report_failure "$missing_destination_count manifest destination(s) are missing below docs/"
fi
if ((duplicate_copy_count > 0)); then
  report_failure "$duplicate_copy_count manifest item(s) exist at both source and destination"
fi

stale_reference_count=0
for source in "${missing_sources[@]}"; do
  [[ "$source" == "__missing_source_sentinel__" ]] && continue
  if (
    cd "$repo_root"
    rg --hidden --quiet --fixed-strings \
      --glob '!.git/**' \
      --glob '!.superpowers/sdd/**' \
      --glob '!docs/documentation-move-manifest.md' \
      -- "$source" .
  ); then
    printf 'ERROR: stale reference uses old document path: %s\n' "$source" >&2
    stale_reference_count=$((stale_reference_count + 1))
  fi
done

for module_relative_source in "${missing_module_relative_sources[@]}"; do
  if (
    cd "$repo_root"
    git grep --quiet --fixed-strings -I \
      -e "$module_relative_source" -- . \
      ':(exclude).superpowers/sdd/**' \
      ':(exclude)docs/documentation-move-manifest.md' \
      ':(exclude)docs/history/**'
  ); then
    printf 'ERROR: stale module-relative reference uses old document path: %s\n' \
      "$module_relative_source" >&2
    stale_reference_count=$((stale_reference_count + 1))
  fi
done

while IFS= read -r link_record; do
  link_file="${link_record%%:*}"
  link_file="${link_file#./}"
  link_match="${link_record#*:}"
  link_match="${link_match#*:}"
  link_target="${link_match#*](}"
  link_target="${link_target%)}"
  link_target="${link_target%%#*}"
  link_target="${link_target%%\?*}"
  link_target="${link_target#<}"
  link_target="${link_target%>}"

  case "$link_target" in
    ''|http://*|https://*|mailto:*|'#'*) continue ;;
  esac

  link_target="${link_target#/}"
  resolved_target="$(normalize_path "$(dirname "$link_file")/$link_target")"
  if contains_path "$resolved_target" "${missing_sources[@]}"; then
    printf 'ERROR: stale Markdown link in %s resolves to old path %s\n' \
      "$link_file" "$resolved_target" >&2
    stale_reference_count=$((stale_reference_count + 1))
  fi
done < <(
  cd "$repo_root"
  rg --hidden --ignore-case --no-heading --line-number --only-matching \
    --glob '*.md' --glob '*.MD' \
    --glob '!.git/**' \
    --glob '!.superpowers/sdd/**' \
    --glob '!docs/documentation-move-manifest.md' \
    '\]\([^)]*\.md(#[^)]*)?\)' . || true
)

if ((stale_reference_count > 0)); then
  report_failure "$stale_reference_count stale old-document reference(s) remain"
fi

if ((failures > 0)); then
  printf 'Documentation layout verification failed with %d issue group(s).\n' "$failures" >&2
  exit 1
fi

printf 'Documentation layout verification passed (%d moves, %d retained entry points).\n' \
  "${#sources[@]}" "${#retained_paths[@]}"
