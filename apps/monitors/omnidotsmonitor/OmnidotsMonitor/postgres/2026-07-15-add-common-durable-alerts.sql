BEGIN;

CREATE TABLE IF NOT EXISTS alert_occurrence
(
    id uuid NOT NULL,
    source varchar(128) NOT NULL,
    source_key_hash bytea NOT NULL,
    notification_id uuid NULL,
    monitor_id uuid NOT NULL,
    serial_id varchar(128) NOT NULL,
    event_time timestamp with time zone NOT NULL,
    alert_type integer NOT NULL,
    alert_field varchar(128) NOT NULL,
    level double precision NOT NULL,
    limit_on double precision NOT NULL,
    averaging_period integer NOT NULL,
    outcome varchar(32) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_alert_occurrence PRIMARY KEY (id),
    CONSTRAINT ck_alert_occurrence_source_key_hash CHECK (octet_length(source_key_hash) = 32),
    CONSTRAINT ck_alert_occurrence_outcome CHECK (outcome IN ('Accepted','Ignored','Suppressed')),
    CONSTRAINT fk_alert_occurrence_notification FOREIGN KEY (notification_id) REFERENCES notification(id) ON DELETE RESTRICT,
    CONSTRAINT fk_alert_occurrence_monitor FOREIGN KEY (monitor_id) REFERENCES monitor(id) ON DELETE RESTRICT,
    CONSTRAINT uq_alert_occurrence_source_key UNIQUE (source, source_key_hash)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'pk_alert_occurrence') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT pk_alert_occurrence PRIMARY KEY (id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'ck_alert_occurrence_source_key_hash') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT ck_alert_occurrence_source_key_hash CHECK (octet_length(source_key_hash) = 32);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'ck_alert_occurrence_outcome') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT ck_alert_occurrence_outcome CHECK (outcome IN ('Accepted','Ignored','Suppressed'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'fk_alert_occurrence_notification') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT fk_alert_occurrence_notification FOREIGN KEY (notification_id) REFERENCES notification(id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'fk_alert_occurrence_monitor') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT fk_alert_occurrence_monitor FOREIGN KEY (monitor_id) REFERENCES monitor(id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_occurrence'::regclass AND conname = 'uq_alert_occurrence_source_key') THEN
        ALTER TABLE alert_occurrence ADD CONSTRAINT uq_alert_occurrence_source_key UNIQUE (source, source_key_hash);
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS alert_delivery_outbox
(
    id uuid NOT NULL,
    occurrence_id uuid NOT NULL,
    delivery_key varchar(64) NOT NULL,
    kind varchar(32) NOT NULL,
    destination varchar(512) NOT NULL,
    payload varchar(8192) NOT NULL,
    status varchar(32) NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    last_error varchar(256) NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_alert_delivery_outbox PRIMARY KEY (id),
    CONSTRAINT fk_alert_delivery_outbox_occurrence FOREIGN KEY (occurrence_id) REFERENCES alert_occurrence(id) ON DELETE CASCADE,
    CONSTRAINT ck_alert_delivery_outbox_kind CHECK (kind IN ('MqttAlert','Email','Sms')),
    CONSTRAINT ck_alert_delivery_outbox_status CHECK (status IN ('Pending','Leased','Completed','DeadLetter')),
    CONSTRAINT uq_alert_delivery_outbox_delivery_key UNIQUE (delivery_key)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_delivery_outbox'::regclass AND conname = 'pk_alert_delivery_outbox') THEN
        ALTER TABLE alert_delivery_outbox ADD CONSTRAINT pk_alert_delivery_outbox PRIMARY KEY (id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_delivery_outbox'::regclass AND conname = 'fk_alert_delivery_outbox_occurrence') THEN
        ALTER TABLE alert_delivery_outbox ADD CONSTRAINT fk_alert_delivery_outbox_occurrence FOREIGN KEY (occurrence_id) REFERENCES alert_occurrence(id) ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_delivery_outbox'::regclass AND conname = 'ck_alert_delivery_outbox_kind') THEN
        ALTER TABLE alert_delivery_outbox ADD CONSTRAINT ck_alert_delivery_outbox_kind CHECK (kind IN ('MqttAlert','Email','Sms'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_delivery_outbox'::regclass AND conname = 'ck_alert_delivery_outbox_status') THEN
        ALTER TABLE alert_delivery_outbox ADD CONSTRAINT ck_alert_delivery_outbox_status CHECK (status IN ('Pending','Leased','Completed','DeadLetter'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'alert_delivery_outbox'::regclass AND conname = 'uq_alert_delivery_outbox_delivery_key') THEN
        ALTER TABLE alert_delivery_outbox ADD CONSTRAINT uq_alert_delivery_outbox_delivery_key UNIQUE (delivery_key);
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_alert_delivery_outbox_due
    ON alert_delivery_outbox (status, next_attempt_at, lease_until, created_at);

COMMIT;
