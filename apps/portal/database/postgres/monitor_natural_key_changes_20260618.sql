-- File summary: Adds PostgreSQL monitor natural-key backfill, duplicate audit, and unique indexes.
-- Major updates:
-- - 2026-06-18 pending Mirrored Timescale monitor natural-key changes for PostgreSQL deployment.
--
-- Run after canonical database naming and Timescale hypertable setup.
-- Known duplicate groups are quarantined before the audit and unique indexes are created.

SET lock_timeout = '5s';
SET search_path TO public;

ALTER TABLE air_q_monitor_status ADD COLUMN IF NOT EXISTS serial_id varchar(64);

UPDATE air_q_monitor_status
SET serial_id = id
WHERE serial_id IS NULL AND id IS NOT NULL;

CREATE TABLE IF NOT EXISTS duplicate_quarantine_svantek_noise_level AS
SELECT *
FROM svantek_noise_level
WHERE false;

CREATE TABLE IF NOT EXISTS duplicate_quarantine_omnidots_peak_level AS
SELECT *
FROM omnidots_peak_level
WHERE false;

CREATE TABLE IF NOT EXISTS duplicate_quarantine_svantek_noise_8_hour_average AS
SELECT *
FROM svantek_noise_8_hour_average
WHERE false;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY ctid
           ) AS rn
    FROM svantek_noise_level
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
),
to_remove AS (
    SELECT ctid
    FROM ranked
    WHERE rn > 1
)
INSERT INTO duplicate_quarantine_svantek_noise_level
SELECT source.*
FROM svantek_noise_level source
JOIN to_remove ON source.ctid = to_remove.ctid;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY ctid
           ) AS rn
    FROM svantek_noise_level
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
)
DELETE FROM svantek_noise_level target
USING ranked
WHERE target.ctid = ranked.ctid
  AND ranked.rn > 1;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY ctid
           ) AS rn
    FROM omnidots_peak_level
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
),
to_remove AS (
    SELECT ctid
    FROM ranked
    WHERE rn > 1
)
INSERT INTO duplicate_quarantine_omnidots_peak_level
SELECT source.*
FROM omnidots_peak_level source
JOIN to_remove ON source.ctid = to_remove.ctid;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY ctid
           ) AS rn
    FROM omnidots_peak_level
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
)
DELETE FROM omnidots_peak_level target
USING ranked
WHERE target.ctid = ranked.ctid
  AND ranked.rn > 1;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY number_of_samples DESC NULLS LAST, ctid
           ) AS rn
    FROM svantek_noise_8_hour_average
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
),
to_remove AS (
    SELECT ctid
    FROM ranked
    WHERE rn > 1
)
INSERT INTO duplicate_quarantine_svantek_noise_8_hour_average
SELECT source.*
FROM svantek_noise_8_hour_average source
JOIN to_remove ON source.ctid = to_remove.ctid;

WITH ranked AS (
    SELECT ctid,
           row_number() OVER (
               PARTITION BY serial_id, sample_time
               ORDER BY number_of_samples DESC NULLS LAST, ctid
           ) AS rn
    FROM svantek_noise_8_hour_average
    WHERE serial_id IS NOT NULL
      AND sample_time IS NOT NULL
)
DELETE FROM svantek_noise_8_hour_average target
USING ranked
WHERE target.ctid = ranked.ctid
  AND ranked.rn > 1;

DO $$
DECLARE
    duplicate_count BIGINT;
    not_null_filter TEXT;
    failures TEXT[] := ARRAY[]::TEXT[];
    checks TEXT[][] := ARRAY[
        ['monitor', 'serial_id, type_of_monitor'],
        ['air_q_monitor_status', 'serial_id'],
        ['omnidots_monitor_status', 'serial_id'],
        ['omnidots_sensor', 'serial_id'],
        ['svantek_monitor_status', 'serial_id'],
        ['air_q_noise_level', 'serial_id, sample_time'],
        ['svantek_noise_level', 'serial_id, sample_time'],
        ['my_atm_dust_level', 'serial_id, sample_time, avrg'],
        ['my_atm_accessory_info', 'serial_id, sample_time'],
        ['omnidots_peak_level', 'serial_id, sample_time'],
        ['omnidots_veff_level', 'serial_id, sample_time'],
        ['omnidots_vdv_level', 'serial_id, sample_time'],
        ['air_q_noise_8_hour_average', 'serial_id, sample_time'],
        ['svantek_noise_8_hour_average', 'serial_id, sample_time']
    ];
BEGIN
    FOR i IN 1 .. array_length(checks, 1) LOOP
        SELECT string_agg(format('%I IS NOT NULL', trim(column_name)), ' AND ')
        INTO not_null_filter
        FROM unnest(regexp_split_to_array(checks[i][2], ',\s*')) AS column_name;

        EXECUTE format(
            'SELECT COUNT(*) FROM (SELECT %2$s FROM %1$I WHERE %3$s GROUP BY %2$s HAVING COUNT(*) > 1) duplicates',
            checks[i][1],
            checks[i][2],
            not_null_filter
        ) INTO duplicate_count;

        IF duplicate_count > 0 THEN
            failures := array_append(
                failures,
                format('%s (%s): %s duplicate groups', checks[i][1], checks[i][2], duplicate_count));
            RAISE WARNING 'Duplicate natural keys found: % (%) duplicate groups=%',
                checks[i][1], checks[i][2], duplicate_count;
            CONTINUE;
        END IF;

        RAISE NOTICE 'Duplicate audit passed: % (%)', checks[i][1], checks[i][2];
    END LOOP;

    IF array_length(failures, 1) > 0 THEN
        RAISE EXCEPTION 'Monitor duplicate audit failed: %', array_to_string(failures, '; ');
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_monitor_serial_id_type_of_monitor
    ON monitor (serial_id, type_of_monitor);

CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_monitor_status_serial_id
    ON air_q_monitor_status (serial_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_monitor_status_serial_id
    ON omnidots_monitor_status (serial_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_sensor_serial_id
    ON omnidots_sensor (serial_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_svantek_monitor_status_serial_id
    ON svantek_monitor_status (serial_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_noise_level_serial_id_sample_time
    ON air_q_noise_level (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_svantek_noise_level_serial_id_sample_time
    ON svantek_noise_level (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_my_atm_dust_level_serial_id_sample_time_avrg
    ON my_atm_dust_level (serial_id, sample_time, avrg);

CREATE UNIQUE INDEX IF NOT EXISTS ux_my_atm_accessory_info_serial_id_sample_time
    ON my_atm_accessory_info (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_peak_level_serial_id_sample_time
    ON omnidots_peak_level (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_veff_level_serial_id_sample_time
    ON omnidots_veff_level (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_vdv_level_serial_id_sample_time
    ON omnidots_vdv_level (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_noise_8_hour_average_serial_id_sample_time
    ON air_q_noise_8_hour_average (serial_id, sample_time);

CREATE UNIQUE INDEX IF NOT EXISTS ux_svantek_noise_8_hour_average_serial_id_sample_time
    ON svantek_noise_8_hour_average (serial_id, sample_time);
