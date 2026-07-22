-- File summary: Restores legacy SQL Server stored procedure names and definitions after canonical rollback.
-- Major updates:
-- - 2026-06-09 pending Added SQL Server routine rollback definitions for the local SQL Server cutover.
-- Run after canonical_database_naming_rollback.sql and canonical_view_module_rewrite_rollback.sql.
BEGIN TRANSACTION;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[ErrorInsert]
    @host [nvarchar](100),
    @source [nvarchar](100),
    @message [nvarchar](max),
    @level [nvarchar](50),
    @stacktrace [nvarchar](max),
    @variables [ntext]
AS
BEGIN
    INSERT INTO [dbo].[ErrorLog]
        ([Host], [Source], [Message], [Level], [StackTrace], [Variables])
    VALUES
        (@host, @source, @message, @level, @stacktrace, @variables);
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[MonitorStatusForMonth]
(
    @MonitorId [uniqueidentifier],
    @Year [int],
    @Month [int],
    @StartDate [datetime],
    @EndDate [datetime]
)
AS
BEGIN
    SELECT MIN([AlertType]) AS [status], DAY([NotificationTime]) AS [day]
    FROM [dbo].[Notifications]
    WHERE [MonitorId] = @MonitorId
      AND MONTH([NotificationTime]) = @Month
      AND YEAR([NotificationTime]) = @Year
      AND [NotificationTime] >= @StartDate
      AND [NotificationTime] <= @EndDate
      AND [AlertType] < 2
    GROUP BY DAY([NotificationTime]);
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[MonitorStatusTimeCheck]
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
            WHEN [LastDataTime1Min] IS NOT NULL THEN [LastDataTime1Min]
            WHEN [LastDataTime15Min] IS NOT NULL THEN [LastDataTime15Min]
            WHEN [LastDataTime1Hour] IS NOT NULL THEN [LastDataTime1Hour]
            ELSE ISNULL([LastDataTime24Hour], ''1970-01-01'')
        END,
        @TypeMonitor = [TypeOfMonitor]
    FROM [dbo].[MonitorsList] WITH (NOLOCK)
    WHERE [Id] = @MonitorId;

    SELECT @MonitorDate AS [MonitorDate], @MonitorDate AS [LastApiDate], GETUTCDATE() AS [UtcDate];
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[PeakRecordBreachAndAlerts]
(
    @Date [datetime]
)
AS
BEGIN
    SELECT
        [P].[SerialId],
        [M].[FleetNr],
        [M].[Id] AS [Monitor Id],
        [P].[SampleTime],
        [N].[Id] AS [Notification Id],
        [N].[NotificationTime],
        [P].[XVtop],
        [P].[YVtop],
        [P].[ZVtop]
    FROM [dbo].[OmnidotsPeakLevels] P
    INNER JOIN [dbo].[MonitorsList] M ON [M].[SerialId] = [P].[SerialId] AND [M].[TypeOfMonitor] = 2
    LEFT JOIN [dbo].[Notifications] N ON [M].[Id] = [N].[MonitorId]
        AND [N].[NotificationTime] = [P].[SampleTime]
        AND [N].[AlertType] IN (0, 1)
    WHERE CAST([P].[SampleTime] AS date) = CAST(@Date AS date)
      AND ([P].[XVtop] > 7 OR [P].[YVtop] > 7 OR [P].[ZVtop] > 7)
    ORDER BY [P].[SerialId], [P].[SampleTime];
END';

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE [dbo].[UserActionsHistoryInsert]
    @userName [nvarchar](50),
    @controller [varchar](50) = NULL,
    @controllerAction [varchar](50) = NULL,
    @parameters [varchar](1024) = NULL,
    @formData [nvarchar](max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[UserActionsHistory]
        ([UserName], [Controller], [ControllerAction], [Parameters], [FormData])
    VALUES
        (@userName, @controller, @controllerAction, @parameters, @formData);
END';

IF OBJECT_ID(N'[dbo].[error_insert]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[error_insert];
IF OBJECT_ID(N'[dbo].[monitor_status_for_month]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[monitor_status_for_month];
IF OBJECT_ID(N'[dbo].[monitor_status_time_check]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[monitor_status_time_check];
IF OBJECT_ID(N'[dbo].[peak_record_breach_and_alerts]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[peak_record_breach_and_alerts];
IF OBJECT_ID(N'[dbo].[user_actions_history_insert]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[user_actions_history_insert];

COMMIT TRANSACTION;
