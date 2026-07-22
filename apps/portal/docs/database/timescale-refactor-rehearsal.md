# Timescale Refactor Rehearsal

Generated: 2026-06-08

This note records the canonical database naming rehearsals and the development TimescaleDB migration. The local development `rvt` database has been physically renamed to canonical names. Do not apply the same operation to any future production-scale database until a current backup/restore rehearsal has been completed on equivalent data.

## Local Timescale Findings

- Container: `rvt-timescaledb`
- Database: `rvt`
- TimescaleDB version: `2.27.1`
- Current hypertables: `10`
- Current chunks: present across the hypertables
- Compression: not enabled on current hypertables
- Continuous aggregates: none found
- Timescale jobs: telemetry and job-history retention only; no hypertable retention/compression policy jobs found

Current hypertables detected:

| Current hypertable | Canonical name |
| --- | --- |
| `AirQNoise8HourAverage` | `air_q_noise_8_hour_average` |
| `AirQNoiseLevels` | `air_q_noise_level` |
| `ErrorLog` | `error_log` |
| `MyAtmDustLevels` | `my_atm_dust_level` |
| `NotificationsSent` | `notification_sent` |
| `OmnidotsPeakLevels` | `omnidots_peak_level` |
| `SiteAverages` | `site_average` |
| `SvantekNoise8HourAverage` | `svantek_noise_8_hour_average` |
| `SvantekNoiseLevels` | `svantek_noise_level` |
| `UserActionsHistory` | `user_action_history` |

## Rehearsal Order

1. Clone or restore the Timescale database into a disposable rehearsal database.
2. Run `database/postgres/verify_timescale_after_rename.sql` and save the pre-rename output.
3. Run `database/postgres/canonical_database_naming.sql`.
4. Run `database/postgres/canonical_constraint_index_naming.sql`.
5. Run `database/postgres/verify_timescale_after_rename.sql` again and compare the output.
6. Run representative dashboard, monitor, archive, and time-bucket queries before and after the rename.
7. Record lock duration, query plan changes, missing objects, and any legacy-name dependencies.
8. Rehearse rollback with `canonical_constraint_index_naming_rollback.sql` followed by `canonical_database_naming_rollback.sql`.

## Verification Expectations

- All expected hypertables should report `renamed` after the canonical rename script.
- Chunk counts should remain stable before and after the rename.
- Compression and continuous aggregate warnings should remain `0` unless those features are added later.
- No application deployment should use the opt-in EF canonical mapping until the physical rename rehearsal succeeds.

## Local Clone Rehearsal Result - 2026-06-08

Rehearsal database: `rvt_naming_rehearsal_20260608222014`

Execution sequence:

1. Cloned local `rvt` with `createdb -T rvt`.
2. Applied `canonical_database_naming.sql`.
3. Applied `canonical_constraint_index_naming.sql`.
4. Ran `verify_timescale_after_rename.sql`.
5. Ran public-view and representative query smoke checks.
6. Applied rollback scripts, corrected rollback ordering, recreated the clone, and repeated forward -> rollback -> forward successfully.

Results:

- Base table count stayed at `48`.
- All 10 expected hypertables reported `renamed`.
- Legacy hypertable names still present: `0`.
- Missing expected canonical hypertables: `0`.
- Compressed hypertables: `0`.
- Continuous aggregates: `0`.
- No non-lowercase public table, view, column, constraint, or index names were found after the final forward pass.
- Public views compiled with `select * from public.<view> limit 0`.
- Representative queries returned: dashboard `4`, site search `442`, monitor search `2282`, help articles `0`, AirQ hourly buckets `5`, site-average daily buckets `5`.

Chunk counts after rename:

| Canonical hypertable | Chunks |
| --- | ---: |
| `air_q_noise_8_hour_average` | 94 |
| `air_q_noise_level` | 99 |
| `error_log` | 34 |
| `my_atm_dust_level` | 1 |
| `notification_sent` | 99 |
| `omnidots_peak_level` | 1 |
| `site_average` | 99 |
| `svantek_noise_8_hour_average` | 65 |
| `svantek_noise_level` | 1 |
| `user_action_history` | 108 |

Rollback finding and fix:

