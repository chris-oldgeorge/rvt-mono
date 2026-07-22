BEGIN;

ALTER TABLE IF EXISTS omnidots_trace
    DROP CONSTRAINT IF EXISTS pk_omnidots_trace;

CREATE INDEX IF NOT EXISTS ix_omnidots_trace_trace_id
    ON omnidots_trace (trace_id);

-- WARNING: Dropping sample_index permanently discards trace sample ordering metadata.
ALTER TABLE IF EXISTS omnidots_trace
    DROP COLUMN IF EXISTS sample_index;

DROP TABLE IF EXISTS omnidots_import_cursor;

COMMIT;
