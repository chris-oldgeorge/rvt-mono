-- File summary: Renames the mangled monitor fleet column (SQL Server). Forward script.
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

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'fleet_row_count')
BEGIN
    EXEC sp_rename N'dbo.monitor.fleet_row_count', N'fleet_nr', N'COLUMN';
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'ix_monitor_fleet_row_count')
BEGIN
    EXEC sp_rename N'dbo.monitor.ix_monitor_fleet_row_count', N'ix_monitor_fleet_nr', N'INDEX';
END;
GO
