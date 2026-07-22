# RVT Portal Review Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the confirmed cutover blockers and security defects identified in `RvTPortal AI Review.docx`, establish production-representative PostgreSQL/SPA gates, and then improve the portal's application boundary without a broad rewrite.

**Architecture:** Stabilize behavior before restructuring. The first phases add executable gates and repair runtime, authorization, time, deployment, and adapter boundaries in the existing projects; the final phase introduces a compile-time application-core boundary incrementally while retaining the three-context shared-transaction design.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL/TimescaleDB, React 19, TypeScript, Vite 6, Vitest, Testing Library, Playwright, Bash, PowerShell, GitHub Actions.

## Global Constraints

- Work from the monorepo root `/Users/oldgeorge/Documents/rvt-mono`; portal paths are rooted at `apps/portal/`.
- Preserve the three `DbContext` split and the single shared `DbConnection`/unit-of-work transaction pipeline.
- Keep active RVT common consumers source-referenced; do not change the package-validation boundary.
- Treat `timestamp without time zone` telemetry as UTC-by-contract: use `DateTimeKind.Unspecified` only at the Npgsql query boundary and restore `DateTimeKind.Utc` before API serialization.
- Treat contract hire fields as calendar dates; convert them to UTC midnight only at the persistence boundary while the entity remains `DateTime`.
- All P0/P1 fixes require a regression test that fails before the production change and passes afterward.
- Real PostgreSQL tests are mandatory for Npgsql `DateTime.Kind`, schema-repair, and schema-qualification behavior; SQLite/InMemory results are not evidence for those cases.
- Return `404` for cross-tenant resource reads and keep forgot-password responses indistinguishable for existing and nonexistent accounts.
- Do not add automatic retries to non-idempotent outbound operations. Add timeouts and typed failure translation first; retry only a demonstrably idempotent operation.
- Keep Help Admin title-focus remediation deferred unless the temporary UI is confirmed for release.
- Resolve What3Words with a product decision: either remove the feature and its secret completely, or retain it behind a typed outbound port with header-based authentication. Do not leave the current query-string secret path in production.

---

## Review Disposition

### Confirmed P0/P1 findings in the current monorepo

| ID | Finding | Current evidence | Disposition |
|---|---|---|---|
| R01 | Vibration trace query uses unmapped `OmnidotsTraces` | `MonitorService.cs` calls `Set<OmnidotsTraces>()`; only `OmnidotsTrace` is mapped by `RVTSearchContext` | Fix immediately |
| R02 | Search timestamp read/query contract is inconsistent | UTC bounds are passed to `timestamp without time zone` filters; returned `DateTime` values reach JSON without a UTC kind | Prove on PostgreSQL, then fix as one boundary |
| R03 | Installer monitor-picture IDOR | the controller allows installers and `CanReadMonitorAsync` accepts `row.IsAssigned` without matching `CompanyId` | Fix immediately; the comment dismissing it confuses upload policy with read authorization |
| R04 | `TimeProvider` is absent from DI | `ReportingServiceReportGenerationClient` requires `TimeProvider`; registration adds only `IRvtDateTimeProvider` | Fix immediately |
| R05 | Expired/future site assignments grant access | `CanReadSiteAsync` and `GetVisibleSitesQuery` check row existence only | Fix immediately with one reusable active-assignment predicate |
| R06 | Password-reset links trust request host by default | `BuildClientUrl` falls back to request scheme/host and `AllowedHosts` is `*` | Require validated `Spa:PublicBaseUrl` in non-development |
| R07 | Contract date-only input writes `Unspecified` to `timestamptz` | `request.OnHireDate.Date` and `OffHireDate?.Date` preserve `Unspecified` | Fix immediately |
| R08 | Required existing-database repair is not deployed | `restore_unmapped_column_defaults.sql` exists, but `ScriptRunner`, the project content items, and publish output omit it | Fix immediately; the reviewer comment calling this a hallucination is contradicted by source |
| R09 | Monitor options leak cross-tenant metadata | `OptionsAsync` has no actor parameter and returns every contract and non-archived site | Fix before cutover |
| R10 | Self-service email change bypasses confirmation | `UpdateProfileAsync` directly replaces `Email` and `UserName` | Replace with change-email token flow or prohibit email changes in profile |
| R11 | Forgot-password behavior can enumerate accounts during provider failures | existing confirmed accounts can return a detailed 500 while unknown accounts return generic 200 | Keep the public response generic; log provider failure internally |
| R12 | Readiness health does not test the database | `/api/health` always returns 200 | Split liveness and readiness; gate deployment on readiness |
| R13 | Forwarded headers are not configured | rate limiting and auth origin use connection/request values directly | Configure trusted proxies/networks before dependent middleware |
| R14 | Managed-identity storage mode does not cover site archives | `SiteArchiveService` always builds `BlobServiceClient` from a connection string | Use the shared storage client factory for both modes |
| R15 | Schema validation ignores schema names | `RvtSchemaValidator` keys only by `table_name` | Key by provider schema plus relation and column |
| R16 | Calendar padding cells request the wrong month | `CalendarDayButton` sends only `dayNumber` | Send the complete ISO date |
| R17 | Local-day defaults are derived through UTC | dashboard and contract defaults use `toISOString().slice(0, 10)` | Format local calendar components directly |
| R18 | Client requests can apply stale responses | company/filter changes lack abort or generation guards | Add request cancellation/generation checks |
| R19 | Outbound timeout/failure translation is incomplete | report and vendor clients do not consistently contain cancellation/URI/configuration failures | Add named-client timeouts and typed results |
| R20 | The monorepo has no active root GitHub workflow | imported workflows remain under module `.github/` directories and therefore do not run for the root repository | Establish root CI first |

### Confirmed P2 or decision-gated findings

