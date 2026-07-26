# Database Routine Inventory

## Purpose

This document records the canonical PostgreSQL routines applied by
`RVT.SchemaDeploy` from `database/postgres/post-load/04_routines.sql`.
Routine bodies use canonical application-owned identifiers while preserving
framework-managed Identity names where required.

## Current routines

| PostgreSQL routine | Current consumer | Status |
| --- | --- | --- |
| `public.error_insert` | No active portal call | Deployed procedure |
| `public.monitor_status_for_month` | `MonitorRepository.MonitorStatusForMonth` | Deployed set-returning function |
| `public.monitor_status_time_check` | `MonitorRepository.MonitorStatusTimeCheck` | Deployed set-returning function |
| `public.peak_record_breach_and_alerts` | `OmnidotsBreachesAndAlertsRepository.BreachesAndAlertsForDate` | Deployed set-returning function |
| `public.user_actions_history_insert` | No active portal call | Deployed procedure |

`IRvtStoredRoutineExecutor` is the application boundary for the three active
reads. Archive SQL is a separate, parameterized adapter surface.

## Rules

- Application-owned routine, relation, and column names are lowercase
  snake_case in `public`.
- Use explicit PostgreSQL functions/procedures and Npgsql parameters.
- Preserve quoted `AspNet*` relation and column names only where a routine
  legitimately reads Identity data.
- Keep approved routines in the SchemaDeploy post-load phase.
- Remove unused insert procedures only through a reviewed migration with a
  matching rollback boundary.

## Verification

Apply `04_routines.sql` with stop-on-error enabled to an isolated target.
Smoke calls must return representative rows for the three read functions.
Execute insert procedures inside rolled-back transactions. The
`user_actions_history_insert` procedure supplies `gen_random_uuid()`
explicitly because `user_action_history.id` has no database default.

Guard tests verify canonical routine definitions, required UUID generation,
Identity exceptions, and absence of retired dialect tokens.
