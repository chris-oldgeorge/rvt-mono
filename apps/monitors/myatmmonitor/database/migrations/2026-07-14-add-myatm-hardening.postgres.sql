-- MyAtm fenced-outbox hardening. Apply after the durable-outbox migration.

ALTER TABLE IF EXISTS my_atm_outbox_message
    ADD COLUMN IF NOT EXISTS lease_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_my_atm_alert_occurrence_recent_lookup
    ON my_atm_alert_occurrence (monitor_id, field, period, triggered_at, alert_type, is_suppressed);
