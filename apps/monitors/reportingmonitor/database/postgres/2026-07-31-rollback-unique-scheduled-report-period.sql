-- Rolls back the scheduled report period uniqueness backstop.
--
-- WARNING: Dropping ux_report_scheduled_period permanently removes the database
-- guard against a rule/frequency/period being reported twice, leaving the
-- generation service's in-lock check as the only thing between a concurrency
-- defect and duplicate report emails. The duplicates the forward migration
-- collapsed are not restored, and the report_sent rows it re-pointed stay with
-- the report they were moved to.

BEGIN;

DROP INDEX IF EXISTS ux_report_scheduled_period;

COMMIT;
