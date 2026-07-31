-- Database backstop for scheduled report idempotency.
-- The report table carried only pk_report, so nothing below the application
-- stopped a rule/frequency/period being generated twice. The generation
-- service re-checks inside its advisory lock, but reports are emailed before
-- they are saved, so a duplicate row is a duplicate report in a recipient's
-- inbox and is worth refusing at the schema too.
--
-- Scheduled reports only. A one-time report resolves to the site's hidden
-- system rule and is written with frequency 5, and two one-time reports over
-- the same site and period are a supported request, so the filter excludes
-- them. It excludes frequency 5 rather than listing the scheduled frequencies
-- so that a frequency added later is protected by default.
--
-- WARNING: pre-existing duplicates are collapsed before the index is created,
-- because a deployed database may already hold some. Within each
-- (report_rule_id, frequency, report_from) group the earliest report_date is
-- kept - that is the copy whose link recipients were actually sent first, and
-- the one the portal's report_search has been showing - with id as the tiebreak
-- so the choice is deterministic when report_date ties to the microsecond.
-- report_sent rows belonging to a losing report are re-pointed at the kept
-- report rather than deleted: there is no foreign key on report_sent.report_id,
-- so deleting the losing rows would silently orphan real delivery history
-- instead of failing loudly, and that history records emails that were sent.

BEGIN;

WITH ranked AS (
    SELECT
        id,
        first_value(id) OVER (
            PARTITION BY report_rule_id, frequency, report_from
            ORDER BY report_date, id
        ) AS keep_id
    FROM report
    WHERE report_rule_id IS NOT NULL
      AND frequency <> 5
)
UPDATE report_sent
SET report_id = ranked.keep_id
FROM ranked
WHERE report_sent.report_id = ranked.id
  AND ranked.id <> ranked.keep_id;

WITH ranked AS (
    SELECT
        id,
        first_value(id) OVER (
            PARTITION BY report_rule_id, frequency, report_from
            ORDER BY report_date, id
        ) AS keep_id
    FROM report
    WHERE report_rule_id IS NOT NULL
      AND frequency <> 5
)
DELETE FROM report
USING ranked
WHERE report.id = ranked.id
  AND ranked.id <> ranked.keep_id;

CREATE UNIQUE INDEX IF NOT EXISTS ux_report_scheduled_period
ON report (report_rule_id, frequency, report_from)
WHERE report_rule_id IS NOT NULL AND frequency <> 5;

COMMIT;
