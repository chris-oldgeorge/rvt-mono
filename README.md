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

Pack the shared RVT libraries, restore the aggregate solution, build it, and
run its tests with:

```bash
scripts/build-mono.sh
```

The script creates `0.2.0-rc.1` packages for `Rvt.Monitor.Common`,
`Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` in
`artifacts/packages`. Active applications use source project references, while
the projects under `libs/rvt-monitor-common/package-validation` intentionally
remain package consumers and restore those locally built artifacts. The build
does not require GitHub Packages credentials.

The nearest imported `AGENTS.md` governs work within a module. Before working
in `apps/portal`, read
`apps/portal/docs/development/development-guidelines.md` first.
