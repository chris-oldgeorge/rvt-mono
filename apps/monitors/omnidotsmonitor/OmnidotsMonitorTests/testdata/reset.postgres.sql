TRUNCATE TABLE
    alert_delivery_outbox,
    alert_occurrence,
    notification_sent,
    notification,
    notification_setting,
    site_user,
    "AspNetUsers",
    omnidots_trace,
    omnidots_trace_index,
    omnidots_import_cursor,
    omnidots_vdv_level,
    omnidots_veff_level,
    omnidots_peak_level,
    omnidots_sensor,
    omnidots_monitor_status,
    omnidots_error_message,
    error_log,
    rvt_alert_rule,
    deployment,
    monitor,
    contract,
    site
RESTART IDENTITY CASCADE;

INSERT INTO rvt_alert_rule
    (id, monitor_id, serial_id, alert_field, limit_on, limit_off, alert_type,
     is_active, averaging_period, weekdays, saturdays, sundays, start_time,
     end_time, is_deleted, created, accessed)
VALUES
    ('00000000-0000-0000-0000-000000000001'::uuid, NULL, NULL,
     'offline-rule', 0, 0, 2, true, 86400, true, true, true, NULL, NULL,
     false, CURRENT_TIMESTAMP, NULL);
