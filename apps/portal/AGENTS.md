# Repository Agent Guidance

This repository is the RVT Portal SPA alpha worktree. Use the workspace-level `project_state.md` for current state before starting broad work.

## Development Guidelines (read first)

- Read `docs/development/development-guidelines.md` before writing or reviewing code, schema, tests, config, or release tooling. Do not work from memory of it.
- Treat its rules as binding. Each one exists because something broke, and each names the guard test that enforces it. If a rule is wrong, change the rule *and* its guard deliberately rather than working around it.
- It covers dates and time zones (the timestamptz vs `timestamp` `DateTime.Kind` split), EF schema/model drift and unmapped columns, persistence boundaries, ports and adapters, what the InMemory/SQLite test suite structurally cannot see, secrets, and client-release doc hygiene.
- When you add a new invariant, add a guard test and prove it fails without the fix, then update the guidelines in the same change.

## Source Comment Hygiene

- When making important source-code changes, update the touched file summary, function summaries, TODO notes, and major-updates comments so onboarding comments stay current.
- If the change is broad or architectural, also update `project_state.md` with affected paths, verification results, and known follow-up work.

## Database Naming Rules

- Future database changes must follow the canonical RVT database naming standard: lowercase, singular, snake_case identifiers.
- Single-column primary keys must be named `id`.
- Foreign keys must be `{referenced_table}_id` unless they reference a non-`id` key.
- Avoid reserved words and data-type-like column names such as `text`, `timestamp`, `uuid`, or `user`.
- C# types remain PascalCase; API JSON and TypeScript-facing models remain camelCase.
- ASP.NET Identity physical objects stay framework-managed and are excluded from the DBR rename refactor.

## DBR Artifacts To Keep Current

- Update `docs/database/database-name-registry.csv` when table, view, or column names change.
- Update `docs/database/sqlserver-name-registry.csv` when SQL Server source mappings change.
- Update `docs/database/database-name-equivalents-for-migrator-sqlserver.csv` when the SQL Server-to-Postgres migrator needs new name mappings.
- Update `docs/database/database-constraint-index-name-registry.csv` when constraints or indexes change.
- The SQL Server-to-Postgres migrator (`RVT.DatabaseMigrator`) was retired on 2026-07-14. A database is built
  from EF migrations (three contexts) plus `RVT.SchemaDeploy`, which applies the SQL under
  `database/postgres/`. See `docs/database/ef-migrations.md`.

## Compatibility Boundaries

- Temporary old-name views may exist only in the `legacy` schema and only through the approved compatibility scripts.
- New application, migrator, and monitor code must not query the `legacy` schema.
- Keep compatibility objects documented with ownership and removal status in `docs/database/legacy-compatibility-deprecation.md`.
