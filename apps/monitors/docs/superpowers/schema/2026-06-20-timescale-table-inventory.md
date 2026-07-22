# Timescale Schema Inventory

Captured on 2026-06-20 using only safe evidence. Docker container environment variables were not inspected.

Source:

- Docker container: `rvt-timescaledb`
- Image: `timescale/timescaledb:latest-pg17`
- Host port: `5432`
- Safe command used: `docker ps --format '{{.Names}} {{.Image}} {{.Ports}}'`
- Safe command result included: `rvt-timescaledb timescale/timescaledb:latest-pg17 0.0.0.0:5432->5432/tcp, [::]:5432->5432/tcp`

Credentials were not written to this file.

## Connection Discovery

No safe PostgreSQL connection string was found in app configuration or YAML files during this local scan:

```bash
rg -n "Postgre|Timescale|ConnectionStrings|RVT_TIMESCALE_SCHEMA_CONNECTION|Host=localhost|Port=5432|Username=|Password=|Database=" -g 'appsettings*.json' -g '*.yml' -g '*.yaml' -g '*.json'
```

Because no credential-safe connection string was discoverable, live `psql` inventory was not run. Live table and column inventory needs `RVT_TIMESCALE_SCHEMA_CONNECTION` to be supplied in the current shell.

Required live commands once the connection string is available:

```bash
psql "$RVT_TIMESCALE_SCHEMA_CONNECTION" -c "\dt public.*"
psql "$RVT_TIMESCALE_SCHEMA_CONNECTION" -c "select table_name, column_name, data_type from information_schema.columns where table_schema = 'public' order by table_name, ordinal_position;"
```

## Tables

Live Timescale table names returned by `\dt public.*`: needs `RVT_TIMESCALE_SCHEMA_CONNECTION`.

Safe canonical table evidence from provider identifier maps:

- Shared: monitor, deployment, contract, site, site_user, notification_setting, notification, notification_sent, rvt_alert_rule, `"AspNetUsers"`.
- AirQ: air_q_monitor_status, air_q_noise_level, air_q_noise_8_hour_average, air_q_error_message, site_average.
- MyAtm: my_atm_dust_level, my_atm_dust_level_8_hour_avg, my_atm_accessory_info, my_atm_error_message.
- Omnidots: omnidots_monitor_status, omnidots_sensor, omnidots_peak_level, omnidots_veff_level, omnidots_vdv_level, omnidots_trace_index, omnidots_trace, omnidots_error_message.
- Svantek: svantek_monitor_status, svantek_noise_level, svantek_noise_8_hour_average, svantek_error_message, site_average.

Evidence sources:

- `airqmonitor/AirQMonitor/api/db/AirQMonitorDbOptions.cs`
- `myatmmonitor/MyAtmMonitor/api/db/MyAtmMonitorDbOptions.cs`
- `omnidotsmonitor/OmnidotsMonitor/api/db/OmnidotsMonitorDbOptions.cs`
- `svantekmonitor/SvantekMonitor/api/db/SvantekMonitorDbOptions.cs`

## Columns

Live Timescale columns from `information_schema.columns`: needs `RVT_TIMESCALE_SCHEMA_CONNECTION`.

Safe canonical column-name evidence from `rvt-monitor-common/Rvt.Monitor.Common/Data/MonitorDb.cs`:

- Common identity and monitor fields: id, fleet_row_count, serial_id, customer_id, listed_at_time, model, location_id, latitude, longitude, location_address, time_zone, customer_display_name, manufacturer, firmware_version, type_of_monitor, offline, battery_status, last_data_time_1_min, last_data_time_15_min, last_data_time_1_hour, last_data_time_24_hour.
- Shared relationship and notification fields: monitor_id, contract_id, site_id, user_id, site_user_id, notification_id, notification_time, send_time, address, error_message, alert_field, alert_type, limit_on, limit_off, averaging_period, level, field, collection_time, closed_time, closed_by_user, is_active, is_deleted, created, accessed, weekdays, saturdays, sundays, start_time, end_time, sat_start_time, sat_end_time, sun_start_time, sun_end_time, start_date, end_date, email, sms, phone_number.
- Status and sample fields: update_time, status, error_count, battery_voltage, calibration_date, calibration_due, filter_change_date, pump_hours, sample_time.
- Noise fields: laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10, number_of_samples.
- MyAtm dust fields: avrg, pm_1, pm_2_5, pm_10, pm_total, weather_t, weather_p, weather_rh.
- Omnidots sensor and status fields: name, lastseen, battery_charge, connected_using, online, measurement_duration, data_save_level, vdv_enabled, vdv_x, vdv_y, vdv_z, vdv_period, trace_save_level, trace_pre_trigger, trace_post_trigger, alarm_value, flat_level, disable_led, log_flush_interval, guide_line, building_level, vector_enabled, atop_enabled, vtop_enabled.
- Omnidots peak fields: x_fdom, x_vtop, x_vtop_overflow, y_fdom, y_vtop, y_vtop_overflow, z_fdom, z_vtop, z_vtop_overflow.

No usernames, passwords, hostnames beyond `localhost`, or full connection strings are included.

## Status

- Container evidence: captured.
- Canonical table mapping evidence: captured from local source.
- Canonical column mapping evidence: captured from local source.
- Live `public` table list: needs `RVT_TIMESCALE_SCHEMA_CONNECTION`.
- Live `public` column inventory: needs `RVT_TIMESCALE_SCHEMA_CONNECTION`.
