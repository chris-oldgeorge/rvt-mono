# EF Core / Data-Access Remediation Plan

Derived from the 2026-07-14 data-access review (master @ `44c506a`). The review found the
modern read paths (CQRS readers, `LookupService`, `MonitorListReader.QueryAsync`, the
stored-routine executor) healthy; the debt is concentrated in the legacy generic-repository
write path, the synchronous search pipeline, unit-of-work transaction hardening, and
connection/registration resiliency.

Each step below is sized as one PR, ordered so that every step lands on a green test suite
and later steps build on earlier ones. Finding IDs (C=critical, Q=query efficiency,
T=correctness trap) refer to the review report.

**Verification for every step:** full test run
(`dotnet test RvtPortal.Spa.Tests -nodeReuse:false -p:UseSharedCompilation=false`),
plus the step-specific checks listed inline. Add regression/architecture tests in the same
PR as the behavior they guard.

---

## Step 1 — Connection resiliency and unit-of-work hardening (C2, C3)

**Goal:** transient DB faults retry instead of surfacing as 500s; the three-context save is
atomic in every branch, and the invariant that makes it possible is asserted.

1. **Add retry + timeout to the provider switch** in
   `RVT.DataAccess/Configuration/RvtDatabaseServiceCollectionExtensions.cs`:
   - `UseSqlServer(..., sql => sql.EnableRetryOnFailure().CommandTimeout(120))`
   - `UseNpgsql(..., npgsql => npgsql.EnableRetryOnFailure().CommandTimeout(120))`
   - Apply to both overloads (connection-string and shared-`DbConnection`).
   - Pick the timeout from the slowest legitimate aggregate view; make it a
     `RvtDatabaseOptions` property with a default rather than a magic number.
2. **Reconcile retry with manual transactions.** `EnableRetryOnFailure` forbids
   user-initiated `BeginTransaction` outside an execution strategy. Wrap the transactional
   block in `EfCoreUnitOfWork.ExecuteInTransactionAsync`
   (`RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs`) with
   `domainContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`.
3. **Close the two atomicity escape hatches** in `EfCoreUnitOfWork`:
   - `HasActiveTransaction()` branch: enlist `searchContext` and `applicationContext` in the
     existing transaction (`UseTransactionAsync` with the current `DbTransaction`) instead of
     saving all three contexts bare.
   - `!SupportsTransactions()` branch: keep for the InMemory test provider only; guard it so
     a relational provider can never take it (`Database.IsRelational()` check + debug assert).
4. **Assert the shared-connection invariant.** The single scoped `DbConnection` registered in
   `RvtPortal.Spa/Program.cs` (`ConfigureDatabases`) is what makes cross-context
   `UseTransactionAsync` legal. Add a startup check (or an architecture test alongside
   `CqrsArchitectureTests`) that all three contexts resolve to the same `DbConnection`
   instance in one scope, with a comment at the registration site explaining the trade-off
   (no context pooling, no intra-request query parallelism).
5. **Set `CommandTimeout` on stored-routine commands** in
   `RVT.DataAccess/Configuration/RvtStoredRoutineExecutor.cs` (same options-driven value).

**Tests:** new UnitOfWork tests: save spanning two contexts where the second save throws →
assert nothing persisted; ambient-transaction path enlists all contexts. Existing
`EfCoreUnitOfWorkTests` / `TransactionPipelineBehaviorTests` stay green.

**Risk:** low — additive configuration plus tightening of already-intended semantics.

---

## Step 2 — Make the search path asynchronous and non-tracking (C1, Q5, part of T4)

**Goal:** no blocking I/O on grid/search queries; read-only queries stop paying
change-tracking cost.

1. **Change the port**: `RVT.Entities/Ports/Persistence/ISearchQueryReader.cs`
   `ReadFiltered<TSource,TResult>` returns `Task<SearchQueryResult<TResult>>` and accepts a
   `CancellationToken`. Do the same for `ReadFiltered` on `IAlertlevelRepository`,
   `ICompanyRepository`, `IDeploymentRepository`, `IMonitorRepository`.
