-- Aligns the existing local Timescale/PostgreSQL Svantek status table with
-- the EF model used by the Svantek monitor app.

ALTER TABLE public.svantek_monitor_status
    ADD COLUMN IF NOT EXISTS update_time timestamp without time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    ADD COLUMN IF NOT EXISTS status character varying NOT NULL DEFAULT 'Active',
    ADD COLUMN IF NOT EXISTS battery_voltage character varying,
    ADD COLUMN IF NOT EXISTS calibration_date timestamp without time zone,
    ADD COLUMN IF NOT EXISTS filter_change_date timestamp without time zone,
    ADD COLUMN IF NOT EXISTS pump_hours character varying;

CREATE TABLE IF NOT EXISTS public.svantek_error_message
(
    tag character varying(64) NOT NULL,
    error character varying(512) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_svantek_error_message_error_time
    ON public.svantek_error_message (error_time);
