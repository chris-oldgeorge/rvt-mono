# Monitor Data Access Migration

The monitor apps now use EF Core database-first-style mappings as the primary direction for data access while keeping SQL Server and PostgreSQL/Timescale provider support.

## Current State

Merged into local `main`:

- Shared EF Core infrastructure supplied by the private `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure` packages at exact version `0.2.0-rc.1`.
- Shared entity mappings for common monitor tables supplied by `Rvt.Monitor.Common`.
- Authoritative shared package implementation and tests in the private `RVT-Group-LTD/rvt-reporting` repository.
- Monitor-specific EF contexts, entities, and aggregate metadata for MyAtm, AirQ, Omnidots, and Svantek.
- EF metadata tests for the common model and each monitor-specific model.
- SQL identifier whitelisting for temporary raw SQL paths that remain during the transition.

The existing monitor `IDBClient` contracts remain stable. The EF changes are implemented behind those contracts so callers do not need to change at the same time as the data layer.

## Verification

Fresh verification on 2026-06-22:

- `git diff --check`
- `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`
- `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj`
- `dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`
- `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj`

All commands completed successfully. MyAtm and AirQ still emit existing nullability/MSTest analyzer warnings.

## Plane Cycle

Cycle name: `Monitor EF Core Migration Hardening`

Cycle goal: finish the migration from temporary mixed EF/ADO.NET data access to verified EF Core provider-backed monitor persistence, with live Timescale confidence and deployment documentation.

Created in Plane:

- Workspace: `rvt-group`
- Project: `RVT Group Foundation` (`RVTGR`)
- Project ID: `1eff77df-acf1-4f43-a8b7-ce257cc2a10a`
- Cycle ID: `b3babc54-fe25-439d-8620-b67a70418950`

Work items created and linked to the cycle:

1. `RVTGR-393` `[MMH.1] Add live Timescale schema smoke tests`
   - Create safe tests that use `RVT_TIMESCALE_SCHEMA_CONNECTION` when present and mark inconclusive when absent.
   - Verify canonical tables such as `monitor`, `air_q_noise_level`, `my_atm_dust_level`, `omnidots_peak_level`, and `svantek_noise_level`.
   - Do not log connection strings or credentials.

2. `RVTGR-394` `[MMH.2] Verify EF mappings against Timescale`
   - Compare EF metadata table/column names to live Timescale schema metadata.
   - Cover shared tables and all four monitor-specific context mappings.
   - Document intentional provider naming differences.

3. `RVTGR-395` `[MMH.3] Remove remaining temporary ADO.NET data access paths`
   - Retire monitor `DBUtil` methods after EF parity exists.
   - Keep only small, justified raw SQL helpers where EF cannot express the operation safely.
   - Add tests for every retained raw SQL path.

4. `RVTGR-396` `[MMH.4] Close dynamic SQL and identifier safety gaps`
   - Replace string-built SQL with EF LINQ or parameterized/interpolated APIs.
   - Use `MonitorDb.RequireMappedSqlIdentifier` for any unavoidable identifier substitution.
   - Add negative tests for unsupported and injected identifiers.

5. `RVTGR-397` `[MMH.5] Add provider selection and connection-string runbook`
   - Document how each monitor selects SQL Server versus PostgreSQL/Timescale.
   - Include local, container, and production configuration examples without secrets.
   - Include rollback notes for switching providers.

6. `RVTGR-398` `[MMH.6] Run full solution and container verification`
   - Run all monitor solution tests.
   - Build monitor containers.
   - Smoke minimal API liveness endpoints and one-shot job validation.

7. `RVTGR-399` `[MMH.7] Reduce existing analyzer and nullability warnings`
   - Triage current warning baseline in MyAtm and AirQ.
   - Fix warnings that touch the migrated data access surface.
   - Document any warnings intentionally deferred.

8. `RVTGR-400` `[MMH.8] Finalize migration cleanup documentation`
   - Update EF plan checkboxes and status notes.
   - Record final verification evidence.
   - Update `project_state.md` with the post-cycle state.
