-- Shared durable delivery outbox for all monitor producers.
-- Apply before deploying code that writes to monitor_delivery_outbox.

CREATE TABLE IF NOT EXISTS monitor_delivery_outbox (
    id uuid NOT NULL,
    producer text NOT NULL,
    notification_id uuid NULL,
    correlation_key text NULL,
    delivery_key text NOT NULL,
    kind text NOT NULL,
    destination text NOT NULL,
    payload_version integer NOT NULL,
    payload text NOT NULL,
    status text NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    dead_lettered_at timestamp with time zone NULL,
    last_error text NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_monitor_delivery_outbox PRIMARY KEY (id)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_monitor_delivery_outbox_producer_delivery'
          AND conrelid = 'monitor_delivery_outbox'::regclass
    ) THEN
        ALTER TABLE monitor_delivery_outbox
            ADD CONSTRAINT uq_monitor_delivery_outbox_producer_delivery
            UNIQUE (producer, delivery_key);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_monitor_delivery_outbox_status'
          AND conrelid = 'monitor_delivery_outbox'::regclass
    ) THEN
        ALTER TABLE monitor_delivery_outbox
            ADD CONSTRAINT ck_monitor_delivery_outbox_status
            CHECK (status IN ('Pending', 'InProgress', 'Completed', 'DeadLetter'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_monitor_delivery_outbox_notification'
          AND conrelid = 'monitor_delivery_outbox'::regclass
    ) THEN
        ALTER TABLE monitor_delivery_outbox
            ADD CONSTRAINT fk_monitor_delivery_outbox_notification
            FOREIGN KEY (notification_id)
            REFERENCES notification (id) ON DELETE SET NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_monitor_delivery_outbox_due
    ON monitor_delivery_outbox (producer, status, next_attempt_at);
