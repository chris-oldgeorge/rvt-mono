# `omnidots_trace` schema ownership

Decided: 2026-07-30. Supersedes nothing; this boundary previously had no owner.

## Ruling

**The portal owns the schema of `public.omnidots_trace` and `public.omnidots_trace_index`.
The Omnidots monitor conforms to it.**

Concretely:

- The canonical names come from `RVTSearchContext` and the EF Search migration chain
  (`apps/portal/RVT.DataAccess/Migrations/Search/`), rendered by
  `ApplyRvtCanonicalDatabaseNames`. The trace-index foreign key is
  **`omnidots_trace_index_id`**, and its supporting index is
  **`ix_omnidots_trace_omnidots_trace_index_id`**.
- The Omnidots monitor is the only writer of rows. It maps the same columns in
  `OmnidotsMonitorContext` and addresses them by the portal's names. It may **add**
  writer-owned columns and constraints it needs — today `sample_index` and
  `pk_omnidots_trace` — through its own `postgres/` migration assets. It must not rename,
  retype, or drop anything the portal declares, including the portal's indexes.
- The portal reads the table (`SiteArchiveQueryCatalog`, `MonitorService`) and must not
  assume it is the only writer: rows appear without the portal's involvement.

The rule of thumb: **names are the portal's; rows are the monitor's.** A change to either
column names or types is a portal change, and the monitor follows it.

## Why the portal wins

The portal's names are the deployed reality. Every environment's `omnidots_trace` was
created either by the portal's EF Search baseline (which creates
`omnidots_trace_index_id`) or by the legacy database whose `"TraceId"` column
`apps/portal/database/postgres/canonical_database_naming.sql` renames to
`omnidots_trace_index_id`. Adopting the monitor's name would have meant renaming a column
in a live database that the portal reads on the archive path, to gain nothing.

## What was wrong, and what deployed databases actually hold

Until 2026-07-30 the monitor addressed the foreign key as `trace_id` in
`OmnidotsMonitorContext` and in its `2026-07-14-add-import-cursors-and-trace-order.sql`
migration. Against any real database both would have failed with
`42703 column "trace_id" does not exist`.

No deployed database can hold the monitor's shape:

- Nothing the repository ships creates `omnidots_trace.trace_id`. The monitor ships no
  create-table DDL for this table at all; the name existed only in the monitor's own test
  fixtures.
- The monitor's forward migration is wrapped in `BEGIN`/`COMMIT`, and PostgreSQL DDL is
  transactional, so a failed application leaves nothing behind — not even the
  `omnidots_import_cursor` table created earlier in the same script. There is no partial
  monitor-shaped state to find.
- That migration fails against **both** candidate deployed shapes: the pre-cutover
  `"TraceId"` (unquoted `trace_id` does not match a quoted mixed-case identifier) and the
  post-cutover `omnidots_trace_index_id`. It has therefore never been applied successfully
  anywhere.
- The rest of the monitor's Omnidots mapping — `omnidots_trace_index.serial_id`,
  `start_time`, `end_time`, and every other Omnidots table — already uses the canonical
  post-cutover names. The monitor has always targeted the post-cutover database
  exclusively; `trace_id` was a lone outlier, not a second schema lineage.

The forward migration was therefore corrected in place rather than superseded by a new
reconciling migration: there is no deployed state to reconcile, and a dated reconciliation
migration would be a no-op everywhere while implying a history that never happened. As
cheap insurance for a hand-built environment that copied the monitor's old test fixture,
the corrected script opens with a guarded, idempotent rename of a stray `trace_id` onto the
canonical name. That block is a no-op on every shape described above.

The monitor's migration also used to end with
`DROP INDEX IF EXISTS ix_omnidots_trace_trace_id`, intending to shed the index the new
primary key makes redundant. That index name never existed. The statement is now gone
rather than retargeted at the portal's real index: `performance_indexes_20260609.sql`
recreates that index on every release and the portal's model snapshot declares it, so a
monitor-side drop would be reverted by the next portal deploy and would leave the portal's
model disagreeing with the database in the meantime. Dropping it is the portal's call to
make, not the monitor's.

## How the boundary is enforced

`apps/portal/RvtPortal.Spa.Tests/OmnidotsTraceSchemaOwnershipTests.cs` is the only place
both owners meet in one database:

- It pins the canonical column name from `RVTSearchContext`'s own model and requires the
  monitor's shipped SQL to spell it the same way (this check needs no database, so it runs
  everywhere).
- It builds the schema from `RVTSearchContext`, applies the monitor's forward migration
  twice, asserts the resulting primary key, and then reads rows back through the portal's
  EF model — so the two owners have to agree in both directions.
- It applies the monitor's rollback and asserts the portal-declared shape is restored,
  including the portal's index and the column's nullability.

The monitor's `OmnidotsMigrationContractTests` still checks script text and behaviour on
the monitor side, but the binding cross-owner guarantee lives in the test above.

## Deployment order

The Omnidots monitor requires the portal's canonical naming cutover
(`apps/portal/database/postgres/canonical_database_naming.sql`, see
[the cutover runbook](portal/database-naming-cutover-runbook.md)) to have been applied.
Its migration assets must be applied after that cutover and before the monitor image that
depends on them.
