-- File summary: Creates the tables and columns of the physical schema that no EF Core model maps (PostgreSQL).
-- Major updates:
-- - 2026-07-14 pending Added so a database can be built from code without a SQL Server source.
--
-- WHY THIS SCRIPT EXISTS
--
-- The EF migrations build everything the application's models map: RVTDbContext's domain tables
-- (CanonicalBaseline), RVTSearchContext's time-series tables (SearchBaseline), and Identity's AspNet* tables
-- (IdentityBaseline). That is not the whole database. The physical schema also carries 23 ingestion/telemetry
-- tables and 13 columns on otherwise-mapped tables that no EF model knows about - they are written by the
-- ingestion pipelines and read through the views in database/postgres/post-load/03_views_and_routines.sql.
-- Some of those views select monitor.offline, so without the columns below the views cannot even be created.
--
-- Until this script existed, the only thing that could produce any of it was RVT.DatabaseMigrator, copying a
-- SQL Server source schema - which made a SQL Server database a hard prerequisite for standing up a PostgreSQL
-- one. This script is the half EF cannot build, so the two together produce a complete database from the
-- repository alone. That is what allowed the migrator to be retired on 2026-07-14.
--
-- The DDL is a faithful dump of the cutover schema, not a hand-transcription. The one place it departs from the
-- SQL Server source is deliberate: it restores the two default constraints the PostgreSQL port dropped (see the
-- ADD COLUMN section below), without which every EF insert into rvt_alert_rule fails.
--
-- WHERE IT FITS in a from-scratch build - see docs/database/ef-migrations.md:
--   1. dotnet ef database update --context RVTDbContext        (domain tables)
--   2. dotnet ef database update --context RVTSearchContext    (time-series tables)
--   3. dotnet ef database update --context ApplicationDbContext (Identity tables)
--   4. this script                                             (the unmapped remainder)
--   5. database/postgres/post-load/*.sql                    (hypertables, aggregates, views, routines)
--
-- Idempotent: safe to re-run. Creates nothing that already exists, and drops nothing. Every NOT NULL column in
-- the ADD COLUMN section now carries a default, so adding it to a table that already has rows backfills cleanly.
-- Note that ADD COLUMN IF NOT EXISTS is a no-op on a column that already exists and so does NOT add a default to
-- one - a database created before this fix keeps its defaultless columns; restore_unmapped_column_defaults.sql
-- repairs those.

-- Tables

CREATE TABLE IF NOT EXISTS public.air_q_noise_8_hour_average (
    serial_id character varying(255) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL,
    number_of_samples integer
);

CREATE TABLE IF NOT EXISTS public.heater_reading (
    serial_id character varying(64) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    outlet_c real,
    inlet_c real,
    ambient_c real,
    setpoint_c real,
    element_c real,
    stage smallint,
    power_pct smallint,
    energy_wh bigint,
    runtime_s bigint,
    cycles integer,
    status character varying(32),
    faults text[]
);

CREATE TABLE IF NOT EXISTS public.air_q_noise_level (
    serial_id character varying(255) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL
);

CREATE TABLE IF NOT EXISTS public.svantek_noise_level (
    serial_id character varying(255) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL
);

CREATE TABLE IF NOT EXISTS public.user_action_history (
    id uuid NOT NULL,
    recorded_at timestamp without time zone NOT NULL,
    user_name character varying(50) NOT NULL,
    controller character varying(50),
    controller_action character varying(50),
    parameters character varying(1024),
    form_data text,
    completed timestamp without time zone
);

CREATE TABLE IF NOT EXISTS public.notification_sent (
    id uuid NOT NULL,
    send_time timestamp without time zone NOT NULL,
    address character varying(256) NOT NULL,
    error_message character varying(256) NOT NULL,
    notification_id uuid NOT NULL
);

CREATE TABLE IF NOT EXISTS public.error_log (
    id bigint NOT NULL,
    logged_at timestamp without time zone NOT NULL,
    host character varying(100),
    source text,
    message text,
    level character varying(50),
    stack_trace text,
    variables text
);

CREATE TABLE IF NOT EXISTS public.svantek_noise_8_hour_average (
    serial_id character varying(255) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL,
    number_of_samples integer
);

CREATE TABLE IF NOT EXISTS public.site_average (
    id uuid NOT NULL,
    site_id uuid NOT NULL,
    monitor_id uuid NOT NULL,
    field character varying(32) NOT NULL,
    level double precision NOT NULL,
    collection_time timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.air_q_error_message (
    tag character varying(64) NOT NULL,
    error character varying(512) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.air_q_monitor_status (
    id character varying(32) NOT NULL,
    update_time timestamp without time zone NOT NULL,
    status character varying(32) NOT NULL,
    error_count integer NOT NULL,
    battery_voltage character varying(32),
    calibration_date timestamp without time zone,
    filter_change_date timestamp without time zone,
    pump_hours character varying(32),
    serial_id character varying(64)
);

CREATE TABLE IF NOT EXISTS public.duplicate_quarantine_omnidots_peak_level (
    serial_id character varying(32),
    sample_time timestamp without time zone,
    x_fdom double precision,
    x_vtop double precision,
    x_vtop_overflow double precision,
    y_fdom double precision,
    y_vtop double precision,
    y_vtop_overflow double precision,
    z_fdom double precision,
    z_vtop double precision,
    z_vtop_overflow double precision
);

CREATE TABLE IF NOT EXISTS public.duplicate_quarantine_svantek_noise_8_hour_average (
    serial_id character varying(255),
    sample_time timestamp without time zone,
    laeq double precision,
    lamax double precision,
    la_90 double precision,
    la_10 double precision,
    lceq double precision,
    lcmax double precision,
    lc_90 double precision,
    lc_10 double precision,
    number_of_samples integer
);

CREATE TABLE IF NOT EXISTS public.duplicate_quarantine_svantek_noise_level (
    serial_id character varying(255),
    sample_time timestamp without time zone,
    laeq double precision,
    lamax double precision,
    la_90 double precision,
    la_10 double precision,
    lceq double precision,
    lcmax double precision,
    lc_90 double precision,
    lc_10 double precision
);

CREATE TABLE IF NOT EXISTS public.my_atm_accessory_info (
    serial_id character varying(32) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    operating_span_point_deviation double precision,
    operating_t_led double precision,
    operating_t_heating double precision,
    operating_volume_flow double precision,
    operating_volume_flow_signal_length double precision,
    operating_volume_flow_time bigint,
    operating_peak_position_15_s double precision,
    operating_velocity double precision,
    operating_sla_noise_level double precision,
    operating_sla_offset_adjustment_voltage double precision,
    operating_tmio double precision,
    operating_pmio double precision,
    operating_rh_mio double precision,
    operating_auto_calibration_peak_position double precision,
    operating_power_led double precision,
    operating_power_pmt double precision,
    operating_power_heating double precision,
    operating_power_volume_flow_blower double precision,
    operating_power_housing_blower double precision,
    operating_power_separator_blower double precision,
    operating_flow_correction_factor double precision,
    digital_calibration_enable_status boolean,
    digital_iads_connected boolean,
    digital_iads_activated boolean,
    digital_ambient_protection_attached boolean,
    digital_coincidence boolean,
    digital_weather_station boolean,
    digital_operating_modus boolean,
    digital_volume_flow boolean,
    digital_suction boolean,
    digital_iads boolean,
    digital_calibration boolean,
    digital_sensor_led boolean,
    digital_sensor_data boolean,
    digital_sensor_noise boolean,
    digital_count_modus boolean,
    digital_liquid_pumps boolean,
    digital_condensation_cooling boolean,
    digital_droplet_size boolean,
    digital_optics_temperature boolean,
    digital_global_warning boolean,
    digital_global_error boolean,
    digital_evaporation_heating boolean
);

CREATE TABLE IF NOT EXISTS public.my_atm_error_message (
    tag character varying(64) NOT NULL,
    error character varying(1024) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.omnidots_error_message (
    tag character varying(64) NOT NULL,
    error character varying(1024) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.omnidots_vdv_level (
    serial_id character varying(32) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    x double precision,
    y double precision,
    z double precision,
    vdv_x character varying(32),
    vdv_y character varying(32),
    vdv_z character varying(32)
);

CREATE TABLE IF NOT EXISTS public.omnidots_veff_level (
    serial_id character varying(32) NOT NULL,
    sample_time timestamp without time zone NOT NULL,
    x double precision,
    y double precision,
    z double precision
);

CREATE TABLE IF NOT EXISTS public.report (
    id uuid NOT NULL,
    site_id uuid NOT NULL,
    report_link character varying(256) NOT NULL,
    report_date timestamp without time zone NOT NULL,
    report_rule_id uuid NOT NULL,
    frequency integer NOT NULL,
    report_from timestamp without time zone NOT NULL,
    report_to timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.report_error_message (
    tag character varying(64) NOT NULL,
    error character varying(1024) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.report_sent (
    id uuid NOT NULL,
    send_time timestamp without time zone NOT NULL,
    address character varying(256) NOT NULL,
    error_message character varying(256) NOT NULL,
    report_id uuid NOT NULL
);

CREATE TABLE IF NOT EXISTS public.svantek_error_message (
    tag character varying(64) NOT NULL,
    error character varying(512) NOT NULL,
    error_time timestamp without time zone NOT NULL
);

-- Identity columns

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_attribute
        WHERE attrelid = 'public.error_log'::regclass AND attname = 'id' AND attidentity <> ''
    ) THEN
        ALTER TABLE public.error_log ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
            SEQUENCE NAME public."ErrorLog_Id_seq"
            START WITH 1
            INCREMENT BY 1
            NO MINVALUE
            NO MAXVALUE
            CACHE 1
        );
    END IF;
END
$$;

-- Primary keys and unique constraints

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_air_q_monitor_status') THEN
        ALTER TABLE ONLY public.air_q_monitor_status
            ADD CONSTRAINT pk_air_q_monitor_status PRIMARY KEY (id);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_error_log') THEN
        ALTER TABLE ONLY public.error_log
            ADD CONSTRAINT pk_error_log PRIMARY KEY (id, logged_at);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_notification_sent') THEN
        ALTER TABLE ONLY public.notification_sent
            ADD CONSTRAINT pk_notification_sent PRIMARY KEY (id, send_time);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_report') THEN
        ALTER TABLE ONLY public.report
            ADD CONSTRAINT pk_report PRIMARY KEY (id);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_report_sent') THEN
        ALTER TABLE ONLY public.report_sent
            ADD CONSTRAINT pk_report_sent PRIMARY KEY (id);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_site_average') THEN
        ALTER TABLE ONLY public.site_average
            ADD CONSTRAINT pk_site_average PRIMARY KEY (id, collection_time);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'pk_user_action_history') THEN
        ALTER TABLE ONLY public.user_action_history
            ADD CONSTRAINT pk_user_action_history PRIMARY KEY (id, recorded_at);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ux_heater_reading_serial_id_sample_time') THEN
        ALTER TABLE ONLY public.heater_reading
            ADD CONSTRAINT ux_heater_reading_serial_id_sample_time UNIQUE (serial_id, sample_time);
    END IF;
END
$$;

-- Indexes

CREATE INDEX IF NOT EXISTS ix_air_q_noise_8_hour_average_sample_time ON public.air_q_noise_8_hour_average USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_air_q_noise_8_hour_average_serial_id_sample_time ON public.air_q_noise_8_hour_average USING btree (serial_id, sample_time) INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10, number_of_samples);
CREATE INDEX IF NOT EXISTS ix_air_q_noise_level_sample_time ON public.air_q_noise_level USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_air_q_noise_level_serial_id_sample_time ON public.air_q_noise_level USING btree (serial_id, sample_time) INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10);
CREATE INDEX IF NOT EXISTS ix_error_log_logged_at ON public.error_log USING btree (logged_at DESC);
CREATE INDEX IF NOT EXISTS ix_heater_reading_sample_time ON public.heater_reading USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_heater_reading_serial_id_sample_time ON public.heater_reading USING btree (serial_id, sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_my_atm_accessory_info_sample_time ON public.my_atm_accessory_info USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_notification_sent_send_time ON public.notification_sent USING btree (send_time DESC);
CREATE INDEX IF NOT EXISTS ix_omnidots_vdv_level_sample_time ON public.omnidots_vdv_level USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_omnidots_veff_level_sample_time ON public.omnidots_veff_level USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_site_average_collection_time ON public.site_average USING btree (collection_time DESC);
CREATE INDEX IF NOT EXISTS ix_site_average_monitor_id_collection_time ON public.site_average USING btree (monitor_id, collection_time) INCLUDE (field, level);
CREATE INDEX IF NOT EXISTS ix_svantek_error_message_error_time ON public.svantek_error_message USING btree (error_time);
CREATE INDEX IF NOT EXISTS ix_svantek_noise_8_hour_average_sample_time ON public.svantek_noise_8_hour_average USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_svantek_noise_8_hour_average_serial_id_sample_time ON public.svantek_noise_8_hour_average USING btree (serial_id, sample_time) INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10, number_of_samples);
CREATE INDEX IF NOT EXISTS ix_svantek_noise_level_sample_time ON public.svantek_noise_level USING btree (sample_time DESC);
CREATE INDEX IF NOT EXISTS ix_svantek_noise_level_serial_id_sample_time ON public.svantek_noise_level USING btree (serial_id, sample_time) INCLUDE (laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10);
CREATE INDEX IF NOT EXISTS ix_user_action_history_recorded_at ON public.user_action_history USING btree (recorded_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_monitor_status_serial_id ON public.air_q_monitor_status USING btree (serial_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_noise_8_hour_average_serial_id_sample_time ON public.air_q_noise_8_hour_average USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_air_q_noise_level_serial_id_sample_time ON public.air_q_noise_level USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_my_atm_accessory_info_serial_id_sample_time ON public.my_atm_accessory_info USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_vdv_level_serial_id_sample_time ON public.omnidots_vdv_level USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_omnidots_veff_level_serial_id_sample_time ON public.omnidots_veff_level USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_svantek_noise_8_hour_average_serial_id_sample_time ON public.svantek_noise_8_hour_average USING btree (serial_id, sample_time);
CREATE UNIQUE INDEX IF NOT EXISTS ux_svantek_noise_level_serial_id_sample_time ON public.svantek_noise_level USING btree (serial_id, sample_time);

-- Columns on EF-created tables that no EF model maps
--
-- These sit on tables the EF migrations DO create, so they are added afterwards rather than declared above.
-- Each one is invisible to EF: it is never read into an entity and never supplied on insert.
--
-- Two of them are NOT NULL - monitor.battery_status and rvt_alert_rule.created - so EF, which never supplies
-- them, can only insert if the database fills them in. SQL Server does: both columns carry a default constraint
-- there (df_monitor_battery_status and df_rvt_alert_rule_created). The PostgreSQL port dropped both, which is
-- what made every EF insert into rvt_alert_rule fail with "23502: null value in column created". The defaults
-- below restore the SQL Server behaviour; see docs/database/ef-migrations.md ("Columns no EF model maps").

ALTER TABLE public.monitor
    ADD COLUMN IF NOT EXISTS offline boolean,
    ADD COLUMN IF NOT EXISTS battery_status smallint NOT NULL DEFAULT 0;

ALTER TABLE public.notification_setting
    ADD COLUMN IF NOT EXISTS site_id uuid,
    ADD COLUMN IF NOT EXISTS user_id uuid;

ALTER TABLE public.report_rule
    ADD COLUMN IF NOT EXISTS is_hidden_system_rule boolean NOT NULL DEFAULT false;

ALTER TABLE public.rvt_alert_rule
    ADD COLUMN IF NOT EXISTS created timestamp without time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    ADD COLUMN IF NOT EXISTS accessed timestamp without time zone;

ALTER TABLE public.svantek_monitor_status
    ADD COLUMN IF NOT EXISTS update_time timestamp without time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    ADD COLUMN IF NOT EXISTS status character varying NOT NULL DEFAULT 'Active',
    ADD COLUMN IF NOT EXISTS battery_voltage character varying,
    ADD COLUMN IF NOT EXISTS calibration_date timestamp without time zone,
    ADD COLUMN IF NOT EXISTS filter_change_date timestamp without time zone,
    ADD COLUMN IF NOT EXISTS pump_hours character varying;
