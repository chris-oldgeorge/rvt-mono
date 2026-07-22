# Project State

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Documents/rvt-mono`
- Status: Task 1 provenance and structural guard established; source import has not started.
- Design: `docs/superpowers/specs/2026-07-22-rvt-mono-repository-design.md`
- Plan: `docs/superpowers/plans/2026-07-22-rvt-mono-repository-bootstrap.md`
- Requested outcome: fresh unified Git history and a shared root solution for
  `rvt-monitors`, `rvtportal-spa-alpha`, `rvt-reporting`, and
  `rvt-reporting-new`.
- Intended modules: `apps/monitors`, `apps/portal`,
  `libs/rvt-monitor-common`, and `services/reporting`.
- Root solution: `Rvt.Mono.slnx`.
- Important boundary: retain module-local build/NuGet configuration and the
  private RVT shared-package boundary during the initial import. Do not merge
  reporting implementations or database schemas.
- Source revisions are pinned in the approved design document.
- Known environment note: authenticated GitHub metadata access was available;
  source clone/restore access must be verified during implementation. Never
  record credentials in this repository.
- Task 1 guard: `.gitignore` excludes generated files, environment files, and
  `.superpowers/sdd/` controller state. `docs/imports/source-manifest.md` pins
  the four approved source snapshots. `tests/verify-mono-layout.test.sh` runs
  `scripts/verify-mono-layout.sh`, which currently fails as intended because
  Task 2 has not created `apps/monitors` and Task 3 has not created
  `Rvt.Mono.slnx`.
