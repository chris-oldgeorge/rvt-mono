-- Reporting service database prerequisites.
-- Major updates: 2026-06-25 added pgcrypto, hidden one-time report-rule marker, and index for rvt-reporting-new.

create extension if not exists pgcrypto;

alter table report_rule
add column if not exists is_hidden_system_rule boolean not null default false;

create unique index if not exists ux_report_rule_hidden_one_time_per_site
on report_rule (site_id, frequency, is_hidden_system_rule)
where is_hidden_system_rule = true and frequency = 5;
