-- File summary: Adds SQL Server monitor natural-key backfill, duplicate audit, and unique indexes.
-- Major updates:
-- - 2026-06-18 pending Mirrored PostgreSQL monitor natural-key changes for SQL Server deployment.
--
-- Run after canonical database naming.
-- Known duplicate groups are quarantined before the audit and unique indexes are created.
-- Filtered unique indexes preserve PostgreSQL's multiple-NULL uniqueness semantics.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.air_q_monitor_status', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_monitor_status', N'serial_id') IS NULL
BEGIN
    ALTER TABLE dbo.[air_q_monitor_status] ADD [serial_id] nvarchar(64) NULL;
END;

IF OBJECT_ID(N'dbo.air_q_monitor_status', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_monitor_status', N'id') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_monitor_status', N'serial_id') IS NOT NULL
BEGIN
    EXEC(N'UPDATE dbo.[air_q_monitor_status] SET [serial_id] = CONVERT(nvarchar(64), [id]) WHERE [serial_id] IS NULL AND [id] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.duplicate_quarantine_svantek_noise_level', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.[duplicate_quarantine_svantek_noise_level]
    FROM dbo.[svantek_noise_level];
END;

IF OBJECT_ID(N'dbo.duplicate_quarantine_omnidots_peak_level', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.[duplicate_quarantine_omnidots_peak_level]
    FROM dbo.[omnidots_peak_level];
END;

IF OBJECT_ID(N'dbo.duplicate_quarantine_svantek_noise_8_hour_average', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.[duplicate_quarantine_svantek_noise_8_hour_average]
    FROM dbo.[svantek_noise_8_hour_average];
END;

;WITH ranked AS
(
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY [serial_id], [sample_time]
            ORDER BY (SELECT 0)
        ) AS duplicate_rank
    FROM dbo.[svantek_noise_level]
    WHERE [serial_id] IS NOT NULL
      AND [sample_time] IS NOT NULL
)
DELETE FROM ranked
OUTPUT
    deleted.[serial_id],
    deleted.[sample_time],
    deleted.[laeq],
    deleted.[lamax],
    deleted.[la_90],
    deleted.[la_10],
    deleted.[lceq],
    deleted.[lcmax],
    deleted.[lc_90],
    deleted.[lc_10]
INTO dbo.[duplicate_quarantine_svantek_noise_level]
(
    [serial_id],
    [sample_time],
    [laeq],
    [lamax],
    [la_90],
    [la_10],
    [lceq],
    [lcmax],
    [lc_90],
    [lc_10]
)
WHERE duplicate_rank > 1;

;WITH ranked AS
(
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY [serial_id], [sample_time]
            ORDER BY (SELECT 0)
        ) AS duplicate_rank
    FROM dbo.[omnidots_peak_level]
    WHERE [serial_id] IS NOT NULL
      AND [sample_time] IS NOT NULL
)
DELETE FROM ranked
OUTPUT
    deleted.[serial_id],
    deleted.[sample_time],
    deleted.[x_fdom],
    deleted.[x_vtop],
    deleted.[x_vtop_overflow],
    deleted.[y_fdom],
    deleted.[y_vtop],
    deleted.[y_vtop_overflow],
    deleted.[z_fdom],
    deleted.[z_vtop],
    deleted.[z_vtop_overflow]
INTO dbo.[duplicate_quarantine_omnidots_peak_level]
(
    [serial_id],
    [sample_time],
    [x_fdom],
    [x_vtop],
    [x_vtop_overflow],
    [y_fdom],
    [y_vtop],
    [y_vtop_overflow],
    [z_fdom],
    [z_vtop],
    [z_vtop_overflow]
)
WHERE duplicate_rank > 1;

;WITH ranked AS
(
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY [serial_id], [sample_time]
            ORDER BY
                CASE WHEN [number_of_samples] IS NULL THEN 1 ELSE 0 END,
                [number_of_samples] DESC
        ) AS duplicate_rank
    FROM dbo.[svantek_noise_8_hour_average]
    WHERE [serial_id] IS NOT NULL
      AND [sample_time] IS NOT NULL
)
DELETE FROM ranked
OUTPUT
    deleted.[serial_id],
    deleted.[sample_time],
    deleted.[laeq],
    deleted.[lamax],
    deleted.[la_90],
    deleted.[la_10],
    deleted.[lceq],
    deleted.[lcmax],
    deleted.[lc_90],
    deleted.[lc_10],
    deleted.[number_of_samples]
