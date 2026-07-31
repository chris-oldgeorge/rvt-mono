#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "${1:-.}" && pwd -P)"

if ! git -C "${repo_root}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'FAIL: repository root is not a Git worktree: %s\n' "${repo_root}" >&2
  exit 2
fi

allowlisted_paths=(
  "docs/superpowers/specs/2026-07-25-postgresql-only-design.md"
  "docs/superpowers/plans/2026-07-25-postgresql-only.md"
  "docs/history/project-state/2026-07-checkpoint-log.md"
  "docs/reviews/2026-07-28-duplication-legacy-consistency-review.md"
  "docs/reviews/2026-07-30-post-remediation-defect-review.md"
  "project_state.md"
  "scripts/verify-postgresql-only.sh"
)

failures=0

is_allowlisted() {
  local path="$1"
  local allowed_path

  for allowed_path in "${allowlisted_paths[@]}"; do
    if [[ "${path}" == "${allowed_path}" ]]; then
      return 0
    fi
  done

  return 1
}

report_finding() {
  local path="$1"
  local rule="$2"

  printf 'FAIL: %s [rule: %s]\n' "${path}" "${rule}" >&2
  failures=$((failures + 1))
}

while IFS= read -r -d '' path; do
  if is_allowlisted "${path}"; then
    continue
  fi

  if [[ "${path}" == database/sqlserver || "${path}" == database/sqlserver/* ||
        "${path}" == */database/sqlserver || "${path}" == */database/sqlserver/* ]]; then
    report_finding "${path}" "retired database/sqlserver path"
    continue
  fi

  if [[ "${path}" == *.sqlserver.sql ]]; then
    report_finding "${path}" "retired .sqlserver.sql script path"
    continue
  fi

  if [[ "${path}" =~ [sS][qQ][lL][[:space:]_-]?[sS]erver|[mM][sS][sS][qQ][lL] ]]; then
    report_finding "${path}" "retired SQL Server path"
  fi
done < <(git -C "${repo_root}" ls-files -z)

report_content_matches() {
  local rule="$1"
  shift
  local path

  while IFS= read -r -d '' path; do
    if ! is_allowlisted "${path}"; then
      report_finding "${path}" "${rule}"
    fi
  done < <(git -C "${repo_root}" grep -I -l -z "$@" -- || true)
}

report_content_matches \
  "forbidden package Microsoft.EntityFrameworkCore.SqlServer" \
  -F -e "Microsoft.EntityFrameworkCore.SqlServer"
report_content_matches \
  "forbidden package Microsoft.Data.SqlClient" \
  -F -e "Microsoft.Data.SqlClient"
report_content_matches "forbidden C# API UseSqlServer" -F -e "UseSqlServer"
report_content_matches "forbidden C# API SqlConnection" -F -e "SqlConnection"
report_content_matches "forbidden C# API SqlBulkCopy" -F -e "SqlBulkCopy"
report_content_matches \
  "forbidden SQL Server provider or support text" \
  -i -E -e 'Sql([[:space:]]+|[-_])?Server|MSSQL'

while IFS= read -r -d '' path; do
  report_finding "${path}" "provider-conditional EF migration"
done < <(
  git -C "${repo_root}" grep -I -l -z -E \
    -e 'ActiveProvider|ProviderName|IsNpgsql|IsSqlServer' -- \
    ':(glob)apps/portal/**/Migrations/*.cs' \
    ':(glob)apps/portal/**/Migrations/**/*.cs' || true
)

if (( failures > 0 )); then
  printf '%d PostgreSQL-only violation(s) found.\n' "${failures}" >&2
  exit 1
fi

printf 'PostgreSQL-only repository boundary verified.\n'