2. **Rewrite `SearchQueryExecutor.ReadFiltered`** (`RVT.DataAccess/SearchQueryExecutor.cs`):
   - Replace `ToList()` / `PagedList.Core.ToPagedList()` with
     `await query.CountAsync(ct)` + `query.Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct)`.
     This also drops the `PagedList.Core` dependency.
   - Add `.AsNoTracking()` to the base query.
   - Non-paged path: keep the `maximumRecords` bound but fetch `Take(max + 1)` and set a
     `Truncated`/`HasMore` flag on `SearchQueryResult` instead of silently capping (T4-adjacent;
     full T4 fix in Step 5).
3. **Propagate async through the call chain**: `GenericRepository.ReadFiltered`,
   `SearchQueryReader`, the four repo wrappers, and their callers in
   `RvtPortal.Spa/Application` (CompanyService.Search, MonitorService Omnidots reads, etc.).
   Thread `CancellationToken` from the controllers' `HttpContext.RequestAborted` where not
   already present.
4. **Sweep the trivial waste flagged in the review** while touching each file:
   - Drop the redundant `res.Value.ToList()` copies in `AlertLevelRepository`,
     `CompanyRepository`, `DeploymentRepository`, `MonitorRepository`.
   - Drop `.ToList()` on already-materialized `IReadOnlyList` routine results
     (`MonitorRepository`, `OmnidotsBreachesAndAlertsRepository`).
   - Add `.AsNoTracking()` to the remaining read-only repo queries
     (`GenericRepository.ReadAllAsync`, `AlertLevelRepository.ReadAllForMonitorAsync`,
     `DeploymentRepository.ReadCurrentForMonitiorAsync`, `OmnidotsSensorRepository`,
     `SvantekMonitorStatusRepository`) and convert the two battery-DTO builders to
     `.Select()` projections instead of loading full entities.
5. **Delete dead search machinery**: the ignored `includeProperties` parameter and the
   include-all-navigations branch (`include: true` — no production caller) in
   `SearchQueryExecutor`; the never-read `GenericRepository.recordCount` field; the
   port-less `GetByIdAsync(id, includeProperties)` overload once Step 3 lands.

**Tests:** existing grid/search coverage (Company search, monitor lists, time-series reads)
runs against the async path. Add one test asserting the `HasMore` flag on an
over-`maximumRecords` result set.

**Risk:** medium — wide but mechanical signature change; the compiler finds every call site.

---

## Step 3 — Retire the self-committing legacy write path (C4, T1, T2, T3)

**Goal:** one persistence model — commands stage changes, the unit-of-work commits; no
repository ever calls `SaveChanges` on its own.

1. **Fix the non-atomic site creation** (worst instance): delete
   `SiteApplicationService.CreateAsync`'s double-save (`Sites/SiteApplicationService.cs`)
   and route callers to the existing `CreateSiteCommandHandler`, which already does the work
   in one transactional boundary. Repeat for the other `SaveChangesAsync` call sites in
   `SiteApplicationService` and `ReportRuleApplicationService` — either convert each write
   to its command equivalent or, where a command doesn't exist yet, inject `IUnitOfWork`
   and make the service method an explicit transactional boundary.
2. **Strip self-commits from `GenericRepository`**: `AddAsync`, `UpdateAsync`, `DeleteAsync`
   stop calling `SaveChangesAsync`; they only stage. Change `AddAsync`'s `bool` return
   (a meaningless `SaveChanges() > 0` signal) to `Task`/`Task<TEntity>`. Callers commit via
   `IUnitOfWork` (CQRS path already does).
3. **Fix `UpdateAsync` semantics** (T3): replace attach-and-mark-Modified with
   load-then-mutate for entity updates, or `ExecuteUpdateAsync` for set-based ones. This
   removes both the every-column update and the "instance already tracked" throw.
