# Database Performance And Index Review - 2026-06-09

## Scope

Reviewed the RVT Portal SPA alpha solution, existing project records, SQL Server `rvt`, and the local Timescale/PostgreSQL container.

- SQL Server source checked: `rvt`, SQL Server 2022 Developer Edition `16.0.4175.1`.
- PostgreSQL checked: `rvt` and `rvt_migrator_validation` in container `rvt-timescaledb`, PostgreSQL `17.10`.
- The Postgres `rvt` database is canonical but not fully data-loaded for all hypertables. The fuller Timescale evidence below uses `rvt_migrator_validation`, plus Timescale chunk rollups.
- No schema changes were applied during this review.

Implementation scripts added after this review:

- `database/sqlserver/performance_indexes_20260609.sql`
- `database/postgres/performance_indexes_20260609.sql`

Local apply status on 2026-06-09:

- Applied the SQL Server script to local SQL Server database `rvt`; catalog verification found all `25` expected performance indexes.
- Applied the PostgreSQL/Timescale script to local Postgres database `rvt`; catalog verification found all `25` expected performance indexes.
- The PostgreSQL script uses regular `CREATE INDEX IF NOT EXISTS` because TimescaleDB hypertables do not support `CREATE INDEX CONCURRENTLY`.
- Full Release application test suite passed after the local database apply: `177/177`.

## Application Query Patterns

The most important live query shapes are:

- Data grid/graph/download: `serial_id = ?`, `sample_time between ? and ?`, sorted by `sample_time`, across dust, noise, vibration, and aggregate views.
- Latest monitor detail: same measurement path with `pageSize = 1`, sorted by `sample_time desc`.
- Vibration trace list: `omnidots_trace_index.serial_id = ?` and `start_time between ? and ?`, ordered by `start_time desc`.
- Vibration trace detail: all `omnidots_trace` rows for one trace index.
- Dashboard: current deployments by `end_date is null`, open notifications by `monitor_id` and `closed_time is null`, alert rules by `monitor_id/is_active/alert_type`.
- Notification calendar/detail: `notification.monitor_id` plus `notification_time` ranges, and deployment matching by `monitor_id/start_date/end_date`.
- Generic repository searches page by running a filtered query plus count. If the filter/index is weak, both the page and count pay the cost.

## Current Evidence

Corrected SQL Server row counts and Timescale chunk rollups agree on the main volume:

| Table | Approx rows | Current concern |
|---|---:|---|
| `omnidots_trace` | 1,100,152 | No primary key; missing child-side FK/index on `omnidots_trace_index_id`. |
| `air_q_noise_level` | 866,939 | SQL Server has no useful index; Postgres only has time index. App filters by `serial_id` and time. |
| `svantek_noise_level` | 500,063 | SQL Server has no useful index; Postgres only has time index. Duplicate natural keys exist. |
| `omnidots_peak_level` | 500,003 | Has useful `(serial_id, sample_time)` index; duplicate natural keys exist. |
| `my_atm_dust_level` | 500,001 | SQL Server has no useful index; Postgres only has time index. App also filters by `avrg`. |
| `user_action_history` | 479,727 | Timescale composite PK includes `recorded_at`; SQL Server only single-column `id`. |
| `notification_sent` | 82,584 | Timescale composite PK includes `send_time`; SQL Server only single-column `id`. |
| `error_log` | 62,368 | Very large SQL Server storage footprint; Timescale composite PK includes `logged_at`. |
| `notification` | 55,024 | DMV strongly recommends notification indexes; app currently loads/filter/sorts too much in memory. |

SQL Server and Postgres both show missing child-side indexes for the same application FKs:

- `contract.company_id`
- `contract.site_id`
- `deployment.contract_id`
- `deployment.monitor_id`
- `notification_setting.site_user_id`
- `omnidots_trace.omnidots_trace_index_id`
- `rvt_alert_rule.monitor_id`
- `site_archived.site_id`
- `site_user.site_id`

ASP.NET Identity FK gaps also appear, but Identity tables are framework-managed and were explicitly excluded from the naming refactor.

## Recommended Indexes

### 1. Measurement Access

Add these first. They match the app's dominant measurement filters and should benefit SQL Server immediately; on Timescale they complement chunk pruning by putting `serial_id` first for per-monitor reads.

```sql
-- SQL Server shape
CREATE INDEX ix_my_atm_dust_level_serial_id_avrg_sample_time
ON dbo.my_atm_dust_level (serial_id, avrg, sample_time);

CREATE INDEX ix_air_q_noise_level_serial_id_sample_time
ON dbo.air_q_noise_level (serial_id, sample_time);

CREATE INDEX ix_svantek_noise_level_serial_id_sample_time
ON dbo.svantek_noise_level (serial_id, sample_time);

CREATE INDEX ix_omnidots_trace_omnidots_trace_index_id
ON dbo.omnidots_trace (omnidots_trace_index_id);

CREATE INDEX ix_omnidots_trace_index_serial_id_start_time
ON dbo.omnidots_trace_index (serial_id, start_time DESC);
```

