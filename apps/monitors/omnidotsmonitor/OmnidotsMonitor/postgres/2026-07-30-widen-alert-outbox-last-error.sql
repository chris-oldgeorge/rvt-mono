-- Widens alert_delivery_outbox.last_error from varchar(256) to varchar(1024)
-- so the outbox row keeps the same error detail as its audit trail
-- (DeliveryDispatchPolicy.SafeError caps errors at 1024 characters).
-- Rerunnable: widening an already-widened column is a no-op.
ALTER TABLE alert_delivery_outbox
    ALTER COLUMN last_error TYPE varchar(1024);
