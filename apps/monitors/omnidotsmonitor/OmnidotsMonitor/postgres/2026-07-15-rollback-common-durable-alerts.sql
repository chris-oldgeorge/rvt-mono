BEGIN;

DROP TABLE IF EXISTS alert_delivery_outbox;

-- WARNING: Dropping alert_occurrence removes permanent webhook replay protection.
DROP TABLE IF EXISTS alert_occurrence;

COMMIT;
