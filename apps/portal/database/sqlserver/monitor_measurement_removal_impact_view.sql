-- File summary: Creates the SQL Server removal-impact aggregate view used by monitor deletion checks.
-- Major updates:
-- - 2026-06-25 pending Added one database-side aggregate for serial-id keyed measurement/status counts.
--
-- Run after canonical database naming and view-module scripts.

CREATE OR ALTER VIEW [dbo].[monitor_measurement_removal_impact]
AS
WITH measurement_counts AS (
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[my_atm_dust_level]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[my_atm_dust_level_8_hour_avg]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[noise_level_15_min_avg]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[noise_level_1_hour_avg]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[noise_level_1_day_avg]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level_1_min]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level_15_min]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level_20_min]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level_5_min]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_peak_level_1_day_peak]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_trace_index]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[omnidots_monitor_status]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]

    UNION ALL
    SELECT [serial_id], CAST(COUNT_BIG(*) AS bigint) AS [row_count]
    FROM [dbo].[svantek_monitor_status]
    WHERE [serial_id] IS NOT NULL AND LTRIM(RTRIM([serial_id])) <> ''
    GROUP BY [serial_id]
)
SELECT
    [serial_id],
    CAST(COUNT_BIG(*) AS bigint) AS [measurement_table_count],
    CAST(COALESCE(SUM([row_count]), 0) AS bigint) AS [measurement_row_count]
FROM measurement_counts
GROUP BY [serial_id];
