-- Roll back only after an application version that does not require fenced leases is active.

DROP INDEX IF EXISTS ix_my_atm_alert_occurrence_recent_lookup;

ALTER TABLE IF EXISTS my_atm_outbox_message
    DROP COLUMN IF EXISTS lease_id;
