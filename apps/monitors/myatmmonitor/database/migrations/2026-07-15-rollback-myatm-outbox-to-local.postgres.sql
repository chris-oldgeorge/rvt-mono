-- Authoritatively synchronize MyAtm payload-version-1 deliveries back to the retained legacy table.
-- Pause MyAtm imports and DispatchOutbox before applying this script. The shared table is left untouched.

BEGIN;

DO $$
BEGIN
    IF to_regclass('monitor_delivery_outbox') IS NULL THEN
        RAISE EXCEPTION 'Prerequisite table monitor_delivery_outbox is missing';
    END IF;

    IF to_regclass('my_atm_outbox_message') IS NULL
       OR to_regclass('my_atm_alert_occurrence') IS NULL THEN
        RAISE EXCEPTION 'Required MyAtm legacy outbox tables are missing';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM monitor_delivery_outbox
        WHERE producer = 'MyAtm'
          AND payload_version = 1
          AND status NOT IN ('Pending', 'InProgress', 'Completed', 'DeadLetter')
    ) THEN
        RAISE EXCEPTION 'Shared MyAtm version-1 outbox contains an unsupported status';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM monitor_delivery_outbox
        WHERE producer = 'MyAtm'
          AND payload_version = 1
          AND kind NOT IN ('MqttDataInserted', 'MqttAlert', 'Email', 'Sms')
    ) THEN
        RAISE EXCEPTION 'Shared MyAtm version-1 outbox contains an unsupported kind';
    END IF;
END
$$;

INSERT INTO my_atm_outbox_message AS legacy (
    id,
    occurrence_key,
    delivery_key,
    kind,
    destination,
    payload,
    status,
    attempt_count,
    next_attempt_at,
    lease_id,
    lease_until,
    completed_at,
    last_error,
    created_at
)
SELECT
    shared.id,
    occurrence.occurrence_key,
    shared.delivery_key,
    shared.kind,
    shared.destination,
    shared.payload,
    CASE shared.status
        WHEN 'InProgress' THEN 'Leased'
        ELSE shared.status
    END,
    shared.attempt_count,
    shared.next_attempt_at,
    shared.lease_id,
    shared.lease_until,
    shared.completed_at,
    shared.last_error,
    shared.created_at
FROM monitor_delivery_outbox AS shared
LEFT JOIN my_atm_alert_occurrence AS occurrence
  ON occurrence.occurrence_key = shared.correlation_key
WHERE shared.producer = 'MyAtm'
  AND shared.payload_version = 1
ON CONFLICT (delivery_key) DO UPDATE
SET occurrence_key = EXCLUDED.occurrence_key,
    kind = EXCLUDED.kind,
    destination = EXCLUDED.destination,
    payload = EXCLUDED.payload,
    status = EXCLUDED.status,
    attempt_count = EXCLUDED.attempt_count,
    next_attempt_at = EXCLUDED.next_attempt_at,
    lease_id = EXCLUDED.lease_id,
    lease_until = EXCLUDED.lease_until,
    completed_at = EXCLUDED.completed_at,
    last_error = EXCLUDED.last_error,
    created_at = EXCLUDED.created_at;

COMMIT;
