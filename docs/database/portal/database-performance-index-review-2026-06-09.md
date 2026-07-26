# Database Performance Index Review — 2026-06-09

## Scope and evidence

The review used the PostgreSQL/TimescaleDB `rvt` database and the fuller
`rvt_migrator_validation` data set on PostgreSQL 17.10. The validation set
contained about 4.3 million rows across 48 base tables and 10 hypertables.

The release asset is
`database/postgres/performance_indexes_20260609.sql`. It creates the 25
approved indexes with `CREATE INDEX IF NOT EXISTS`; Timescale hypertables do
not support concurrent index creation.

## Highest-volume tables

| Table | Approximate rows | Primary concern |
| --- | ---: | --- |
| `omnidots_trace` | 1,100,152 | Missing child-side support for trace-index joins. |
| `air_q_noise_level` | 866,939 | Per-monitor time reads need serial first. |
| `svantek_noise_level` | 500,063 | Duplicate natural keys require separate remediation. |
| `omnidots_peak_level` | 500,003 | Existing serial/time access is useful. |
| `my_atm_dust_level` | 500,001 | Reads also filter by average period. |
| `user_action_history` | 479,727 | Timescale key includes `recorded_at`. |
| `notification_sent` | 82,584 | Timescale key includes `send_time`. |
| `error_log` | 62,368 | Retention and write volume need monitoring. |
| `notification` | 55,024 | Dashboard and delivery filtering need bounded indexes. |

The review also found missing child-side indexes for application foreign keys,
including `contract.company_id`, `site.contract_id`, monitor/deployment
relations, notification contacts, and report-rule membership.

## Index priorities

1. Measurement reads: put `serial_id` before sample time and include
   `avrg` where the query contract filters it.
2. Notifications/dashboard: index monitor/status/time access and the due-work
   predicates used by delivery.
3. Foreign keys: add child-side indexes where deletes, joins, or ownership
   checks use the relationship.
4. Search: use B-tree indexes for equality/prefix filters. If contains-search
   becomes a measured hotspot, evaluate `pg_trgm` separately.
5. Aggregate views: consider continuous aggregates or maintained rollups only
   after measuring the indexed base-table plans.

Do not add unique indexes to measurement tables until duplicate natural keys
have been profiled and a cleanup policy is approved.

## Application and verification order

1. Restore a production-like backup to an isolated PostgreSQL/TimescaleDB
   database.
2. Capture representative `EXPLAIN (ANALYZE, BUFFERS)` plans and latency.
3. Apply `database/postgres/performance_indexes_20260609.sql` with lock and
   statement timeouts appropriate to the maintenance window.
4. Confirm all 25 expected indexes and unchanged hypertable/chunk metadata.
5. Re-run dashboard, notification, measurement, archive, and search plans.
6. Record write amplification, index size, and regression evidence.
7. Roll back only the affected release through its reviewed PostgreSQL rollback
   procedure or restore the verified backup if integrity is uncertain.
