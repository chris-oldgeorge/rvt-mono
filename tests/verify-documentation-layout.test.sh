#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
expected_moves=85

output="$("$repo_root/scripts/verify-documentation-layout.sh")"
printf '%s\n' "$output"
grep -Fqx \
  "Documentation layout verification passed ($expected_moves moves, 6 retained entry points)." \
  <<<"$output"

fixture_root="$(mktemp -d)"

cleanup() {
  rm -rf "$fixture_root"
}
trap cleanup EXIT

mkdir -p \
  "$fixture_root/scripts" \
  "$fixture_root/docs/architecture/reporting" \
  "$fixture_root/docs/database/monitors" \
  "$fixture_root/docs/development/portal" \
  "$fixture_root/docs/development" \
  "$fixture_root/docs/history/monitors/evidence" \
  "$fixture_root/docs/imports" \
  "$fixture_root/docs/modules/monitors" \
  "$fixture_root/docs/operations/monitors" \
  "$fixture_root/docs/release/monitors" \
  "$fixture_root/docs/reviews" \
  "$fixture_root/apps/monitors" \
  "$fixture_root/apps/portal" \
  "$fixture_root/libs/rvt-monitor-common"

cp "$repo_root/scripts/verify-documentation-layout.sh" \
  "$fixture_root/scripts/verify-documentation-layout.sh"

cat > "$fixture_root/docs/documentation-move-manifest.md" <<'EOF'
# Documentation Move Manifest

| Source | Destination |
| --- | --- |
EOF

for index in $(seq 1 "$expected_moves"); do
  mkdir -p "$fixture_root/docs/generated"
  touch "$fixture_root/docs/generated/move-$index.md"
  printf '| `apps/monitors/docs/legacy-%d.md` | `docs/generated/move-%d.md` |\n' \
    "$index" "$index" >> "$fixture_root/docs/documentation-move-manifest.md"
done

cat > "$fixture_root/README.md" <<'EOF'
[Standard](docs/development/engineering-standards.md)
[Guide](docs/development/engineering-standards-enforcement.md)
[Report](docs/reviews/2026-07-27-engineering-standards-enforcement-report.md)
EOF

cat > "$fixture_root/docs/index.md" <<'EOF'
[Reporting](architecture/reporting/architecture.md)
[Portal](development/portal/development-guidelines.md)
[Containers](operations/monitors/container-builds.md)
[Release](release/monitors/client-release-runbook.md)
[Database](database/monitors/monitor-data-access-migration.md)
[Timers](modules/monitors/monitor-timer-triggers.md)
[Evidence](history/monitors/evidence/2026-07-17-rvt-common-monitor-source-removal.md)
[Imports](imports/source-manifest.md)
[Standard](development/engineering-standards.md)
[Guide](development/engineering-standards-enforcement.md)
[Report](reviews/2026-07-27-engineering-standards-enforcement-report.md)
EOF

cat > \
  "$fixture_root/docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md" <<'EOF'
[RVT Engineering Standards](../development/engineering-standards.md)
EOF

touch \
  "$fixture_root/apps/monitors/README.md" \
  "$fixture_root/apps/monitors/AGENTS.md" \
  "$fixture_root/apps/portal/README.md" \
  "$fixture_root/apps/portal/AGENTS.md" \
  "$fixture_root/libs/rvt-monitor-common/README.md" \
  "$fixture_root/docs/architecture/reporting/architecture.md" \
  "$fixture_root/docs/database/monitors/monitor-data-access-migration.md" \
  "$fixture_root/docs/development/portal/development-guidelines.md" \
  "$fixture_root/docs/development/engineering-standards.md" \
  "$fixture_root/docs/development/engineering-standards-enforcement.md" \
  "$fixture_root/docs/history/monitors/evidence/2026-07-17-rvt-common-monitor-source-removal.md" \
  "$fixture_root/docs/imports/source-manifest.md" \
  "$fixture_root/docs/modules/monitors/monitor-timer-triggers.md" \
  "$fixture_root/docs/operations/monitors/container-builds.md" \
  "$fixture_root/docs/release/monitors/client-release-runbook.md" \
  "$fixture_root/docs/reviews/2026-07-27-engineering-standards-enforcement-report.md"

(
  cd "$fixture_root"
  git init --quiet
  git add .
)

"$fixture_root/scripts/verify-documentation-layout.sh" >/dev/null

assert_mutation_rejected() {
  local label="$1"
  local relative_path="$2"
  local needle="$3"
  local replacement="$4"
  local target="$fixture_root/$relative_path"
  local original="$target.original"

  cp "$target" "$original"
  sed "s|$needle|$replacement|" "$original" > "$target"

  if "$fixture_root/scripts/verify-documentation-layout.sh" >/dev/null 2>&1; then
    printf 'FAIL: documentation guard accepted %s mutation.\n' "$label" >&2
    exit 1
  fi

  mv "$original" "$target"
}

assert_mutation_rejected \
  "removed root standard link" \
  "README.md" \
  'docs/development/engineering-standards.md' \
  'docs/development/removed.md'
assert_mutation_rejected \
  "removed root enforcement-guide link" \
  "README.md" \
  'docs/development/engineering-standards-enforcement.md' \
  'docs/development/removed.md'
assert_mutation_rejected \
  "removed root report link" \
  "README.md" \
  'docs/reviews/2026-07-27-engineering-standards-enforcement-report.md' \
  'docs/reviews/removed.md'
assert_mutation_rejected \
  "removed documentation-index standard link" \
  "docs/index.md" \
  'development/engineering-standards.md' \
  'development/removed.md'
assert_mutation_rejected \
  "removed documentation-index enforcement-guide link" \
  "docs/index.md" \
  'development/engineering-standards-enforcement.md' \
  'development/removed.md'
assert_mutation_rejected \
  "removed documentation-index report link" \
  "docs/index.md" \
  'reviews/2026-07-27-engineering-standards-enforcement-report.md' \
  'reviews/removed.md'
assert_mutation_rejected \
  "removed authoritative remediation-review standard link" \
  "docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md" \
  '../development/engineering-standards.md' \
  '../development/removed.md'

mv \
  "$fixture_root/docs/development/engineering-standards.md" \
  "$fixture_root/docs/development/moved-engineering-standards.md"
if "$fixture_root/scripts/verify-documentation-layout.sh" >/dev/null 2>&1; then
  printf 'FAIL: documentation guard accepted a moved normative standard.\n' >&2
  exit 1
fi

printf 'Documentation entry-point link mutations rejected.\n'
