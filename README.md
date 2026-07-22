# RVT Mono-Repository

This repository contains four imported RVT modules:

- `apps/monitors`
- `apps/portal`
- `libs/rvt-monitor-common`
- `services/reporting`

Run the repository guards from the root:

```bash
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
```

Restore and build the aggregate solution with:

```bash
dotnet restore Rvt.Mono.slnx
dotnet build Rvt.Mono.slnx --no-restore --nologo
```

The nearest imported `AGENTS.md` governs work within a module. Before working
in `apps/portal`, read
`apps/portal/docs/development/development-guidelines.md` first.
