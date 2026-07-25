# SDD ledger — plan: docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md

Preflight: starts after the approved storage/provider source split at `e8089dd`.
The source split intentionally removed all newly created storage locks and the
test-only CodeAnalysis catalog entry; this release plan owns atomic regeneration of
the complete eleven-package lock graph. Preserve unrelated untracked files and the
documented Future Pending Work boundary.
Task 1: complete from base `e8089dd` (`build: define eleven-package release train`).

- Added the exact ordered eleven-package TSV catalog and a strict catalog
  contract test.
- Captured focused RED while the catalog was absent; focused GREEN passes 1/1.
- Set the default release version to `1.0.0-rc.1` and generalized exact RVT
  project-reference pinning to every packable project.
- Reviewed the central package catalog: all entries remain referenced, so no
  obsolete infrastructure-only entry was removed; the four required provider
  SDK pins are retained.
- A no-restore SendGrid pack probe emitted exact
  `Rvt.Communication.Abstractions` `[1.0.0-rc.1]`.
- No lock or active-consumer reference changed. Unrelated untracked files remain
  preserved. Full evidence: `task-1-report.md`.

Task 2: pending.