INTO dbo.[duplicate_quarantine_svantek_noise_8_hour_average]
(
    [serial_id],
    [sample_time],
    [laeq],
    [lamax],
    [la_90],
    [la_10],
    [lceq],
    [lcmax],
    [lc_90],
    [lc_10],
    [number_of_samples]
)
WHERE duplicate_rank > 1;

DECLARE @checks TABLE
(
    table_name sysname NOT NULL,
    columns_csv nvarchar(400) NOT NULL,
    not_null_filter nvarchar(1000) NOT NULL
);

INSERT INTO @checks (table_name, columns_csv, not_null_filter)
VALUES
    (N'monitor', N'serial_id, type_of_monitor', N'[serial_id] IS NOT NULL AND [type_of_monitor] IS NOT NULL'),
    (N'air_q_monitor_status', N'serial_id', N'[serial_id] IS NOT NULL'),
    (N'omnidots_monitor_status', N'serial_id', N'[serial_id] IS NOT NULL'),
    (N'omnidots_sensor', N'serial_id', N'[serial_id] IS NOT NULL'),
    (N'svantek_monitor_status', N'serial_id', N'[serial_id] IS NOT NULL'),
    (N'air_q_noise_level', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'svantek_noise_level', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'my_atm_dust_level', N'serial_id, sample_time, avrg', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL AND [avrg] IS NOT NULL'),
    (N'my_atm_accessory_info', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'omnidots_peak_level', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'omnidots_veff_level', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'omnidots_vdv_level', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'air_q_noise_8_hour_average', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL'),
    (N'svantek_noise_8_hour_average', N'serial_id, sample_time', N'[serial_id] IS NOT NULL AND [sample_time] IS NOT NULL');

DECLARE @failures TABLE (failure nvarchar(1000) NOT NULL);
DECLARE @table_name sysname;
DECLARE @columns_csv nvarchar(400);
DECLARE @not_null_filter nvarchar(1000);
DECLARE @missing_columns nvarchar(1000);
DECLARE @duplicate_count bigint;
DECLARE @sql nvarchar(max);

DECLARE natural_key_checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT table_name, columns_csv, not_null_filter
    FROM @checks;

OPEN natural_key_checks;
FETCH NEXT FROM natural_key_checks INTO @table_name, @columns_csv, @not_null_filter;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@table_name), N'U') IS NULL
    BEGIN
        INSERT INTO @failures (failure)
        VALUES (N'Missing table dbo.' + @table_name);

        FETCH NEXT FROM natural_key_checks INTO @table_name, @columns_csv, @not_null_filter;
        CONTINUE;
    END;

    SELECT @missing_columns = STRING_AGG(LTRIM(RTRIM([value])), N', ')
    FROM STRING_SPLIT(@columns_csv, N',')
    WHERE COL_LENGTH(N'dbo.' + @table_name, LTRIM(RTRIM([value]))) IS NULL;

    IF @missing_columns IS NOT NULL
    BEGIN
        INSERT INTO @failures (failure)
        VALUES (N'Missing columns dbo.' + @table_name + N': ' + @missing_columns);

        FETCH NEXT FROM natural_key_checks INTO @table_name, @columns_csv, @not_null_filter;
        CONTINUE;
    END;

    SET @duplicate_count = 0;
    SET @sql = N'SELECT @duplicate_count = COUNT(*) FROM (' +
        N'SELECT ' + @columns_csv +
        N' FROM dbo.' + QUOTENAME(@table_name) +
        N' WHERE ' + @not_null_filter +
        N' GROUP BY ' + @columns_csv +
        N' HAVING COUNT(*) > 1) duplicates;';

    EXEC sys.sp_executesql
        @sql,
        N'@duplicate_count bigint OUTPUT',
        @duplicate_count OUTPUT;

    IF @duplicate_count > 0
    BEGIN
        INSERT INTO @failures (failure)
        VALUES (@table_name + N' (' + @columns_csv + N'): ' + CONVERT(nvarchar(30), @duplicate_count) + N' duplicate groups');
    END
    ELSE
    BEGIN
        PRINT N'Duplicate audit passed: ' + @table_name + N' (' + @columns_csv + N')';
    END;

    FETCH NEXT FROM natural_key_checks INTO @table_name, @columns_csv, @not_null_filter;
END;

CLOSE natural_key_checks;
DEALLOCATE natural_key_checks;

IF EXISTS (SELECT 1 FROM @failures)
BEGIN
    SELECT failure FROM @failures ORDER BY failure;
    THROW 51000, 'Monitor duplicate audit failed. Resolve duplicate natural keys before creating unique indexes.', 1;
