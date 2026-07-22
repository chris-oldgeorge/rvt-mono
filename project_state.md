# Project State

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Documents/rvt-mono`
- Status: approved design; implementation has not yet started.
- Design: `docs/superpowers/specs/2026-07-22-rvt-mono-repository-design.md`
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