- The destructive dev restore suppresses `pg_restore` failure with `|| true`; lower production impact does not justify a false success after dropping the target database.
- What3Words puts its key in a logged URL. Product ownership must decide retain-or-remove before implementation.
- Help Admin keys an asset row by editable title. The defect is real, but it remains deferred while that UI is explicitly temporary.
- `NoiseLevel15minAvg.SampleTime` lacks the explicit provider column type; all telemetry mappings should be audited together.
- `RVT.BusinessLogic.csproj` retains unused Azure Blob and HTTP packages, weakening architectural guards.
- Secret values are passed to `dotnet user-secrets set` as process arguments in `set-dev-secrets.ps1`.
- Some large reads use `Take(1000000)` and paging without a deterministic tie-break; address after correctness and security gates.
- Sonar's lifecycle-script findings remain open for `npm ci`; the runtime container already uses `nginx-unprivileged`, so the old run-as-root observation is superseded.

### Superseded or partially resolved observations

- Root `project_state.md` exists and is current.
- The monorepo is not on the reviewed SMB checkout; AppleDouble, Word lock, `.vs`, `TestResults`, and module artifact debris are absent.
- SendGrid uses a singleton `ISendGridClientFactory`; the per-email client observation is obsolete.
- `ReportingServiceReportGenerationClient` already translates `HttpRequestException`, but still needs a configured timeout and cancellation/configuration containment.
- The release exporter and architecture guards remain strengths and should be preserved.

---

## Phase 1: Establish evidence and remove immediate cutover blockers

### Task 1: Activate root portal CI with PostgreSQL evidence

**Files:**
- Create: `.github/workflows/portal-verify.yml`
- Create: `apps/portal/scripts/verify-postgres-integration.sh`
- Modify: `apps/portal/scripts/verify-backend.sh`
- Modify: `docs/release/portal/CUTOVER_RUNBOOK.md`
- Test: `tests/verify-mono-layout.test.sh`

**Interfaces:**
- Consumes: `RVT_TEST_POSTGRES_CONNECTION`, the portal solution, and existing `RequiresPostgresFact` tests.
- Produces: an active root workflow with `portal-static`, `portal-postgres`, and `portal-client-e2e` jobs; no module-local workflow is treated as active.

- [ ] **Step 1: Add a failing repository-layout assertion for active workflows**

Extend `tests/verify-mono-layout.test.sh` to require `.github/workflows/portal-verify.yml` and to reject documentation that describes `apps/portal/.github/workflows/build.yml` as active monorepo CI.

- [ ] **Step 2: Run the layout test and verify RED**

Run: `tests/verify-mono-layout.test.sh`

Expected: FAIL naming the missing root portal workflow.

- [ ] **Step 3: Add the root workflow**

Use `working-directory: apps/portal` for module commands. Configure a `postgres:17-alpine` service with a health check, and set:

```yaml
env:
  RVT_TEST_POSTGRES_CONNECTION: Host=localhost;Port=5432;Database=rvt_tests;Username=postgres;Password=postgres
```

The static job runs `./scripts/verify-backend.sh` and `./scripts/verify-frontend.sh`. The PostgreSQL job runs `./scripts/verify-postgres-integration.sh`. The client job installs Playwright Chromium and runs `npm run test:e2e` in addition to Vitest.

- [ ] **Step 4: Make the PostgreSQL script fail if provider tests skip**

Implement `verify-postgres-integration.sh` with strict mode, database initialization, the filtered PostgreSQL test run, and a TRX result check that rejects any skipped `[RequiresPostgresFact]` test:

```bash
#!/usr/bin/env bash
set -euo pipefail

test -n "${RVT_TEST_POSTGRES_CONNECTION:-}"
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~UtcTimestampGuardTests|FullyQualifiedName~UnmappedColumnDefaultTests|FullyQualifiedName~DashboardBreachTimestamptzTests|FullyQualifiedName~SearchTimestampPostgresTests|FullyQualifiedName~SchemaValidationPostgresTests' \
  --logger 'trx;LogFileName=postgres.trx' \
  --results-directory artifacts/postgres-tests
if rg -q 'outcome="NotExecuted"' artifacts/postgres-tests/postgres.trx; then
  echo 'A required PostgreSQL test was skipped.' >&2
  exit 1
fi
```

- [ ] **Step 5: Align the cutover runbook with the active gates**

Document the root workflow path, `RVT_TEST_POSTGRES_CONNECTION`, real Playwright execution, and the distinction between liveness and readiness.

- [ ] **Step 6: Verify GREEN**

Run:

```bash
tests/verify-mono-layout.test.sh
bash -n apps/portal/scripts/verify-backend.sh apps/portal/scripts/verify-postgres-integration.sh
```

Expected: both commands exit 0.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/portal-verify.yml apps/portal/scripts/verify-postgres-integration.sh apps/portal/scripts/verify-backend.sh docs/release/portal/CUTOVER_RUNBOOK.md tests/verify-mono-layout.test.sh
git commit -m "ci: activate portal verification in monorepo"
```

### Task 2: Fix service wiring and the unmapped vibration trace query

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorData.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Data/DataApplicationService.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/SpaHostSmokeTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`

**Interfaces:**
- Consumes: mapped `OmnidotsTrace` rows and `TimeProvider.System`.
- Produces: resolvable report-rule services and `SearchQueryResult<OmnidotsTrace>` throughout the vibration-trace path.

- [ ] **Step 1: Write failing DI and vibration tests**

Add one host test that resolves `IReportGenerationClient` from a scope and one data-view test that exercises `GetVibrationTraces` against the EF model rather than a fake `IMonitorService`.

