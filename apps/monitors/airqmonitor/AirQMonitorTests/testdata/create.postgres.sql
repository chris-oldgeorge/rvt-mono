CREATE TABLE monitor (
    id uuid PRIMARY KEY,
    fleet_row_count text NULL,
    serial_id text NOT NULL,
    customer_id integer NULL,
    listed_at_time timestamp with time zone NOT NULL,
    model text NOT NULL,
    location_id integer NULL,
    latitude double precision NULL,
    longitude double precision NULL,
    location_address text NULL,
    time_zone text NULL,
    customer_display_name text NULL,
    manufacturer text NOT NULL,
    firmware_version text NOT NULL,
    type_of_monitor integer NOT NULL,
    offline boolean NULL,
    last_data_time_1_min timestamp with time zone NULL,
    last_data_time_15_min timestamp with time zone NULL,
    last_data_time_1_hour timestamp with time zone NULL,
    last_data_time_24_hour timestamp with time zone NULL,
    battery_status smallint NULL
);

CREATE INDEX ix_monitor_serial_id_type_of_monitor
    ON monitor (serial_id, type_of_monitor);

CREATE TABLE air_q_monitor_status (
    id text NOT NULL,
    serial_id text PRIMARY KEY,
    update_time timestamp with time zone NOT NULL,
    status text NOT NULL,
    error_count integer NOT NULL,
    battery_voltage text NULL,
    calibration_date timestamp with time zone NULL,
    filter_change_date timestamp with time zone NULL,
    pump_hours text NULL
);

CREATE TABLE air_q_noise_level (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_air_q_noise_level_serial_id_sample_time
    ON air_q_noise_level (serial_id, sample_time);

CREATE TABLE air_q_noise_8_hour_average (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    laeq double precision NOT NULL,
    lamax double precision NOT NULL,
    la_90 double precision NOT NULL,
    la_10 double precision NOT NULL,
    lceq double precision NOT NULL,
    lcmax double precision NOT NULL,
    lc_90 double precision NOT NULL,
    lc_10 double precision NOT NULL,
    number_of_samples integer NOT NULL,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_air_q_noise_8_hour_average_serial_id_sample_time
    ON air_q_noise_8_hour_average (serial_id, sample_time);

CREATE TABLE air_q_error_message (
    tag text NOT NULL,
    error text NOT NULL,
    error_time timestamp with time zone NOT NULL,
    PRIMARY KEY (tag, error_time, error)
);

CREATE TABLE site (
    id uuid PRIMARY KEY,
    site_name text NULL,
    create_date timestamp with time zone NOT NULL,
    address_line_1 text NULL,
    address_line_2 text NULL,
    postcode text NULL,
    city text NULL,
    county text NULL,
    start_time time NULL,
    end_time time NULL,
    sat_start_time time NULL,
    sat_end_time time NULL,
    sun_start_time time NULL,
    sun_end_time time NULL
);

CREATE TABLE contract (
    id uuid PRIMARY KEY,
    contract_number text NOT NULL,
    on_hire_date timestamp with time zone NOT NULL,
    off_hire_date timestamp with time zone NULL,
    company_id uuid NOT NULL,
    site_id uuid NULL
);

CREATE TABLE deployment (
    id uuid PRIMARY KEY,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone NULL,
    lng double precision NOT NULL,
    lat double precision NOT NULL,
    what2words text NULL,
    what3words text NULL,
    picture_link text NULL,
    contract_id uuid NOT NULL REFERENCES contract (id),
    monitor_id uuid NOT NULL REFERENCES monitor (id)
);

CREATE TABLE rvt_alert_rule (
    id uuid PRIMARY KEY,
    monitor_id uuid NULL REFERENCES monitor (id) ON DELETE CASCADE,
    serial_id text NULL,
    alert_field text NOT NULL,
    limit_on double precision NOT NULL,
    limit_off double precision NOT NULL,
    alert_type integer NOT NULL,
    is_active boolean NOT NULL,
    averaging_period integer NOT NULL,
    weekdays boolean NOT NULL,
    saturdays boolean NOT NULL,
    sundays boolean NOT NULL,
    start_time time NULL,
    end_time time NULL,
    is_deleted boolean NOT NULL,
    created timestamp with time zone NOT NULL,
    accessed timestamp with time zone NULL
);

CREATE TABLE "AspNetUsers" (
    "Id" text PRIMARY KEY,
    company_id uuid NULL,
    is_disabled boolean NOT NULL,
    name text NULL,
    "UserName" text NULL,
    normalized_user_name text NULL,
    "Email" text NOT NULL,
    normalized_email text NULL,
    email_confirmed boolean NOT NULL,
    password_hash text NULL,
    security_stamp text NULL,
    concurrency_stamp text NULL,
    "PhoneNumber" text NULL,
    phone_number_confirmed boolean NOT NULL,
    two_factor_enabled boolean NOT NULL,
    lockout_end timestamp with time zone NULL,
    lockout_enabled boolean NOT NULL,
    access_failed_count integer NOT NULL,
    company_role text NULL
);

CREATE TABLE site_user (
    id uuid PRIMARY KEY,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone NULL,
    user_id uuid NOT NULL,
    site_id uuid NOT NULL
);

CREATE TABLE notification_setting (
    id uuid PRIMARY KEY,
    email boolean NOT NULL,
    sms boolean NOT NULL,
    start_time time NULL,
    end_time time NULL,
    site_user_id uuid NOT NULL REFERENCES site_user (id)
);

CREATE TABLE notification (
    id uuid PRIMARY KEY,
    notification_time timestamp with time zone NOT NULL,
    limit_on double precision NOT NULL,
    averaging_period integer NOT NULL,
    level double precision NOT NULL,
    closed_time timestamp with time zone NULL,
    closed_by_user uuid NULL,
    closed_by_note text NULL,
    monitor_id uuid NOT NULL REFERENCES monitor (id),
    alert_field text NOT NULL,
    alert_type integer NOT NULL
);

CREATE TABLE notification_sent (
    id uuid PRIMARY KEY,
    send_time timestamp with time zone NOT NULL,
    address text NOT NULL,
    error_message text NOT NULL,
    notification_id uuid NOT NULL REFERENCES notification (id)
);

CREATE TABLE site_average (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL,
    monitor_id uuid NOT NULL,
    field text NOT NULL,
    level double precision NOT NULL,
    collection_time timestamp with time zone NOT NULL
);

CREATE TABLE error_log (
    host text NOT NULL,
    source text NOT NULL,
    message text NOT NULL,
    level text NOT NULL,
    stack_trace text NULL,
    variables text NULL,
    logged_at timestamp with time zone NOT NULL
);
