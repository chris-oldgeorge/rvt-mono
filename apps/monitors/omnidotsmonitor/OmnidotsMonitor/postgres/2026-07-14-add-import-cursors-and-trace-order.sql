-- omnidots_trace is owned by the portal: docs/database/omnidots-trace-ownership.md.
-- The portal's canonical cutover named the trace-index foreign key
-- omnidots_trace_index_id (database/postgres/canonical_database_naming.sql), so this
-- migration reads and writes that column and never renames or drops a portal-owned object.
BEGIN;

CREATE TABLE IF NOT EXISTS omnidots_import_cursor
(
    serial_id text NOT NULL,
    series text NOT NULL,
    last_sample_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_omnidots_import_cursor PRIMARY KEY (serial_id, series),
    CONSTRAINT ck_omnidots_import_cursor_series CHECK (series IN ('Peak', 'Veff', 'Vdv'))
);

-- BEGIN legacy-name reconciliation
-- No shipped asset creates omnidots_trace.trace_id - the portal migration creates
-- omnidots_trace_index_id and the portal's cutover renames the legacy "TraceId" onto it - so
-- this branch is expected to be a no-op everywhere. It exists so a hand-built environment that
-- guessed the column name converges on the canonical one instead of failing below with
-- 42703. This block is the only place in the monitor's assets where the non-canonical
-- identifier may appear; the portal suite's OmnidotsTraceSchemaOwnershipTests enforces that.
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM pg_attribute
        WHERE attrelid = 'omnidots_trace'::regclass
          AND attname = 'trace_id'
          AND NOT attisdropped
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM pg_attribute
        WHERE attrelid = 'omnidots_trace'::regclass
          AND attname = 'omnidots_trace_index_id'
          AND NOT attisdropped
    ) THEN
        ALTER TABLE omnidots_trace
            RENAME COLUMN trace_id TO omnidots_trace_index_id;
    END IF;
END
$$;
-- END legacy-name reconciliation

ALTER TABLE omnidots_trace
    ADD COLUMN IF NOT EXISTS sample_index integer;

WITH indexed_samples AS
(
    SELECT
        ctid,
        row_number() OVER (PARTITION BY omnidots_trace_index_id ORDER BY ctid) - 1 AS assigned_sample_index
    FROM omnidots_trace
    WHERE sample_index IS NULL
)
UPDATE omnidots_trace AS sample
SET sample_index = indexed_samples.assigned_sample_index
FROM indexed_samples
WHERE sample.ctid = indexed_samples.ctid;

ALTER TABLE omnidots_trace
    ALTER COLUMN sample_index SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'omnidots_trace'::regclass
          AND contype = 'p'
    ) THEN
        ALTER TABLE omnidots_trace
            ADD CONSTRAINT pk_omnidots_trace PRIMARY KEY (omnidots_trace_index_id, sample_index);
    END IF;
END
$$;

-- The portal's ix_omnidots_trace_omnidots_trace_index_id is now redundant with the primary
-- key's leading column, but it is a portal-owned index: it is declared by the portal's EF
-- model snapshot and recreated by database/postgres/performance_indexes_20260609.sql on every
-- release. Dropping it here would be reverted by the next portal deploy and would leave the
-- portal's model disagreeing with the database in between, so this migration leaves it alone.

COMMIT;
