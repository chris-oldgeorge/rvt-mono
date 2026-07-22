-- File summary: Drops the PostgreSQL removal-impact aggregate view.
-- Major updates:
-- - 2026-06-25 pending Added rollback for monitor_measurement_removal_impact.

DROP VIEW IF EXISTS public.monitor_measurement_removal_impact;