- [ ] **Step 2: Verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SpaHostSmokeTests|FullyQualifiedName~DataViewTests'
```

Expected: the DI test reports an unresolved `System.TimeProvider`; the vibration test reports that `OmnidotsTraces` is not in the model.

- [ ] **Step 3: Register the framework time provider**

Add exactly one singleton registration:

```csharp
services.AddSingleton(TimeProvider.System);
```

- [ ] **Step 4: Use the mapped trace entity end-to-end**

Change `IMonitorService.GetVibrationTraces`, `MonitorService.GetVibrationTraces`, `MonitorData.VibrationTraces`, and trace dataset mapping from `OmnidotsTraces` to `OmnidotsTrace`. Query through `searchContext.OmnidotsTraces` and remove the unmapped plural DTO from the execution path.

- [ ] **Step 5: Verify GREEN**

Run the focused command from Step 2 and expect all selected tests to pass.

- [ ] **Step 6: Commit**

```bash
git add apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
git commit -m "fix: restore report and vibration runtime paths"
```

### Task 3: Close tenant and assignment authorization gaps

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorAdministrationReadService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorReadAuthorizationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorListReader.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/MonitorsController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Sites/SiteApplicationService.cs`
- Create: `apps/portal/RvtPortal.Spa/Application/Sites/ActiveSiteAssignment.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`

**Interfaces:**
- Consumes: `PortalUserContext.CompanyId`, monitor row `CompanyId`, and `SiteUsers.StartDate`/`EndDate`.
- Produces: one active-assignment predicate used by list and detail queries, plus actor-scoped monitor options.

- [ ] **Step 1: Add failing authorization tests**

Cover all of these cases:

```text
installer company A GETs company B monitor picture -> 404
company user with expired assignment GETs site -> 404
company user with future assignment lists sites -> site absent
installer/company user GETs monitor options -> only actor-visible contracts and sites
```

- [ ] **Step 2: Verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~ContractSiteOperationsTests'
```

Expected: the new cross-company and inactive-window assertions fail.

- [ ] **Step 3: Centralize active assignment semantics**

Create an expression helper whose inclusive window is:

```csharp
siteUser.StartDate <= nowUtc &&
(!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= nowUtc)
```

Inject `TimeProvider` into `SiteApplicationService` so tests can fix `nowUtc`.

- [ ] **Step 4: Apply the company match to every installer read path**

For installer reads require:

```csharp
row.IsAssigned &&
row.CompanyId.HasValue &&
actor.CompanyId.HasValue &&
row.CompanyId.Value == actor.CompanyId.Value
```

Use the same rule for detail, picture, and list authorization.

- [ ] **Step 5: Pass the actor into monitor options**

Change `OptionsAsync(CancellationToken)` to `OptionsAsync(PortalUserContext actor, CancellationToken)` and filter contracts/sites with the same visibility rules used by inventory reads.

- [ ] **Step 6: Verify GREEN**

Run the focused test command from Step 2 and expect all selected tests to pass.

- [ ] **Step 7: Commit**

```bash
git add apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
git commit -m "fix: enforce active tenant authorization"
```

### Task 4: Harden public auth origins and account workflows

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Program.cs`
- Modify: `apps/portal/RvtPortal.Spa/appsettings.json`
- Modify: `apps/portal/RvtPortal.Spa/Application/Auth/AuthApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/AuthController.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/SecurityHardeningTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/SpaHostSmokeTests.cs`

**Interfaces:**
- Consumes: `Spa:PublicBaseUrl`, configured forwarded-header trust, Identity change-email tokens, internal logging.
- Produces: host-independent auth links, proxy-correct request metadata, confirmed email changes, and indistinguishable forgot-password responses.

- [ ] **Step 1: Write failing tests**

Add tests proving:

```text
Production startup without Spa:PublicBaseUrl fails configuration validation.
A malicious Host header never appears in a reset link.
Changing profile email does not immediately change the confirmed login address.
SendGrid failure returns the same public 200 body as an unknown account.
Forwarded headers are honored only from configured proxies/networks.
```

- [ ] **Step 2: Verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SecurityHardeningTests|FullyQualifiedName~SpaHostSmokeTests'
```

- [ ] **Step 3: Require a validated public base URI**

Bind `Spa:PublicBaseUrl` to options and validate it as an absolute HTTPS URI outside Development/Testing. Remove the production request-host fallback from `BuildClientUrl`. Replace `AllowedHosts: "*"` with the exact externally accepted host names for each deployed environment.

- [ ] **Step 4: Configure forwarded headers before rate limiting, HTTPS redirect, authentication, and CSRF checks**

Clear the framework defaults and populate `KnownProxies`/`KnownNetworks` only from configuration. Do not enable unrestricted `ForwardedHeaders.XForwardedFor | XForwardedProto | XForwardedHost` trust.

- [ ] **Step 5: Make email changes a confirmed workflow**

If `request.Email` differs, generate a change-email token and send a confirmation link; leave `Email`, `UserName`, and `EmailConfirmed` unchanged until confirmation. Apply non-email profile fields independently.

- [ ] **Step 6: Make forgot-password output uniform**

Return `PasswordResetMessage()` for unknown, unconfirmed, and provider-failure paths. Log the provider exception with an internal correlation id; never place provider details in the anonymous response.

- [ ] **Step 7: Verify GREEN and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SecurityHardeningTests|FullyQualifiedName~SpaHostSmokeTests'
git add apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
git commit -m "fix: harden public authentication boundaries"
```

### Task 5: Establish one explicit UTC/search timestamp contract

**Files:**
- Create: `apps/portal/RvtPortal.Spa/Application/Monitors/SearchTimestampPolicy.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Data/DataApplicationService.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RVTSearchContext.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Contracts/ContractCommands.cs`
- Modify: `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.tsx`
- Test: `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/SearchTimestampPostgresTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`
- Test: `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.test.tsx`