- The first rollback attempt exposed an ordering defect in `canonical_database_naming_rollback.sql`: relation names were restored before columns, so column rollback statements no longer targeted the canonical relation names.
- The Postgres and SQL Server rollback scripts were reordered so columns are restored first and relations last.
- `DatabaseNamingConventionTests` now includes a guardrail that verifies both provider rollback scripts keep column renames before relation renames.
- The corrected Postgres rollback restored legacy relation, column, and selected PK names on the clone before the final forward pass was re-applied.

## Current Open Items

- Constraint/index scripts still need human registry review before production use.
- Registry exceptions still need review, especially natural primary keys and measurement acronyms.
- The old SQL Server-to-Postgres migrator must be updated to use the saved name-equivalence CSV before writing into a renamed Postgres schema.
- Repeat the same rehearsal against a full production backup, or another 400M-row-equivalent data set, before any later production-scale historical import/cutover.

## PostgreSQL Routine Rehearsal - 2026-06-09

Target database: `rvt_naming_rehearsal_20260608222014`.

Applied `RVT.DatabaseMigrator/post-load/04_routines.sql` to the canonical rehearsal clone and executed all five exported routine ports:

| Routine | Smoke result |
| --- | --- |
| `public.monitor_status_time_check` | `1` row |
| `public.monitor_status_for_month` | `1` row |
| `public.peak_record_breach_and_alerts` | `341` rows |
| `public.error_insert` | `CALL` succeeded inside rollback transaction |
| `public.user_actions_history_insert` | `CALL` succeeded inside rollback transaction |

Finding:

- `user_action_history.id` has no default in the canonical clone, while its Timescale primary key includes `(id, recorded_at)`. The first routine smoke failed until `user_actions_history_insert` was updated to write `gen_random_uuid()` explicitly.

Verification:

- `04_routines.sql` reapplied successfully with `ON_ERROR_STOP=1`.
- Rolled-back procedure smoke confirmed no rehearsal audit/error rows were committed.

## Local Timing And Query Plan Rehearsal - 2026-06-09

Rehearsal database: `rvt_perf_rehearsal_202606090145`.

Execution sequence:

1. Cloned local `rvt` with `createdb -T rvt`.
2. Captured pre-rename `EXPLAIN (ANALYZE, BUFFERS)` plans for dashboard, site search, monitor search, and an AirQ hourly `time_bucket` query.
3. Applied `canonical_database_naming.sql`, `canonical_constraint_index_naming.sql`, `03_views_and_routines.sql`, and `04_routines.sql` with `lock_timeout='5s'`.
4. Captured post-rename plans using canonical relation and column names.
5. Ran `verify_timescale_after_rename.sql`, compiled every public view with `SELECT * FROM public.<view> LIMIT 0`, and ran representative row-count smoke checks.

Local script timings:

| Step | Wall-clock result |
| --- | ---: |
| Clone from `rvt` | ~1.8s |
| `canonical_database_naming.sql` | 0.41s |
| `canonical_constraint_index_naming.sql` | 0.17s |
| `03_views_and_routines.sql` | 0.14s |
| `04_routines.sql` | 0.07s |

These are local clone timings only. They show no `5s` lock-timeout failures in the rehearsal, but they are not a substitute for production-like lock-duration evidence.

Representative query plans:

| Query | Pre planning | Pre execution | Post planning | Post execution | Plan note |
| --- | ---: | ---: | ---: | ---: | --- |
| Admin dashboard count | 2.098ms | 0.727ms | 1.559ms | 0.589ms | Same hash aggregate/join shape after canonical names. |
| Site search count | 0.952ms | 1.276ms | 0.785ms | 1.400ms | Same broad join shape; Identity joins now cast `AspNetUsers.Id` to `uuid` for canonical user columns. |
| Monitor search count | 0.594ms | 0.850ms | 0.947ms | 1.899ms | Canonical view includes the offline alert-rule subquery; review with production-like data before cutover. |
| AirQ hourly `time_bucket` | 12.183ms | 0.860ms | 11.805ms | 0.983ms | Timescale still uses `Custom Scan (ChunkAppend)` and renamed chunk indexes such as `_hyper_1_93_chunk_ix_air_q_noise_level_sample_time`. |

Post-rename verification:

- All 10 expected hypertables reported `renamed`.
- Missing expected canonical hypertables: `0`.
- Legacy hypertable names still present: `0`.
- Compressed hypertables: `0`.
- Continuous aggregates: `0`.
- Public view compile smoke succeeded for every public view.
- Representative counts returned: dashboard `4`, site search `442`, monitor search `2282`, users-for-site search `1296`.

