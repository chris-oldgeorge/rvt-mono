# Project Agent Instructions

- Start each new session by reading `../../project_state.md` to get up to speed.
- Use the native macOS clone as the active working directory:
  - `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`
- Do not remount or edit the Parallels Windows C: share for normal project work. The old `/Volumes/[C] Windows 11/...` and `/private/tmp/win11c/...` workspaces are retired fallbacks only.
- Sync code through GitHub before switching machines or workspaces:
  - fetch/pull in the native macOS clone before starting work
  - commit and push completed changes from the native macOS clone

## Preferred Monitor Design Direction

- Keep code style and architectural conventions consistent among monitor subprojects: use the shared host, focused handlers, narrow ports, async conventions, focused tests, and current deployment documentation. Document any vendor-specific deviation at the boundary where it is required.

- Use the current EF Core-backed, PostgreSQL-first monitor architecture as the default path for new monitor work.
- Keep each monitor app's `IDBClient` as a compatibility facade only; prefer adding narrow query/command interfaces for new behavior:
  - monitor catalog/status queries and commands
  - measurement write commands
  - rule/notification queries
  - operational commands for errors, notifications, and audit writes
- Route API/application code through those narrow interfaces instead of calling a broad concrete database client field directly.
- Use Mapperly inside monitor app projects for simple DTO/entity mapping. Keep `Riok.Mapperly` as an analyzer-only, app-local dependency with `PrivateAssets="all"` and `OutputItemType="Analyzer"`.
- Do not put Mapperly in the shared `Rvt.Monitor.*` packages owned by the authoritative private `RVT-Group-LTD/rvt-reporting` repository; keep shared infrastructure free of monitor-specific mapping policy.
- Keep vendor JSON parsing, notification/rule state machines, aggregate field selection, and non-trivial business logic manual and covered by tests.
- Add or preserve focused architecture tests that protect the dependency boundaries when changing monitor data access.
