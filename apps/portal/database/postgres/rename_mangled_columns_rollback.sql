-- File summary: Reverts the fleet_nr rename for operators applying SQL directly (PostgreSQL).
-- Major updates:
-- - 2026-07-14 pending Added rollback for the RenameMangledColumns migration.
--
-- Undoes RenameMangledColumns: monitor.fleet_nr -> fleet_row_count, plus its index.
-- Only the monitor table needs DDL; the row_count / row_count_sites names were view output aliases, and the
-- views are dropped and recreated from database/postgres/post-load/03_views_and_routines.sql, so rolling
-- those back means redeploying the previous version of that script.
--
-- Application code and schema must move together: the previous release expects fleet_row_count.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'monitor' AND column_name = 'fleet_nr')
    THEN
        ALTER TABLE public.monitor RENAME COLUMN fleet_nr TO fleet_row_count;
    END IF;
END
$$;

ALTER INDEX IF EXISTS public.ix_monitor_fleet_nr RENAME TO ix_monitor_fleet_row_count;
