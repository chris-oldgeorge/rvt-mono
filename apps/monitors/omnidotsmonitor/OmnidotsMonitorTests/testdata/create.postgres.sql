CREATE TABLE site (
    id uuid PRIMARY KEY,
    site_name text,
    create_date timestamp with time zone NOT NULL,
    address_line_1 text,
    address_line_2 text,
    postcode text,
    city text,
    county text,
    start_time time,
    end_time time,
    sat_start_time time,
    sat_end_time time,
    sun_start_time time,
    sun_end_time time
);

CREATE TABLE contract (
    id uuid PRIMARY KEY,
    contract_number text NOT NULL,
    on_hire_date timestamp with time zone NOT NULL,
    off_hire_date timestamp with time zone,
    company_id uuid NOT NULL,
    site_id uuid
);

CREATE TABLE monitor (
    id uuid PRIMARY KEY,
    fleet_row_count text NOT NULL DEFAULT 'test-fleet',
    serial_id text NOT NULL,
    customer_id integer,
    listed_at_time timestamp with time zone NOT NULL,
    model text NOT NULL,
    location_id integer,
    latitude double precision,
    longitude double precision,
    location_address text,
    time_zone text,
    customer_display_name text,
    manufacturer text NOT NULL,
    firmware_version text NOT NULL,
    type_of_monitor integer NOT NULL,
    offline boolean,
    last_data_time_1_min timestamp with time zone,
    last_data_time_15_min timestamp with time zone,
    last_data_time_1_hour timestamp with time zone,
    last_data_time_24_hour timestamp with time zone,
    battery_status smallint
);

CREATE INDEX ix_monitor_serial_type ON monitor (serial_id, type_of_monitor);

CREATE TABLE deployment (
    id uuid PRIMARY KEY,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone,
    lng double precision NOT NULL,
    lat double precision NOT NULL,
    what2words text,
    what3words text,
    picture_link text,
    contract_id uuid NOT NULL REFERENCES contract (id),
    monitor_id uuid NOT NULL REFERENCES monitor (id) ON DELETE CASCADE
);

CREATE TABLE omnidots_monitor_status (
    id uuid PRIMARY KEY,
    serial_id text NOT NULL,
    measurement_duration integer,
    data_save_level double precision,
    vdv_enabled boolean NOT NULL,
    vdv_x text,
    vdv_y text,
    vdv_z text,
    vdv_period integer,
    trace_save_level double precision,
    trace_pre_trigger double precision,
    trace_post_trigger double precision,
    alarm_value double precision,
    flat_level double precision,
    disable_led boolean NOT NULL,
    log_flush_interval integer NOT NULL,
    guide_line text,
    building_level text NOT NULL,
    vector_enabled boolean NOT NULL,
    atop_enabled boolean NOT NULL,
    vtop_enabled boolean NOT NULL
);

CREATE INDEX ix_omnidots_monitor_status_serial_id
    ON omnidots_monitor_status (serial_id);

CREATE TABLE omnidots_sensor (
    id uuid PRIMARY KEY,
    serial_id text NOT NULL,
    name text NOT NULL,
    lastseen timestamp with time zone NOT NULL,
    battery_charge integer NOT NULL,
    connected_using text NOT NULL,
    online boolean NOT NULL
);

CREATE INDEX ix_omnidots_sensor_serial_id ON omnidots_sensor (serial_id);

