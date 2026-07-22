-- File summary: Reverts the fleet_nr rename for operators applying SQL directly (SQL Server).
-- Major updates:
-- - 2026-07-14 pending Added rollback for the RenameMangledColumns migration.
--
-- Undoes RenameMangledColumns: monitor.fleet_nr -> fleet_row_count, plus its index.
-- Only the monitor table needs DDL; the row_count / row_count_sites names were view output aliases, and the
-- views are dropped and recreated from database/postgres/post-load/03_views_and_routines.sql, so rolling
-- those back means redeploying the previous version of that script.
--
-- Application code and schema must move together: the previous release expects fleet_row_count.

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'fleet_nr')
BEGIN
    EXEC sp_rename N'dbo.monitor.fleet_nr', N'fleet_row_count', N'COLUMN';
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'ix_monitor_fleet_nr')
BEGIN
    EXEC sp_rename N'dbo.monitor.ix_monitor_fleet_nr', N'ix_monitor_fleet_row_count', N'INDEX';
END;
GO
