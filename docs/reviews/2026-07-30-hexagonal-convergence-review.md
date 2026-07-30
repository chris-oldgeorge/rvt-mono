# RVT Monorepo — Hexagonal Convergence, Legacy & Code-Quality Review

Date: 2026-07-30 (third full review, at main `ebc48f7f`)
Focus, as commissioned: code better moved/renamed to different projects so the
repo converges on hexagonal principles, unused legacy code, and
problematic/bad code blocks.
Method: five parallel subsystem reviews (monitors, portal backend, shared
libs + reporting, frontend, cross-cutting topology), each briefed with the
2026-07-28 and 2026-07-29 reviews and their resolution notes so only new or
still-open findings are reported. Every dead-code claim was grep-verified
repo-wide (per-monitor scoped where names collide); MediatR dispatch sites,
client API callers, and DI registrations were verified individually.

---

## Executive summary

The topology has genuinely converged: the project-reference graph is acyclic
and direction-correct, the legacy sync messaging path is verifiably gone, and
last review's deletions stayed dead. What remains splits into four themes:

1. **One live P1 behavior gap**: AirQ and Svantek never dispatch the durable
   alert outbox when the Quartz scheduler is enabled — alerts commit and are
   silently never delivered (§3 B1). A second P1: AirQ's rule-window
   arithmetic evaluates incomplete windows and re-arms latched rules (§3 B2).
