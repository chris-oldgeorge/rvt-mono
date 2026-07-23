-- File summary: Restores the two unmapped NOT NULL column defaults the PostgreSQL port dropped. Forward script.
-- Major updates:
-- - 2026-07-14 pending Added to fix EF inserts into rvt_alert_rule failing with 23502.
--
-- rvt_alert_rule.created and monitor.battery_status are NOT NULL and are mapped by no EF model, so EF never
-- supplies them on insert. In SQL Server that is survivable, because both columns carry a default constraint
-- there (df_rvt_alert_rule_created, df_monitor_battery_status). The PostgreSQL port did not carry those
-- defaults over, so on PostgreSQL the columns are NOT NULL with nothing to fill them, and EF's INSERT fails:
--
--   23502: null value in column "created" of relation "rvt_alert_rule" violates not-null constraint
--
-- That breaks every alert-rule insert - creating an alert level, and creating a monitor, which seeds default
-- alert levels. monitor.battery_status has the same shape but no EF insert path reaches it today.
--
-- This script only sets defaults. It does not backfill, because neither column has null rows to backfill: they
-- are NOT NULL already. New databases get these defaults from create_unmapped_schema.sql instead; this script is
-- for databases that already have the columns, where ADD COLUMN IF NOT EXISTS is a no-op and cannot add them.
-- RVT.SchemaDeploy deliberately runs this after create_unmapped_schema.sql on every deployment, so new and
-- existing databases follow the same resolved script list.
--
-- Idempotent: safe to re-run. ALTER COLUMN SET DEFAULT is a catalogue-only change - it does not rewrite the
-- table and does not touch existing rows.

ALTER TABLE public.rvt_alert_rule
    ALTER COLUMN created SET DEFAULT (now() AT TIME ZONE 'utc');

ALTER TABLE public.monitor
    ALTER COLUMN battery_status SET DEFAULT 0;
