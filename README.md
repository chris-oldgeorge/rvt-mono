# RVT Mono-Repository

This repository contains four imported RVT modules:

- `apps/monitors`
- `apps/portal`
- `libs/rvt-monitor-common`
- `services/reporting`

Start with the [documentation index](docs/index.md) for architecture,
development, operations, release, database, module, and historical guidance.

Run the manual SonarQube Cloud workflow only from its dedicated trusted-code
self-hosted runner; see the [operator guide](docs/operations/github-actions/self-hosted-sonar-runner.md).

Run the repository guards from the root:

```bash
bash scripts/verify-postgresql-only.sh .
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
```

PostgreSQL is the repository's only supported relational database. Portal uses
three Npgsql EF migration histories plus `RVT.SchemaDeploy` for canonical
PostgreSQL and TimescaleDB objects; monitors and reporting use the same
PostgreSQL/TimescaleDB contract. Provider selection is retired, and the
PostgreSQL-only guard runs automatically at the start of every aggregate build.

Restore the aggregate solution, build the complete project-reference graph,
and run its tests with:

```bash
scripts/build-mono.sh
```

All internal RVT dependencies use direct `ProjectReference` entries. The
aggregate build does not pack, publish, or restore internal RVT NuGet packages
and does not require package-feed credentials. NuGet remains in use only for
third-party dependencies.

Monitor container definitions use the monorepo root as their build context so
their direct references to `libs/rvt-monitor-common` resolve inside the image
build. See
[`docs/operations/monitors/container-builds.md`](docs/operations/monitors/container-builds.md).

The nearest imported `AGENTS.md` governs work within a module. Before working
in `apps/portal`, read
[`docs/development/portal/development-guidelines.md`](docs/development/portal/development-guidelines.md)
first.