2. **Placement debt, not defect debt**: `RVT.BusinessLogic` is a misnamed
   Spa-only ports bag duplicating the extracted core's paging/result types;
   `Rvt.Monitor.Common/Delivery` is MyAtm's private outbox living in the
   shared kernel (and maps its table into every monitor's EF model).
3. **A thin, deletable legacy crust**: ~40 dead sync-alerting DB members
   across the four monitors (the durable migration's residue), seven
   client-dead portal endpoints, dead message-rendering chains in Common.
4. **Guards are honest but asymmetric**: monitor-to-monitor references,
   three of four monitors' internal layering, and portal Adapters→Api are
   convention-held; the portal's real-database tests silently skip on every
   PR because of an env-var name mismatch.

Sibling asymmetry is the recurring defect pattern: Svantek got the correct
window math but the fragile failure collector; AirQ got the resilient catch
shape but broken window math; battery handlers latch before signaling while
offline handlers do it right. Convergence fixes are mostly "copy the correct
sibling".

---

## 1. Hexagonal placement — move / rename / dissolve

| # | Finding | Where | Should live | Cost |
|---|---------|-------|-------------|------|
| M1 (P2) | `RVT.BusinessLogic` has zero external consumers — it is RvtPortal.Spa's driven-ports bag (Ports/{Notifications,Storage,Vendors}, report models, paging, `AccountMessenger`, `IRvtDateTimeProvider`, vendored AForge) under a legacy layer name. It duplicates the extracted core: `PageRequest`/`PagedResult` exist in both `RVT.BusinessLogic/Application/Paging/` and `RvtPortal.Application/Common/` — `SitesController.cs` uses both in one file (51 vs 226) — and `ApplicationResult<T>` parallels `UseCaseResult`. | apps/portal/RVT.BusinessLogic (17 files) | Merge into `RvtPortal.Application` (one paging/result vocabulary), delete the project. Removes one of three legacy project edges. | M |
| M2 (P2) | `Rvt.Monitor.Common/Delivery/*` is MyAtm-only: every non-test consumer is under apps/monitors/myatmmonitor (10 files). Side effect: `MonitorDbContextBase.cs:19` + `MonitorModelBuilderExtensions.cs:136` map `monitor_delivery_outbox` into every monitor's EF model, but AirQ/Svantek/Omnidots schemas don't create the table — four contexts advertise a DbSet that fails if touched. Only `DeliveryDispatchPolicy` + `DeliveryRetrySchedule` are genuinely shared (used by the alerts dispatcher). | libs Rvt.Monitor.Common/Delivery | Move the ~10 MyAtm outbox files + the entity mapping into MyAtm (`OnMonitorModelCreating`); keep the two policy files in Common. | M |
| M3 (P3) | `RuleNotificationRequest` is declared in `Rules/NoiseRuleEvaluator.cs:174` but the evaluator never uses it; consumers are `RuleAlertDeliveryPlanner` and MyAtm — it is the MyAtm outbox planning contract. | Rules/NoiseRuleEvaluator.cs | Beside the planner (or into MyAtm with M2). | S |
| M4 (P3) | `Rules/RvtContactDtoCompatibility.cs` holds the **canonical** contact DTO since step 5, yet is still named *Compatibility*. | libs Rules/ | Rename to `RvtContactDto.cs`. | S |
| M5 (P3) | AForge vendored math (`Complex.cs` 1,115 lines, FourierTransform, Tools) is consumed solely by `MonitorData.cs`'s FFT (461–464). | RVT.BusinessLogic/AForge | Vendored subfolder next to MonitorData (or `RVT.Vendored.AForge`), trimmed to the used members. | S |
| M6 (P3) | The legacy chain is now three single-method hops: `RVT.Entities/Ports/Persistence/IMonitorRepository` + `IDeploymentRepository` each expose one `GetByIdAsync(Guid)` (no CT), implemented in RVT.DataAccess, consumed only by Spa's `MonitorService`. | RVT.Entities/RVT.DataAccess | Fold into Spa readers; keep `ISearchQueryReader` (earns its keep). | S–M |
| M7 (P3) | `JAN1_1970` triplicated on the AirQ/Svantek/Omnidots api facades and consumed by model/db layers (model→api inversion; MyAtm already migrated). Same inversion: `BatteryAlertType` enums on the Omnidots/Svantek facades. | monitor facades | Retarget to `DateTimeUtil.JAN1_1970`; move the enums to model. | S |
| M8 (P3) | Five files break even the *pinned* monitor namespaces: `AirQMonitor.model.dto` (SiteMonitorsWithSiteHoursDto), `SvantekMonitor.model.dto` (4 files) vs the projects' `AirQ.Model`/`Svantek.Model` scheme. Omnidots tests carry two roots (36× `OmnidotsAdapterTests`, 1× `OmnidotsMonitorTests`). Svantek `model/json/File.cs:28-35` also derives a timestamp inside a JSON DTO (filename slicing + local-time fallback — defect half in §3 B12). | monitor model layers | Rename to majority scheme; move the derivation beside `ValidateFileRow`. | S |
| M9 (P3) | Frontend structure: six multi-screen "Panels" monoliths + a 1,569-line App.tsx (and 3,706-line single-describe App.test.tsx). Top named extractions: ContractSitePanels (1,879 ln) → `SiteFormPanel` 1043–1404, `SiteDetailPanel` 770–1042, split Contracts (139–603) from Sites (604–1648); App.tsx → `PrivacyPage` 459–708, auth pages 731–1058 → `auth/`, `PortalShell` 1059–1363; MonitorPanels → detail/removal/assignment panels; NotificationAlertPanels → Notifications 68–530 vs Alert Levels 531–1058; AdminPanels → Companies 89–527 vs Users 528–1027. Also: `DashboardRoutePanels.tsx` contains Map+Calendar panels (misnamed); "SPA migration" placeholder text ships in the sidebar brand (App.tsx:1142). | RvtPortal.Client/src | Split at the named seams (tests import public exports — mostly import-path-only). | M |
| M10 (P3) | Hoistable test residue: `TestUtil.ReadTextFromFile` ×4 byte-similar, the `IAlertIngressPort` accept-mock factory ×3, `UseTestMonitorContextFactory` ×2; portal tests re-implement `FindRepositoryRoot()` (CutoverReadinessTests, DatabaseBackendMirrorTests). | monitor/portal test projects | One shared helper file in `Rvt.Monitor.IntegrationTesting`; fold portal copies opportunistically. | S |
| M11 (decision) | **EF/persistence split of Rvt.Monitor.Common: rejected — record it.** The graph is clean (Common references only Abstractions); EF/Quartz/MQTTnet are internally confined (Data/ + Alerts/Persistence; Scheduling/+Hosting/+background service; Mqtt/ single file); all six consumers are hosts needing every layer. A physical split multiplies refs ×3 across six csproj for no consumer gain. The real risk is internal erosion — guard it instead (§4 G7). | — | Record decision; add the confinement guard. | — |

Solution/config hygiene (P3): `apps/monitors/rvt-monitors.sln` half-includes
the four `Rvt.Storage.*` projects but not Common/Communication (arbitrary;
strip or guard per-area slns); Moq drifts 4.20.72 (libs) vs 4.20.69
(monitors) with no cross-CPM alignment check — the recorded NU1109 incident
was exactly this failure mode; the portal `.editorconfig` is a 552-line
Roslyn-repo copy that sets `IDE0005 = none` (silencing a ratchet-tracked rule)
while the monitors' 17-line file mostly re-declares root values; `apps/portal/
RVT.Utilities/` is an untracked empty husk (bin/obj only) — delete.

---

## 2. Unused legacy code (all grep-verified)

| # | Finding | Action |
|---|---------|--------|
| L1 (P2) | **The sync-alerting DB surface survived the durable migration in all four monitors (~40 members + 2 DTOs + their tests).** Dead in production: `WriteNotification` + `WriteNotificationAudit` (all four DBClients + `I*OperationalCommands`); `HasOpenNotification` (AirQ/MyAtm/Svantek); `ReadAlertContacts(…, out Guid)` (AirQ/Svantek); `ReadSiteInfo` + the whole `SiteInfoDto` incl. `ShouldReportForDate` (AirQ/Svantek); Omnidots `ReadNotifications`, `GetAveragePeakLevels`, `UpdateAlertRule`; MyAtm `UpdateAlertRule`. Pinned only by TestDbClient sections testing the dead members — deleting the cluster also shrinks the four TestDbClients the standing ruling deferred. **Do NOT delete** MyAtm `ClaimNextDueAsync`/`CompleteAsync`/`RetryAsync`/`DeadLetterAsync` — live via `IMonitorDeliveryOutboxQueries/Commands` from `MonitorDeliveryDispatcher`. | Delete members + interface declarations + dead-only tests |
| L2 (P2) | **Seven portal API endpoints have no client caller** (grepped by path fragment + DTO/function name across all of RvtPortal.Client/src; only server tests exercise them): `GET api/monitors/options` (MonitorsController:101), `GET api/monitors/deployments/{id}` (:151), `PUT api/monitors/{id}/fleet-number` (:206, drags `SetMonitorFleetNumberCommand`), `GET api/monitors/{id}/removal-impact` (:278), `GET api/reports/{id}` (ReportsController:48 — client uses `reportLink` blob URLs), `GET api/sites/{id}/monitors` + `GET api/sites/{id}/notifications/open` (SitesController:220,231 — client renders from the embedded `SiteDetailResponse` lists with client-side paging; these two look intended-but-unadopted → **adopt-or-delete ruling**). | Delete (or adopt the two paged ones) |
| L3 (P2) | `NotificationDto.GetMessage()` dead chain: `RvtNotificationDto.cs:49-75`, `ApiMessage` (:26, never read/written), `Policy` (:47) — zero callers (composition moved to `NotificationMessageComposer`). Drags `MonitorNotificationStyle` + `MonitorRulePolicy.NotificationStyle`, `DateTimeUtil.FormatString`, and the third (untrimmed) copy of the notification-URL builder. | Delete the chain (~4 test files touch it) |
| L4 (P2) | `IMonitorEventPublisher.PublishAlert` (sync, `MonitorEventPublisher.cs:12,49-52`) — doc says "retained only for the legacy synchronous rule evaluator", which step 4 deleted. Zero callers. Deleting it fixes the sync-over-async default-interface hazard too. | Delete; make `PublishAlertAsync` the abstract member |
| L5 (P3) | Async-migration sync twins with zero production callers: Svantek DBClient ×13 (`ReadLatestNotification`, `WriteSoundFile` + interface, `SetMonitorBatteryStatus`, `SetMonitorOffline`, `WriteMonitorList`, `ReadSiteMonitorsWithSiteHours`, `Create8hourAverage`, `WriteDailyAverage`, `InsertNoiseDtos` ×2, `InsertNoiseRecordsTable`, `WriteLatestTimestamp`, `UpdateMonitorStatus`, `ClearErrorMessages`); Omnidots ×5 (`InsertPeakRecords`, `InsertPeakRecordsTable`, `InsertVeffRecords`, `InsertVdvRecords`, `WriteLatestTimestamp`); MyAtm ×4 (`InsertDustDtos`, `InsertAccessoryDto`, `WriteFleetNr`, `WriteLatestTimestamp`). | Delete |
| L6 (P3) | `OmnidotsQueryProcessor` production-dead (only caller: `TestInputProcessor.cs`); Omnidots `ReadMonitorList(DateTime?)` parameter is a decoy — never used, but `CheckForOfflineMonitorsHandler.cs:53` passes `offlineDateTime` believing it filters. | Delete both; remove or implement the parameter |
| L7 (P3) | Test-only compat shims now deletable: `Rules.NotificationDto` (`NotificationDtoCompatibility.cs`) and `Notifications.AlertActivityTimeDto` (empty derived class) — production uses the base types everywhere; consumers are 3 test usings + fixtures. `MonitorDeliveryProducers.Svantek` — Svantek stopped producing deliveries at step 4; tests only. `MonitorDbReaderExtensions.GetTimeSpan` — zero call sites. `MonitorDbParameterExtensions.AddWithValue` — 396 call sites, **all tests**; move into the testing project. `RvtMqttClient()` parameterless ctor — zero callers. `MonitorRuntimeDefaultsResolver` — still adopt-or-delete (self + RvtConfigTests only). | Retarget usings, delete/move |
| L8 (P3) | Portal: `RVT.Entities/NotificationsSent.cs` dead entity (no DbSet, absent from migrations; CutoverReadinessTests pins it as *retired*); `RvtPortal.Application/Sites/ActiveSiteAssignment.cs` (`SiteAssignmentWindow`/`IsActive`) has zero production consumers while the rule lives twice more in production (Spa EF expression ×7 sites + inline copy in `EfSiteReadAdapter.cs:306`) — the "tested therefore alive" trap; `DateExtensions.DisplayUtcAsLocal` ×2 + `IRvtDateTimeProvider.DisplayUtcAsLocal` production-dead (only a reflective name pin); `SearchQueryResult.WasSuccessful/ErrorMessage` can never fire and have zero readers (`IOperationResult` has no consumer as abstraction); four unused PackageReferences in `RVT.DataAccess.csproj:12-15` (AspNetCore Identity EF + three Configuration packages). | Delete each |
| L9 (P3) | Frontend: `src/operations/dataViewDateTime.ts` entirely dead (19 lines; created by 2be4aac1, never wired; contains a drift-prone `formatDateTime` copy); `@vitejs/plugin-react` in `dependencies` instead of dev; `downloadFile`'s export is test-only. | Delete / move |
| L10 (P3) | Repo: `.worktrees/full-monorepo-client-release` sits on a branch **merged into main** (PR #43); sibling local branch (v1) abandoned-unmerged. Pollutes repo-wide searches. | `git worktree remove` + delete both branches |

---

## 3. Problematic code blocks (concrete failure scenarios)

| # | Finding | Failure scenario | Action |
|---|---------|------------------|--------|
| B1 (P1) | **AirQ and Svantek never dispatch or clean the durable alert outbox in Quartz-scheduler mode.** `DurableAlertBackgroundService.cs:44-45` disables the poller when the scheduler is enabled, expecting a catalog job; Omnidots and MyAtm have one, **AirQ (`AirQMonitorJobs.cs:14-25`) and Svantek (`SvantekMonitorJobs.cs:13-26`) have neither the job nor a schedule entry** — and both appsettings ship complete cron schedules for exactly this mode. | Run either monitor with `MonitorScheduler:Enabled=true`: breaches commit occurrence/notification/outbox rows; emails/SMS/MQTT are never sent; nothing errors; the outbox grows unboundedly. | Add `DispatchAlerts`/`CleanupAlerts` to both catalogs + appsettings, or keep the background service on when the catalog lacks a dispatch job |
| B2 (P1) | **AirQ hour/day rule loops evaluate incomplete trailing windows; empty windows score 0.0.** `AirQRuleProcessor.cs:100-116,124-139` lack Svantek's full-window guard (`SvantekRuleProcessor.cs:128`); `GetAverageNoiseLevel` returns `?? 0.0` on empty windows; `NoiseRuleEvaluator` treats `level <= LimitOff` as recovery. AirQ also stamps every window's alert with `alertTime = end` (run-dependent `SourceEventKey`, misattributed breach time); Svantek correctly uses window end. Bonus: a new monitor's 1-year seed drives ~8,760 hour-loop iterations of synchronous DB averages in one run. | A quiet/partial window deactivates an active rule → it re-fires next full evaluation (duplicate alert); partial-data spikes false-fire. | Adopt Svantek's window guard + window-end alert time |
| B3 (P1) | **Portal PostgreSQL integration tests silently skip on every PR.** `tests.yml:62-64` sets `RVT__POSTGRES_INTEGRATION_CONNECTION`; portal's `RequiresPostgresFactAttribute` reads `RVT_TEST_POSTGRES_CONNECTION` and skips when unset. Only manual sonarqube.yml sets both. | The portal Postgres provider tests have never run on a PR. | Add the second env var to tests.yml; consider fail-instead-of-skip in CI |
| B4 (P2) | **Battery handlers latch the dedup gate before the durable signal is accepted** (`SvantekMonitor/../NotifyBatteryLevelsHandler.cs:114-128`, `OmnidotsMonitor/../NotifyBatteryLevelsHandler.cs:129-137`); both monitors' *offline* handlers do signal-then-latch correctly. | A transient `AcceptAsync` failure permanently loses the battery alert (gate suppresses every retry until the battery recovers). | Signal first, latch on success; add Omnidots' missing per-monitor failure isolation |
| B5 (P2) | **`SvantekFailureCollector.Capture` rethrows any `OperationCanceledException`** without consulting a token (`SvantekFailureCollector.cs:18-21`); `HttpClient.Timeout` surfaces as `TaskCanceledException`. MyAtm's collector checks the token and guards the `HandleException` DB write; Svantek's and AirQ's inline pattern do not. | One 30 s vendor timeout aborts the whole fleet run instead of being recorded. | Align Svantek/AirQ on MyAtm's semantics |
| B6 (P2) | **AirQ `NotifySiteAveragesHandler` crashes the run on a site without hours** (`:42-43` `StartTime!.Value` no guard, no per-monitor try/catch; Svantek twin has both). | One deployment on a site with unset hours aborts daily averages + site-hours alerting for every site. | Port guard + collector from Svantek |
| B7 (P2) | **Svantek stores malformed vendor readings as 0.0 dB** (`StoreNoiseLevelsHandler.cs:260-263`; columns are nullable); `:181` parses vendor timestamps with bare `DateTime.Parse` (no culture/kind). | Zeros drag averages down, can reset a latched rule below `LimitOff` → duplicate re-fire. | Map unparseable to DBNull; parse invariant |
| B8 (P2) | **Unknown monitor serial poisons alert ingestion**: `EfAlertCommitStore.cs:102-104` `Monitors.SingleAsync(...)` → unclassified `InvalidOperationException`; on the Omnidots webhook that is an opaque 500 on every vendor retry. Also `SingleAsync` on a non-unique index (SerialId, TypeOfMonitor). | A newly installed device webhooks before `StoreMonitors` imports it → undiagnosable retry storm. | Distinct unknown-serial outcome/exception; `SingleOrDefaultAsync` |
| B9 (P2) | **Portal: cancellation severed at the heaviest read path.** `IMonitorDataSource.GetDeploymentDataAsync` and static `MonitorData.GetDeploymentData` take no CancellationToken; `DataApplicationService` uses the request token only for the visibility pre-check (`:256,294,340,427`). Also tokenless: the two legacy repos' `GetByIdAsync`, `GetVibrationMonitorStatusAsync`, `TracesIndexReadOne`. | Abandoned grid/graph/CSV requests run TimescaleDB paging + FFT to completion holding pooled connections. | Thread a CT through `DeploymentDataQuery` → service calls |
| B10 (P2) | **Unattached-monitors N+1**: `MonitorAdministrationReadService.cs:389-393` calls `MonitorRemovalImpactReader.BuildAsync` per row — 3 COUNTs + a ~14-hypertable impact-view query, ×page size, serially. | One admin page load issues dozens of hypertable count scans. | Batch the COUNTs per page; `IN`-query the view |
| B11 (P2) | **Frontend still-open P0 pair**: `format.ts:11-24` `formatDate/formatDateTime` throw `RangeError` on malformed input inside render (panel taken down by the error boundary; the un-consolidated local copies DO have the guard); user delete is one un-confirmed click (`AdminPanels.tsx:669-671,795-800`; also `removeUserFromSite`, `removeReportRuleUser`) — three confirmation tiers coexist while `ConfirmDialog` already exists. New: PortalShell refetches `/api/health`+`/api/profile` on every route change (`App.tsx:1116-1119` effect chain depends on `route`; unaborted, last-write-wins staleness). | Renders crash on bad data; destructive one-click deletes; redundant requests + stale profile state per navigation. | Guard + test; adopt ConfirmDialog; split the route-dependent error routing out of the fetch effect |
| B12 (P3) | Compact list, still-open or small: contact send-windows evaluated in UTC while rule activity windows are local (`EfAlertCommitStore.ShouldSendAtEventTime`, `RvtContactDto.ShouldSendAtTime` vs `AlertActivityTimeDto` — BST shifts quiet hours; **needs the timezone product ruling**); outbox-row error re-truncated to 256 while its audit keeps 1024 (`EfAlertOutboxStore.cs:21,99-101`); deleted *global* offline rules keep alerting (`ReadRules(null)` skips `IsDeleted` in three monitors; **needs ruling**); Omnidots `ReadMonitorList` indexer throws fleet-wide on one missing status row (`DBClient.cs:129-131`, no TryGetValue); Svantek `ProjectFile.triggerDate` slices `filename[..17]` unguarded with a local-`DateTime.Now` fallback; `HandleException` ×4 logs `exception.Message` only (no stack/type at either sink) and the startup catch writes to the DB from a DB-failure path; Svantek has no `ClearOlderErrorMessages` job (table grows unboundedly; its sync `ClearErrorMessages` is dead); portal `DateTime.Today` seeds business-day windows ×5 (survived three reviews — **needs the one-time ruling**); `MonitorListReader` reads `DateTime.UtcNow` while six siblings inject `TimeProvider`; `DataController` still hand-rolls invalid-sort ProblemDetails (prior *P0*, still unshipped); `CompanyService.ReadOneAsync` returns `(…)!` with all three callers null-checking anyway; InMemory-provider branching ×7 sites (needs the provider ruling); the timing-based negative assertion (`Task.Delay(250)`) persists; frontend: Sites-list sort-direction one-liner (`ContractSitePanels.tsx:614` — default passed as value), stale `schema.d.ts` (6,367 lines, zero Help entries, nothing regenerates it), dashboard summary refetched per deployment switch, email address in `/forgot-password?email=` URL (PII in history/logs), installer status fetch unaborted + failure-as-no-status. | | Each is one-file scale |

Step-6 residue in the two-dispatcher split (P3, compact): the
`AlertType→NotificationMessageKind` switch is verbatim ×2; audit/envelope
record twins; claim logic in two mechanisms and status vocabularies
(`'Leased'` raw SQL vs `"InProgress"` EF loop); three identity-hash schemes;
delivery-kind string constants declared twice inside Alerts itself
(`EfAlertCommitStore.cs:26-28` vs `IAlertDeliveryAdapter.cs:17-19`, trivially
unifiable). The durable-alerts doc's claim that claim/lease fencing is
"single-sourced" overstates this — correct the doc or unify the pieces.

---

## 4. Guard coverage gaps

| # | Gap | Cheapest guard |
|---|-----|----------------|
| G1 (P1) | Portal Postgres tests skip on PRs (see B3). | One env line in tests.yml |
| G2 (P2) | Monitor-to-monitor ProjectReferences structurally unguarded (the reference-matrix guard filters to RVT lib targets before comparing). | One assertion in `CommonPackageBoundaryTests`: no ProjectReference from `apps/monitors/X/**` targets `apps/monitors/Y/**` |
| G3 (P2) | Guard depth asymmetric: AirQ has five guards; Svantek exactly one narrow one; Omnidots/MyAtm guard alerting but not EF confinement or sync-blocking. The July port extraction is convention-held in three of four monitors. | Generalize AirQ's suite into `Rvt.Monitor.IntegrationTesting` as a parameterized contract (the `CommunicationsCompositionContract` pattern); instantiate per monitor |
| G4 (P2) | Monitors' model/→api/ layering unguarded, with 4 live violations: MyAtm `model/dto/{MyAtmDustImportCommit,MyAtmAlertCommit,DustMonitorDto,MyAtmRuleEvaluation}.cs` import `MyAtm.Api`. | Fix/relocate the four, add a model-must-not-import-api scan to G3's contract |
| G5 (P2) | Portal Adapters→Api unguarded (prior P25): the two known offenders persist (`Adapters/Reporting/ReportGeneration{Client,Gateway}.cs:9`). | Two-file `Adapters/` baseline in the existing guard |
| G6 (P3) | No guard against new `RvtConfig` static consumers (19 production files read it today). | Allowlist file-scan in CommonTests |
| G7 (P3) | Rvt.Monitor.Common internal technology confinement unguarded (the M11 decision's enforcement): EF/Npgsql only under Data/ + Alerts/Persistence; Quartz under Scheduling/+Hosting/+background service; MQTTnet under Mqtt/. | Same file-scan shape as `CommunicationsBoundaryTests` |
| G8 (P3) | CI: two workflows duplicate contract-test steps and the 4-step setup preamble; no `paths` filters (docs-only PRs run everything); workflows are `pull_request`-only (direct pushes to main bypass all gates); Dockerfiles float `sdk:10.0` while global.json pins 10.0.302. Compose: `RvtConfig.cs:100` claims every deployed monitor declares `RVT__MONITOR_KIND`, but the reportingmonitor compose service doesn't (harmless today; fix the remark or the compose). | Composite action; paths filters with required-check-safe no-ops; doc fix |

---

## Consolidated priority list

**P1 — fix now**
1. AirQ + Svantek durable-outbox dispatch/cleanup jobs missing in scheduler mode (B1).
   **Done 2026-07-30: both services expose `DispatchAlertsAsync`/`CleanupAlertsAsync`
   (dispatcher + cleanup injected), the catalogs and appsettings declare them at
   Omnidots' cadence, and the Svantek job-contract tests pin the names.**
2. AirQ partial-window rule evaluation re-arming latched rules + alert-time misattribution (B2).
   **Done 2026-07-30: the hour/day loops evaluate only complete windows
   (Svantek's guard) and stamp alerts with the window end; the 15-minute
   branch stamps the sample time. Idempotency keys are now deterministic.**
3. Portal Postgres PR-gate env var (B3/G1).
   **Done 2026-07-30, per ruling: `RVT_TEST_POSTGRES_CONNECTION` is replaced
   everywhere by `RVT__POSTGRES_INTEGRATION_CONNECTION`; tests.yml prepares
   the portal schema on the integration database (extensions + three EF
   chains + SQL deploy, mirroring the SonarQube workflow — monitor fixtures
   isolate in throwaway schemas, so `public` is free); sonarqube.yml drops the
   duplicate variable. The portal suite went from 548 passed / 11 skipped to
   564 passed / 0 skipped.**

**P2 — next slices**
4. Battery latch-before-signal in Svantek + Omnidots (B4); Svantek failure-collector cancellation semantics + AirQ site-averages guard (B5, B6); Svantek 0.0 dB parsing (B7).
   **Done 2026-07-30, PR #47: both battery handlers signal the durable ingress
   before latching (Omnidots gains per-monitor failure isolation); the Svantek
   collector and AirQ's inline catch adopt MyAtm's cancellation semantics; AirQ
   site averages skip sites without hours; malformed Svantek readings persist
   as DBNull and parse invariant.**
5. Unknown-serial ingestion outcome (B8).
   **Done 2026-07-30, PR #47: `EfAlertCommitStore` uses `SingleOrDefaultAsync`
   and maps a missing serial to a distinct `AlertUnknownMonitorException`.**
6. Dead sync-alerting DB surface ×~40 across monitors (L1) — also shrinks the four TestDbClients.
   **Done 2026-07-30, PR #48: the members, interface declarations, both DTOs,
   and their dead-only tests are deleted across the four monitors; the live
   MyAtm outbox claim/complete/retry/dead-letter surface was kept as specified.**
7. Dissolve RVT.BusinessLogic into RvtPortal.Application; unify paging/result types (M1); move MyAtm's Delivery/* out of Common + fix the outbox mapping leak (M2, M3).
   **Done 2026-07-30: M1 in PR #50 (ports/paging/time merged into
   RvtPortal.Application, one paging vocabulary, project deleted); M2 + M3 in
   PR #51 (the ten MyAtm outbox files and `RuleNotificationRequest` moved into
   MyAtm api/Delivery with namespaces preserved, `monitor_delivery_outbox`
   mapped only in `MyAtmMonitorContext`; the two shared policy files stay in
   Common).**
8. Seven client-dead portal endpoints — adopt-or-delete (L2); dead message chain + sync PublishAlert in Common (L3, L4).
   **Partially done 2026-07-30: the five unambiguous endpoints are deleted in
   PR #50; the two paged `sites/{id}` endpoints still await the adopt-or-delete
   ruling and remain open. L3 + L4 landed in PR #48 (GetMessage chain deleted,
   `PublishAlertAsync` is the abstract member).**
9. Portal cancellation threading (B9) + unattached N+1 (B10).
   **Done 2026-07-30, PR #50: the deployment-data read path takes a
   CancellationToken end to end, and the unattached-monitors page batches its
   removal-impact reads.**
10. Frontend still-open P0 pair + PortalShell refetch (B11).
    **Done 2026-07-30, PR #49: formatters return malformed input instead of
    throwing in render, `ConfirmDialog` is the one confirmation idiom, the
    shell health/profile fetch runs once per mount with an AbortSignal, and
    the frontend B12 one-liners (sort default, dashboard refetch,
    forgot-password email out of the query string) rode along.**
11. Guard pack: G2 + G3 + G4 + G5 (one afternoon, converts convention to enforcement).
    **Done 2026-07-30, PR #52: G2 — `CommonPackageBoundaryTests` rejects
    any monitor→monitor ProjectReference. G3 — AirQ's five guards generalized
    into `MonitorDependencyBoundaryContract` in `Rvt.Monitor.IntegrationTesting`
    and instantiated by all four vendor monitors (per-monitor allowlists pin
    today's known exceptions). G4 — `Period` moved from MyAtm api/ to model/
    (`MyAtm.Model`), clearing the four dto imports, and the contract's
    model-must-not-import-api scan freezes the remaining M7 baseline (one file
    each in Svantek/Omnidots). G5 — the portal `Adapters/` Api-import surface is
    pinned to the two known reporting files in
    `ApplicationBoundaryArchitectureTests`.**

**P3 — batched cleanups**
12. L5–L10 deletions; M4–M10 renames/moves/splits; B12 one-liners; G6–G8; solution/config hygiene.

**Product rulings needed**: timezone policy (quiet-hours UTC vs local — B12; supersedes the earlier per-site items); deleted global offline rules (B12); adopt-or-delete for the two paged site endpoints (L2); InMemory-provider branching (B12); the M11 no-split decision (recorded above, needs sign-off).
