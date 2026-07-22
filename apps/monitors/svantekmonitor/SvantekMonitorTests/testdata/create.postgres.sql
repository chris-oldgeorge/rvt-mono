-- Canonical PostgreSQL schema for Svantek persistence integration tests.
-- Objects are intentionally unqualified so the fixture's generated search_path owns them.

CREATE TABLE monitor (
    id uuid PRIMARY KEY,
    fleet_row_count text,
    serial_id text NOT NULL,
    manufacturer text NOT NULL,
    model text NOT NULL,
    firmware_version text NOT NULL,
    type_of_monitor integer NOT NULL,
    location_id integer,
    latitude double precision,
    longitude double precision,
    location_address text,
    time_zone text,
    customer_id integer,
    customer_display_name text,
    listed_at_time timestamp with time zone NOT NULL,
    last_data_time_1_min timestamp with time zone,
    last_data_time_15_min timestamp with time zone,
    last_data_time_1_hour timestamp with time zone,
    last_data_time_24_hour timestamp with time zone,
    offline boolean,
    battery_status smallint
);

CREATE INDEX ix_monitor_serial_id_type_of_monitor
    ON monitor (serial_id, type_of_monitor);

CREATE TABLE svantek_monitor_status (
    serial_id text PRIMARY KEY,
    update_time timestamp with time zone NOT NULL,
    status text NOT NULL,
    error_count integer NOT NULL,
    battery_voltage text,
    calibration_date timestamp with time zone,
    filter_change_date timestamp with time zone,
    pump_hours text,
    project_id integer,
    point_id integer,
    active text,
    lastlogin text,
    lastlogout text,
    isonline text,
    laststatustimestamp text,
    batterycharge integer,
    batterytimetoempty integer,
    powersource text,
    isbatterycharging text,
    gsmsignalquality integer,
    measurementstate text
);

CREATE TABLE svantek_noise_level (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    laeq double precision,
    lamax double precision,
    la_90 double precision,
    la_10 double precision,
    lceq double precision,
    lcmax double precision,
    lc_90 double precision,
    lc_10 double precision,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_svantek_noise_level_serial_id_sample_time
    ON svantek_noise_level (serial_id, sample_time);

CREATE TABLE svantek_noise_8_hour_average (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    laeq double precision,
    lamax double precision,
    la_90 double precision,
    la_10 double precision,
    lceq double precision,
    lcmax double precision,
    lc_90 double precision,
    lc_10 double precision,
    number_of_samples integer NOT NULL,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE TABLE svantek_error_message (
    tag character varying(64) NOT NULL,
    error character varying(512) NOT NULL,
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

CREATE TABLE deployment (
    id uuid PRIMARY KEY,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone,
    lng double precision NOT NULL,
    lat double precision NOT NULL,
    what_3_words text,
    picture_link text,
    contract_id uuid NOT NULL REFERENCES contract (id),
    monitor_id uuid NOT NULL REFERENCES monitor (id) ON DELETE CASCADE
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
    created timestamp with time zone NOT NULL,
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
    alert_type integer NOT NULL,
    recording_link text
);

CREATE TABLE notification_sent (
    id uuid PRIMARY KEY,
    send_time timestamp with time zone NOT NULL,
    address text NOT NULL,
    error_message text NOT NULL,
    notification_id uuid NOT NULL REFERENCES notification (id) ON DELETE CASCADE
);

CREATE TABLE site_average (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL,
    monitor_id uuid NOT NULL,
    field text NOT NULL,
    level double precision NOT NULL,
    collection_time timestamp with time zone NOT NULL
);

-- Production deployments predate monitor catalog refreshes. The fixture creates
-- one active deployment for new catalog rows so ReadMonitorList sees test monitors.
CREATE FUNCTION create_default_svantek_deployment()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO contract (id, contract_number, on_hire_date, company_id, site_id)
    VALUES (
        '11111111-1111-1111-1111-111111111111',
        'fixture-contract',
        NEW.listed_at_time - interval '1 day',
        '33333333-3333-3333-3333-333333333333',
        '22222222-2222-2222-2222-222222222222')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO deployment (
        id, start_date, end_date, lng, lat, what_3_words, picture_link, contract_id, monitor_id)
    VALUES (
        NEW.id,
        NEW.listed_at_time - interval '1 day',
        NULL,
        COALESCE(NEW.longitude, 0),
        COALESCE(NEW.latitude, 0),
        NULL,
        NULL,
        '11111111-1111-1111-1111-111111111111',
        NEW.id);

    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_monitor_default_svantek_deployment
AFTER INSERT ON monitor
FOR EACH ROW
EXECUTE FUNCTION create_default_svantek_deployment();
