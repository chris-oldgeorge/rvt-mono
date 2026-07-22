-- File summary: Adds canonical PostgreSQL/TimescaleDB performance indexes for the RVT Portal query paths.
-- Major updates:
-- - 2026-07-09 pending Added report-recipient and report-list indexes for paged assignment and report queries.
-- - 2026-06-09 pending Added measurement, notification, dashboard, visibility, and FK-support indexes.
--
-- Run after the canonical database naming scripts. The script is idempotent and can be rerun.
-- TimescaleDB hypertables do not support CREATE INDEX CONCURRENTLY; run during a reviewed maintenance window.
-- Existing ASP.NET Identity objects are intentionally not modified.

SET lock_timeout = '5s';

-- Measurement access: portal data grids, graphs, downloads, latest readings, and removal impact counts.
CREATE INDEX IF NOT EXISTS ix_my_atm_dust_level_serial_id_avrg_sample_time
ON public.my_atm_dust_level (serial_id, avrg, sample_time)
INCLUDE (pm_1, pm_2_5, pm_10, pm_total, weather_t, weather_p, weather_rh);

CREATE INDEX IF NOT EXISTS ix_air_q_noise_level_serial_id_sample_time
ON public.air_q_noise_level (serial_id, sample_time)
INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10);

CREATE INDEX IF NOT EXISTS ix_svantek_noise_level_serial_id_sample_time
ON public.svantek_noise_level (serial_id, sample_time)
INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10);

CREATE INDEX IF NOT EXISTS ix_air_q_noise_8_hour_average_serial_id_sample_time
ON public.air_q_noise_8_hour_average (serial_id, sample_time)
INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10, number_of_samples);

CREATE INDEX IF NOT EXISTS ix_svantek_noise_8_hour_average_serial_id_sample_time
ON public.svantek_noise_8_hour_average (serial_id, sample_time)
INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10, number_of_samples);

CREATE INDEX IF NOT EXISTS ix_site_average_monitor_id_collection_time
ON public.site_average (monitor_id, collection_time)
INCLUDE (field, level);

CREATE INDEX IF NOT EXISTS ix_omnidots_trace_omnidots_trace_index_id
ON public.omnidots_trace (omnidots_trace_index_id);

CREATE INDEX IF NOT EXISTS ix_omnidots_trace_index_serial_id_start_time
ON public.omnidots_trace_index (serial_id, start_time DESC);

-- Monitor and vendor status lookups.
CREATE INDEX IF NOT EXISTS ix_monitor_serial_id
ON public.monitor (serial_id);

CREATE INDEX IF NOT EXISTS ix_monitor_fleet_nr
ON public.monitor (fleet_nr);

CREATE INDEX IF NOT EXISTS ix_omnidots_sensor_serial_id_lastseen
ON public.omnidots_sensor (serial_id, lastseen DESC)
INCLUDE (battery_charge, online, name);

CREATE INDEX IF NOT EXISTS ix_omnidots_monitor_status_serial_id
ON public.omnidots_monitor_status (serial_id);

CREATE INDEX IF NOT EXISTS ix_svantek_monitor_status_serial_id
ON public.svantek_monitor_status (serial_id);

-- Notification, dashboard, and calendar access.
CREATE INDEX IF NOT EXISTS ix_notification_monitor_id_notification_time
ON public.notification (monitor_id, notification_time);

CREATE INDEX IF NOT EXISTS ix_notification_open_monitor_alert_time
ON public.notification (monitor_id, alert_type, notification_time DESC)
INCLUDE (alert_field, level, limit_on, averaging_period, recording_link)
WHERE closed_time IS NULL;

CREATE INDEX IF NOT EXISTS ix_rvt_alert_rule_monitor_active_type
ON public.rvt_alert_rule (monitor_id, is_active, alert_type);

-- Deployment and site visibility.
CREATE INDEX IF NOT EXISTS ix_deployment_monitor_id_start_end
ON public.deployment (monitor_id, start_date, end_date);

CREATE INDEX IF NOT EXISTS ix_deployment_current_monitor_id_start_date
ON public.deployment (monitor_id, start_date DESC)
INCLUDE (contract_id, lat, lng, what_3_words, location)
WHERE end_date IS NULL;

CREATE INDEX IF NOT EXISTS ix_deployment_contract_id_end_date
ON public.deployment (contract_id, end_date);

CREATE INDEX IF NOT EXISTS ix_site_user_user_site_dates
ON public.site_user (user_id, site_id, start_date, end_date);

-- Report and report-recipient access.
CREATE INDEX IF NOT EXISTS ix_report_user_report_rule_id_user_id
ON public.report_user (report_rule_id, user_id);

CREATE INDEX IF NOT EXISTS ix_report_rule_site_id_deleted
ON public.report_rule (site_id, deleted);

CREATE INDEX IF NOT EXISTS ix_report_deleted_report_date
ON public.report (deleted, report_date DESC);

CREATE INDEX IF NOT EXISTS ix_site_user_site_id_end_date_user_id
ON public.site_user (site_id, end_date, user_id);

-- Child-side FK support for joins, deletes, and cascades.
CREATE INDEX IF NOT EXISTS ix_contract_company_id
ON public.contract (company_id);

CREATE INDEX IF NOT EXISTS ix_contract_site_id
ON public.contract (site_id);

CREATE INDEX IF NOT EXISTS ix_notification_setting_site_user_id
ON public.notification_setting (site_user_id);

CREATE INDEX IF NOT EXISTS ix_site_archived_site_id
ON public.site_archived (site_id);

CREATE INDEX IF NOT EXISTS ix_site_user_site_id
ON public.site_user (site_id);