CREATE TABLE omnidots_peak_level (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    x_fdom double precision,
    x_vtop double precision,
    x_vtop_overflow double precision,
    y_fdom double precision,
    y_vtop double precision,
    y_vtop_overflow double precision,
    z_fdom double precision,
    z_vtop double precision,
    z_vtop_overflow double precision,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_omnidots_peak_level_serial_sample
    ON omnidots_peak_level (serial_id, sample_time);

CREATE TABLE omnidots_veff_level (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    x double precision,
    y double precision,
    z double precision,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_omnidots_veff_level_serial_sample
    ON omnidots_veff_level (serial_id, sample_time);

CREATE TABLE omnidots_vdv_level (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    x double precision,
    y double precision,
    z double precision,
    vdv_x text,
    vdv_y text,
    vdv_z text,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_omnidots_vdv_level_serial_sample
    ON omnidots_vdv_level (serial_id, sample_time);

CREATE TABLE omnidots_import_cursor (
    serial_id text NOT NULL,
    series text NOT NULL,
    last_sample_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    PRIMARY KEY (serial_id, series),
    CONSTRAINT ck_omnidots_import_cursor_series
        CHECK (series IN ('Peak', 'Veff', 'Vdv'))
);

CREATE TABLE omnidots_trace_index (
    id uuid PRIMARY KEY,
    serial_id text,
    start_time timestamp with time zone NOT NULL,
    end_time timestamp with time zone NOT NULL
);

CREATE TABLE omnidots_trace (
    trace_id uuid NOT NULL REFERENCES omnidots_trace_index (id) ON DELETE CASCADE,
    sample_index integer NOT NULL,
    x double precision,
    y double precision,
    z double precision,
    PRIMARY KEY (trace_id, sample_index)
);

CREATE TABLE omnidots_error_message (
    tag text NOT NULL,
    error text NOT NULL,
    error_time timestamp with time zone NOT NULL,
    PRIMARY KEY (tag, error_time, error)
);

CREATE TABLE error_log (
    host text NOT NULL,
    source text NOT NULL,
    message text NOT NULL,
    level text NOT NULL,
    stack_trace text,
    variables text,
    logged_at timestamp with time zone NOT NULL
);

CREATE TABLE rvt_alert_rule (
    id uuid PRIMARY KEY,
    monitor_id uuid REFERENCES monitor (id) ON DELETE CASCADE,
    serial_id text,
    alert_field text NOT NULL,
    limit_on double precision NOT NULL,
    limit_off double precision NOT NULL,
    alert_type integer NOT NULL,
    is_active boolean NOT NULL,
    averaging_period integer NOT NULL,
    weekdays boolean NOT NULL,
    saturdays boolean NOT NULL,
    sundays boolean NOT NULL,
    start_time time,
    end_time time,
    is_deleted boolean NOT NULL,
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    accessed timestamp with time zone
);

CREATE TABLE "AspNetUsers" (
    "Id" text PRIMARY KEY,
    company_id uuid,
    is_disabled boolean NOT NULL,
    name text,
    "UserName" text,
    normalized_user_name text,
    "Email" text NOT NULL,
    normalized_email text,
    email_confirmed boolean NOT NULL,
    password_hash text,
    security_stamp text,
    concurrency_stamp text,
    "PhoneNumber" text,
    phone_number_confirmed boolean NOT NULL,
    two_factor_enabled boolean NOT NULL,
    lockout_end timestamp with time zone,
    lockout_enabled boolean NOT NULL,
    access_failed_count integer NOT NULL,
    company_role text
);

CREATE TABLE site_user (
    id uuid PRIMARY KEY,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone,
    user_id uuid NOT NULL,
    site_id uuid NOT NULL
);

CREATE TABLE notification_setting (
    id uuid PRIMARY KEY,
    email boolean NOT NULL,
    sms boolean NOT NULL,
    start_time time,
    end_time time,
    site_user_id uuid NOT NULL REFERENCES site_user (id) ON DELETE CASCADE
);

CREATE TABLE notification (
    id uuid PRIMARY KEY,
    notification_time timestamp with time zone NOT NULL,
    limit_on double precision NOT NULL,
    averaging_period integer NOT NULL,
    level double precision NOT NULL,
    closed_time timestamp with time zone,
    closed_by_user uuid,
    closed_by_note text,
    monitor_id uuid NOT NULL REFERENCES monitor (id) ON DELETE CASCADE,
    alert_field text NOT NULL,
    alert_type integer NOT NULL
);

CREATE TABLE notification_sent (
    id uuid PRIMARY KEY,
    send_time timestamp with time zone NOT NULL,
    address text NOT NULL,
    error_message text NOT NULL,
    notification_id uuid NOT NULL REFERENCES notification (id) ON DELETE CASCADE
);

CREATE TABLE alert_occurrence (
    id uuid PRIMARY KEY,
    source varchar(128) NOT NULL,
    source_key_hash bytea NOT NULL CHECK (octet_length(source_key_hash) = 32),
    notification_id uuid NULL REFERENCES notification(id) ON DELETE RESTRICT,
    monitor_id uuid NOT NULL REFERENCES monitor(id) ON DELETE RESTRICT,
    serial_id varchar(128) NOT NULL,
    event_time timestamp with time zone NOT NULL,
    alert_type integer NOT NULL,
    alert_field varchar(128) NOT NULL,
    level double precision NOT NULL,
    limit_on double precision NOT NULL,
    averaging_period integer NOT NULL,
    outcome varchar(32) NOT NULL CHECK (outcome IN ('Accepted','Ignored','Suppressed')),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_alert_occurrence_source_key UNIQUE (source, source_key_hash)
);

CREATE TABLE alert_delivery_outbox (
    id uuid PRIMARY KEY,
    occurrence_id uuid NOT NULL REFERENCES alert_occurrence(id) ON DELETE CASCADE,
    delivery_key varchar(64) NOT NULL,
    kind varchar(32) NOT NULL CHECK (kind IN ('MqttAlert','Email','Sms')),
    destination varchar(512) NOT NULL,
    payload varchar(8192) NOT NULL,
    status varchar(32) NOT NULL CHECK (status IN ('Pending','Leased','Completed','DeadLetter')),
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    last_error varchar(256) NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_alert_delivery_outbox_delivery_key UNIQUE (delivery_key)
);

CREATE INDEX ix_alert_delivery_outbox_due
    ON alert_delivery_outbox (status, next_attempt_at, lease_until, created_at);

CREATE FUNCTION create_default_omnidots_deployment()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO contract
        (id, contract_number, on_hire_date, off_hire_date, company_id, site_id)
    VALUES
        ('11111111-1111-1111-1111-111111111111'::uuid, 'fixture-contract',
         '2000-01-01T00:00:00Z'::timestamp with time zone, NULL,
         '33333333-3333-3333-3333-333333333333'::uuid,
         '22222222-2222-2222-2222-222222222222'::uuid)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO deployment
        (id, start_date, end_date, lng, lat, what2words, what3words,
         picture_link, contract_id, monitor_id)
    VALUES
        (NEW.id, NEW.listed_at_time, NULL, COALESCE(NEW.longitude, 0),
         COALESCE(NEW.latitude, 0), NULL, NULL, NULL,
         '11111111-1111-1111-1111-111111111111'::uuid, NEW.id);

    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_monitor_default_omnidots_deployment
AFTER INSERT ON monitor
FOR EACH ROW
EXECUTE FUNCTION create_default_omnidots_deployment();
