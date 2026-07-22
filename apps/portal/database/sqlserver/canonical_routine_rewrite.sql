-- !! SUPERSEDED (2026-07-14) !!
-- This script is a record of the original canonical cutover and still emits the OLD mangled column names
-- (fleet_row_count / row_count / row_count_sites). Those were corrected by the RenameMangledColumns EF
-- migration. Re-running this script against a migrated database would recreate views and routines bound to
-- columns that no longer exist. The current definitions live in database/postgres/post-load/, which the
-- migrator re-applies on every run. Kept for historical reference only.
-- File summary: Rewrites SQL Server stored procedures to canonical names and canonical table/column references.
-- Major updates:
-- - 2026-06-09 pending Added canonical SQL Server routine definitions for the local SQL Server cutover.
-- - 2026-06-09 pending Preserved legacy result-column aliases while keeping canonical routine internals.
-- Run after canonical_database_naming.sql and canonical_view_module_rewrite.sql.
BEGIN TRANSACTION;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[error_insert]
    @host [nvarchar](100),
    @source [nvarchar](100),
    @message [nvarchar](max),
    @level [nvarchar](50),
    @stacktrace [nvarchar](max),
    @variables [ntext]
AS
BEGIN
    INSERT INTO [dbo].[error_log]
        ([host], [source], [message], [level], [stack_trace], [variables])
    VALUES
        (@host, @source, @message, @level, @stacktrace, @variables);
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[monitor_status_for_month]
(
    @MonitorId [uniqueidentifier],
    @Year [int],
    @Month [int],
    @StartDate [datetime],
    @EndDate [datetime]
)
AS
BEGIN
    SELECT MIN([alert_type]) AS [status], DAY([notification_time]) AS [day]
    FROM [dbo].[notification]
    WHERE [monitor_id] = @MonitorId
      AND MONTH([notification_time]) = @Month
      AND YEAR([notification_time]) = @Year
      AND [notification_time] >= @StartDate
      AND [notification_time] <= @EndDate
      AND [alert_type] < 2
    GROUP BY DAY([notification_time]);
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[monitor_status_time_check]
(
    @MonitorId [uniqueidentifier]
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonitorDate AS datetime2(7);
    DECLARE @LastApiDate AS datetime2(7);
    DECLARE @TypeMonitor AS int;

    SELECT TOP 1
        @MonitorDate = CASE
            WHEN [last_data_time_1_min] IS NOT NULL THEN [last_data_time_1_min]
            WHEN [last_data_time_15_min] IS NOT NULL THEN [last_data_time_15_min]
            WHEN [last_data_time_1_hour] IS NOT NULL THEN [last_data_time_1_hour]
            ELSE ISNULL([last_data_time_24_hour], ''1970-01-01'')
        END,
        @TypeMonitor = [type_of_monitor]
    FROM [dbo].[monitor] WITH (NOLOCK)
    WHERE [id] = @MonitorId;

    SELECT @MonitorDate AS [MonitorDate], @MonitorDate AS [LastApiDate], GETUTCDATE() AS [UtcDate];
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[peak_record_breach_and_alerts]
(
    @Date [datetime]
)
AS
BEGIN
    SELECT
        [P].[serial_id] AS [SerialId],
        [M].[fleet_row_count] AS [FleetNr],
        [M].[id] AS [Monitor Id],
        [P].[sample_time] AS [SampleTime],
        [N].[id] AS [Notification Id],
        [N].[notification_time] AS [NotificationTime],
        [P].[x_vtop] AS [XVtop],
        [P].[y_vtop] AS [YVtop],
        [P].[z_vtop] AS [ZVtop]
    FROM [dbo].[omnidots_peak_level] P
    INNER JOIN [dbo].[monitor] M ON [M].[serial_id] = [P].[serial_id] AND [M].[type_of_monitor] = 2
    LEFT JOIN [dbo].[notification] N ON [M].[id] = [N].[monitor_id]
        AND [N].[notification_time] = [P].[sample_time]
        AND [N].[alert_type] IN (0, 1)
    WHERE CAST([P].[sample_time] AS date) = CAST(@Date AS date)
      AND ([P].[x_vtop] > 7 OR [P].[y_vtop] > 7 OR [P].[z_vtop] > 7)
    ORDER BY [P].[serial_id], [P].[sample_time];
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[user_actions_history_insert]
    @userName [nvarchar](50),
    @controller [varchar](50) = NULL,
    @controllerAction [varchar](50) = NULL,
    @parameters [varchar](1024) = NULL,
    @formData [nvarchar](max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[user_action_history]
        ([user_name], [controller], [controller_action], [parameters], [form_data])
    VALUES
        (@userName, @controller, @controllerAction, @parameters, @formData);
END';

IF OBJECT_ID(N'[dbo].[ErrorInsert]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[ErrorInsert];
IF OBJECT_ID(N'[dbo].[MonitorStatusForMonth]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[MonitorStatusForMonth];
IF OBJECT_ID(N'[dbo].[MonitorStatusTimeCheck]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[MonitorStatusTimeCheck];
IF OBJECT_ID(N'[dbo].[PeakRecordBreachAndAlerts]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[PeakRecordBreachAndAlerts];
IF OBJECT_ID(N'[dbo].[UserActionsHistoryInsert]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[UserActionsHistoryInsert];

COMMIT TRANSACTION;