**Interfaces:**
- Consumes: UTC application instants, PostgreSQL `timestamp without time zone` telemetry, and date-only contract strings.
- Produces: `SearchTimestampPolicy.ToDatabase(DateTime)` and `SearchTimestampPolicy.FromDatabase(DateTime?)` as the only conversion points.

- [ ] **Step 1: Write PostgreSQL RED tests for the disputed behavior**

The integration test must insert a known telemetry row at `2026-07-01 14:30:00`, query it with UTC bounds, and assert both query success and returned JSON `2026-07-01T14:30:00Z`. Add a separate contract command test that persists `2026-07-01` without triggering `UtcTimestampGuardInterceptor`.

- [ ] **Step 2: Run against PostgreSQL and record RED**

Run:

```bash
RVT_TEST_POSTGRES_CONNECTION="$RVT_TEST_POSTGRES_CONNECTION" \
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter 'FullyQualifiedName~SearchTimestampPostgresTests|FullyQualifiedName~ContractSiteOperationsTests'
```

Expected: the query either throws Npgsql's UTC-to-`timestamp` error or the JSON assertion lacks `Z`; contract persistence rejects `Unspecified`.

- [ ] **Step 3: Implement the boundary policy**

```csharp
internal static class SearchTimestampPolicy
{
    public static DateTime ToDatabase(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            : throw new ArgumentException(
                "Search timestamp bounds must be UTC.",
                nameof(value));

    public static DateTime? FromDatabase(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
```

Apply `ToDatabase` only to time-series `SampleTime` bounds. Apply `FromDatabase` while constructing API rows/graph points so JSON emits an unambiguous UTC timestamp.

- [ ] **Step 4: Complete the EF telemetry type audit**

Every non-date `SampleTime` in `RVTSearchContext` must use `dateTimeColumnType`; explicit daily aggregates remain `date`. Add a model test that enumerates every `SampleTime` property and compares its provider store type to the approved table.

- [ ] **Step 5: Mark contract calendar dates as UTC midnight at persistence**

Use one helper in `ContractCommandWorkflow`:

```csharp
private static DateTime AsUtcDate(DateTime value) =>
    DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
```

Apply it to both create and update, including nullable `OffHireDate`.

- [ ] **Step 6: Make the client test the API contract, not the workstation timezone**

Keep `Intl.DateTimeFormat` for local presentation, but add tests under `TZ=Europe/London` and `TZ=UTC` proving the same UTC instant is interpreted correctly. Do not append `Z` in the client as a repair for ambiguous server output; the API owns that contract.

- [ ] **Step 7: Verify GREEN and commit**

```bash
RVT_TEST_POSTGRES_CONNECTION="$RVT_TEST_POSTGRES_CONNECTION" dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SearchTimestampPostgresTests|FullyQualifiedName~ContractSiteOperationsTests'
cd apps/portal/RvtPortal.Client && TZ=Europe/London npm run test:run -- DataViewPanels.test.tsx
git add apps/portal/RVT.DataAccess apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests apps/portal/RvtPortal.Client
git commit -m "fix: define portal timestamp boundaries"
```

### Task 6: Make schema deployment complete and failure-aware

**Files:**
- Modify: `apps/portal/RVT.SchemaDeploy/ScriptRunner.cs`
- Modify: `apps/portal/RVT.SchemaDeploy/RVT.SchemaDeploy.csproj`
- Modify: `apps/portal/RVT.SchemaDeploy/DeployOptions.cs`
- Modify: `apps/portal/database/postgres/restore_unmapped_column_defaults.sql`
- Test: `apps/portal/RvtPortal.Spa.Tests/SchemaDeployTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/UnmappedColumnDefaultTests.cs`
- Modify: `apps/portal/docs/deploy/share-dev-database.sh`

**Interfaces:**
- Consumes: canonical SQL root and a PostgreSQL connection.
- Produces: deterministic script order `create_unmapped_schema.sql` -> `restore_unmapped_column_defaults.sql` -> `post-load/*.sql`, including publish output.

- [ ] **Step 1: Add failing deploy-order and publish tests**

Assert that dry-run output contains the repair script exactly once between create and post-load scripts, and that `RVT.SchemaDeploy.csproj` publishes it as `sql/restore_unmapped_column_defaults.sql`.

- [ ] **Step 2: Verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SchemaDeployTests|FullyQualifiedName~UnmappedColumnDefaultTests'
```

Expected: the repair-order and published-content assertions fail.

- [ ] **Step 3: Add the repair as an explicit script stage**

In `ResolveScripts`, add:

```csharp
var repair = Path.Combine(options.ScriptRoot, "restore_unmapped_column_defaults.sql");
if (File.Exists(repair))
{
    scripts.Add(repair);
}
```

Place it after `create_unmapped_schema.sql` and before `post-load` enumeration. Add a matching `<Content>` item with `CopyToOutputDirectory="PreserveNewest"`.

- [ ] **Step 4: Prove idempotency against real PostgreSQL**

Run the deploy tool twice against the same fixture database and assert both runs succeed and the repaired defaults equal the canonical expressions. A second run must not mutate data or fail.

- [ ] **Step 5: Stop suppressing destructive restore failures**

Remove `|| true` from `pg_restore`. Capture its status before verification, exit nonzero on restore failure, and require the expected verification queries to return nonzero table counts before printing `Restore complete.`

- [ ] **Step 6: Verify GREEN and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SchemaDeployTests|FullyQualifiedName~UnmappedColumnDefaultTests'
bash -n apps/portal/docs/deploy/share-dev-database.sh
git add apps/portal/RVT.SchemaDeploy apps/portal/database/postgres apps/portal/RvtPortal.Spa.Tests apps/portal/docs/deploy/share-dev-database.sh
git commit -m "fix: deploy required database repairs"
```

