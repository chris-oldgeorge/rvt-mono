-- File summary: Drops the unmapped NOT NULL column defaults for operators applying SQL directly (PostgreSQL).
-- Major updates:
-- - 2026-07-14 pending Added rollback for restore_unmapped_column_defaults.sql.
--
-- Undoes restore_unmapped_column_defaults.sql: removes the defaults from rvt_alert_rule.created and
-- monitor.battery_status, returning them to NOT NULL with nothing to fill them.
--
-- Running this re-breaks EF inserts into rvt_alert_rule (23502 on "created"), which is the defect the forward
-- script exists to fix. Only run it if the defaults themselves are the problem.
--
-- Existing rows keep the values they were given; a default is only consulted on insert.

ALTER TABLE public.rvt_alert_rule
    ALTER COLUMN created DROP DEFAULT;

ALTER TABLE public.monitor
    ALTER COLUMN battery_status DROP DEFAULT;
