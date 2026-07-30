-- Narrows alert_delivery_outbox.last_error back to varchar(256), the width it
-- had before 2026-07-30-widen-alert-outbox-last-error.sql.
-- Rerunnable: narrowing an already-narrow column is a no-op.
BEGIN;

-- WARNING: Narrowing last_error permanently truncates delivery errors longer
-- than 256 characters. Without this the ALTER would simply fail on the first
-- such row, so the truncation is explicit rather than accidental.
DO $$
BEGIN
    IF to_regclass('alert_delivery_outbox') IS NOT NULL THEN
        UPDATE alert_delivery_outbox
        SET last_error = left(last_error, 256)
        WHERE last_error IS NOT NULL
          AND length(last_error) > 256;
    END IF;
END
$$;

ALTER TABLE IF EXISTS alert_delivery_outbox
    ALTER COLUMN last_error TYPE varchar(256);

COMMIT;
