-- Verifies TimescaleDB state before and after the canonical database naming rehearsal.
-- Run on a clone before applying any production rename migration:
--   psql -U postgres -d rvt -f database/postgres/verify_timescale_after_rename.sql

\pset pager off

\echo '== Timescale extension version =='
SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('timescaledb', 'pgcrypto')
ORDER BY extname;

\echo '== Expected hypertable rename status =='
WITH expected(current_name, canonical_name) AS (
    VALUES
        ('AirQNoise8HourAverage', 'air_q_noise_8_hour_average'),
        ('AirQNoiseLevels', 'air_q_noise_level'),
        ('ErrorLog', 'error_log'),
        ('MyAtmDustLevels', 'my_atm_dust_level'),
        ('NotificationsSent', 'notification_sent'),
        ('OmnidotsPeakLevels', 'omnidots_peak_level'),
        ('SiteAverages', 'site_average'),
        ('SvantekNoise8HourAverage', 'svantek_noise_8_hour_average'),
        ('SvantekNoiseLevels', 'svantek_noise_level'),
        ('UserActionsHistory', 'user_action_history')
),
actual AS (
    SELECT hypertable_schema, hypertable_name, num_chunks, compression_enabled
    FROM timescaledb_information.hypertables
    WHERE hypertable_schema = 'public'
)
SELECT
    expected.current_name,
    expected.canonical_name,
    coalesce(current_hypertable.num_chunks, canonical_hypertable.num_chunks) AS num_chunks,
    coalesce(current_hypertable.compression_enabled, canonical_hypertable.compression_enabled) AS compression_enabled,
    CASE
        WHEN canonical_hypertable.hypertable_name IS NOT NULL THEN 'renamed'
        WHEN current_hypertable.hypertable_name IS NOT NULL THEN 'pre_rename'
        ELSE 'missing'
    END AS rename_status
FROM expected
LEFT JOIN actual current_hypertable
    ON current_hypertable.hypertable_name = expected.current_name
LEFT JOIN actual canonical_hypertable
    ON canonical_hypertable.hypertable_name = expected.canonical_name
ORDER BY expected.current_name;

\echo '== Chunk counts by hypertable =='
SELECT hypertable_schema, hypertable_name, count(*) AS chunk_count, bool_or(is_compressed) AS any_compressed
FROM timescaledb_information.chunks
GROUP BY hypertable_schema, hypertable_name
ORDER BY hypertable_schema, hypertable_name;

\echo '== Continuous aggregates =='
SELECT view_schema, view_name, materialization_hypertable_schema, materialization_hypertable_name
FROM timescaledb_information.continuous_aggregates
ORDER BY view_schema, view_name;

\echo '== Timescale jobs and policies =='
SELECT application_name, proc_schema, proc_name, hypertable_schema, hypertable_name, config
FROM timescaledb_information.jobs
ORDER BY application_name, proc_schema, proc_name;

\echo '== Post-rename warning checks =='
WITH expected(current_name, canonical_name) AS (
    VALUES
        ('AirQNoise8HourAverage', 'air_q_noise_8_hour_average'),
        ('AirQNoiseLevels', 'air_q_noise_level'),
        ('ErrorLog', 'error_log'),
        ('MyAtmDustLevels', 'my_atm_dust_level'),
        ('NotificationsSent', 'notification_sent'),
        ('OmnidotsPeakLevels', 'omnidots_peak_level'),
        ('SiteAverages', 'site_average'),
        ('SvantekNoise8HourAverage', 'svantek_noise_8_hour_average'),
        ('SvantekNoiseLevels', 'svantek_noise_level'),
        ('UserActionsHistory', 'user_action_history')
),
actual AS (
    SELECT hypertable_name
    FROM timescaledb_information.hypertables
    WHERE hypertable_schema = 'public'
)
SELECT
    'missing_expected_canonical_hypertables' AS check_name,
    count(*) FILTER (WHERE canonical.hypertable_name IS NULL) AS warning_count
FROM expected
LEFT JOIN actual canonical
    ON canonical.hypertable_name = expected.canonical_name
UNION ALL
SELECT
    'legacy_hypertable_names_still_present',
    count(*) FILTER (WHERE legacy.hypertable_name IS NOT NULL)
FROM expected
LEFT JOIN actual legacy
    ON legacy.hypertable_name = expected.current_name
UNION ALL
SELECT
    'compressed_hypertables',
    count(*) FILTER (WHERE compression_enabled)
FROM timescaledb_information.hypertables
WHERE hypertable_schema = 'public'
UNION ALL
SELECT
    'continuous_aggregates',
    count(*)
FROM timescaledb_information.continuous_aggregates;
