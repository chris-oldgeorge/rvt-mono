# Task 6 Report: Complete and failure-aware schema deployment

## Outcome

- Status: `DONE_WITH_CONCERNS`
- Intended commit: `fix: deploy required database repairs`
- Live-provider closure: unavailable because `RVT_TEST_POSTGRES_CONNECTION`
  is unset and Docker access could not be approved.

## Implemented contract

- `ScriptRunner` resolves one deterministic sequence for dry-run and execution:
  `create_unmapped_schema.sql`, `restore_unmapped_column_defaults.sql`, then
  ordinally sorted `post-load/*.sql`.
- `RVT.SchemaDeploy.csproj` publishes the repair exactly once as
  `sql/restore_unmapped_column_defaults.sql`.
- The runner supports a caller-owned open `NpgsqlConnection`, allowing two
  deployments to execute inside one provider-test transaction and roll back.
- The repair retains the canonical UTC-naive default expression introduced by
  Task 5 and remains idempotent.
- `share-dev-database.sh` preserves the exact nonzero `pg_restore` status,
  aborts before success verification on failure, and prints completion only
  after valid nonzero public-table and hypertable counts.

## TDD evidence

RED focused result: `4 failed, 4 passed, 2 provider-gated skipped`. The four
expected failures proved the resolved list had three rather than four stages,
publish content omitted the repair, `pg_restore` status 23 became success, and
zero verification counts still returned success.

GREEN evidence:

- focused schema/repair slice: `8 passed, 2 provider-gated skipped`;
- full portal project: `352 passed, 7 provider-gated skipped, 0 failed`;
- `bash -n apps/portal/docs/deploy/share-dev-database.sh`: passed;
- fake-Docker shell harness: exact status 23 propagated and verification was
  skipped; zero counts failed; nonzero counts printed completion;
- actual `dotnet publish` succeeded and contained the repair once;
- portal solution build: succeeded with zero errors and only the five existing
  `System.Security.Cryptography.Xml` NU1903 warnings;
- `git diff --check`: passed.

Provider-gated tests deploy twice through the same caller-owned PostgreSQL
connection/transaction, compare canonical defaults, seed rows through EF, and
fingerprint values/counts before and after the second run. They were discovered
but did not execute, so live PostgreSQL idempotency is not claimed.

## Files changed

- `apps/portal/RVT.SchemaDeploy/DeployOptions.cs`
- `apps/portal/RVT.SchemaDeploy/Program.cs`
- `apps/portal/RVT.SchemaDeploy/RVT.SchemaDeploy.csproj`
- `apps/portal/RVT.SchemaDeploy/ScriptRunner.cs`
- `apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`
- `apps/portal/RvtPortal.Spa.Tests/SchemaDeployTests.cs`
- `apps/portal/RvtPortal.Spa.Tests/UnmappedColumnDefaultTests.cs`
- `apps/portal/database/postgres/restore_unmapped_column_defaults.sql`
- `apps/portal/docs/deploy/share-dev-database.sh`
- `project_state.md`

## Self-review and independent pre-commit review

The implementer review found no Critical, Important, or Minor issues. It
confirmed that dry-run and execution share one resolver, the caller-owned
connection enlists commands and EF in one transaction, the provider test
fingerprints data across the second run, shell failure propagation is exact,
and Task 5 UTC SQL remains intact. Recommendation: run the provider-gated tests
against a dedicated PostgreSQL test database because deployment DDL takes
substantial locks.

## Remaining concerns

- Real PostgreSQL double-run/idempotency remains unverified.
- The generated `apps/.nuget-packages/` cache is unrelated and must not be
  committed.
- Existing NU1903 advisories remain outside this task.

## Parent review fix - 2026-07-23

### Finding disposition

The parent review found that the initial resolver could silently omit an
individual required stage and proceed whenever at least one other script
remained. That Important finding is fixed:

- `ResolveScripts` now throws `DeployException` when
  `create_unmapped_schema.sql` is absent;
- it separately throws when `restore_unmapped_column_defaults.sql` is absent;
- it requires the `post-load` stage to contain at least one real `*.sql` file;
  an absent directory, empty directory, or AppleDouble-only directory cannot
  satisfy the stage;
- validation happens inside the one resolver used by dry-run, connection-string
  execution, and caller-owned-connection execution, before output or connection
  work;
- when complete, the resolved list remains create, repair, then ordinally
  sorted post-load scripts, exactly once.

The Minor shell-test finding is also closed. The fake-Docker harness now
executes these branches independently:

- `5|0` rejects a zero TimescaleDB hypertable count;
- `x|2` rejects a malformed public-table count;
- `5|x` rejects a malformed hypertable count.

The existing `0|0`, exact `pg_restore` status 23, abort-before-verification, and
`5|2` completion controls remain.

### Review-fix TDD evidence

RED command:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter 'FullyQualifiedName~SchemaDeployTests|FullyQualifiedName~UnmappedColumnDefaultTests' \
  --no-restore --nologo -m:1
```

Pre-production result: `6 failed, 11 passed, 2 skipped, 19 total`.

All six failures were intended required-stage cases. Dry-run returned without
an exception, while execution mode reached the invalid connection instead of
reporting missing create, repair, or post-load. The three new shell branch
cases passed at RED because they characterize defensive production branches
that already existed.

GREEN result for the same command: `17 passed, 0 failed, 2 skipped, 19 total`.
The two skips are the provider-gated PostgreSQL cases.

Final verification:

- full portal suite: `361 passed, 0 failed, 7 skipped, 368 total`;
- portal solution build: succeeded with `0` errors and the five existing
  `System.Security.Cryptography.Xml` NU1903 advisories;
- `bash -n apps/portal/docs/deploy/share-dev-database.sh`: passed;
- actual `dotnet publish`: succeeded, included
  `sql/restore_unmapped_column_defaults.sql` exactly once, and its executable
  dry-run listed all seven scripts in canonical order;
- `git diff --check`: passed.

### Review-fix files

- `apps/portal/RVT.SchemaDeploy/ScriptRunner.cs`
- `apps/portal/RvtPortal.Spa.Tests/SchemaDeployTests.cs`
- `project_state.md`
- `.superpowers/sdd/task-6-report.md`

### Remaining concern

`RVT_TEST_POSTGRES_CONNECTION` remains unset. The transaction-scoped
double-run/idempotency test is still discovered but was not executed against a
live PostgreSQL provider, so deployed-schema closure remains unclaimed.