4. **Fix `GetByIdAsync(id, includes)`** (T2): `FirstAsync()` → `FirstOrDefaultAsync()`,
   nullable return; `CompanyRepository.GetByIdWithContractsAsync` handles the null instead
   of `!`.
5. **Un-inherit `OmnidotsBreachesAndAlertsRepository`** (T1): it is a pure stored-routine
   adapter; drop the self-referential `GenericRepository<OmnidotsBreachesAndAlertsRepository>`
   base and take only `IRvtStoredRoutineExecutor` in the constructor.
6. ~~**Align `UploadMonitorPictureCommand`** with the transactional convention: make it an
   `ITransactionalRequest`.~~ **REJECTED — deliberate design choice; see below.**
7. **Guardrail:** add an architecture test that no type in `RVT.DataAccess` calls
   `SaveChanges`/`SaveChangesAsync` (mirror the existing
   `BusinessLogicCore_DoesNotReferenceDataAccessAdapter` reflection style, or a source
   grep test like the existing naming-convention tests).

### Design choice: `UploadMonitorPictureCommand` keeps its own `SaveChanges`

`UploadMonitorPictureCommandHandler` saves *inside* the handler rather than deferring to the
transaction pipeline, and that stays. It is a deliberate exception to the "commands stage,
the pipeline commits" convention, not an oversight:

- The handler writes to **two stores** — blob storage and the database. It uploads the picture
  first, then saves the row, and on save failure it **deletes the uploaded blob** in a `catch`
  before rethrowing. That compensating cleanup is the only thing preventing orphaned blobs, and
  it is covered by `MonitorPictureCommandTests`
  (`Assert.Equal(storage.SavedLink, Assert.Single(storage.DeletedLinks))`).
- Making it `ITransactionalRequest` would move the `SaveChanges` to *after* the handler returns.
  The handler would no longer see the failure, the `catch` would never run, and every failed save
  would leak a blob. The test would fail — correctly.
- It writes to a single `DbContext`, so there is no partial-commit hazard to fix. The convention
  exists to prevent multi-context half-commits; this handler has no such exposure.

The rule to apply when reviewing similar handlers: **a command may own its own `SaveChanges` when
it coordinates a non-transactional external side effect that needs compensation on failure.**
Everything else defers to the pipeline. Revisit only if the picture write ever needs to be atomic
with a domain write — at which point the correct fix is an outbox/compensation record, not moving
the save.

**Tests:** site create/update/archive flows (`ContractSiteOperationsTests`), report-rule
CRUD (`ReportWorkflowTests`), monitor picture upload (`MonitorPictureCommandTests`) all
green; new guardrail test.

**Risk:** medium-high — touches write semantics. Do 1 (sites), then 2–5 (repository), then
6–7, verifying the suite between sub-steps.

---

## Step 4 — Query hotspots: ownership window, dashboard, recipients, site counters (Q1–Q4)

**Goal:** the four measured whole-table / N+1 hotspots become bounded SQL queries.

1. **Make the ownership window translatable** (root cause of Q1/Q3):
   `MonitorOwnershipWindowResolver.ForDeployment(...).Contains(...)` is plain C#, forcing
   materialize-then-filter everywhere. Express the window as an EF-translatable predicate —
   an expression helper like
   `OwnershipWindow.ContainsExpression(DateTime at) : Expression<Func<Deployment,bool>>`
   (date comparisons on `StartDate`/`EndDate` only) — and keep the existing resolver for
   in-memory use so both implementations share one definition (unit-test them against each
   other).
2. **Dashboard fleet load** (`Dashboard/DashboardApplicationService.cs`,
   `BuildMonitorRowsAsync` + callers):
   - Compute visible-site-ids **once per request** and pass them down (currently re-queried
     up to three times).
   - Push role/site filtering into the deployments/monitors query using the Step 4.1
     predicate.
   - Replace the in-memory open-alert/caution counting with a SQL `GroupBy`/`CountAsync`.
   - Combine the duplicate whole-fleet passes in `GetSummaryAsync`/`GetMapMarkersAsync`
     (row build + site options from one query result).