Postgres equivalents should use the same key order. On hypertables, create the index on the hypertable root and let Timescale propagate it to chunks.

Also add provider-specific variants for:

- `air_q_noise_8_hour_average(serial_id, sample_time)`
- `svantek_noise_8_hour_average(serial_id, sample_time)`
- `site_average(monitor_id, collection_time)`
- `monitor(serial_id)`
- `monitor(fleet_row_count)` for exact fleet-number existence checks
- `omnidots_sensor(serial_id, lastseen desc)`
- `omnidots_monitor_status(serial_id)`
- `svantek_monitor_status(serial_id)`, optionally filtered/partial for rows with battery charge

### 2. Notifications And Dashboard

The SQL Server missing-index DMV repeatedly points at `notification`. The most useful starting set is:

```sql
CREATE INDEX ix_notification_monitor_id_notification_time
ON dbo.notification (monitor_id, notification_time);

CREATE INDEX ix_notification_open_monitor_alert_time
ON dbo.notification (monitor_id, alert_type, notification_time DESC)
WHERE closed_time IS NULL;

CREATE INDEX ix_rvt_alert_rule_monitor_active_type
ON dbo.rvt_alert_rule (monitor_id, is_active, alert_type);
```

For Postgres, use the same names and a partial index:

```sql
CREATE INDEX ix_notification_open_monitor_alert_time
ON public.notification (monitor_id, alert_type, notification_time DESC)
WHERE closed_time IS NULL;
```

### 3. Deployment And Visibility

Dashboard, notification matching, site options, and deployment detail all rely on these joins and filters:

```sql
CREATE INDEX ix_deployment_monitor_id_start_end
ON dbo.deployment (monitor_id, start_date, end_date);

CREATE INDEX ix_deployment_current_monitor_id_start_date
ON dbo.deployment (monitor_id, start_date DESC)
WHERE end_date IS NULL;

CREATE INDEX ix_deployment_contract_id_end_date
ON dbo.deployment (contract_id, end_date);

CREATE INDEX ix_site_user_user_site_dates
ON dbo.site_user (user_id, site_id, start_date, end_date);
```

Add the remaining FK-support indexes too:

- `contract(company_id)`
- `contract(site_id)`
- `notification_setting(site_user_id)`
- `site_archived(site_id)`

## Key Recommendations

Domain tables are mostly in good canonical shape: single-column primary keys are `id`, with expected exceptions for ASP.NET Identity and Timescale-adjusted tables.

For keyless measurement tables, do not add unique constraints blindly. Duplicate checks in the Timescale validation database found:

| Candidate natural key | Duplicate rows |
|---|---:|
| `my_atm_dust_level(serial_id, avrg, sample_time)` | 0 |
| `air_q_noise_level(serial_id, sample_time)` | 0 |
| `air_q_noise_8_hour_average(serial_id, sample_time)` | 0 |
| `svantek_noise_level(serial_id, sample_time)` | 29,228 |
| `omnidots_peak_level(serial_id, sample_time)` | 93 |
| `svantek_noise_8_hour_average(serial_id, sample_time)` | 1 |

Suggested path:

- Add non-unique performance indexes first.
- Add unique constraints only after deciding how to resolve duplicate source readings.
- For Timescale hypertables, any unique index must include the partitioning time column.
- `omnidots_trace` needs a design decision. It has only `omnidots_trace_index_id`, `x`, `y`, and `z`; the code has a TODO about trace ordering. Add the FK lookup index now, then introduce either a surrogate `id` or a `sample_index` column before treating it as fully keyed and deterministically ordered.

## Query/Model Follow-Up

Indexes will help, but two application behaviors should be fixed as data grows:

- `NotificationsController.Query` loads all notifications and then filters/sorts/pages in memory. Convert this to an `IQueryable` pipeline with database-side filtering, sorting, and paging.
- Dashboard builders load broad monitor/deployment/notification sets and shape them in memory. This is acceptable at the current size, but the indexes above should be paired with query narrowing before production-scale growth.

For aggregate measurement views, especially hourly/daily noise and vibration aggregates, consider Timescale continuous aggregates or maintained aggregate tables. The current views group raw readings; indexes help the base scans but do not eliminate repeated aggregation work.

For `%contains%` search over site/contract/fleet fields, normal B-tree indexes will not help much. Consider SQL Server full-text indexes or Postgres `pg_trgm` indexes if those screens become search hot spots.

## Verification Before Applying

Before applying indexes permanently:

1. Generate provider-specific migrations or scripts using canonical lowercase names.
2. Run `EXPLAIN`/actual execution plans for representative data grid, latest metric, dashboard, notification list, trace list, and trace detail queries.
3. Re-check write overhead on ingest-heavy tables after index creation.
4. Update `docs/database/database-constraint-index-name-registry.csv` for any accepted constraint or index names.