## Phase 2: Complete release-critical platform and client hardening

### Task 7: Replace the false health gate with liveness and readiness

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Program.cs`
- Replace: `apps/portal/RvtPortal.Spa/Api/HealthController.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/SpaHostSmokeTests.cs`
- Modify: `docs/release/portal/CUTOVER_RUNBOOK.md`

**Interfaces:**
- Consumes: ASP.NET Core health checks and all three portal database contexts.
- Produces: `/api/health/live` for process liveness and `/api/health/ready` for database/schema readiness.

- [ ] **Step 1: Add failing health tests**

Prove that liveness remains 200 when the database is unavailable, readiness returns 503 when connection/schema validation fails, and readiness returns 200 only after the database is usable.

- [ ] **Step 2: Verify RED**

Run: `dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~SpaHostSmokeTests`

- [ ] **Step 3: Register tagged checks and map explicit endpoints**

Use `AddHealthChecks().AddDbContextCheck<...>()` for the contexts and one custom schema check. Map liveness with a predicate that selects no dependency checks, and readiness with the `ready` tag. Return JSON containing only status and check names; do not leak connection details.

- [ ] **Step 4: Update deployment documentation**

Replace all release gating references to `/api/health` with `/api/health/ready`; retain `/api/health/live` for container probes.

- [ ] **Step 5: Verify GREEN and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~SpaHostSmokeTests
git add apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests docs/release/portal/CUTOVER_RUNBOOK.md
git commit -m "feat: add portal readiness health gate"
```

### Task 8: Unify storage and outbound integration safety

**Files:**
- Create: `apps/portal/RVT.BusinessLogic/Ports/Geocoding/IWhat3WordsGateway.cs` only if What3Words is retained
- Create: `apps/portal/RvtPortal.Spa/Adapters/Vendors/What3WordsGateway.cs` only if retained
- Modify: `apps/portal/RvtPortal.Spa/Application/Installers/InstallerApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Vendors/OmnidotsVibrationGateway.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Reporting/ReportGenerationClient.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveService.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `apps/portal/docs/deploy/set-dev-secrets.ps1`
- Test: `apps/portal/RvtPortal.Spa.Tests/OmnidotsVibrationGatewayTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/ReportGenerationClientTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/SiteArchiveServiceSecurityTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`

**Interfaces:**
- Consumes: named `HttpClient` instances, typed adapter options, shared blob-client construction, and cancellation tokens.
- Produces: bounded external calls that return typed failures and support both connection-string and managed-identity storage.

- [ ] **Step 1: Record the What3Words product decision**

Add an ADR under `docs/architecture/portal/` with exactly one outcome:

```text
Retain: What3Words remains a supported installer workflow and must use X-Api-Key via IWhat3WordsGateway.
Remove: delete endpoint, configuration, secret prompts, client calls, tests, and documentation as one change.
```

Do not start the adapter implementation until the ADR selects one outcome.

- [ ] **Step 2: Write failing adapter tests**

For retained integrations, cover configured timeout, caller cancellation, timeout translation, invalid/missing URL, non-success body truncation, and absence of secrets from exception/log messages. Add archive tests for both connection-string and `ServiceUri` modes.

- [ ] **Step 3: Configure named clients**

Set explicit timeouts in `ServiceCollectionExtensions.cs`:

```csharp
services.AddHttpClient<IReportGenerationClient, ReportingServiceReportGenerationClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
services.AddHttpClient<IVibrationVendorGateway, OmnidotsVibrationGateway>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
```

Catch timeout-shaped `OperationCanceledException` only when the caller token was not cancelled, and translate it to the adapter's typed failure. Keep genuine caller cancellation cancellable.

- [ ] **Step 4: Move What3Words out of the application service or remove it**

If retained, send the key only in `X-Api-Key`; the application service calls `IWhat3WordsGateway` and no longer consumes raw `IConfiguration` or `IHttpClientFactory` for this integration.

- [ ] **Step 5: Reuse the storage-client factory**

Inject the existing storage client/factory used by monitor pictures into `SiteArchiveService`; never construct `BlobServiceClient` directly from `blobConnectionString` inside archive methods.

- [ ] **Step 6: Avoid exposing secrets in process arguments**

Change sensitive `dotnet user-secrets set` calls to pipe the value through standard input where supported, or write directly through a short-lived .NET configuration helper that reads `SecureString`/stdin. Ensure `Get-CimInstance Win32_Process` or `ps` cannot see the secret value.

- [ ] **Step 7: Verify and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~OmnidotsVibrationGatewayTests|FullyQualifiedName~ReportGenerationClientTests|FullyQualifiedName~SiteArchiveServiceSecurityTests|FullyQualifiedName~MonitorWorkflowTests'
git add apps/portal/RVT.BusinessLogic apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests apps/portal/docs/deploy/set-dev-secrets.ps1 docs/architecture/portal
git commit -m "fix: contain portal integration failures"
```

### Task 9: Repair calendar, local-date, and stale-response client behavior

**Files:**
- Modify: `apps/portal/RvtPortal.Client/src/operations/DashboardRoutePanels.tsx`
- Modify: `apps/portal/RvtPortal.Client/src/operations/DashboardPanels.tsx`
- Modify: `apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.tsx`
- Modify: affected API client functions under `apps/portal/RvtPortal.Client/src/api/`
- Test: `apps/portal/RvtPortal.Client/src/operations/DashboardRoutePanels.test.tsx`
- Test: `apps/portal/RvtPortal.Client/src/operations/DashboardPanels.test.tsx`
- Test: `apps/portal/RvtPortal.Client/src/operations/ContractSitePanels.test.tsx`