3. **Recipient reader Identity N+1**
   (`ReportRules/ReportRuleRecipientReader.cs`): filter candidate users in SQL by the
   visible/assigned id set before materializing, and batch-load roles via a join on
   `UserRoles`/`Roles` (or reuse the `UserSearch` view's `Role` column, as
   `ReportRuleApplicationService.BuildReportCandidateUsersAsync` already does). Eliminates
   both `GetRolesAsync`-per-user loops.
4. **Site counters and lists** (`Sites/SiteApplicationService.cs`):
   - `PopulateSiteCountersAsync`: one grouped query (site id → monitor count, open
     notification count) instead of ~3 queries per site on the page.
   - `QueryMonitorsAsync` / `QueryOpenNotificationsAsync`: move search text, sort, and
     `Skip/Take` into SQL (copy the `MonitorListReader.QueryAsync` pattern); replace the
     load-both-tables-and-dictionary-join in `BuildOpenNotificationItemsAsync` with a
     query-side join.
5. **`MonitorListReader` projection bloat** (Q4): project the latest active deployment once
   (grouped join / `OUTER APPLY`-shaped subquery) instead of repeating the same correlated
   subquery for each of ~9 columns.

**Constraint to respect:** all three contexts share one scoped `DbConnection`, so do **not**
parallelize queries with `Task.WhenAll` within a request. Sequential-but-fewer queries is
the target.

**Tests:** `DashboardMapCalendarTests`, `ReportWorkflowTests` recipients coverage,
`ContractSiteOperationsTests`, `MonitorWorkflowTests` all green. Add an ownership-window
equivalence test (expression vs. resolver) over boundary cases (open-ended deployments,
gap windows, moved monitors — reuse the moved-monitor scenarios).

**Risk:** medium — behavior-preserving rewrites of measured hotspots; the moved-monitor
regression tests are the safety net for window semantics.

---

## Step 5 — Search-infrastructure correctness and provider hygiene (T4, T5, T6, T7, remainder)

**Goal:** invalid input fails loudly, provider assumptions are explicit, and configuration
footguns are removed.

1. **Reject invalid filter/sort names** (T4): `FilterExpression` and `Ordering`
   (`RVT.Entities/Querying/`) currently drop unknown property names silently — an all-invalid
   filter returns the whole (capped) table. Distinguish "no filter supplied" from "filter
   supplied but unrecognized"; throw/return a validation error for the latter. Cache the
   reflected `MethodInfo` in `OrderedBy` while there.
2. **Surface truncation**: expose the Step 2 `HasMore` flag through the API responses that
   use the non-paged path, so a silently capped result is visible to clients.
3. **Fix DateTime handling in Svantek projections** (T5):
   `SvantekMonitorStatusRepository` — `DateTime.Now` fallback → `DateTime.UtcNow`;
   `DateTime.Parse(...)` → parse with `DateTimeStyles.AssumeUniversal | AdjustToUniversal`.
   Audit other `DateTime.Now` uses in the data path.
4. **Replace naming heuristics with an explicit map** (T6):
   `DatabaseNamingRules` `Replace("nr","row_count")` + trailing-`s` stripping produced the
   semantically wrong `fleet_row_count` alias already baked into routine readers. Because the
   PostgreSQL schema already contains the mangled names, do this in two moves:
   a. freeze the current entity→relation/column output into an explicit dictionary
      (generate it once by running the existing rules over the model);
   b. delete the heuristic code and add a startup/CI model-validation check that every mapped
      relation and column exists in the target database (catch drift at boot, not first query).
   Renaming the mangled DB objects themselves (e.g. `fleet_row_count` → `fleet_nr`) is a
   separate, optional migration follow-up.