Post-load issues found and fixed:

- `notification_user_search` and `omnidots_read_status` were missing terminating semicolons before the next view block.
- Several view joins compared framework-managed `AspNetUsers.Id` (`varchar`) to canonical `site_user.user_id` / `report_user.user_id` (`uuid`) without casts.
- `site_search` and `site_user_search` incorrectly referenced `U.email` instead of the Identity column `U."Email"`.
- `CutoverReadinessTests` now includes guardrails for view statement termination and Identity-to-canonical UUID joins.

## Production-Like Restored Dataset Rehearsal - 2026-06-09

Rehearsal database: `rvt_prodlike_rehearsal_20260609083554`.

This rehearsal used the local seeded Timescale image data, restored through a logical backup/restore path rather than `createdb -T`. It is production-like for schema, Timescale hypertables, view/routine behavior, and several million migrated rows, but it is not the final full-volume rehearsal for the expected ~400M production history.

Restore setup:

1. Dumped source database `rvt` from container `rvt-timescaledb` with `pg_dump -Fc`.
2. Dump artifact: `/tmp/rvt_prodlike_rehearsal_20260609083554.dump`, `292.3M`.
3. Created database `rvt_prodlike_rehearsal_20260609083554`.
4. Enabled TimescaleDB and ran `timescaledb_pre_restore()`.
5. Restored with `pg_restore --no-owner --no-privileges --exit-on-error`.
6. Ran `timescaledb_post_restore()`.

Restore timings:

| Step | Wall-clock result |
| --- | ---: |
| `pg_dump -Fc rvt` | ~23.8s |
| `pg_restore` into validation DB | ~25.7s |
| `timescaledb_pre_restore()` / `post_restore()` | passed |

Pre-rename baseline:

| Metric | Value |
| --- | ---: |
| Database size | `992 MB` |
| Base tables | `48` |
| Columns | `733` |
| Estimated user-table rows | `4,316,843` |
| Hypertables | `10` |
| Compressed hypertables | `0` |
| Continuous aggregates | `0` |

Hypertable row-count sample:

| Legacy hypertable | Rows |
| --- | ---: |
| `AirQNoise8HourAverage` | `26,629` |
| `AirQNoiseLevels` | `866,939` |
| `ErrorLog` | `62,335` |
| `MyAtmDustLevels` | `500,001` |
| `NotificationsSent` | `82,584` |
| `OmnidotsPeakLevels` | `500,003` |
| `SiteAverages` | `42,606` |
| `SvantekNoise8HourAverage` | `60,000` |
| `SvantekNoiseLevels` | `500,063` |
| `UserActionsHistory` | `479,607` |

Forward execution:

| Step | Wall-clock result |
| --- | ---: |
| `canonical_database_naming.sql` | ~0.24s |
| `canonical_constraint_index_naming.sql` | ~0.05s |
| `03_views_and_routines.sql` | ~0.02s |
| `04_routines.sql` | <0.01s |

The rename scripts ran with `SET LOCAL lock_timeout = '5s'` and no lock-timeout failures were observed on the restored validation database.

Representative query-plan comparison:

| Query | Pre planning | Pre execution | Post planning | Post execution | Plan note |
| --- | ---: | ---: | ---: | ---: | --- |
| Dashboard count | 0.134ms | 0.337ms | 0.126ms | 0.295ms | Same hash aggregate/join shape after canonical names. |
| Site search count | 0.278ms | 0.919ms | 0.229ms | 0.932ms | Same broad join shape; Identity table remains `AspNetUsers`. |
| Monitor search count | 0.227ms | 0.759ms | 0.202ms | 0.926ms | Canonical view includes the offline alert-rule lookup. |
| AirQ hourly `time_bucket` | 1.429ms | 0.636ms | 0.970ms | 0.600ms | Timescale still uses `Custom Scan (ChunkAppend)` and the renamed chunk index. |

Post-rename verification:

- All 10 expected hypertables reported `renamed`.
- Chunk counts were preserved: `94`, `99`, `34`, `1`, `99`, `1`, `99`, `65`, `1`, `108`.
- Missing expected canonical hypertables: `0`.
- Legacy hypertable names still present: `0`.
- Compressed hypertables: `0`.
- Continuous aggregates: `0`.
- Noncanonical public relations, columns, indexes, and constraints excluding ASP.NET Identity: `0`.
- Public view compile smoke passed for all public views in `10.394s`.
- Representative counts returned: dashboard `4`, site search `442`, monitor search `2,282`, `air_q_noise_level` `866,939`, `site_average` `42,606`.

