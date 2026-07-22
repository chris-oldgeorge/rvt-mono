-- Backfill the MyAtm version-1 delivery contract into the shared outbox.
-- Pause MyAtm imports and DispatchOutbox before applying this script.

BEGIN;

DO $$
BEGIN
    IF to_regclass('monitor_delivery_outbox') IS NULL THEN
        RAISE EXCEPTION 'Prerequisite table monitor_delivery_outbox is missing';
    END IF;

    IF to_regclass('my_atm_outbox_message') IS NULL
       OR to_regclass('my_atm_alert_occurrence') IS NULL
       OR to_regclass('notification') IS NULL THEN
        RAISE EXCEPTION 'Required MyAtm legacy outbox tables are missing';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM my_atm_outbox_message
        WHERE status NOT IN ('Pending', 'Leased', 'Completed', 'DeadLetter')
    ) THEN
        RAISE EXCEPTION 'MyAtm legacy outbox contains an unsupported status';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM my_atm_outbox_message
        WHERE kind NOT IN ('MqttDataInserted', 'MqttAlert', 'Email', 'Sms')
    ) THEN
        RAISE EXCEPTION 'MyAtm legacy outbox contains an unsupported kind';
    END IF;
END
$$;

-- PayloadVersion remains 1 for the existing MyAtm JSON contract.
INSERT INTO monitor_delivery_outbox AS shared (
    id,
    producer,
    notification_id,
    correlation_key,
    delivery_key,
    kind,
    destination,
    payload_version,
    payload,
    status,
    attempt_count,
    next_attempt_at,
    lease_id,
    lease_until,
    completed_at,
    dead_lettered_at,
    last_error,
    created_at
)
SELECT
    legacy.id,
    'MyAtm',
    notification.id,
    legacy.occurrence_key,
    legacy.delivery_key,
    legacy.kind,
    legacy.destination,
    1,
    legacy.payload,
    CASE legacy.status
        WHEN 'Leased' THEN 'InProgress'
        ELSE legacy.status
    END,
    legacy.attempt_count,
    legacy.next_attempt_at,
    legacy.lease_id,
    legacy.lease_until,
    legacy.completed_at,
    NULL, -- dead_lettered_at is not represented by the legacy schema.
    legacy.last_error,
    legacy.created_at
FROM my_atm_outbox_message AS legacy
LEFT JOIN my_atm_alert_occurrence AS occurrence
  ON occurrence.occurrence_key = legacy.occurrence_key
LEFT JOIN notification
  ON occurrence.notification_id = notification.id
ON CONFLICT (producer, delivery_key) DO UPDATE
SET notification_id = EXCLUDED.notification_id,
    correlation_key = EXCLUDED.correlation_key,
    kind = EXCLUDED.kind,
    destination = EXCLUDED.destination,
    payload_version = EXCLUDED.payload_version,
    payload = EXCLUDED.payload,
    status = EXCLUDED.status,
    attempt_count = EXCLUDED.attempt_count,
    next_attempt_at = EXCLUDED.next_attempt_at,
    lease_id = EXCLUDED.lease_id,
    lease_until = EXCLUDED.lease_until,
    completed_at = EXCLUDED.completed_at,
    dead_lettered_at = EXCLUDED.dead_lettered_at,
    last_error = EXCLUDED.last_error,
    created_at = EXCLUDED.created_at
WHERE (
      shared.attempt_count < EXCLUDED.attempt_count
      OR (
          shared.attempt_count = EXCLUDED.attempt_count
          AND NOT (
              shared.status = 'Completed'
              AND EXCLUDED.status = 'Completed'
              AND shared.completed_at IS NOT NULL
              AND (EXCLUDED.completed_at IS NULL OR shared.completed_at > EXCLUDED.completed_at)
          )
      )
  )
  AND NOT (
      shared.status IN ('Completed', 'DeadLetter')
      AND EXCLUDED.status NOT IN ('Completed', 'DeadLetter')
  );

COMMIT;
