# MyATM Reliability Refactor Design

## Goal

Make MyATM ingestion, alerting, scheduling, and operational endpoints reliable and consistent with the handler/port architecture used by Omnidots, while preserving the existing EF Core PostgreSQL-first model and MyATM vendor contract.

## Scope

The work is limited to `myatmmonitor/`, its focused tests and documentation, plus root `AGENTS.md` and `project_state.md`. It does not alter Omnidots vendor logic or introduce monitor-specific mapping policy into `rvt-monitor-common`.

## Architecture

MyATM retains the established shape used by Omnidots: a composition root creates a facade over focused handlers; handlers depend on narrow database ports and a vendor gateway; `IDBClient` remains a compatibility facade. The refactor will make those boundaries explicit for configuration, durable measurement import, and alert-state commits.

`MyAtmMonitorOptions` will provide the customer ID, device-page size, and portal base URL. The composition root will bind and validate the options once, then inject them into the service/facade/gateway path. No credentials are added to tracked configuration.

The HTTP facade and MyATM handlers will expose asynchronous APIs and accept cancellation tokens. Expected vendor or parsing failures are recorded using the operational command and then rethrown so one-shot and Quartz telemetry record a failed execution. Per-monitor import loops may continue to record other monitor failures, but the completed job must fail if any monitor failed.

## Data Flow

### Catalogue

The gateway requests an explicit `$top` equal to the configured page size and advances `$skip` by that same value. The catalogue handler stops only when a page contains fewer than the requested size. Per-device detail retrieval remains vendor-specific, but writes stay behind the existing monitor command.

### Dust and accessory measurements

The gateway treats vendor arrays as unordered: it converts all timestamps to UTC, filters strictly after the stored watermark, and returns records in ascending timestamp order. The caller derives the watermark from the maximum returned timestamp, never array position zero.

A narrow dust-import command persists deduplicated readings and the matching monitor watermark in one EF Core transaction. A separate narrow accessory command reads the latest persisted accessory timestamp and persists a batch, avoiding a new monitor-table column and eliminating one context/save per reading.

### Rules and notifications

Rule evaluation becomes a decision that carries the mutated rule state and an optional notification. A narrow operational command commits the rule state and notification in one transaction. Contact delivery happens only after that commit, and notification-audit writes remain independent because communications are external side effects. This makes normal ingestion durable across polls and removes reliance on the currently unused open-notification query.

For eight-hour rules, a completed time block without an aggregate explicitly advances `Accessed` and records a warning. This prevents a missing block from permanently blocking subsequent blocks; it intentionally trades retroactive alerting for forward progress, while raw readings remain available for later investigation.

## Operations

`StoreAccessoryInfo` becomes a supported one-shot and Quartz job with a daily UTC schedule. `/liveness` remains process-only; `/readiness` checks database reachability through a narrow health port. It does not call the vendor API, because a vendor probe would consume credentials/rate-limit budget and should instead be reflected by actual job failures and telemetry.

The README will describe the current .NET shared host modes, configuration, container liveness/readiness paths, and job names. The obsolete Azure Functions commands and URLs will be removed.

## Testing

Tests will be written before each behavior change and will cover:

- catalogue requests across two pages and exact `$top`/`$skip` values;
- supported accessory dispatch and schedule validation;
- unordered measurement and accessory responses, maximum watermark selection, and batched persistence;
- persisted rule activation/deactivation and exactly-once notification creation across successive imports;
- advancement of an empty eight-hour block;
- failed vendor response producing a non-zero job result;
- readiness success and database-unavailable response;
- architecture boundaries for new MyATM options, health, and narrow command interfaces.

Focused MyATM unit tests and PostgreSQL fixture tests are required. The final verification also runs the MyATM project build, `git diff --check`, and the relevant scheduler/endpoint tests.

## Constraints

- Use the native macOS clone and sync through GitHub.
- Use EF Core with PostgreSQL-first mappings; retain SQL Server compatibility where current tests require it.
- Keep Mapperly app-local and analyzer-only.
- Do not commit credentials or inspect container secrets.
- Do not change vendor JSON contracts or use Omnidots code as a vendor-protocol template.
- Add the root rule that code style and architectural consistency among monitor subprojects is important, while allowing vendor-domain differences.
