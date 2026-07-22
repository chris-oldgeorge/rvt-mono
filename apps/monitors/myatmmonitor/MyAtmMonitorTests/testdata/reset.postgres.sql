-- Truncation resets nullable lease IDs alongside every other MyAtm-owned outbox state.
TRUNCATE TABLE monitor_delivery_outbox, my_atm_outbox_message, my_atm_alert_occurrence, notification_sent, notification, notification_setting, site_user,
  "AspNetUsers", my_atm_accessory_info, my_atm_dust_level_8_hour_avg,
  my_atm_dust_level, monitor, deployment, contract, site, rvt_alert_rule,
  my_atm_error_message, error_log RESTART IDENTITY CASCADE;

INSERT INTO rvt_alert_rule
  (id, monitor_id, serial_id, alert_field, limit_on, limit_off, alert_type,
   is_active, averaging_period, weekdays, saturdays, sundays, is_deleted, created)
VALUES
  ('00000000-0000-0000-0000-000000000001'::uuid, NULL, NULL, 'offline-rule', 0, 0, 2,
   true, 86400, true, true, true, false, CURRENT_TIMESTAMP);
