-- File summary: Deploys reviewed PostgreSQL routine ports for SQL Server stored procedures.
-- Major updates:
-- - 2026-07-09 pending Dropped legacy PascalCase PostgreSQL routine names and canonicalized routine result aliases.
-- - 2026-06-09 pending Added canonical PostgreSQL routines for the DBR stored procedure porting gate.
-- - 2026-06-09 pending Generated user_action_history ids for Timescale composite primary keys.
-- Keep application-owned routine, table, and column names lowercase snake_case.
-- ASP.NET Identity objects remain excluded from the naming refactor and must be quoted if referenced.

SET search_path TO public;

-- Function summary: Inserts an application error log row using canonical error_log columns.
DROP PROCEDURE IF EXISTS public.error_insert(text, text, text, text, text, text);
CREATE OR REPLACE PROCEDURE public.error_insert(
    p_host text,
    p_source text,
    p_message text,
    p_level text,
    p_stack_trace text,
    p_variables text)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.error_log
        (host, source, message, level, stack_trace, variables, logged_at)
    VALUES
        (p_host, p_source, p_message, p_level, p_stack_trace, p_variables, CURRENT_TIMESTAMP);
END;
$$;

-- Function summary: Returns notification alert status values grouped by day for one monitor and month.
DROP FUNCTION IF EXISTS public."MonitorStatusForMonth"(uuid, integer, integer);
DROP FUNCTION IF EXISTS public.monitor_status_for_month(uuid, integer, integer);
CREATE OR REPLACE FUNCTION public.monitor_status_for_month(
    p_monitor_id uuid,
    p_year integer,
    p_month integer)
RETURNS TABLE(status integer, day integer)
LANGUAGE sql
STABLE
AS $$
    SELECT
        min(n.alert_type)::integer AS status,
        extract(day from n.notification_time)::integer AS day
    FROM public.notification n
    WHERE n.monitor_id = p_monitor_id
      AND extract(year from n.notification_time)::integer = p_year
      AND extract(month from n.notification_time)::integer = p_month
      AND n.alert_type < 2
    GROUP BY extract(day from n.notification_time)::integer;
$$;

-- Function summary: Returns the last known monitor data time and current UTC time for status checks.
DROP FUNCTION IF EXISTS public."MonitorStatusTimeCheck"(uuid);
DROP FUNCTION IF EXISTS public.monitor_status_time_check(uuid);
CREATE OR REPLACE FUNCTION public.monitor_status_time_check(p_monitor_id uuid)
RETURNS TABLE(
    monitor_date timestamp without time zone,
    last_api_date timestamp without time zone,
    utc_date timestamp with time zone)
LANGUAGE sql
STABLE
AS $$
    SELECT
        current_status.monitor_date AS monitor_date,
        current_status.monitor_date AS last_api_date,
        CURRENT_TIMESTAMP AS utc_date
    FROM (
        SELECT COALESCE(
            m.last_data_time_1_min,
            m.last_data_time_15_min,
            m.last_data_time_1_hour,
            m.last_data_time_24_hour,
            timestamp '1970-01-01') AS monitor_date
        FROM public.monitor m
        WHERE m.id = p_monitor_id
        LIMIT 1
    ) current_status;
$$;

-- Function summary: Returns Omnidots breach rows and matching notification rows for a calendar date.
DROP FUNCTION IF EXISTS public."PeakRecordBreachAndAlerts"(timestamp without time zone);
DROP FUNCTION IF EXISTS public.peak_record_breach_and_alerts(timestamp without time zone);
CREATE OR REPLACE FUNCTION public.peak_record_breach_and_alerts(p_date timestamp without time zone)
RETURNS TABLE(
    serial_id text,
    fleet_nr text,
    monitor_id uuid,
    sample_time timestamp without time zone,
    notification_id uuid,
    notification_time timestamp without time zone,
    x_vtop double precision,
    y_vtop double precision,
    z_vtop double precision)
LANGUAGE sql
STABLE
AS $$
    SELECT
        p.serial_id::text AS serial_id,
        m.fleet_nr::text AS fleet_nr,
        m.id AS monitor_id,
        p.sample_time AS sample_time,
        n.id AS notification_id,
        n.notification_time AS notification_time,
        p.x_vtop AS x_vtop,
        p.y_vtop AS y_vtop,
        p.z_vtop AS z_vtop
    FROM public.omnidots_peak_level p
    INNER JOIN public.monitor m
        ON m.serial_id = p.serial_id
       AND m.type_of_monitor = 2
    LEFT JOIN public.notification n
        ON n.monitor_id = m.id
       AND n.notification_time = p.sample_time
       AND n.alert_type IN (0, 1)
    WHERE p.sample_time >= p_date::date
      AND p.sample_time < (p_date::date + interval '1 day')
      AND (p.x_vtop > 7 OR p.y_vtop > 7 OR p.z_vtop > 7)
    ORDER BY p.serial_id, p.sample_time;
$$;

-- Function summary: Inserts an audit row for a user action using canonical user_action_history columns.
DROP PROCEDURE IF EXISTS public.user_actions_history_insert(text, text, text, text, text);
CREATE OR REPLACE PROCEDURE public.user_actions_history_insert(
    p_user_name text,
    p_controller text DEFAULT NULL,
    p_controller_action text DEFAULT NULL,
    p_parameters text DEFAULT NULL,
    p_form_data text DEFAULT NULL)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.user_action_history (id, user_name, controller, controller_action, parameters, form_data, recorded_at)
    VALUES
        (gen_random_uuid(), p_user_name, p_controller, p_controller_action, p_parameters, p_form_data, CURRENT_TIMESTAMP);
END;
$$;
