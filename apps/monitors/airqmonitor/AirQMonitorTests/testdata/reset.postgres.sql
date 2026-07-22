TRUNCATE TABLE notification_sent, notification, notification_setting, site_user,
  "AspNetUsers", site_average, air_q_noise_8_hour_average, air_q_noise_level,
  air_q_monitor_status, monitor, deployment, contract, site, rvt_alert_rule,
  error_log RESTART IDENTITY CASCADE;

TRUNCATE TABLE air_q_error_message RESTART IDENTITY CASCADE;

INSERT INTO rvt_alert_rule
  (id, monitor_id, serial_id, alert_field, limit_on, limit_off, alert_type,
   is_active, averaging_period, weekdays, saturdays, sundays, is_deleted, created)
VALUES
  ('00000000-0000-0000-0000-000000000001'::uuid, NULL, NULL, 'offline-rule', 0, 0, 2,
   true, 86400, true, true, true, false, CURRENT_TIMESTAMP);
