-- Local demo helper for testlocal=true.
-- Rewinds Noise - E125V - 157206 to a Svantek API window that returns samples.
-- Intended for local/demo Postgres only; do not run as a production migration.

BEGIN;

UPDATE public.monitor AS monitor
SET last_data_time_15_min = timestamp '2026-03-15 20:00:00'
WHERE monitor.serial_id = '157206'
  AND monitor.fleet_row_count = 'E125V'
  AND monitor.type_of_monitor = 1
  AND EXISTS (
      SELECT 1
      FROM public.deployment AS deployment
      WHERE deployment.monitor_id = monitor.id
        AND deployment.end_date IS NULL
  );

COMMIT;
