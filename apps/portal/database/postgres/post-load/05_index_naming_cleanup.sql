-- File summary: Renames Timescale-created PostgreSQL indexes to the canonical RVT index naming style.
-- Major updates:
-- - 2026-07-09 pending Added post-load cleanup for default Timescale sample_time index names.
-- Function summary: Aligns non-constraint sample_time indexes with the ix_{relation}_{column} naming convention.

SET search_path TO public;

DO $$
BEGIN
    IF to_regclass('public.heater_reading_sample_time_idx') IS NOT NULL
       AND to_regclass('public.ix_heater_reading_sample_time') IS NULL THEN
        ALTER INDEX public.heater_reading_sample_time_idx RENAME TO ix_heater_reading_sample_time;
    END IF;

    IF to_regclass('public.my_atm_accessory_info_sample_time_idx') IS NOT NULL
       AND to_regclass('public.ix_my_atm_accessory_info_sample_time') IS NULL THEN
        ALTER INDEX public.my_atm_accessory_info_sample_time_idx RENAME TO ix_my_atm_accessory_info_sample_time;
    END IF;

    IF to_regclass('public.omnidots_vdv_level_sample_time_idx') IS NOT NULL
       AND to_regclass('public.ix_omnidots_vdv_level_sample_time') IS NULL THEN
        ALTER INDEX public.omnidots_vdv_level_sample_time_idx RENAME TO ix_omnidots_vdv_level_sample_time;
    END IF;

    IF to_regclass('public.omnidots_veff_level_sample_time_idx') IS NOT NULL
       AND to_regclass('public.ix_omnidots_veff_level_sample_time') IS NULL THEN
        ALTER INDEX public.omnidots_veff_level_sample_time_idx RENAME TO ix_omnidots_veff_level_sample_time;
    END IF;
END;
$$;
