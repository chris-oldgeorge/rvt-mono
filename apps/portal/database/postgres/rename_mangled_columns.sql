-- File summary: Renames the mangled monitor fleet column (PostgreSQL). Forward script.
-- Major updates:
-- - 2026-07-14 pending Added as the deployable form of the RenameMangledColumns change.
--
-- The old canonical naming rules applied Replace("nr", "row_count") to column names without anchoring it, so
-- Monitor.FleetNr was mapped to a column called "fleet_row_count". This restores the intended name.
--
-- Only the monitor table needs DDL. The other mangled names (row_count, row_count_sites) are view output
-- aliases, and the views are dropped and recreated from database/postgres/post-load/03_views_and_routines.sql
-- on every migrator run - so applying that script is what corrects them.
--
-- ORDER OF OPERATIONS
--   1. this script
--   2. post-load/03_views_and_routines.sql and post-load/04_routines.sql (rebuild views and routines)
--   3. deploy the matching application release
--
-- BREAKING: the previous release reads fleet_row_count and will fail as soon as this runs. Schema and code must
-- go out together. To undo, run rename_mangled_columns_rollback.sql and redeploy the previous release.
-- Idempotent: safe to re-run.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'monitor' AND column_name = 'fleet_row_count')
    THEN
        ALTER TABLE public.monitor RENAME COLUMN fleet_row_count TO fleet_nr;
    END IF;

    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'ix_monitor_fleet_row_count') THEN
        ALTER INDEX public.ix_monitor_fleet_row_count RENAME TO ix_monitor_fleet_nr;
    END IF;
END
$$;
