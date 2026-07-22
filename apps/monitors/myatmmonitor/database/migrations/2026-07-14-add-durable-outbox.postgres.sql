-- MyAtm durable alert occurrence and per-destination delivery outbox.
-- Apply once to the monitor schema before deploying the durability remediation.

CREATE TABLE IF NOT EXISTS my_atm_alert_occurrence (
    occurrence_key text PRIMARY KEY,
    notification_id uuid NOT NULL UNIQUE,
    monitor_id uuid NOT NULL REFERENCES monitor (id),
    rule_id uuid NOT NULL REFERENCES rvt_alert_rule (id),
    period integer NOT NULL,
    alert_type integer NOT NULL,
    field text NOT NULL,
    level double precision NOT NULL,
    triggered_at timestamp with time zone NOT NULL,
    is_suppressed boolean NOT NULL,
    created_at timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS my_atm_outbox_message (
    id uuid PRIMARY KEY,
    occurrence_key text NULL REFERENCES my_atm_alert_occurrence (occurrence_key),
    delivery_key text NOT NULL UNIQUE,
    kind text NOT NULL,
    destination text NOT NULL,
    payload text NOT NULL,
    status text NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    last_error text NULL,
    created_at timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_my_atm_outbox_message_status_next_attempt_at
    ON my_atm_outbox_message (status, next_attempt_at);