5. **Context construction hygiene** (T7): remove the public parameterless context
   constructors and the `Environment.CurrentDirectory` `OnConfiguring` fallback from
   `RVTDbContext`/`RVTSearchContext`; keep CWD-based config only inside
   `RVTDbContextDesignTimeFactory`. Make the design-time factory env-var-only
   (`RVT_EF_CONNECTION`) — no hardcoded `postgres/postgres` fallback.
   Unify `InitDataAccess`'s `TryAddScoped` context registration with the host's
   `AddDbContext` path so tests and production construct contexts the same way.
6. **Model configuration**: add explicit `ToView(...)` for the keyless entities that rely on
   inferred names (`MyAtmDustLevel`, `OmnidotsPeakLevel`, `OmnidotsTrace`); audit `decimal`
   properties and add `HasPrecision` where the provider default (18,2) is wrong; set a global
   `QuerySplittingBehavior.SplitQuery` default if any multi-collection `Include` remains
   after Step 2's dead-code removal.

**Tests:** new negative tests: unknown sort field → 400 problem-details (the invalid-sort
test in `SharedInfrastructureTests` already covers the API edge — extend to filters);
truncation flag surfaced; model-validation check runs in CI (extend
`DatabaseBackendMirrorTests`/`DatabaseNamingConventionTests`).

**Risk:** step 5.4 needs care — the frozen name map must be byte-identical to current
output before the heuristics are deleted (snapshot test first, then swap).

---

## Deliberately out of scope

- **`UploadMonitorPictureCommand` staying self-committing** — an accepted design choice, not debt.
  See the rationale under Step 3 (blob-storage compensation on save failure).

- **Context pooling / intra-request query parallelism** — structurally blocked by the shared
  scoped `DbConnection` that enables cross-context transactions. Revisit only if the
  single-transaction requirement is dropped (e.g. outbox pattern for the search context).
- **Renaming mangled PostgreSQL objects** (`fleet_row_count` etc.) — optional migration after
  Step 5.4's explicit map exists.
- **`AsNoTrackingWithIdentityResolution` tuning** — only relevant if an include-heavy read
  path reappears.

## Progress tracking

| Step | PR | Status |
|------|----|--------|
| 1. Resiliency + UoW hardening | #10 | merged |
| 2. Async search path | #12 | merged |
| 3. Retire legacy write path | #13 | in review |
| 4a. Ownership predicate, dashboard, N+1s | #15 | merged |
| 4b. Site-list SQL paging + MonitorListReader subquery | #16 | merged |
| 5a. Query validation + provider hygiene | #17 | in review |
| 5b. Naming-map swap + context-construction cleanup | #18 | in review |

### Follow-ups — done (PR #22)

- **`HasMore` is surfaced.** The grid and graph responses carry a `truncated` flag, and the CSV export carries an
  `X-RVT-Truncated` header (a CSV body has nowhere to put a flag). A read that stopped at its row bound no longer
  looks complete.
- **Boot-time schema validation.** `RvtSchemaValidator` compares every relation and column the EF models map
  against `information_schema`, and `SchemaValidationHostedService` fails startup when the database is missing
  something the model expects. It fails on *drift only*: if the schema cannot be read at all (outage, bad
  connection string) it logs a warning and starts, because refusing to boot over a transient outage is worse
  than the problem it solves. Disable with `Database:ValidateSchemaOnStartup=false`.
- **Keyless table names pinned.** `MyAtmDustLevel`, `OmnidotsPeakLevel` and `OmnidotsTrace` now declare
  `ToTable(...)` explicitly instead of relying on DbSet-name inference plus the naming rules.
- **Decimal precision: nothing to do.** The review flagged this generically, but the model contains exactly one
  `decimal` (`SvantekMonitorStatus.Meterfirmware`) and it already declares `numeric(4,2)`. No other decimal
  columns exist, so there is no provider-default truncation risk to fix.

### Remaining (optional)

- Renaming the *remaining* deliberate exception (`NrUsers` -> `user_count`) is not planned; unlike the mangled
  names it was an intentional choice.
