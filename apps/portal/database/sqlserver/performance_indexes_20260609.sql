-- File summary: Adds canonical SQL Server performance indexes for the RVT Portal query paths.
-- Major updates:
-- - 2026-07-09 pending Added report-recipient and report-list indexes for paged assignment and report queries.
-- - 2026-06-09 pending Added measurement, notification, dashboard, visibility, and FK-support indexes.
--
-- Run after the canonical database naming scripts. The script is idempotent and can be rerun.
-- Existing ASP.NET Identity objects are intentionally not modified.

SET XACT_ABORT ON;

-- Measurement access: portal data grids, graphs, downloads, latest readings, and removal impact counts.
IF OBJECT_ID(N'dbo.my_atm_dust_level', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.my_atm_dust_level') AND name = N'ix_my_atm_dust_level_serial_id_avrg_sample_time')
BEGIN
    CREATE INDEX [ix_my_atm_dust_level_serial_id_avrg_sample_time]
    ON dbo.[my_atm_dust_level] ([serial_id], [avrg], [sample_time])
    INCLUDE ([pm_1], [pm_2_5], [pm_10], [pm_total], [weather_t], [weather_p], [weather_rh]);
END;

IF OBJECT_ID(N'dbo.air_q_noise_level', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.air_q_noise_level') AND name = N'ix_air_q_noise_level_serial_id_sample_time')
BEGIN
    CREATE INDEX [ix_air_q_noise_level_serial_id_sample_time]
    ON dbo.[air_q_noise_level] ([serial_id], [sample_time])
    INCLUDE ([laeq], [lamax], [la_90], [la_10], [lceq], [lcmax], [lc_90], [lc_10]);
END;

IF OBJECT_ID(N'dbo.svantek_noise_level', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_noise_level') AND name = N'ix_svantek_noise_level_serial_id_sample_time')
BEGIN
    CREATE INDEX [ix_svantek_noise_level_serial_id_sample_time]
    ON dbo.[svantek_noise_level] ([serial_id], [sample_time])
    INCLUDE ([laeq], [lamax], [la_90], [la_10], [lceq], [lcmax], [lc_90], [lc_10]);
END;

IF OBJECT_ID(N'dbo.air_q_noise_8_hour_average', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.air_q_noise_8_hour_average') AND name = N'ix_air_q_noise_8_hour_average_serial_id_sample_time')
BEGIN
    CREATE INDEX [ix_air_q_noise_8_hour_average_serial_id_sample_time]
    ON dbo.[air_q_noise_8_hour_average] ([serial_id], [sample_time])
    INCLUDE ([laeq], [lamax], [la_90], [la_10], [lceq], [lcmax], [lc_90], [lc_10], [number_of_samples]);
END;

IF OBJECT_ID(N'dbo.svantek_noise_8_hour_average', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_noise_8_hour_average') AND name = N'ix_svantek_noise_8_hour_average_serial_id_sample_time')
BEGIN
    CREATE INDEX [ix_svantek_noise_8_hour_average_serial_id_sample_time]
    ON dbo.[svantek_noise_8_hour_average] ([serial_id], [sample_time])
    INCLUDE ([laeq], [lamax], [la_90], [la_10], [lceq], [lcmax], [lc_90], [lc_10], [number_of_samples]);
END;

IF OBJECT_ID(N'dbo.site_average', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.site_average') AND name = N'ix_site_average_monitor_id_collection_time')
BEGIN
    CREATE INDEX [ix_site_average_monitor_id_collection_time]
    ON dbo.[site_average] ([monitor_id], [collection_time])
    INCLUDE ([field], [level]);
END;

IF OBJECT_ID(N'dbo.omnidots_trace', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_trace') AND name = N'ix_omnidots_trace_omnidots_trace_index_id')
BEGIN
    CREATE INDEX [ix_omnidots_trace_omnidots_trace_index_id]
    ON dbo.[omnidots_trace] ([omnidots_trace_index_id]);
END;

IF OBJECT_ID(N'dbo.omnidots_trace_index', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_trace_index') AND name = N'ix_omnidots_trace_index_serial_id_start_time')
BEGIN
    CREATE INDEX [ix_omnidots_trace_index_serial_id_start_time]
    ON dbo.[omnidots_trace_index] ([serial_id], [start_time] DESC);
END;

-- Monitor and vendor status lookups.
IF OBJECT_ID(N'dbo.monitor', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'ix_monitor_serial_id')
BEGIN
    CREATE INDEX [ix_monitor_serial_id]
    ON dbo.[monitor] ([serial_id]);
END;

IF OBJECT_ID(N'dbo.monitor', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.monitor') AND name = N'ix_monitor_fleet_nr')
BEGIN
    CREATE INDEX [ix_monitor_fleet_nr]
    ON dbo.[monitor] ([fleet_nr]);
END;

IF OBJECT_ID(N'dbo.omnidots_sensor', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_sensor') AND name = N'ix_omnidots_sensor_serial_id_lastseen')
BEGIN
    CREATE INDEX [ix_omnidots_sensor_serial_id_lastseen]
    ON dbo.[omnidots_sensor] ([serial_id], [lastseen] DESC)
    INCLUDE ([battery_charge], [online], [name]);
END;

IF OBJECT_ID(N'dbo.omnidots_monitor_status', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.omnidots_monitor_status') AND name = N'ix_omnidots_monitor_status_serial_id')
BEGIN
    CREATE INDEX [ix_omnidots_monitor_status_serial_id]
    ON dbo.[omnidots_monitor_status] ([serial_id]);
END;

IF OBJECT_ID(N'dbo.svantek_monitor_status', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.svantek_monitor_status') AND name = N'ix_svantek_monitor_status_serial_id')
BEGIN
    CREATE INDEX [ix_svantek_monitor_status_serial_id]
    ON dbo.[svantek_monitor_status] ([serial_id]);
END;

-- Notification, dashboard, and calendar access.
IF OBJECT_ID(N'dbo.notification', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.notification') AND name = N'ix_notification_monitor_id_notification_time')
BEGIN
    CREATE INDEX [ix_notification_monitor_id_notification_time]
    ON dbo.[notification] ([monitor_id], [notification_time]);
END;

IF OBJECT_ID(N'dbo.notification', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.notification') AND name = N'ix_notification_open_monitor_alert_time')
BEGIN
    CREATE INDEX [ix_notification_open_monitor_alert_time]
    ON dbo.[notification] ([monitor_id], [alert_type], [notification_time] DESC)
    INCLUDE ([alert_field], [level], [limit_on], [averaging_period], [recording_link])
    WHERE [closed_time] IS NULL;
END;

IF OBJECT_ID(N'dbo.rvt_alert_rule', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.rvt_alert_rule') AND name = N'ix_rvt_alert_rule_monitor_active_type')
BEGIN
    CREATE INDEX [ix_rvt_alert_rule_monitor_active_type]
    ON dbo.[rvt_alert_rule] ([monitor_id], [is_active], [alert_type]);
END;

-- Deployment and site visibility.
IF OBJECT_ID(N'dbo.deployment', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.deployment') AND name = N'ix_deployment_monitor_id_start_end')
BEGIN
    CREATE INDEX [ix_deployment_monitor_id_start_end]
    ON dbo.[deployment] ([monitor_id], [start_date], [end_date]);
END;

IF OBJECT_ID(N'dbo.deployment', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.deployment') AND name = N'ix_deployment_current_monitor_id_start_date')
BEGIN
    CREATE INDEX [ix_deployment_current_monitor_id_start_date]
    ON dbo.[deployment] ([monitor_id], [start_date] DESC)
    INCLUDE ([contract_id], [lat], [lng], [what_3_words], [location])
    WHERE [end_date] IS NULL;
END;

IF OBJECT_ID(N'dbo.deployment', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.deployment') AND name = N'ix_deployment_contract_id_end_date')
BEGIN
    CREATE INDEX [ix_deployment_contract_id_end_date]
    ON dbo.[deployment] ([contract_id], [end_date]);
END;

IF OBJECT_ID(N'dbo.site_user', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.site_user') AND name = N'ix_site_user_user_site_dates')
BEGIN
    CREATE INDEX [ix_site_user_user_site_dates]
    ON dbo.[site_user] ([user_id], [site_id], [start_date], [end_date]);
END;

-- Report and report-recipient access.
IF OBJECT_ID(N'dbo.report_user', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.report_user') AND name = N'ix_report_user_report_rule_id_user_id')
BEGIN
    CREATE INDEX [ix_report_user_report_rule_id_user_id]
    ON dbo.[report_user] ([report_rule_id], [user_id]);
END;

IF OBJECT_ID(N'dbo.report_rule', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.report_rule') AND name = N'ix_report_rule_site_id_deleted')
BEGIN
    CREATE INDEX [ix_report_rule_site_id_deleted]
    ON dbo.[report_rule] ([site_id], [deleted]);
END;

IF OBJECT_ID(N'dbo.report', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.report') AND name = N'ix_report_deleted_report_date')
BEGIN
    CREATE INDEX [ix_report_deleted_report_date]
    ON dbo.[report] ([deleted], [report_date] DESC);
END;

IF OBJECT_ID(N'dbo.site_user', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.site_user') AND name = N'ix_site_user_site_id_end_date_user_id')
BEGIN
    CREATE INDEX [ix_site_user_site_id_end_date_user_id]
    ON dbo.[site_user] ([site_id], [end_date], [user_id]);
END;

-- Child-side FK support for joins, deletes, and cascades.
IF OBJECT_ID(N'dbo.contract', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.contract') AND name = N'ix_contract_company_id')
BEGIN
    CREATE INDEX [ix_contract_company_id]
    ON dbo.[contract] ([company_id]);
END;

IF OBJECT_ID(N'dbo.contract', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.contract') AND name = N'ix_contract_site_id')
BEGIN
    CREATE INDEX [ix_contract_site_id]
    ON dbo.[contract] ([site_id]);
END;

IF OBJECT_ID(N'dbo.notification_setting', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.notification_setting') AND name = N'ix_notification_setting_site_user_id')
BEGIN
    CREATE INDEX [ix_notification_setting_site_user_id]
    ON dbo.[notification_setting] ([site_user_id]);
END;

IF OBJECT_ID(N'dbo.site_archived', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.site_archived') AND name = N'ix_site_archived_site_id')
BEGIN
    CREATE INDEX [ix_site_archived_site_id]
    ON dbo.[site_archived] ([site_id]);
END;

IF OBJECT_ID(N'dbo.site_user', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.site_user') AND name = N'ix_site_user_site_id')
BEGIN
    CREATE INDEX [ix_site_user_site_id]
    ON dbo.[site_user] ([site_id]);
END;
