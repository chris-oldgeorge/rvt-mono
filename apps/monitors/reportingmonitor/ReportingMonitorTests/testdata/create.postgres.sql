create table site_search (
    id uuid primary key,
    site_name text not null,
    create_date timestamptz not null,
    address_line_1 text null,
    address_line_2 text null,
    postcode text null,
    city text null,
    county text null,
    contracts text null,
    company_name text null,
    company_id uuid null,
    archived boolean not null default false
);

create table report_rule (
    id uuid primary key,
    site_id uuid not null,
    user_id uuid not null,
    frequency integer not null,
    day_of_week integer null,
    day_of_month integer null,
    last_generated timestamptz null,
    report_name text null,
    deleted boolean not null default false,
    is_hidden_system_rule boolean not null default false
);

create unique index ux_report_rule_hidden_one_time_per_site
on report_rule (site_id, frequency, is_hidden_system_rule)
where is_hidden_system_rule = true and frequency = 5;

create table report (
    id uuid primary key, site_id uuid not null, report_rule_id uuid null, frequency integer not null,
    report_date timestamptz not null, report_from timestamptz not null, report_to timestamptz not null, report_link text not null
);
create table report_sent (
    id uuid primary key, report_id uuid not null, send_time timestamptz not null, address text not null, error_message text null
);
create table report_user (id uuid primary key, report_rule_id uuid not null, user_id uuid not null);
create table "AspNetUsers" ("Id" text primary key, "Email" text not null);

create table monitor_report (
    id uuid primary key, site_id uuid not null, active boolean not null, deployment_id uuid null,
    fleet_row_count text null, serial_id text not null, type_of_monitor integer not null, off_line boolean not null,
    alerts boolean not null, cautions boolean not null, latitude double precision null, longitude double precision null,
    start_date timestamptz null, end_date timestamptz null, what_3_words text null, last_data_time timestamptz null,
    location text null, calibration_date timestamptz null
);

create table contract (
    id uuid primary key,
    contract_number text not null,
    on_hire_date timestamptz not null,
    off_hire_date timestamptz null,
    company_id uuid not null,
    site_id uuid null
);

create table deployment (
    id uuid primary key,
    start_date timestamptz not null,
    end_date timestamptz null,
    contract_id uuid not null references contract (id),
    monitor_id uuid not null
);

create table notification (
    id uuid primary key, monitor_id uuid not null, notification_time timestamptz not null, limit_on double precision not null,
    averaging_period integer not null, level double precision not null, closed_time timestamptz null,
    closed_by_note text null, alert_field text not null, alert_type integer not null
);

create table rvt_alert_rule (
    id uuid primary key, monitor_id uuid null, alert_field text not null, limit_on double precision not null,
    alert_type integer not null, averaging_period integer not null, is_active boolean not null default true,
    is_deleted boolean not null default false
);

create table my_atm_dust_level (serial_id text not null, avrg integer not null, sample_time timestamptz not null, pm_10 double precision null);
create table my_atm_dust_level_1_day_avg (serial_id text not null, sample_time timestamptz not null, pm_10 double precision null);
create table noise_level_1_hour_avg (serial_id text not null, sample_time timestamptz not null, laeq double precision null);
create table noise_level_1_day_avg (serial_id text not null, sample_time timestamptz not null, laeq double precision null);
create table noise_level_site_avg (serial_id text not null, sample_time timestamptz not null, laeq double precision null);
create table omnidots_peak_level_1_day_peak (
    serial_id text not null, sample_time timestamptz not null,
    x_vtop double precision null, y_vtop double precision null, z_vtop double precision null
);
