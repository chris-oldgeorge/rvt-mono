# RVT Mono-Repository

This repository contains four imported RVT modules:

- `apps/monitors`
- `apps/portal`
- `libs/rvt-monitor-common`
- `services/reporting`

Start with the [documentation index](docs/index.md) for architecture,
development, operations, release, database, module, and historical guidance.

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

Monitor source builds are supported from the mono-repository, but the checked-in
monitor container build path is currently unsupported because its build context
cannot reach the shared source projects. See
[`docs/operations/monitors/container-builds.md`](docs/operations/monitors/container-builds.md)
for the limitation and required follow-up.

The nearest imported `AGENTS.md` governs work within a module. Before working
in `apps/portal`, read
[`docs/development/portal/development-guidelines.md`](docs/development/portal/development-guidelines.md)
first.
