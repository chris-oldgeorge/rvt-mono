-- File summary: Drops the SQL Server removal-impact aggregate view.
-- Major updates:
-- - 2026-06-25 pending Added rollback for monitor_measurement_removal_impact.

IF OBJECT_ID(N'[dbo].[monitor_measurement_removal_impact]', N'V') IS NOT NULL
    DROP VIEW [dbo].[monitor_measurement_removal_impact];
