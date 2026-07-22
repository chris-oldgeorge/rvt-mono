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

ALTER TABLE omnidots_trace
    ADD COLUMN IF NOT EXISTS sample_index integer;

WITH indexed_samples AS
(
    SELECT
        ctid,
        row_number() OVER (PARTITION BY trace_id ORDER BY ctid) - 1 AS assigned_sample_index
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
            ADD CONSTRAINT pk_omnidots_trace PRIMARY KEY (trace_id, sample_index);
    END IF;
END
$$;

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'omnidots_trace'::regclass
          AND conname = 'pk_omnidots_trace'
          AND contype = 'p'
    ) THEN
        DROP INDEX IF EXISTS ix_omnidots_trace_trace_id;
    END IF;
END
$$;

COMMIT;