**Interfaces:**
- Consumes: full `CalendarMonthDayItem.date`, local browser calendar fields, `AbortSignal` or monotonically increasing request generation.
- Produces: exact-date selection and stale-safe client mutations.

- [ ] **Step 1: Add RED tests**

Cover clicking `30 April` in a May grid, local date defaults around midnight in `Pacific/Kiritimati` and `America/Los_Angeles`, and two out-of-order company/filter requests where the older response arrives last.

- [ ] **Step 2: Verify RED**

Run:

```bash
cd apps/portal/RvtPortal.Client
TZ=Pacific/Kiritimati npm run test:run -- DashboardRoutePanels.test.tsx DashboardPanels.test.tsx ContractSitePanels.test.tsx
```

- [ ] **Step 3: Pass complete dates from the calendar**

Change the callback contract to `onSelect(date: string)` and call `onSelect(day.date)`. Selection equality uses the complete ISO date, not the day number.

- [ ] **Step 4: Format local date inputs without UTC conversion**

Add one shared helper:

```ts
export function localDateInputValue(date = new Date()) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
```

Use it for dashboard and contract defaults. Preserve existing API date strings with `value.slice(0, 10)` instead of parsing and re-serializing date-only values.

- [ ] **Step 5: Reject stale responses**

Use `AbortController` where the API wrapper accepts a signal; otherwise capture an incrementing generation and apply results only when it equals the current generation. Abort on effect cleanup and on a new company/filter selection.

- [ ] **Step 6: Verify GREEN and commit**

```bash
cd apps/portal/RvtPortal.Client
npm run lint
npm run test:run -- DashboardRoutePanels.test.tsx DashboardPanels.test.tsx ContractSitePanels.test.tsx
git add src
git commit -m "fix: stabilize portal date and request behavior"
```

### Task 10: Decide and, if needed, repair the temporary Help Admin key

**Files:**
- Modify only if retained: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx`
- Test only if retained: `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.ts`
- Modify: `docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md`

**Interfaces:**
- Consumes: product decision on whether Help Admin ships.
- Produces: either a documented exclusion or a stable per-row client id.

- [ ] **Step 1: Record ship/defer status in the readiness matrix**

If deferred, state that Help Admin is excluded from the cutover surface and stop. If shipped, continue.

- [ ] **Step 2: Add a failing typing test**

Render an asset with title `A`, type `BC`, and assert typing `AB` leaves focus on the title input and preserves the second character.

- [ ] **Step 3: Use a stable key**

Add a client-only id when an asset form row is created and key by that id. Do not key by title, URL, or array index.

- [ ] **Step 4: Verify and commit**

```bash
(cd apps/portal/RvtPortal.Client && npm run test:run -- HelpAdminPanel.test.ts)
git add apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.test.ts docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md
git commit -m "fix: stabilize help asset editing"
```

## Phase 3: Data governance, performance, and release completion

### Task 11: Qualify schema validation and protect destructive relationships

**Files:**
- Modify: `apps/portal/RVT.DataAccess/Configuration/RvtSchemaValidator.cs`
- Modify: `apps/portal/RVT.DataAccess/Context/RVTDbContext.cs`
- Create: provider migration files only for confirmed delete-behavior changes under `apps/portal/RVT.DataAccess/Migrations/`
- Test: `apps/portal/RvtPortal.Spa.Tests/DatabaseNamingConventionTests.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/SchemaValidationPostgresTests.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/DeleteBehaviorTests.cs`

**Interfaces:**
- Consumes: EF schema/table/column metadata and the database's `table_schema`, `table_name`, `column_name` rows.
- Produces: schema-qualified validation and an explicitly approved delete-behavior matrix.

- [ ] **Step 1: Add a failing wrong-schema test**

Create identical relation names in two schemas, omit a required column from the mapped schema, and assert validation fails even when the other schema contains that column.

- [ ] **Step 2: Key schema observations by a value object**

Use:

```csharp
internal readonly record struct DatabaseRelation(string Schema, string Name);
```

Read all three information-schema columns and compare with the model's resolved schema and relation name using provider-appropriate case rules.

- [ ] **Step 3: Inventory delete behavior before changing it**

Generate an approved test matrix for every relationship that owns notification/breach/audit history. Require `Restrict` or `NoAction` for historical records and allow `Cascade` only for true owned children such as Help assets.

- [ ] **Step 4: Add migrations only for matrix mismatches**

Do not globally disable cascade deletes. Change only relationships whose deletion would erase required history, and include forward/rollback validation.

- [ ] **Step 5: Verify against PostgreSQL and commit**

```bash
RVT_TEST_POSTGRES_CONNECTION="$RVT_TEST_POSTGRES_CONNECTION" dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SchemaValidationPostgresTests|FullyQualifiedName~DeleteBehaviorTests|FullyQualifiedName~DatabaseNamingConventionTests'
git add apps/portal/RVT.DataAccess apps/portal/RvtPortal.Spa.Tests
git commit -m "fix: enforce schema and history boundaries"
```

### Task 12: Bound large reads and make paging deterministic

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorService.cs`
- Modify: query helpers under `apps/portal/RVT.Entities/Querying/`
- Test: `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/QueryPagingTests.cs`

**Interfaces:**
- Consumes: explicit maximum export size, page size, and stable unique tie-break fields.
- Produces: bounded reads and deterministic order for every `Skip`/`Take` query.

- [ ] **Step 1: Write failing size and tie-break tests**

Assert API page size above the approved maximum returns validation failure, export paths use streaming, and equal primary sort values are ordered by a unique secondary key.

- [ ] **Step 2: Remove `Take(1000000)` from request paths**

Use bounded paging for UI calls and `IAsyncEnumerable` streaming for CSV/export. Keep an explicit hard safety ceiling only for operations that cannot stream.

