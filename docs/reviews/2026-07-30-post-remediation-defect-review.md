# Post-remediation defect review — 2026-07-30

Fourth full review of the monorepo, run on `main` at `a3562559` — immediately
after the third review (`2026-07-30-hexagonal-convergence-review.md`) was closed
out in full, including its P1/P2/P3 tiers, its six product rulings, and the
`SpaTestApplicationFactory` replatform follow-up (PRs #46–#65).

**How this review differs from the previous three.** The first three reviews were
largely *structural* — duplication, legacy retirement, hexagonal convergence, dead
code. Those seams are now closed, so this pass was aimed squarely at **defects**:
five reviewers each traced live execution paths end-to-end in their own territory
and were told to report only what they could tie to a concrete failure scenario.
Each reviewer read the prior reviews first and was forbidden from re-reporting a
resolved finding.

Two rules were imposed after the earlier passes shipped miscounts that cost real
remediation time ("three monitors" that was four; "7 InMemory sites" that were 11):
every claim carries an exact `file:line` and an exact count, and every finding is
labelled **CONFIRMED** (a stated failure scenario with inputs) or **SUSPECTED**.

| Territory | Findings | P1 | P2 | P3 |
|---|---|---|---|---|
| Shared kernel + communications | 14 | 0 | 6 | 8 |
| Monitor applications | 18 | 3 | 9 | 6 |
| Portal backend | 17 | 1 | 10 | 6 |
| Frontend | 17 | 1 | 7 | 9 |
| Cross-cutting + operations | 10 + batch | 3 | 7 | batch |

Only **two** findings are SUSPECTED (M-8, PB-17); everything else is confirmed.

---

## §1 — P1: fix first

Eight findings. Three cause silent data loss, two are user-visible breakage on
every use, two let broken code reach `main`, and one is a cross-subsystem schema
conflict that breaks ingestion against a migrated database.

### P1-1 — A single future-dated vendor sample blinds an AirQ monitor permanently
`apps/monitors/airqmonitor/AirQMonitor/api/http/AirQHttpGateway.cs:213-239`

`TruncateByLatestMills` logs a warning when a sample's timestamp is in the future
(`:220-223`) and then **still raises the watermark to it** (`:225-228`). One
`2030-01-01` sample in a `/latestData` response is persisted as the monitor's
`LastDataTime15Min` (`StoreNoiseLevelsHandler.cs:110`). From then on every real
sample compares as older and is discarded — no data written, no watermark change,
no error raised. `CheckForOfflineMonitorsHandler.cs:54` compares against the same
2030 timestamp, so the monitor is never flagged offline either. Silent, permanent,
self-sustaining; only a manual database edit recovers it.

**Fix:** clamp the watermark to `min(sampleTime, utcNow)` and drop future-dated
samples rather than warning about them. The same clamp belongs on Svantek's
`lastDataTime` (`StoreNoiseLevelsHandler.cs:230`). **S**

### P1-2 — Empty averaging windows score 0.0 dB, resetting latched rules and fabricating daily averages
`apps/monitors/airqmonitor/AirQMonitor/api/db/DBClient.cs:199`,
`apps/monitors/svantekmonitor/SvantekMonitor/api/db/DBClient.cs:220`

Both `GetAverageNoiseLevel` implementations end in `?? 0.0`. **MyAtm is the correct
sibling**: `GetAverageDustLevel` returns `double?` and
`MyAtmAlertTransitionEvaluator.cs:26-29` treats null as "no data, no transition".

A monitor with a latched 1-hour rule loses connectivity for six hours, then
uploads a backlog. `AirQRuleProcessor.cs:103-119` walks every complete hour; the
five empty ones score 0.0, `NoiseRuleEvaluator.cs:82-96` sees `0.0 <= LimitOff`
and clears the latch, and the final noisy hour re-fires a breach contacts were
already told about.

Second blast site: `NotifySiteAveragesHandler` in both monitors
(AirQ `:86-95`, Svantek `:50-62`) writes `WriteDailyAverage(level: 0.0)` for any
site-day with no samples, putting **fabricated 0.0 dB daily averages into the
reports table**, then clears any latched site-hours rule.

Note this is *not* covered by the B2 fix from the previous review: that added a
window-*completeness* guard, and a complete window containing zero samples still
scores 0.0. **M**

### P1-3 — First-import runs issue tens of thousands of synchronous per-window queries
Svantek `StoreNoiseLevelsHandler.cs:244` + `SvantekRuleProcessor.cs:128-148`;
AirQ `StoreNoiseLevelsHandler.cs:64-65` + `:99-108` + `AirQRuleProcessor.cs:103-119`

Svantek caps the *vendor request* at 7 days (`MaximumInitialBackfill`) but nothing
caps the *rule-evaluation start*, which comes from `LastDataTime ?? DeployedStart`.
A monitor deployed a year ago whose first sample arrives today drives ≈35,000
iterations for a single 15-minute rule — each a separate `CreateContext()` plus
aggregate query — inside the per-project loop that blocks the rest of the fleet.
AirQ seeds an unwatermarked monitor at `UtcNow.AddYears(-1)`, giving ≈1,095
`Create8hourAverage` calls plus ≈8,760 hour windows.

**Fix:** clamp the rule start the same way the request window is clamped —
`max(periodStart, end - MaximumInitialBackfill)`. **S**

### P1-4 — Site-archive SQL silently drops the final day of every contract's data
`apps/portal/RvtPortal.Spa/Adapters/Archive/SiteArchiveQueryCatalog.cs:269-272`

The monitor-ownership-window rule exists in three copies. Two normalise a
date-only off-hire to the next midnight — `MonitorOwnershipWindow.cs:108-118`
(`NormalizeContractEnd`) and `:56-59` (the EF expression, whose comment states
*"a date-only off-hire covers the whole day, so the exclusive end is the next
midnight"*). The raw-SQL `EffectiveEndExpression()` uses `c.off_hire_date` raw.
The sibling `EffectiveStartExpression()` (`:263-266`) *does* match its C# twin,
which rules out a deliberate difference.

Because `ContractCommands.AsUtcDate` (`:161-162`) stores `value.Date`, **every**
`off_hire_date` in the database is midnight — so this is not an edge case. The
portal grid, graph and CSV show data through the final day; the archive zip
excludes it across all six measurement exports plus breaches. No error, no
warning. **S**

### P1-5 — The Calendar "Day Detail" pane always sends `day=NaN` and 400s
`apps/portal/RvtPortal.Client/src/operations/MapCalendarPanels.tsx:474-477`,
consumed at `:215`

`parseCalendarDate` splits on `-`, but `selectedDate` is never a `YYYY-MM-DD`
string — it is the raw server `DateTime` (`DashboardApiContracts.cs:122`), which
serialises as `2026-05-24T00:00:00Z`. The third segment is `"24T00:00:00Z"`, and
`Number(...)` of that is `NaN`; `getCalendarDay` puts the string `"NaN"` into the
query and `DashboardController.cs:138-153` returns a 400.

Verified by execution. **Every user opening `/calendar` sees a validation error
instead of readings, for every deployment and every day.** The test fixture
(`App.test.tsx:2378-2403`) mocks by pathname and ignores query parameters, which
is why nothing caught it. Secondary defect in the same component:
`CalendarDayButton` (`:364-365`) does `new Date(day.date).getDate()`, shifting the
printed day number for viewers west of UTC.

**Fix:** slice to `value.slice(0, 10)` once before splitting, use the same slice
for the button label, and assert the emitted query string in a test. **S**

### P1-6 — The docs-only CI gate is fail-open on renames
`scripts/detect-code-changes.sh:80`

`git diff --name-only` has rename detection on by default. A change that deletes a
non-doc path and adds a similar `docs/**/*.md` path collapses to the destination
only, which classifies as documentation. Reproduced in a scratch repo both ways,
including the realistic case of relocating a content-pinned `docs/modules/**` file
(that tree is *deliberately* classified as code because `CommonPackageBoundaryTests`
and `TestReportingFixture` assert its text). With `--no-renames` the same diff
correctly lists both paths.

Result: the `.NET tests`, `Portal client tests` and `Engineering standards` jobs
all skip, the content pins never run, and the PR merges green.

**Fix:** add `--no-renames` (one word), plus a rename case in
`tests/detect-code-changes.test.sh` — its nine cases cover none. **S**

### P1-7 — If the gate job fails, every heavy job is skipped, and skipped satisfies required checks
`tests.yml:41`, `tests.yml:110`, `engineering-standards.yml:40`

All three read `if: needs.changes.outputs.code == 'true'`. A job whose `needs`
failed is *skipped*, not failed, and the output is then empty — so the condition
is false either way, and GitHub counts a skipped job as satisfying a required
check (which is the design premise stated at `tests.yml:16-19`). Any failure of
the `changes` job — checkout flake, a future bug in or deletion of
`detect-code-changes.sh`, a `usage()` exit — turns all three gates green-by-skip.
The expression is currently *pinned as a contract* at
`tests/verify-tests-workflow.test.sh:59`.

**Fix:** invert the default to `!= 'false'`, so anything other than an explicit
documentation-only verdict runs everything; update the contract pin. **S**

*(The gate-is-not-a-required-check half is SUSPECTED — the expression semantics
are confirmed.)*

### P1-8 — `omnidots_trace` has two irreconcilable schema owners
Portal: `omnidots_trace_index_id` — `apps/portal/database/postgres/canonical_database_naming.sql:494`
(the production cutover), `Migrations/Search/20260714134534_SearchBaseline.cs:177,186,194`,
`RVTSearchContextModelSnapshot.cs:840`, and the live read at
`SiteArchiveQueryCatalog.cs:231,236`.
Monitor: `trace_id` — `OmnidotsMonitorContext.cs:164`,
`postgres/2026-07-14-add-import-cursors-and-trace-order.sql:20,42,57`,
`testdata/create.postgres.sql:168`.

The monitor's is a **live production write path** (`DBClient.cs:417-421`). Against
a database that went through the portal's canonical-naming cutover, Omnidots trace
ingestion fails with `42703 column "trace_id" does not exist`, and the monitor's
own forward migration fails at line 20 for the same reason. Its line-57
`DROP INDEX IF EXISTS ix_omnidots_trace_trace_id` is dead code, because the portal
named that index differently.

No test catches it because each suite builds the table from its own side's shape
and nothing ever puts both in one database; `OmnidotsMigrationContractTests.cs:44-54`
only string-matches migration text.

**Fix:** decide the canonical name, correct the losing side, and add one
integration test applying the Omnidots forward migration to a schema built by
`RVTSearchContext`. Also settle **who owns this table** — it is the one place the
kernel/monitor/portal boundary has no owner. **M** (the decision is the work)

---

## §2 — P2

### Shared kernel and communications

| # | Finding | Location |
|---|---|---|
| SK-1 | **MQTT deliveries are marked `Completed` without anything being sent.** `PublishAsync` returns normally when `Enabled` is false (`:52-56`) and publishes at QoS 0 — no broker acknowledgement (`:59`). The adapter returns a null audit, so `DurableAlertDispatcher` marks the row delivered. The entire lease/retry/dead-letter machinery is decorative for the one channel that cannot observe failure. | `Mqtt/RvtMqttClient.cs:52-59`, `Alerts/MqttAlertDeliveryAdapter.cs:18-24` |
| SK-2 | **Graph large-attachment send leaks an orphan draft, once per retry.** Draft POST → chunk upload → send, with no `try/finally` and no cleanup. The ≥3 MB path is live via `ReportMessageSender.cs:38`. A 429 mid-upload leaves a draft in the shared sender mailbox; the retry leaves another. | `MicrosoftGraphEmailAdapter.cs:105-177` |
| SK-3 | **`CommitAsync` destroys the original stack trace** and lacks the cancellation guard its own sibling twenty lines below has. `Classify` returns the caught instance unchanged for pass-through cases, and `throw classified;` resets `StackTrace`. `RecoverDuplicateAsync:72-89` gets this exactly right. | `Alerts/Persistence/EfAlertCommitStore.cs:52-65` |
| SK-4 | **TransmitSMS treats a successful-but-unparseable HTTP 200 as *permanent*.** A null error code — empty body, `null` literal, schema change — throws with `statusCode: null`, which classifies as `Permanent` and dead-letters on attempt 1, writing a failure audit for a message the provider accepted. | `TransmitSmsClient.cs:48-56`, `TransmitSmsAdapter.cs:82-88,108-111` |
| SK-5 | **Undisposed PKCS12 certificate per MQTT reconnect.** The PEM cert is `using`-scoped; the loaded PKCS12 result is not, and `Dispose(bool)` doesn't touch it. `GetCert()` runs on every `ConnectAsync`, and `EnsureConnectedAsync` reconnects on any publish that finds the client disconnected. | `Mqtt/RvtMqttClient.cs:96-99,113-122` |
| SK-6 | **Quiet hours are evaluated against the alert's event time, not the send time** — see §4, needs a ruling. | `EfAlertCommitStore.cs:214,339-353` |

### Monitor applications

| # | Finding | Location |
|---|---|---|
| M-1 | **AirQ's offline check is the only one of four with no per-monitor isolation** — no `try`/`catch` at all. Svantek, MyAtm and Omnidots all use failure collectors. One `AcceptAsync` failure on monitor 3 of 200 leaves 4–200 neither evaluated nor marked offline, with nothing recorded. | `AirQMonitor/api/UseCases/CheckForOfflineMonitorsHandler.cs:33-92` |
| M-2 | **AirQ and Svantek advance the watermark *before* evaluating rules**, in separate contexts. MyAtm commits measurements, watermark, rule state, notifications and outbox rows in one transaction. A rule-processing failure therefore loses those alerts permanently — the next run starts after the samples. | AirQ `:110` vs `:119`; Svantek `:230` vs `:244`; MyAtm `DBClient.cs:260-371` |
| M-3 | **An Omnidots measuring point without a sensor is invisible to the entire fleet pipeline.** `ReadMonitors` inner-joins `Sensors`, but the DTO models the sensor as nullable and `ReadMonitor` uses `FirstOrDefault` — absence is legitimate. Such a point is imported into the catalogue, then never polled and never alerted on. | `OmnidotsMonitor/api/db/DBClient.cs:111-118` |
| M-4 | **MyAtm's delivery outbox is never purged.** No delete exists anywhere in MyAtm production code; the other three monitors run `CleanupAlerts` daily at `0 15 3 * * ?`. Completed and dead-lettered rows accumulate forever, and `ClaimNextDueAsync` orders over that growing table every minute. | `MyAtmMonitor/api/Delivery/IMonitorDeliveryOutboxCommands.cs`, `DBClient.cs:393-437` |
| M-5 | **One malformed Svantek filename blocks recordings for every alert sharing its project/point/day, on every run** — a blast-radius regression from PR #60. `TriggerDate` now throws `InvalidDataException` from inside a LINQ predicate over the whole cached file list; previously the file was silently excluded. | `CheckForSoundRecordingsHandler.cs:153-172`, called at `:111-117` |
| M-6 | **UTC-everywhere leftovers (exact inventory, 4 sites):** `AirQService.cs:67` and `StoreAllNoiseLevelsForYesterdayHandler.cs:19` use `DateTime.Today` for jobs scheduled at 00:03/00:05 under `TimeZoneId = "UTC"` — on a UTC+2 host these average the wrong date; Svantek `DBClient.cs:398` compares `DateTime.Now.AddHours(-12)` against UTC-written notification times; `StoreMonitorsHandler.cs:68` writes `ListedAtTime = DateTime.Now` where all three siblings use `UtcNow`. | as listed |
| M-7 | **Svantek's `last-status-timestamp` parse is culture-sensitive and style-less** — `DateTime.TryParse(value, out …)` with no `CultureInfo`/`DateTimeStyles`, while the sibling parse in the same path was fixed to invariant. The value gates the request window, so a culture-flipped day/month silently stops imports for that monitor with no error. Same bug class as the portal `TryCreateDate` 500. | `Mapping/SvantekDbMapper.cs:150-153` |
| M-8 | **SUSPECTED — Omnidots requests an unbounded window against a 4 MB response cap** with no chunking or fallback. A months-old monitor with no cursor requests everything at once; exceeding the cap or the 30 s timeout means the cursor never advances and every later run repeats the same oversized request. Svantek chunks; MyAtm pages. | `StorePeakRecordsHandler.cs:64-87`, `HttpWebClient.cs:9,18` |
| M-9 | **A failed scheduled report period is never regenerated and never surfaces.** Per-rule failures are logged only, so the Quartz job completes successfully; periods derive from `triggerUtc.Date`, so the missed one is never revisited. Weekly rules lose the whole week. | `ReportGenerationService.cs:46-67`, `ReportPeriodCalculator.cs:24-44` |

### Portal backend

| # | Finding | Location |
|---|---|---|
| PB-1 | **The dashboard loads the entire `monitor` table on every request, for every role** — no `Where`, not even `!Archived`; role filtering happens in memory afterwards. Backs `/api/dashboard/summary` and `/map-markers`, so a CompanyUser on one site pulls the whole fleet plus all open notifications. The comment beside it claims this anti-pattern was fixed *for deployments*. | `DashboardApplicationService.cs:449,469-472,488-506` |
| PB-2 | **Cancellation is still severed on the vibration-trace path (PR #50 was incomplete).** `GetTraceIndexesAsync`/`GetTraceIndexAsync` take no token, so `ToListAsync`/`SingleOrDefaultAsync` run tokenless from a request path that has one. `GetTraceIndexesAsync` is also unbounded. | `Api/MonitorDataSource.cs:22,24,62-82` |
| PB-3 | **CSV export buffers up to 1,000,000 rows into one string, then doubles it.** `DownloadAsync` sets no page size, so `MonitorService` falls through to `pageSize ?? 1000000`; the rows are joined into one string and then `GetBytes` allocates an equal array — roughly 100 MB + 100 MB per concurrent download. | `DataApplicationService.cs:288-311`, `MonitorService.cs:134,163,196,225,254,283` |
| PB-4 | **`RvtPortal.Spa/Application` depends on the HTTP contracts — 27 of 50 files** carry `using RvtPortal.Spa.Api;`, and no guard covers it (the boundary tests cover `RvtPortal.Application` and `Adapters/` only). After the BusinessLogic dissolution the repo has two things called "Application" with opposite rules. Placement debt, not a defect — but it is why boundary reviews keep finding leaks here. Cheapest honest fix is a rename to `UseCases`. | `RvtPortal.Spa/Application/**` |
| PB-5 | **Provider sniffing survives — correcting this session's own close-out.** The verification was `grep IsRelational`, which was too narrow. `EfSiteWriteAdapter.cs:188-198` has `IsPostgres()`/`IsSqlite()` at two call sites (the Sqlite branch is unreachable in production), and `RVTSearchContext.cs:502-505` branches the **entity model shape** on provider inside `OnModelCreating`. Nine files also still carry suppression justifications reading "runs on the InMemory test provider". | as listed |
| PB-6 | **Archive SQL hard-codes `public`, and the replatform left the whole path untested.** `Table(name)` emits `"public"."<name>"` into all eight CSV exports while every other portal read honours `SearchPath`. The test host would catch this except that it **replaces `ISiteArchiveService` with a fake** — so nine hand-written SQL statements have zero integration coverage, and a test actively asserts the hard-coding is correct. | `SiteArchiveQueryCatalog.cs:275-278`, `SpaTestApplicationFactory.cs:91-99` |
| PB-7 | **Batch-close accepts an unbounded notification-id list** — validated only for `Count == 0`, then `ids.Contains(...)` inside a transaction. One authenticated user can POST 200k GUIDs. Every other list endpoint normalises page size. | `NotificationsController.cs:113-131`, `NotificationCloseCommands.cs:130-133` |
| PB-8 | **Site-assignment removal orphans `notification_setting` rows.** Assignment creates one; all three deletion paths remove only the `SiteUsers` row. There is no relationship and no FK in the baseline migration, so nothing cascades and nothing errors — rows accumulate keyed to ids that no longer exist. | `UserSiteAssignmentCommands.cs:83-90,189`; `CompanyCommands.cs:138-141`; `UserAccountCommands.cs:275-278` |
| PB-9 | **A fourth, semantically different copy of "active site assignment".** `ActiveSiteAssignment.ForUser` checks start *and* end; two inline copies restate it faithfully; but seven report-rule sites use `EndDate == null` alone, ignoring `StartDate` and treating any future end date as inactive. Blast radius is nil today because nothing ever writes `SiteUsers.EndDate` — it will start dropping recipients the day soft-delete arrives. | `ReportRuleRecipientReader.cs:216,298,343,399`; `ReportRuleApplicationService.cs:485,525,582` |
| PB-10 | **Removing a monitor from a contract hard-deletes the deployment if it is under an hour old** — see §4, needs a ruling. | `MonitorContractAssignmentCommands.cs:136-143` |

### Frontend

| # | Finding | Location |
|---|---|---|
| FE-1 | **`ConfirmDialog` has no focus management, no focus trap and no Escape.** It renders `<dialog open>` (attribute), which is *non-modal* — only `showModal()` gives Escape, the top layer and background inertness. So focus stays on the trigger behind the scrim, `aria-modal="true"` lies to screen readers, and Tab walks the page behind it. This is now the only confirmation idiom, gating all ten destructive actions. | `components/FormControls.tsx:93-108` |
| FE-2 | **The unattached-monitor "Removal reason" can never be filled.** It renders only once `selectedMonitor` is set — which is exactly what opens the dialog whose backdrop covers it. So `reason` is always empty and **every archived monitor's audit reason is null**, despite the placeholder promising it is "recorded for audit history". | `MonitorRemovalPanel.tsx:174-183,207-219` |
| FE-3 | **Stored-XSS sink: Leaflet tooltips are set via `innerHTML`** (verified against the installed 1.9.4 `DivOverlay._updateContent`). Content is admin-written `fleetNumber` and vendor-supplied `serialId`; a 30-character `<img src=x onerror=…>` fits the field limit and executes for anyone hovering the marker. No CSP ships in `nginx.conf` to blunt it. | `components/MonitorMap.tsx:63,148-150` |
| FE-4 | **Two one-click destructive `removeMonitorFromContract` actions with no confirmation** — a gap the PR #49 sweep missed rather than a split regression (confirmed against the pre-split blob). | `MonitorDetailPanel.tsx:134-139`, `MonitorAssignmentPanel.tsx:144-151` |
| FE-5 | **A malformed `fromDate` in the URL crashes the whole authenticated shell.** `fromDateToApi` calls `toISOString()` on an invalid date inside an effect, so `AppErrorBoundary` replaces the app with "Something went wrong". Same class as the `format.ts` guard from PR #49; this sibling was missed. | `DataViewPanels.tsx:890-896`, reached from `:193` |
| FE-6 | **`MapPanel` still refetches the dashboard summary on every site-filter change** — the third sibling of the fix applied to `DashboardPanel` (PR #49) and `CalendarPanel` (PR #59), in the same file as the latter. | `MapCalendarPanels.tsx:73-94` |
| FE-7 | **Installer deployment coordinates: blank saves as `0,0`, garbage saves as an opaque 400.** `Number(lat \|\| 0)` pins a cleared field at 0°,0° (Gulf of Guinea) on every map; a typo yields `NaN` → `null` → a binding failure. The admin twin correctly uses `numberOrNull`, and this file's own changelog claims blank coordinates were preserved as null. | `MonitorPanels.tsx:571-576` vs `:423-424,767-774` |

### Cross-cutting and operations

| # | Finding | Location |
|---|---|---|
| OP-1 | **The ratchet cannot see changes to its own inputs.** `isSourcePath` counts only `*.cs` and client files, so a range touching only `.editorconfig`/`Directory.Build.props`/`global.json` collects zero diagnostics (verified empirically: 170 "baseline decrease" lines, exit 0, no tool invoked). One line — `dotnet_diagnostic.IDE1006.severity = none` — zeroes 135 of the 171 baseline entries and passes, permanently. | `scripts/engineering-standards/verify.mjs:698-706` |
| OP-2 | **`development-guidelines.md` gives two instructions that cannot be followed**: it routes ports into deleted `RVT.BusinessLogic` and cites a guard test that does not exist (`:104-115`), and it states most tests run on InMemory/SQLite and that Postgres tests "skip in CI (which has no PostgreSQL)" (`:136-146`) — CI has provisioned TimescaleDB on every run since before PR #63 replatformed the suite onto it. | `docs/development/portal/development-guidelines.md` |
| OP-3 | **SchemaDeploy runs the whole deploy in one transaction with no `lock_timeout`, and rebuilds PKs on populated hypertables every run.** `01_pk_adjustments.sql` guards on "does a PK exist", not "is it correct", so each deploy takes `ACCESS EXCLUSIVE` on `error_log` (479k rows), `notification_sent` (82k) and others and holds it through ~40 view definitions — blocking the monitors' error-write path fleet-wide. One idle-in-transaction reader blocks the deploy indefinitely. | `ScriptRunner.cs:44-46,196`; `post-load/01_pk_adjustments.sql:19-35` |
| OP-4 | **The new `last_error` widening migration has no transaction, no table guard and no rollback twin** — six lines, a bare `ALTER TABLE`, where every sibling wraps in `BEGIN`/`COMMIT` and ships a rollback. The contract test lists it but asserts transactionality only against the forward script. *(The 1024 value itself is correct in all five declarations.)* | `omnidotsmonitor/postgres/2026-07-30-widen-alert-outbox-last-error.sql` |
| OP-5 | **Five deploy scripts pin `SET search_path TO public`** (non-`LOCAL`) on a shared connection, so script 01 overrides the connection's `SearchPath` for all later scripts; DDL is `public.`-qualified throughout and there is no `--schema` option. Point SchemaDeploy at a scoped connection — exactly how the test infrastructure isolates — and it silently writes into `public` instead of failing. | `post-load/{01..05}.sql`, `ScriptRunner.cs:185-207` |
| OP-6 | **`.dockerignore` does not withhold what the client-release policy treats as secret** — no `.env`, `appsettings.Development.json`, `*.key/*.pem/*.pfx`. All five monitor Dockerfiles `COPY . .` from the repo root, and compose reads real API secrets from the environment. The shipped image is clean (only `/app/publish` is copied forward) but the build layer and BuildKit cache are not. | `.dockerignore` vs `docs/release/client-release-exclusions.txt:40-70` |
| OP-7 | **The portal is compile-time locked to SendGrid while the monitors are provider-pluggable — and the boundary guard pins the asymmetry**, requiring the Spa to reference exactly those two packages. An org-wide move to Graph would switch the monitors, leave the portal behind, and require editing the guard to fix it. | `ServiceCollectionExtensions.cs:105-113`, `scripts/verify-rvt-common-source-boundary.sh:168-169` |

---

## §3 — P3

**Shared kernel:** `DurableAlertDispatcher` throws `AggregateException` on dead-letter
— the *designed* terminal outcome — failing the whole minute's dispatch job, and its
claim call sits outside the `try` so a mid-batch DB error discards the accumulated
dead-letter list (`:39,44-47,108-120`). A failed daily cleanup sets
`lastCleanupDate` before running, skipping cleanup for 24 hours (`:90-98`). A dead
`key` parameter degrades every reparse-point storage error across 10 call sites
(`LocalObjectStorageClient.cs:336-339`). The integration-DB helper's cleanup `catch`
can replace the real setup failure and orphan a schema permanently
(`PostgreSqlIntegrationDatabase.cs:55-59,79-98`). Alert SMS templates carry a double
space and an inconsistent URL rendering versus their Caution twins
(`NotificationMessageComposer.cs:27,29,31` vs `:38,40,42`). `Enabled` defaults
disagree between TransmitSms (false) and SendGrid (true), and disabled SMS still
plans rows that dead-letter and write a failure audit per contact per alert.
`IAlertIngressPort` documents none of its three semantically distinct exceptions,
and only one of six call sites distinguishes them. Two unused usings.

**Monitors:** stale validation message from the Delivery move
(`MonitorDeliveryOptions.cs:23` still names Svantek). `ClearMonitorsOfflineFlag` is
production-dead (absent from the interface, catalog and endpoints). Omnidots'
per-serial `ReadRules` branch is production-dead. `ReadRules` `IsDeleted` filtering
is asymmetric — only Svantek filters in the per-serial branch; the others rely on
downstream guards, so PR #54's note that site-rule semantics were already correct in
all four is true only of Svantek. AirQ writes a pointless `SetMonitorOffline(id,
false)` per online monitor per run, three times an hour, and both AirQ and Omnidots
re-read the fleet inside a per-rule loop. A culture-sensitive `ToString("yyyyMMdd")`
day code in an otherwise invariant file.

**Portal:** 14 direct `DateTime.UtcNow` reads remain (exact list captured) where six
sibling classes inject a clock — the 2026-07-30 ruling covered `DateTime.Today`
only. `SiteArchiveQueryExecutor.StreamAsync` accepts a token it never uses (both
callers happen to apply `.WithCancellation`). `CreateDefaultMonitorAlertLevels`
issues an `AnyAsync` per monitor across the whole fleet. `RVT.BusinessLogic/` and
`RVT.Utilities/` still exist on disk as untracked `bin`/`obj` husks, polluting greps
exactly like the `.worktrees` remnant did. Six archive exports use an inert
`RIGHT JOIN` that collapses to inner-join semantics. **SUSPECTED:** `FilterExpression`
builds values as `Expression.Constant`, which EF inlines as SQL *literals*, so each
distinct serial/window produces distinct SQL text — no plan reuse on TimescaleDB.
Cheap to verify with sensitive-data logging on one test.

**Frontend:** error-on-confirm leaves the dialog open with the error painted *behind*
the backdrop in two of four copies of the pattern. Substantial duplication left by
the M9 splits (exact grep counts): `pageSize = 10` ×6, `ListExecution` ×5, the route
props shape ×6, `DetailItem` ×4, `LoadingInline` ×3 (two byte-identical),
`NotificationList`/`notificationTone` byte-identical across two files, a redeclared
`roleNames` missing a role, and `useGridSortHandler` bypassed by three hand-rolled
copies — the two "shared" modules the split created already diverge from each other.
Two test-only exports (one is the repo's sole lint warning). An unhandled promise
rejection on sign-out failure. A silent no-op when what3words conversion fails. Alert
thresholds silently coerce blank/garbage to a **0 threshold that fires on every
reading**. A decoy "Delete company" row action that only shows a notice. No security
headers in `nginx.conf`. Marker arrays recomputed three times per render in two panels.

**Operations:** eleven documentation defects verified against source (ports catalog,
cutover runbook, Sonar runner connection count and `dotnet-ef` version, React
onboarding map, EF-migrations rationale, container-builds script path, observability
package version, a dead exclusions path, the readiness matrix's split-away evidence,
and a `tests.yml` comment claiming Postgres fixtures fail rather than skip — true of
the monitor fixture, false of the portal's 161 skip-gated tests). `--base auto` on a
push resolves to `HEAD^`, grading only the last commit of a rebase-merge. Runtime
images still float `aspnet:10.0` while SDKs are pinned. `airqmonitor-api` is the only
compose service with no port mapping. `cancel-in-progress` now applies to pushes on
`main`, so rapid merges cancel the first verification. `06_site_write_uniqueness.sql`
duplicates indexes that an EF migration also creates. `verify-mono-solution.sh`
discovers only three top-level trees, so a new one would be ungraded.

---

## §4 — Product rulings needed

1. **Quiet hours: event time or send time?** Contact send-windows are evaluated
   against the alert's *event* time, while the rule's activity window uses a
   different field — by design, they are populated from different clocks
   (`AirQRuleProcessor.cs:81`). So AirQ's 00:03 UTC backfill job can SMS a contact
   at midnight for a 14:00 breach that passed their 09:00–17:00 window. This is a
   *quantity* question the 2026-07-30 timezone ruling did not address — that one
   decided which zone, not which instant.
2. **Is the under-an-hour deployment hard-delete intentional?** Removing a monitor
   from a contract deletes the deployment row outright if it is less than an hour
   old, making any data that already arrived permanently unattributable, with no
   confirmation and no audit row. The sibling unattached-monitor path soft-archives.
   If this is an "undo a mis-assignment" affordance it should be conditioned on *no
   data having arrived*, not on elapsed time — and the one-hour value needs an
   explanation either way.
3. **Who owns `omnidots_trace`?** (P1-8.) Beyond picking the column name, the table
   needs a single owning subsystem.
4. **Should reporting backfill missed periods?** (M-9.) Making the job fail loudly is
   uncontroversial; deriving periods from `LastGenerated` so a missed week is
   regenerated is a behaviour change.

---

## §5 — Corrections to earlier claims

- **"Zero provider sniffing remains anywhere in production code" (this session's
  PR #63 close-out) was overstated.** It was verified with `grep IsRelational`,
  which missed `IsPostgres()`/`IsSqlite()` helpers at four call sites in two
  production files, including a provider branch inside `RVTSearchContext`'s
  `OnModelCreating`. See PB-5. The ruling is substantially satisfied, not literally.
- **The third review's claim that the portal `.editorconfig` "sets `IDE0005 = none`,
  silencing a ratchet-tracked rule" is wrong.** That line sits *inside* the
  `[**/Generated/**/*.cs]` section (a blank line does not close an EditorConfig
  section), so it is scoped to generated code only. Do not spend a PR "fixing" it.
- **PR #54's note that deleted-rule semantics "were already correct" in all four
  monitors holds only for Svantek** in the per-serial `ReadRules` branch; the other
  three rely on downstream guards. No live leak, but the two mechanisms can drift.
- **PR #50's cancellation threading was incomplete** — the vibration-trace port
  methods still take no token (PB-2).

---

## §6 — Verified clean

Recorded so the coverage is legible, and so a fifth review does not re-derive it.

**The durable alert core is sound.** The reviewer traced commit → outbox → claim →
lease → retry → dead-letter and could not construct a path that loses or duplicates
an email/SMS alert. `FOR UPDATE SKIP LOCKED` with lease-expiry reclaim, lease
fencing on `(id, status, lease_id)` that rejects a stolen lease rather than mutating,
`DeliveryTimeout < Lease` enforced by an options validator, correct
cancellation-leaves-rows-reclaimable behaviour, sound duplicate recovery under the
unique index, and correct FK cascade preventing orphaned outbox rows. The B1
scheduler-mode alert-loss finding is genuinely closed: all three monitors that call
`AddDurableAlerts` declare both jobs in catalog *and* appsettings.

**Portal authorization.** Every controller's role attributes were read;
`SiteAuthorizationPolicy` is correct, including that a CompanyUser may update only
their own notification setting; mutations re-authorise the rebuilt detail; installer
actors are company-scoped; every grid/graph/trace/CSV read goes through a visibility
gate. **Every inbound `DateTime` on a write or query-bound path was traced and all
are normalised — no third instance of the two 500s exists.** Transaction handling in
`EfCoreUnitOfWork` is genuinely careful (shared-connection invariant, execution-strategy
retry with change-tracker reset, reverse-order disposal preserving the primary
exception). CSRF, constant-time internal-key comparison, correlation-id sanitisation,
forwarded-header trust, file-upload magic-byte validation before storage, and
sort/pagination validation against static dictionaries all check out.

**Frontend.** No `dangerouslySetInnerHTML`, no storage/cookie use, no `any`, one
non-null assertion (the React root). URL hardening rejects `//host`, backslashes,
dot segments and non-http schemes; `returnTo` cannot be turned into an open
redirect; the request-lifecycle generation protocol is used consistently by all four
consumers; the PortalShell mount-once fix holds; role gating is backed by real
server authorization; no PII in URLs, logs or storage.

**CI hardening.** All `uses:` are 40-char SHA-pinned; root `permissions: contents:
read` with no job widening it; no `pull_request_target`; no `github.event.*`
interpolation in any `run:` block. `detect-code-changes.sh` fails safe on zero/all-zero
bases, unresolvable revisions, empty diffs, spaces, non-ASCII filenames and code
deletions (all six tested) — the rename case (P1-6) is the sole gap.

**Ratchet integrity, apart from OP-1.** `--update-baseline` is monotonic and
mode-restricted; a PR cannot raise its own baseline; the five environment escape
hatches are blocked under `GITHUB_ACTIONS`; renames grade as full new files;
whole-file reformats trip the changed-surface check; a new violation on an untouched
line still trips the per-key count; path handling rejects quoted, NUL-bearing,
symlinked and out-of-root paths.

**Structure.** The project-reference graph is acyclic and direction-correct, with no
monitor→monitor and no lib→app edge; all 47 project entries across seven legacy
`.sln` files resolve; the three EF chains use separate history tables and create
disjoint tables; the MyAtm Delivery move left nothing orphaned in Common, and
`DeliveryDispatchPolicy`/`DeliveryRetrySchedule` are genuinely shared with a
cross-check test asserting both call sites agree.

---

## §7 — Suggested execution order

1. **P1 tier**, in two independent waves: the three monitor defects (P1-1/2/3) and
   the two CI gate fixes (P1-6/7, both one-liners plus test updates) can run in
   parallel with the portal archive fix (P1-4) and the calendar fix (P1-5). P1-8
   blocks on a ruling.
2. **The rulings in §4**, so the ruling-dependent P2s can be scheduled.
3. **P2 by territory**, four parallel slices — with SK-1 (MQTT silent success),
   M-2 (watermark-before-rules), PB-1 (full-table dashboard read) and FE-3
   (XSS sink) taken first inside their slices.
4. **P3 as batched cleanups**, with the eleven doc corrections landing together.