Routine smoke:

| Routine | Smoke result |
| --- | --- |
| `public.monitor_status_time_check` | `1` row |
| `public.monitor_status_for_month` | `0` rows for the current month sample |
| `public.peak_record_breach_and_alerts` | `0` rows for the current timestamp sample |
| `public.error_insert` | `CALL` succeeded inside rollback transaction |
| `public.user_actions_history_insert` | `CALL` succeeded inside rollback transaction |

Rollback rehearsal:

| Step | Wall-clock result |
| --- | ---: |
| `canonical_constraint_index_naming_rollback.sql` | ~0.08s |
| `canonical_database_naming_rollback.sql` | ~0.25s |

Rollback verification:

- All 10 expected hypertables reported `pre_rename`.
- Selected legacy checks passed: `public."Sites"` returned `442`, `public."MonitorsList"` returned `786`, and `public."AdminDashboardData"` returned `4`.
- The validation database was then moved forward again and left in canonical state.

Final canonical state:

- Database size: `995 MB`.
- Base tables: `48`.
- Hypertables: `10`.
- Compressed hypertables: `0`.
- Continuous aggregates: `0`.
- Noncanonical public relations, columns, indexes, and constraints excluding ASP.NET Identity: `0`.
- Final public view compile smoke passed in `10.394s`.

Remaining scale caveat:

- This restored rehearsal validates the scripted backup/restore path and canonical rename behavior on the seeded local Timescale data set.
- A full-volume rehearsal against a true production backup, or another representative ~400M-row data set, remains required before any future production-scale cutover because lock pressure and query plans may change materially at that size.

## Development Database Migration - 2026-06-09

Target database: `rvt` in local container `rvt-timescaledb`.

The development database was physically migrated to canonical database names after the seeded restored rehearsal passed and the team confirmed that the app is still in development. No feature-freeze window was used, and no `legacy` compatibility views were deployed.

Safety backup:

- Backup artifact: `/tmp/rvt_pre_canonical_migration_20260609.dump`.
- Backup type: `pg_dump -Fc`.
- Backup size: `292.3M`.
- Restore note: the dump was taken before any physical rename was applied to `rvt`.

Execution sequence:

1. `database/postgres/canonical_database_naming.sql`.
2. `database/postgres/canonical_constraint_index_naming.sql`.
3. `RVT.DatabaseMigrator/post-load/03_views_and_routines.sql`.
4. `RVT.DatabaseMigrator/post-load/04_routines.sql`.

Compatibility decision:

- The `legacy` compatibility schema was not present before migration.
- The `legacy` compatibility scripts were not run.
- Post-migration verification confirmed that the `legacy` schema still does not exist.

Post-migration verification:

- All 10 expected hypertables reported `renamed`.
- Chunk counts were preserved: `94`, `99`, `34`, `1`, `99`, `1`, `99`, `65`, `1`, `108`.
- Missing expected canonical hypertables: `0`.
- Legacy hypertable names still present: `0`.
- Compressed hypertables: `0`.
- Continuous aggregates: `0`.
- Final database size: `966 MB`.
- Noncanonical public relations, columns, indexes, and constraints excluding ASP.NET Identity: `0`.
- Public view compile smoke passed in about `11.46s`.
- Representative counts returned: dashboard `4`, site search `442`, monitor search `2,282`, `air_q_noise_level` `866,939`, `site_average` `42,606`.

Routine smoke:

| Routine | Smoke result |
| --- | --- |
| `public.monitor_status_time_check` | `1` row |
| `public.monitor_status_for_month` | `0` rows for the current month sample |
| `public.peak_record_breach_and_alerts` | `0` rows for the current timestamp sample |
| `public.error_insert` | `CALL` succeeded inside rollback transaction |
| `public.user_actions_history_insert` | `CALL` succeeded inside rollback transaction |

SPA verification after migration:

- Backend SPA/API tests: `155/155` passed.
- Frontend Vitest suite: `34/34` passed from a clean temporary client copy under `/private/tmp/rvtportal-client-test` because the Windows-mounted `node_modules` tree was missing the macOS Rollup optional package.
- Playwright e2e suite: `4/4` passed from the same temporary client copy after allowing the Vite test server to bind to `127.0.0.1:5173`.
