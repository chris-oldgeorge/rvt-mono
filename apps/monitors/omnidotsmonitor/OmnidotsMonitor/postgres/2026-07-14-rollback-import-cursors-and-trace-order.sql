-- Rollback twin of 2026-07-14-add-import-cursors-and-trace-order.sql. The forward script drops
-- no index, so this one creates none: the portal-owned
-- ix_omnidots_trace_omnidots_trace_index_id still covers trace-index lookups after the primary
-- key goes away. See docs/database/omnidots-trace-ownership.md.
BEGIN;

ALTER TABLE IF EXISTS omnidots_trace
    DROP CONSTRAINT IF EXISTS pk_omnidots_trace;

-- Adding the primary key implicitly made omnidots_trace_index_id NOT NULL and dropping the key
-- does not undo that, so this restores the nullability the portal's model declares.
ALTER TABLE IF EXISTS omnidots_trace
    ALTER COLUMN omnidots_trace_index_id DROP NOT NULL;

-- WARNING: Dropping sample_index permanently discards trace sample ordering metadata.
ALTER TABLE IF EXISTS omnidots_trace
    DROP COLUMN IF EXISTS sample_index;

DROP TABLE IF EXISTS omnidots_import_cursor;

COMMIT;