- [ ] **Step 3: Add stable secondary ordering**

After the requested sort, append `ThenBy` on the entity's stable key; for keyless telemetry use `SampleTime` plus the natural sensor/trace identity available to that dataset.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~DataViewTests|FullyQualifiedName~QueryPagingTests'
git add apps/portal/RvtPortal.Spa apps/portal/RVT.Entities apps/portal/RvtPortal.Spa.Tests
git commit -m "perf: bound and stabilize portal queries"
```

### Task 13: Remove dead dependencies and close architecture-guard holes

**Files:**
- Modify: `apps/portal/RVT.BusinessLogic/RVT.BusinessLogic.csproj`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `docs/architecture/portal/ports-and-adapters-catalog.md`
- Test: `apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs`

**Interfaces:**
- Consumes: current project references and package usage.
- Produces: an inner business project with no Azure Blob or HTTP implementation packages and executable package/project dependency rules.

- [ ] **Step 1: Add a failing project-file guard**

Assert `RVT.BusinessLogic.csproj` does not reference `Azure.Storage.Blobs` or `Microsoft.Extensions.Http`, and that no inner project references the SPA host or DataAccess.

- [ ] **Step 2: Verify RED**

Run: `dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~RvtCommonDependencyBoundaryTests'`

- [ ] **Step 3: Remove unused dependencies**

Delete the two package references only after `rg` and compilation prove no source consumer remains. Update the ports/adapters catalog to describe the enforced project graph accurately.

- [ ] **Step 4: Verify GREEN and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~RvtCommonDependencyBoundaryTests'
git add apps/portal/RVT.BusinessLogic/RVT.BusinessLogic.csproj apps/portal/RvtPortal.Spa.Tests docs/architecture/portal/ports-and-adapters-catalog.md
git commit -m "refactor: enforce portal core dependencies"
```

### Task 14: Complete release orchestration and Sonar CI fixes

**Files:**
- Modify: `.github/workflows/portal-verify.yml`
- Modify: `apps/portal/scripts/verify-backend.sh`
- Modify: `apps/portal/scripts/verify-frontend.sh`
- Modify: `apps/portal/RvtPortal.Client/Dockerfile`
- Create: `apps/portal/scripts/verify-release-artifact.sh`
- Modify: `docs/release/portal/CUTOVER_RUNBOOK.md`

**Interfaces:**
- Consumes: tested SPA host, three EF migration chains, schema-deploy artifact, Playwright results, npm dependency policy.
- Produces: one release artifact/gate that proves application and schema can be deployed together.

- [ ] **Step 1: Add a failing release-artifact test**

Require the staged release directory to contain the SPA publish output, `RVT.SchemaDeploy.dll`, the complete `sql/` tree including the repair script, and a manifest that records the application commit.

- [ ] **Step 2: Publish both application and schema deployer**

`verify-backend.sh` must publish `RvtPortal.Spa` and `RVT.SchemaDeploy` into separate subdirectories of one release root. `verify-release-artifact.sh` validates required files and runs the schema deployer in `--dry-run` mode.

- [ ] **Step 3: Make Playwright a real CI gate**

Run `npm run test:e2e` in the active root workflow. Keep mocked component tests, but add at least one deployed-stack smoke that uses the real API health/readiness endpoint rather than mocking every request.

- [ ] **Step 4: Resolve npm lifecycle findings deliberately**

Run `npm query ':attr(scripts)'` and record the allowlist. Use `npm ci --ignore-scripts`, then run only required trusted package rebuilds by exact package name. Apply the same policy in `verify-frontend.sh` and the Docker build stage. The final image remains `nginx-unprivileged`.

- [ ] **Step 5: Verify the full portal gate**

```bash
cd apps/portal
./scripts/verify-backend.sh
./scripts/verify-frontend.sh
./scripts/verify-release-artifact.sh
```

Expected: backend, PostgreSQL integration, frontend unit/lint/build, Playwright, container smoke, schema dry-run, and artifact manifest all pass.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/portal-verify.yml apps/portal/scripts apps/portal/RvtPortal.Client/Dockerfile docs/release/portal/CUTOVER_RUNBOOK.md
git commit -m "ci: gate complete portal releases"
```

## Phase 4: Incremental application-core extraction after cutover blockers are green

### Task 15: Introduce a compile-time `RvtPortal.Application` boundary

**Files:**
- Create: `apps/portal/RvtPortal.Application/RvtPortal.Application.csproj`
- Create: `apps/portal/RvtPortal.Application/Users/PortalUserContext.cs`
- Create: `apps/portal/RvtPortal.Application/Ports/` with focused application-owned port interfaces
- Move incrementally: use-case contracts currently under `apps/portal/RvtPortal.Spa/Application/`
- Modify: `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `Rvt.Mono.slnx`
- Modify: `docs/architecture/portal/ports-and-adapters-catalog.md`
- Modify: `docs/architecture/portal/hexagonal-edges-change-log.md`
- Test: `tests/verify-mono-solution.test.sh`

**Interfaces:**
- Consumes: transport-neutral request/result models and focused ports proven by prior tasks.
- Produces: a project that may reference `RVT.Entities` and abstraction packages only; it may not reference ASP.NET Core, `RvtPortal.Spa`, `RVT.DataAccess`, Azure/SendGrid/vendor SDKs, raw `IConfiguration`, or `IHttpClientFactory`.

- [ ] **Step 1: Add a failing project-graph test before creating the project**

The architecture test requires `RvtPortal.Application.csproj`, asserts its allowed project/package set, and rejects forbidden namespaces/packages. `tests/verify-mono-solution.test.sh` must require the new project in the portal solution folder.

- [ ] **Step 2: Verify RED**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter FullyQualifiedName~CqrsArchitectureTests
tests/verify-mono-solution.test.sh
```

