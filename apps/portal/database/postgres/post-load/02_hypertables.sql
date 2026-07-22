-- Convert candidate tables to TimescaleDB hypertables.
-- Idempotent: if_not_exists => TRUE makes re-runs safe.
-- Uses canonical target names. ASP.NET Identity tables are not hypertables.

SET search_path TO public;

DO $$
DECLARE
    candidates TEXT[][] := ARRAY[
        -- table_name,                 time_column
        ['air_q_noise_level',             'sample_time'],
        ['svantek_noise_level',           'sample_time'],
        ['omnidots_peak_level',           'sample_time'],
        ['my_atm_dust_level',             'sample_time'],
        ['user_action_history',           'recorded_at'],
        ['notification_sent',             'send_time'],
        ['error_log',                     'logged_at'],
        ['svantek_noise_8_hour_average',  'sample_time'],
        ['site_average',                  'collection_time'],
        ['air_q_noise_8_hour_average',    'sample_time']
    ];
BEGIN
    FOR i IN 1 .. array_length(candidates, 1) LOOP
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET NOT NULL',
                       candidates[i][1], candidates[i][2]);

        PERFORM create_hypertable(
            format('public.%I', candidates[i][1])::regclass,
            candidates[i][2],
            chunk_time_interval => INTERVAL '7 days',
            if_not_exists       => TRUE,
            migrate_data        => TRUE
        );

        RAISE NOTICE 'Hypertable ready: public.% on (%)',
            candidates[i][1], candidates[i][2];
    END LOOP;
END $$;

SELECT hypertable_schema, hypertable_name, num_chunks
FROM timescaledb_information.hypertables
WHERE hypertable_schema = 'public'
ORDER BY hypertable_name;
