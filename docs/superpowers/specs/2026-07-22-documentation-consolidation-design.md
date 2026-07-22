# Documentation Consolidation Design

## Goal

Make `docs/` the repository's single documentation home while preserving module
provenance, historical plans/evidence, and practical module entry points.

## Target Layout

```text
docs/
  architecture/
  database/
  development/
  operations/
  release/
  modules/
    monitors/
    portal/
    rvt-monitor-common/
    reporting/
  history/
    monitors/{plans,specs,evidence,schema}/
    portal/plans/
    reporting/plans/
  imports/
  index.md
```

## Scope

Move all module documentation Markdown into the root structure, including
historical plans, specifications, evidence, and schema inventories. Preserve
each source module in the destination path so documentation origin remains
clear.

The root `README.md`, module entry `README.md` files, and all `AGENTS.md`
instruction files remain in place. They become concise entry points linking to
their root documentation locations. Markdown links, source comments, scripts,
and project-state references are updated to their new root-relative targets.

## Rules

- Do not delete historical documentation; move it under `docs/history`.
- Do not merge unrelated content into a single large document. Consolidation
  means one root information architecture, not loss of document boundaries.
- Retain module-specific operational documentation under `docs/modules/<name>`.
- Add `docs/index.md` as the navigable starting point, grouped by current
  architecture, development, operations/release, database, modules, and history.
- Update current-state references in `project_state.md` and preserve all
  source-code/runtime files unchanged except for documentation links.

## Verification

- No non-entry Markdown file remains below `apps/`, `libs/`, or `services/`.
- Every moved file exists exactly once below `docs/`.
- `rg` detects no stale Markdown links to the old paths.
- Root and module READMEs link to valid documentation targets.
- `git diff --check` is clean.
