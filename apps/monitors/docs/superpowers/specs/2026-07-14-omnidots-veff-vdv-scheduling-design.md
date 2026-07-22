# Omnidots Veff and VDV Scheduling Design

## Goal

Make the existing Omnidots Veff and VDV import handlers executable as named one-shot jobs and automatic Quartz jobs.

## Scope

- Add `StoreVeffRecords` and `StoreVdvRecords` to the supported Omnidots job names.
- Add the matching `OmnidotsService` forwarding methods and dispatch each name through that service boundary.
- Add two enabled Quartz schedule entries in UTC:
  - Veff at the start of every two-hour window: `0 0 0/2 * * ?`.
  - VDV fifteen minutes later: `0 15 0/2 * * ?`.
- Add focused regression tests for job dispatch and the configured schedules.

## Design

The import handlers already exist behind `OmnidotsApi.StoreVeffRecords(int)` and `OmnidotsApi.StoreVdvRecords(int)`. `OmnidotsService` will expose matching forwarding methods so the job runner can call each through the established service boundary with a 120-minute window, matching its two-hour recurrence.

`OmnidotsMonitorJobDispatcher.SupportedJobNames` will contain the two names so Quartz startup validation accepts them. `MonitorJobRunner.RunAsync` will recognize the same strings, which enables both `--job <name>` and `RVT__MONITOR_JOB=<name>` one-shot execution.

The schedule remains configuration-driven. No endpoint, handler, data model, or shared scheduler behavior changes. The two jobs are independent: a Veff failure does not prevent the later VDV run, and each existing handler continues to persist its own operational errors.

## Testing

Focused tests will prove that both new job names invoke their intended service methods and return success. A configuration test will read `appsettings.json` and assert both enabled jobs, their UTC cron expressions, and their 15-minute offset. The Omnidots test project will then run in full.

## Constraints

- Preserve the existing job names exactly: `StoreVeffRecords` and `StoreVdvRecords`.
- Keep the scheduled values in Quartz six-field cron format.
- Do not alter the existing Veff/VDV import logic or their database schema.