END;

IF OBJECT_ID(N'dbo.monitor', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.monitor', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.monitor', N'type_of_monitor') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'ux_monitor_serial_id_type_of_monitor')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_monitor_serial_id_type_of_monitor] ON dbo.[monitor] ([serial_id], [type_of_monitor]) WHERE [serial_id] IS NOT NULL AND [type_of_monitor] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.air_q_monitor_status', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_monitor_status', N'serial_id') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.air_q_monitor_status') AND name = N'ux_air_q_monitor_status_serial_id')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_air_q_monitor_status_serial_id] ON dbo.[air_q_monitor_status] ([serial_id]) WHERE [serial_id] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.omnidots_monitor_status', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_monitor_status', N'serial_id') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_monitor_status') AND name = N'ux_omnidots_monitor_status_serial_id')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_omnidots_monitor_status_serial_id] ON dbo.[omnidots_monitor_status] ([serial_id]) WHERE [serial_id] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.omnidots_sensor', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_sensor', N'serial_id') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_sensor') AND name = N'ux_omnidots_sensor_serial_id')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_omnidots_sensor_serial_id] ON dbo.[omnidots_sensor] ([serial_id]) WHERE [serial_id] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.svantek_monitor_status', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.svantek_monitor_status', N'serial_id') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_monitor_status') AND name = N'ux_svantek_monitor_status_serial_id')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_svantek_monitor_status_serial_id] ON dbo.[svantek_monitor_status] ([serial_id]) WHERE [serial_id] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.air_q_noise_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_noise_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_noise_level', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.air_q_noise_level') AND name = N'ux_air_q_noise_level_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_air_q_noise_level_serial_id_sample_time] ON dbo.[air_q_noise_level] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.svantek_noise_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.svantek_noise_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.svantek_noise_level', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_noise_level') AND name = N'ux_svantek_noise_level_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_svantek_noise_level_serial_id_sample_time] ON dbo.[svantek_noise_level] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.my_atm_dust_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.my_atm_dust_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.my_atm_dust_level', N'sample_time') IS NOT NULL
    AND COL_LENGTH(N'dbo.my_atm_dust_level', N'avrg') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.my_atm_dust_level') AND name = N'ux_my_atm_dust_level_serial_id_sample_time_avrg')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_my_atm_dust_level_serial_id_sample_time_avrg] ON dbo.[my_atm_dust_level] ([serial_id], [sample_time], [avrg]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL AND [avrg] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.my_atm_accessory_info', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.my_atm_accessory_info', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.my_atm_accessory_info', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.my_atm_accessory_info') AND name = N'ux_my_atm_accessory_info_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_my_atm_accessory_info_serial_id_sample_time] ON dbo.[my_atm_accessory_info] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.omnidots_peak_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_peak_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_peak_level', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_peak_level') AND name = N'ux_omnidots_peak_level_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_omnidots_peak_level_serial_id_sample_time] ON dbo.[omnidots_peak_level] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.omnidots_veff_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_veff_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_veff_level', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_veff_level') AND name = N'ux_omnidots_veff_level_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_omnidots_veff_level_serial_id_sample_time] ON dbo.[omnidots_veff_level] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.omnidots_vdv_level', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_vdv_level', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.omnidots_vdv_level', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_vdv_level') AND name = N'ux_omnidots_vdv_level_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_omnidots_vdv_level_serial_id_sample_time] ON dbo.[omnidots_vdv_level] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.air_q_noise_8_hour_average', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_noise_8_hour_average', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.air_q_noise_8_hour_average', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.air_q_noise_8_hour_average') AND name = N'ux_air_q_noise_8_hour_average_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_air_q_noise_8_hour_average_serial_id_sample_time] ON dbo.[air_q_noise_8_hour_average] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;

IF OBJECT_ID(N'dbo.svantek_noise_8_hour_average', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.svantek_noise_8_hour_average', N'serial_id') IS NOT NULL
    AND COL_LENGTH(N'dbo.svantek_noise_8_hour_average', N'sample_time') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_noise_8_hour_average') AND name = N'ux_svantek_noise_8_hour_average_serial_id_sample_time')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ux_svantek_noise_8_hour_average_serial_id_sample_time] ON dbo.[svantek_noise_8_hour_average] ([serial_id], [sample_time]) WHERE [serial_id] IS NOT NULL AND [sample_time] IS NOT NULL;');
END;
