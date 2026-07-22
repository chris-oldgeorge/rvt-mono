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

CREATE TABLE my_atm_dust_level (
    serial_id text NOT NULL,
    avrg integer NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    pm_1 double precision NULL,
    pm_2_5 double precision NULL,
    pm_10 double precision NULL,
    pm_total double precision NULL,
    weather_t double precision NULL,
    weather_p double precision NULL,
    weather_rh double precision NULL,
    PRIMARY KEY (serial_id, sample_time, avrg)
);

CREATE INDEX ix_my_atm_dust_level_serial_id_sample_time
    ON my_atm_dust_level (serial_id, sample_time);

CREATE TABLE my_atm_dust_level_8_hour_avg (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    pm_1 double precision NULL,
    pm_2_5 double precision NULL,
    pm_10 double precision NULL,
    pm_total double precision NULL,
    weather_t double precision NULL,
    weather_p double precision NULL,
    weather_rh double precision NULL,
    number_of_samples integer NOT NULL,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE INDEX ix_my_atm_dust_level_8_hour_avg_serial_id_sample_time
    ON my_atm_dust_level_8_hour_avg (serial_id, sample_time);

CREATE TABLE my_atm_accessory_info (
    serial_id text NOT NULL,
    sample_time timestamp with time zone NOT NULL,
    operating_span_point_deviation double precision NULL,
    operating_t_led double precision NULL,
    operating_t_heating double precision NULL,
    operating_volume_flow double precision NULL,
    operating_volume_flow_signal_length double precision NULL,
    operating_volume_flow_time bigint NOT NULL,
    operating_peak_position_15_s double precision NULL,
    operating_velocity double precision NULL,
    operating_sla_noise_level double precision NULL,
    operating_sla_offset_adjustment_voltage double precision NULL,
    operating_tmio double precision NULL,
    operating_pmio double precision NULL,
    operating_rh_mio double precision NULL,
    operating_auto_calibration_peak_position double precision NULL,
    operating_power_led double precision NULL,
    operating_power_pmt double precision NULL,
    operating_power_heating double precision NULL,
    operating_power_volume_flow_blower double precision NULL,
    operating_power_housing_blower double precision NULL,
    operating_power_separator_blower double precision NULL,
    operating_flow_correction_factor double precision NULL,
    digital_calibration_enable_status boolean NOT NULL,
    digital_iads_connected boolean NOT NULL,
    digital_iads_activated boolean NOT NULL,
    digital_ambient_protection_attached boolean NOT NULL,
    digital_coincidence boolean NOT NULL,
    digital_weather_station boolean NOT NULL,
    digital_operating_modus boolean NOT NULL,
    digital_volume_flow boolean NOT NULL,
    digital_suction boolean NOT NULL,
    digital_iads boolean NOT NULL,
    digital_calibration boolean NOT NULL,
    digital_sensor_led boolean NOT NULL,
    digital_sensor_data boolean NOT NULL,
    digital_sensor_noise boolean NOT NULL,
    digital_count_modus boolean NOT NULL,
    digital_liquid_pumps boolean NOT NULL,
    digital_condensation_cooling boolean NOT NULL,
    digital_droplet_size boolean NOT NULL,
    digital_optics_temperature boolean NOT NULL,
    digital_global_warning boolean NOT NULL,
    digital_global_error boolean NOT NULL,
    digital_evaporation_heating boolean NOT NULL,
    PRIMARY KEY (serial_id, sample_time)
);

CREATE TABLE my_atm_error_message (
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

CREATE TABLE my_atm_alert_occurrence (
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

CREATE TABLE my_atm_outbox_message (
    id uuid PRIMARY KEY,
    occurrence_key text NULL REFERENCES my_atm_alert_occurrence (occurrence_key),
    delivery_key text NOT NULL UNIQUE,
    kind text NOT NULL,
    destination text NOT NULL,
    payload text NOT NULL,
    status text NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    last_error text NULL,
    created_at timestamp with time zone NOT NULL
);

CREATE INDEX ix_my_atm_outbox_message_status_next_attempt_at
    ON my_atm_outbox_message (status, next_attempt_at);

CREATE TABLE monitor_delivery_outbox (
    id uuid NOT NULL,
    producer text NOT NULL,
    notification_id uuid NULL,
    correlation_key text NULL,
    delivery_key text NOT NULL,
    kind text NOT NULL,
    destination text NOT NULL,
    payload_version integer NOT NULL,
    payload text NOT NULL,
    status text NOT NULL,
    attempt_count integer NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    lease_id uuid NULL,
    lease_until timestamp with time zone NULL,
    completed_at timestamp with time zone NULL,
    dead_lettered_at timestamp with time zone NULL,
    last_error text NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_monitor_delivery_outbox PRIMARY KEY (id),
    CONSTRAINT uq_monitor_delivery_outbox_producer_delivery UNIQUE (producer, delivery_key),
    CONSTRAINT ck_monitor_delivery_outbox_status CHECK (status IN ('Pending', 'InProgress', 'Completed', 'DeadLetter')),
    CONSTRAINT fk_monitor_delivery_outbox_notification
        FOREIGN KEY (notification_id) REFERENCES notification (id) ON DELETE SET NULL
);

CREATE INDEX ix_monitor_delivery_outbox_due
    ON monitor_delivery_outbox (producer, status, next_attempt_at);

CREATE INDEX ix_my_atm_alert_occurrence_recent_lookup
    ON my_atm_alert_occurrence (monitor_id, field, period, triggered_at, alert_type, is_suppressed);

CREATE TABLE error_log (
    host text NOT NULL,
    source text NOT NULL,
    message text NOT NULL,
    level text NOT NULL,
    stack_trace text NULL,
    variables text NULL,
    logged_at timestamp with time zone NOT NULL
);
