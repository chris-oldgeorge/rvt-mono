# SQL Script Sonar Analysis Policy

## Scope

SonarCloud should continue to analyze application code for maintainability, reliability, and security issues. Generated or one-off SQL migration scripts are handled differently because their primary quality gate is database rehearsal, not source-level maintainability scoring.

## Decision

The SonarQube workflow ignores these SQL-only maintainability rules:

- `plsql:S1192` for duplicated literals in `*.sql` files.
- `plsql:LiteralsNonPrintableCharactersCheck` for embedded SQL Server dynamic-module rewrite scripts under `database/sqlserver/canonical_*_rewrite*.sql`.

These findings are intentionally scoped because the affected files are generated or cutover/rehearsal artifacts where repeated object names and embedded module definitions are expected.

## Validation Still Required

SQL changes must still be validated through the database-specific checks that apply to the changed artifact:

- SQL Server scripts must be rehearsed against a cloned SQL Server database before production use.
- PostgreSQL/Timescale scripts must be run against the local Timescale/Postgres container or a clone matching the target schema.
- Database naming changes must keep the registries under `docs/database` current.
- Post-load scripts live in `database/postgres/post-load/` and are applied by `RVT.SchemaDeploy`.
  They are guarded by `CutoverReadinessTests`, which parses them; run the test suite after changing one.
  A new script needs a two-digit order prefix - `RVT.SchemaDeploy` applies them in filename order, and that
  order is a dependency order.

This policy removes noisy static maintainability findings from generated SQL artifacts; it does not remove database review or execution evidence requirements.