- [ ] **Step 3: Create the minimal project and move policies first**

Start with `PortalUserContext`, active-assignment policy, auth/use-case interfaces, and outbound port contracts already detached by earlier tasks. Do not move EF query implementations yet.

- [ ] **Step 4: Move one vertical slice at a time**

For each slice:

```text
move application request/result/interface into RvtPortal.Application
keep controller/API DTO mapping in RvtPortal.Spa
keep EF implementation in an adapter project or the host temporarily
add a focused read/write port without IQueryable leakage
run slice tests and architecture tests
commit the slice
```

Begin with Auth, then Sites, then Monitors. Keep direct EF projections where valuable, but hide them behind focused read ports.

- [ ] **Step 5: Keep the transaction adapter intact**

Define command-side persistence/unit-of-work interfaces inward; retain the existing `EfCoreUnitOfWork` and three-context shared transaction as the adapter implementation. Do not replace it with generic repositories.

- [ ] **Step 6: Map API DTOs only at controllers**

Application requests/results must not import `RvtPortal.Spa.Api`. Add a guard that scans the application project for ASP.NET/HTTP/API namespaces.

- [ ] **Step 7: Verify the aggregate solution**

```bash
tests/verify-mono-solution.test.sh
dotnet restore Rvt.Mono.slnx
dotnet build Rvt.Mono.slnx --no-restore --nologo
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-build
```

- [ ] **Step 8: Commit**

```bash
git add apps/portal/RvtPortal.Application apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests Rvt.Mono.slnx docs/architecture/portal
git commit -m "refactor: establish portal application boundary"
```

## Phase 5: Close the explicitly low-priority review backlog

### Task 16: Resolve provider scope, dead code, DTO drift, and display-only defects

**Files:**
- Create: `docs/architecture/portal/database-provider-support.md`
- Modify or remove after the decision: SQL Server-specific paths under `apps/portal/database/` and provider branches under `apps/portal/RVT.DataAccess/`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorData.cs`
- Modify: `apps/portal/RvtPortal.Client/vite.config.ts`
- Modify: `apps/portal/RvtPortal.Client/package.json`
- Remove only after caller proof: dead Azure blob service/AForge code identified by `rg` and compiler analysis
- Test: `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`
- Test: `apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs`
- Test: `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.test.tsx`

**Interfaces:**
- Consumes: an explicit supported-database decision, OpenAPI source schema, and vibration FFT display requirements.
- Produces: one documented provider policy, generated client contract drift detection, safe local dev binding, and no retained dead implementation.

- [ ] **Step 1: Decide the database-provider contract**

The ADR must select one of two executable policies:

```text
PostgreSQL-only: remove SQL Server release/tooling claims and unsupported branches, retaining historical migration evidence only under docs/history.
Dual-provider: add a SQL Server CI job that applies its migrations and passes the same behavioral contract tests as PostgreSQL where semantics are intended to match.
```

Do not continue advertising a provider that has no green deployment path.

- [ ] **Step 2: Add client-contract drift detection**

Regenerate the OpenAPI TypeScript schema in CI and fail when `git diff --exit-code -- RvtPortal.Client/src/api/schema.d.ts` is nonzero. Replace hand-written Help/report DTOs with generated types or add explicit mapper contract tests where their shape intentionally differs.

- [ ] **Step 3: Make the dev server local by default**

Bind Vite to `127.0.0.1` unless a deliberate `RVT_DEV_SERVER_HOST` override is supplied. Keep insecure proxy behavior development-only and test that production builds cannot enable it.

- [ ] **Step 4: Define and test FFT display normalization**

Add a fixture with the same physical signal across two query-window lengths. Either normalize magnitudes by sample count/window gain or label the values as raw window-dependent magnitude. The test must encode the selected product meaning.

- [ ] **Step 5: Remove dead code only after caller proof**

Use `rg`, solution build, and coverage to prove the old `AzureBlobService` and vendored AForge complex-scalar paths have no callers. Delete the dead classes and their exclusive package references in the same commit; do not repair unreachable behavior.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~DataViewTests|FullyQualifiedName~CutoverReadinessTests'
(cd apps/portal/RvtPortal.Client && npm run lint && npm run test:run -- DataViewPanels.test.tsx)
git diff --exit-code -- apps/portal/RvtPortal.Client/src/api/schema.d.ts
git add apps/portal docs/architecture/portal/database-provider-support.md
git commit -m "chore: close portal review backlog"
```

---

## Final Release Gate

- [ ] Every P0/P1 row in the Review Disposition table has a linked regression test and closed implementation commit.
- [ ] The active root GitHub workflow is green on `main`; module-local workflows are removed or explicitly archived as historical evidence.
- [ ] Required PostgreSQL tests execute with zero skips.
- [ ] Cross-company picture, expired assignment, and monitor-options tests return `404` or scoped results as specified.
- [ ] Password-reset URLs come only from validated configuration; public forgot-password responses are indistinguishable.
- [ ] Telemetry query and JSON timestamp tests pass in UTC and Europe/London, including a DST date.
- [ ] Contract create/update passes against real PostgreSQL.
- [ ] Schema deploy dry-run lists the repair script and a repeated live run is idempotent.
- [ ] Readiness returns 503 on database/schema failure and 200 only when the deployment is usable.
- [ ] Vitest, Playwright, backend tests, release artifact validation, Sonar, and documentation guards pass.
- [ ] `git diff --check`, `tests/verify-mono-layout.test.sh`, `tests/verify-mono-solution.test.sh`, `tests/verify-rvt-common-source-boundary.test.sh`, and both documentation-layout tests pass.
- [ ] The cutover runbook and functionality-readiness matrix reflect the real automated gates and no longer cite pre-monorepo paths.
