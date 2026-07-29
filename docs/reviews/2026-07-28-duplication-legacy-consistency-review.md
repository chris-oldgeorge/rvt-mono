# RVT Monorepo — Duplication, Legacy Code & Consistency Review

Date: 2026-07-28 (second full review, after merge commit `6be9c90` / PR #15)
Focus, as commissioned: duplicate code, stale/dead legacy blocks and files,
engineering-guardrail and Sonar alignment, hexagonal adherence, and coding-style
consistency across the codebase.
Method: five parallel subsystem reviews against `main`, each briefed with the
previous review (`2026-07-28-full-codebase-review.md`) and the interim fixes so
that only still-open or new findings are reported. Every dead-code claim below
was grep-verified (zero references outside the definition unless stated).

---

## Executive summary

> **Status 2026-07-29 — remediation complete.** Every priority-list item is
> resolved in-place below (executed, or withdrawn with rationale). The legacy
> sync messaging path is deleted end-to-end (§8 steps 1–6), all four monitors
> alert through the durable stack (`docs/architecture/rvt-monitor-common/durable-alerts.md`),
> guardrail gaps G1 and G3 are closed by PR-run tests and the AirQ guards
> (G5's portal Application→Spa.Api boundary guard landed too), and the
> ratchet baseline is down from 7,709 tolerated violations to ~534. The
> figures in this summary and §2 describe the 2026-07-28 snapshot and are kept
> for the record. Open threads: the Omnidots `RVT__OMNIDOTS_USE_TOKEN` product
> ruling, §8 step 7's `RvtConfig` endgame, and a main-side baseline
> regeneration (a fresh `--all` scan exceeds the checked-in baseline in files
> the remediation never touched).

The codebase's biggest quality lever is no longer bugs — it is **repetition and
retirement**. The ratchet baseline tolerates **7,709 violations**, and 13% of
them live in four near-identical `TestDbClient.cs` files. The portal carries
**~800+ lines of verified-dead production code** including two entire MediatR
command files that are never dispatched. The legacy sync messaging path
survives in three monitors plus a verbatim inline copy. And the single
highest-risk finding is a guardrail gap, not a code defect: **no PR workflow
executes any tests** — the architecture guards and the whole unit/integration
suite run only in the manual SonarQube workflow, so a PR that breaks hexagonal
boundaries or every test merges green.

Two findings implicate the July 28 remediation work itself and are reported
with the same severity as anything else:

- **Svantek was skipped by the HTTP-timeout fix.** AirQ and Omnidots got a 30 s
  bound; Svantek's vendor client still has no `Timeout` anywhere and runs on
  the 100 s framework default — the exact defect the fix commit describes.
- **The targeted style pass created intra-class naming mixes.** Renaming only
  ratchet-flagged fields left `_gateway` beside plain `monitorReader` in the
  same constructors (Omnidots handlers, `OmnidotsApi`, three AirQ handlers) —
  violating the standard's "a logical unit must not mix styles".

---

## 1. Guardrail gaps (highest risk first)

| # | Gap | Evidence |
|---|-----|----------|
| G1 | **No test execution on PRs.** `engineering-standards.yml` never runs `dotnet test`/`npm test`; all architecture guards and unit/integration tests execute only in the manual (`workflow_dispatch`) SonarQube workflow. | `.github/workflows/engineering-standards.yml`, `sonarqube.yml:3` |
| G2 | SonarQube is manual-only; nothing schedules it or gates releases on its quality gate, despite it building everything against real TimescaleDB with coverage. | `sonarqube.yml` |
| G3 | **airqmonitor has zero architecture guard tests** (only monitor without); portal legacy projects and `RvtPortal.Client` also unguarded. | no `Architecture/` in `AirQMonitorTests` |
| G4 | No sync-over-async ban (no BannedApiAnalyzers/AsyncFixer); 8 live `GetAwaiter().GetResult()` sites in MyAtm production code and nothing preventing new ones. | `MyAtmApi.cs:131-185`, `MyAtmRuleProcessor.cs:126` |
| G5 | No guard against new `RvtConfig` static usage; no `no-console` ESLint rule; no `Application → Spa.Api` boundary guard in the portal (the 31-file violation is unguarded). | — |
| G6 | The ratchet never tightens automatically — decreases are informational; baseline reduction requires manual `--update-baseline`, so debt plateaus. | `verify.mjs:1174,1498` |
| G7 | The five root `verify-*.sh` guards (layout, solution, postgresql-only, source-boundary, documentation) are wired into **no** workflow — local discipline only. Workflows trigger on `pull_request` only; direct pushes to main bypass even the ratchet. | `scripts/`, workflows |
| G8 | `.editorconfig` severities are `suggestion`, so `EnforceCodeStyleInBuild=true` in apps/monitors is effectively a no-op — misleading enforcement. | `.editorconfig:19-21` |

## 2. Ratchet-baseline debt map (authoritative numbers)

> 2026-07-29: snapshot numbers. After the remediation series and main's own
> sweeps, the baseline tolerates ~534 violations, dominated by IDE1006 naming
> (blocked on Roslyn's fix-all-less naming code fix) and deliberate IDE0130
> namespaces. See item 18 for the tooling limits.

- **1,994 entries / 7,709 tolerated violations** (`eng/standards/baseline.json`).
- By rule: IDE0008 `var` **4,518 (59%)**, IDE1006 naming 1,175, IDE0005 unused
  usings 319, IDE0130 namespace-folder 276, IDE0305 258, IDE0161 file-scoped 209,
  CHARSET 58 (all in apps/portal), prettier 30.
- By module: apps/monitors 5,078 (omnidots 1,819), apps/portal 1,462,
  libs 1,169.
- **Top debt holders:** the four `TestDbClient.cs` files (331+246+226+202 =
  1,005 violations, 13% of everything), `MonitorService.cs` (220) +
  `MonitorData.cs` (79) in the portal, the four monitor `api/db/DBClient.cs`
  (385 combined).

## 3. Dead / stale code (grep-verified)

### Portal backend (~800+ deletable lines)
- `RvtPortal.Spa/Application/ReportRules/ReportRuleCommands.cs` (361 lines):
  five commands + handlers + workflow, dispatched nowhere (MediatR assembly
  scanning hides the deadness); duplicates live validation in
  `ReportRuleApplicationService` with already-diverged messages.
- `RvtPortal.Spa/Application/Monitors/GetMonitorDetailQuery.cs` (98 lines):
  never constructed; detail flows through `IMonitorAdministrationWorkflowService`.
  Deleting it also shrinks the Application→Api hex-violation set.
- **15 of 29 `IMonitorService` methods** have zero production callers, taking
  with them `OmnidotsSensorRepository`, `SvantekMonitorStatusRepository`,
  `MonitorStatusForMonth/TimeCheck` + 4 DTOs + ports.
- `IOmnidotsBreachesAndAlertsRepository`: registered in DI, consumed by
  nothing (and it was *migrated into the core ports on 2026-07-10* while dead).
- **[still open]** 12 of 21 `ILookupService` methods, two returning raw entities.
- Whole dead files: `RVT.Entities/CreateDB.cs` (entirely commented-out code —
  the only DOC-002 commented-code violation found), `IMyAtmDustLevel.cs`,
  `AirQNoiseLevelSiteAvg.cs`, `SiteAverage.cs`, `AuthorizeRolesAttribute`,
  `BatteryAlertTypeEnum`, dead API DTOs `QuerySiteUsersRequest`,
  `ReportUserAssignmentSummaryResponse`.
- Stale package refs: `Azure.Storage.Blobs` + `Microsoft.Extensions.Http` in
  RVT.BusinessLogic, `Newtonsoft.Json` in RVT.Entities (with its unused using).

### Monitors
- **`airqmonitor/SmsSender.cs` — orphan, never compiled** (sits outside both
  project dirs), references the Infobip SDK, declares a `Rvt.Monitor.Common`
  namespace, `using MyAtm.Api` inside the AirQ folder, reads secrets from raw
  env vars. Untouched since import. Delete.
- MyAtm sync wrappers (7 × `GetAwaiter().GetResult()`): two fully dead
  (`ClearOlderErrorMessages()`, `ProcessDustLevels<T>`), five test-only.
- `OmnidotsApi.StorePeakRecordsLastDataTimeNewAsync` — zero callers anywhere;
  `StorePeakRecordsAsync` and `AuthenticateAsync` test-only; three facade
  methods forward to one handler.
- `Liveness()` duplicated ×2 (Svantek, Omnidots) with zero callers of either.
- `OmnidotsMonitorTests/manualtest/` — stale Node.js scratch tooling.
- AirQ carries two same-named `DateTimeUtil` classes (local + common).

### Shared libs (deletable now, no behavior change)
- `MessageService.MessageContent` nested class (zero refs).
- `RvtConfig` zero-consumer fields: `SMS_ENABLED`, `EMAIL_ENABLED`,
  `WEBHOOK_URL`, `CONFIG_SECRET`, `NOTIFICATION_DELAY_MINUTES`, internal
  `MonitorKind`; `WEBHOOK_SECRET` is test-fixture-only.
- `DateTimeUtil.GetStartTime`, `IMonitorEventPublisher.PublishDataInserted`
  (sync member), `RvtContactDto.FromNotificationDto` (dead converter direction).
- `LegacyMessageKind.Password_*/Report_*` + `LegacyMessageChannel.Both`:
  rejection-test-only enum members whose mappers all throw.

### Frontend
- 8 dead `client.ts` exports (+ their DTO plumbing), dead `ErrorSummary`
  component, dead `authError` state (both setter branches set null), dead
  `.metrics` CSS.

### Repo-wide stale files
- `apps/portal/artifacts/spa-proxy-repair/` (114 MB recovery residue) and
  `artifacts/` root (1.3 GB local cache) — both gitignored, prunable.
- `.worktrees/repository-engineering-standards` — branch merged, at main tip;
  `/private/tmp/rvt-mono-help-admin` — upstream gone. Four merged local
  branches deletable.
- **26 git-tracked files under gitignored `.superpowers/sdd/`** (38 MB dir).
- `docs/development/monitors/sonarqube.md` + two scripts it references —
  superseded by the root Sonar workflow.
- `project_state.md`: **5,445 lines / 342 KB / ~40 checkpoints** — an
  append-only log wearing a "state" filename; archive history, keep a
  one-screen current state.
- Assorted empty directories; untracked nested `.DS_Store` (ignore pattern
  covers root only).

## 4. Duplication

### Cross-monitor (the dominant cluster)
- `AddEmailProvider(...)` ×5 (four byte-identical, one drifted to `var`).
  **Resolved as intentional (2026-07-29):** the provider split
  (docs/architecture/rvt-monitor-common/communications.md) deliberately makes
  each host own provider selection — no facade, no vendor names in the
  neutral projects (guard-enforced down to the literal string `SendGrid` in
  `Rvt.Communication` source). `HostCommunicationsCompositionParityTests`
  now pins the five copies byte-identical so they cannot drift.
- `GetJobName` ×5; the dispatcher "parameterless ctor throws at runtime" hack
  is now ×5 (grew); each monitor maintains the job-name list twice
  (dispatcher set + runner switch = 10 hand-maintained lists).
- Four `IHttpClient`/`HttpWebClient` families — now all async+CT, so **more**
  consolidatable, but newly inconsistent: timeouts 30 s / 30 s / **none
  (Svantek)** / 15 s; only MyAtm has retry + bounded read; phantom `<T>` in 3
  of 4; Svantek's `GetByteArrayAsync` actually sends POST; the Omnidots
  fake-auth seam survived the rewrite verbatim.
  **Resolved 2026-07-29:** one `VendorHttpTransport` engine in
  `Rvt.Monitor.Common.Http` (send/dispose loop, optional pacing/retry via
  `IVendorRequestPolicy` — MyAtm's policy generalized to
  `VendorRequestPolicy` — and optional bounded reads); the four clients are
  thin wrappers keeping their own pinned headers, timeouts, and error
  contracts. Phantom `<T>` removed; Svantek's method renamed
  `PostForBytesAsync`; the Omnidots seam moved to the
  `OmnidotsStaticTokenClient` composition decorator (behaviour and the
  `RVT__OMNIDOTS_USE_TOKEN` default preserved — whether the seam should exist
  at all is still the open product question).
- `ClearOlderErrorMessagesHandler` ×3 (same 7-day cutoff, now with sync/async
  signature drift, all reading the clock directly).
- Test scaffolding: `TestDbClient.cs` ×4 (~6,400 lines; the "13% of ratchet
  debt" figure was made stale by PR #21's baseline regeneration — it is now
  1.4%), `TestRuleActivity` ×4, `CommunicationsCompositionTests` ×5 (4 diff
  lines), plus **new** duplication from the July refactor: cancellation-test
  twins, timeout-test twins, and a copy-pasted `#pragma warning disable
  IDE0130` block in both Ports files.
  **Resolved 2026-07-29 (partially):** `TestRuleActivity` ×4 deleted — the
  copies tested only Common types, and their differences silently encoded each
  monitor's ambient `RvtConfig.RulePolicy` (MyAtm asserting the opposite
  result on the same window) plus unhardened timezone handling; one explicit
  policy-parameterized copy now lives in `Rvt.Monitor.CommonTests`.
  `CommunicationsCompositionTests` ×5 now delegate to the framework-neutral
  `CommunicationsCompositionContract` in `Rvt.Monitor.IntegrationTesting`.
  **`TestDbClient` unification is deferred**: its shared-looking members carry
  2-4 real variants each (schema, DTO twins from `Rules` vs `Notifications`,
  policy semantics); it unifies naturally after the §8 DTO retirement, and the
  debt argument no longer applies.

### Portal backend
- `InvalidSort` helper copy-pasted into **10 controllers in two diverged
  shapes**; report-frequency label logic ×4 (twice inside entity computed
  properties — presentation logic in the persistence model); 20 test files
  hand-roll their own `DbContextOptionsBuilder`; two overlapping paging models
  (`Paging` vs `PageRequest`); repeated literals (`"Auth:SkipPasswordResetEmail"`
  ×5, `"Site not found"` ×4).

### Frontend (grew since last review)
- `parsePositiveInt` ×7 (two semantics — `?page=2abc` behaves differently per
  screen); `normalizeSortDirection` ×6 (three semantics — `?sortDir=descending`
  honored on five screens, ignored on three); formatters ×16 with **two locale
  policies** (hardcoded `en-GB` vs browser locale — same timestamp renders
  differently per screen); `useGridSortHandler` ×3 + inlined ×5; **new**:
  `claimRequest`/`ownsRequest` ×4 byte-identical; `'https://rvt.local'` ×25;
  `DetailItem` ×4; `safeReportLink` still reimplements `safeHref` minus the
  protocol-relative check (an open-redirect gap).

### Shared libs
- The two durable dispatchers still duplicate `IsTerminal`, claim/lease loops,
  safe-error formatting (with a truncation divergence: 1,024-char cap on one
  side only), dead-letter audit construction, ownership-loss logging, and
  identity hashing (SHA256→Guid vs SHA256→hex). Backoff is unified; the rest
  is not.
- **A fourth inline copy of the legacy dispatcher loop** lives in
  `OmnidotsRuleProcessor.cs:65-109` (character-identical log literals) —
  Omnidots is split-brain: durable alerts for measurements, legacy sync for
  offline/battery.
- MyAtm's `LegacyNotificationDeliveryService` wraps `IMessageService` back
  into `INotificationDeliveryService` — a round-trip through the legacy
  contract for nothing.
- Six near-identical startup-validation hosted services (two naming
  conventions); three options classes re-implementing the same config helpers;
  **(Validator consolidation withdrawn 2026-07-29: each service is ~15 trivial
  lines, the six type names are pinned by four test layers, and the only shared
  home — `Rvt.Communication.Abstractions` — is guarded to stay
  dependency-free. A generic base plus six name-preserving stubs would not be
  smaller than the six files.)**
  registration naming drift across three conventions; the retired
  `Rvt.Monitor.Common.Infrastructure` facade's `AddMonitorCommunications` was
  deliberately dissolved into per-host composition (not "never built" as this
  review first stated); the five-host block is a documented decision, now
  drift-pinned by `HostCommunicationsCompositionParityTests`.

## 5. Hexagonal architecture

- **Vendor-port asymmetry (High):** AirQ and Omnidots now have driven ports;
  **Svantek and MyAtm use cases still bind concrete gateways** (no Ports
  folder, gateways implement no interface). Same pattern everywhere is the goal.
- **[resolved 2026-07-29] One-shot cancellation was severed:** the P0 guardrail
  change now threads `MonitorHost.RunAsync`'s shutdown token through all five
  one-shot paths.
- **[partially resolved 2026-07-29] Omnidots cancellation semantics diverged
  within one refactor:** `StoreTracesHandler` now checks the caller token at
  the monitor boundary and rethrows caller cancellation before recording
  monitor failure. Reusing `RunFleetAsync` for Veff/Vdv remains future
  convergence work.
- **[still open, now guarded]** Portal: 31 host Application files import
  `RvtPortal.Spa.Api`. PR #20 added a shrinking architecture baseline, so the
  count cannot grow even though the existing boundary debt remains;
  `ReportGenerationClient` still consumes inbound API DTOs;
  ports-and-adapters catalog is stale (describes the now-dead MediatR detail
  query); InMemory-provider branching in two production classes.
- Facades still hand-`new` their handler graphs in 3 of 4 vendor monitors;
  Omnidots builds a **second** gateway instance from `RvtConfig` in the facade
  even though DI registers the port. Job-service abstraction has four shapes
  (concrete class / two interfaces / raw `IServiceProvider` service-location).

## 6. Style consistency

- **Field naming needs a ruling, not a cleanup:** the ratified standard says
  `_camelCase`; **zero** portal files comply (even greenfield
  `RvtPortal.Application` uses `this.camelCase`), libs are 2 files vs the
  rest, and the July style pass introduced *mixed styles within single
  classes* in monitors. Either amend the standard or fix the config so the
  ratchet pushes one way — today standard and practice diverge 100%.
- Namespace styles are split mid-project: RVT.Entities 31 block vs 6
  file-scoped; Svantek/MyAtm/Omnidots roughly half-and-half; the 11
  block-scoped files in `Rvt.Monitor.Common` are precisely the legacy
  carry-overs. Reportingmonitor and portal tests are uniform.
- **Dated "Major updates:" headers violate DOC-002 in 384 files**
  (272 portal + 112 monitors) and are still being copied into newly touched
  files; 1,811 `// Function summary:` comments narrate obvious syntax, some
  factually wrong.
- Test conventions drift: `TestX` vs `XTests` naming coexists in every monitor
  test project; Omnidots tests have **two root namespaces**; `Usings.cs` vs
  `GlobalUsings.cs`; frontend has five distinct in-flight-request idioms.
- Frontend confirmation UX has three tiers (ConfirmDialog / globalThis.confirm
  / nothing) — user delete is still one un-confirmed click.

## 7. Sonar-style findings

- Complexity/size hotspots: `DataApplicationService.cs` 1,073 lines (per-type
  switches begging for a strategy), `DashboardApplicationService.cs` 918,
  monitor `DBClient` facades 561–1,254 lines, `ContractSitePanels.tsx` 1,904,
  `App.tsx` 1,573 (~250 of it an inline static privacy page).
- **[resolved 2026-07-29]** `MonitorDetailSummaryService` now propagates
  cancellation and emits a structured warning for genuine optional-summary
  failures; `SiteArchiveAdapter` preserves its mapped fallback results but
  emits structured error logs without signed URLs.
- Magic numbers still inline in AirQ/Svantek rule processors (900/3600/86400);
  battery thresholds duplicated with clashing naming conventions;
  `"OmniDots guest"` ×3 — and *not* applied in `StoreTracesHandler`
  (guest traces still fetched: behavioral inconsistency, not just style).
- **[resolved 2026-07-29]** AirQ now seeds a missing watermark from injected
  UTC `TimeProvider` time, and the behavior-neutral aggregate rethrow blocks
  are gone without changing aggregate exception handling.
- **[partially resolved 2026-07-29] Config hygiene:** Omnidots no longer commits
  a personal alert recipient or a customer serial allow-list. Svantek+MyAtm
  Dockerfiles still carry an unexplained Kerberos lib (likely SQL Server-era
  drift); AirQ is the only compose service without a port mapping; frontend
  `openapi-typescript@latest` is unpinned; OpenAPI schema is still stale (zero
  Help endpoints in 6,367 lines), so the Help Admin filter bug
  (`toSearchParams` whitelist) **remains live**.

## 8. Legacy-path retirement map (from the shared-libs review)

Live callers of the sync messaging path: AirQ + Svantek rule processors (via
`RuleAlertNotificationDispatcher`), Omnidots offline/battery (via its inline
copy). MyAtm's direct-delivery route is compat-only and architecture-test-
enforced dead. `[Obsolete]` still missing from `IMessageService` itself, so no
caller sees a warning. Retirement order (each step unblocks the next):

1. Deletable now: dead members/fields listed in §3; add `[Obsolete]` to the
   interface; generic startup-validation service. (A library-level
   `AddMonitorCommunications` is withdrawn — it contradicts the guard-enforced
   provider split; see §4.)
   **Done 2026-07-29: `IMessageService` carries `[Obsolete]` with diagnostic
   `RVT0001`; the eight still-consuming monitor projects hold documented
   NoWarns that steps 4-5 delete. The generic validation service is withdrawn
   (see §4).**
2. One-file compat kills: retarget Omnidots' `AlertActivityTimeDto` alias;
   retarget AirQ/Svantek `NotificationDto` aliases; replace MyAtm's inverted
   adapter with the DI-registered service.
   **Done 2026-07-29: all three aliases retarget the base types; MyAtm's
   `LegacyNotificationDeliveryService` round-trip is deleted — the facade
   constructors take `INotificationDeliveryService` directly.**
3. Omnidots offline/battery → durable alerts (cheapest retirement — the
   durable stack already runs in-process); delete the inline loop.
   **Done 2026-07-29: the offline and battery handlers signal
   `IAlertIngressPort` (the ingress and acceptance policy now admit the
   transition-driven types), `OmnidotsRuleProcessor` and its inline dispatcher
   loop are deleted, and Omnidots no longer consumes `IMessageService` at all —
   its RVT0001 NoWarns are gone. AirQ, Svantek, and MyAtm's rule processors are
   the remaining step-4 targets.**
4. AirQ + Svantek alerting → durable stack; retires
   `RuleAlertNotificationDispatcher`, sync `NoiseRuleEvaluator`, sync
   `PublishAlert`.
   **Done 2026-07-29: `NoiseRuleEvaluator` is async and emits `AlertSignal`s
   via `IAlertIngressPort` (shared `RuleAlertSignals` factory; MQTT alert
   delivery rides the durable Mqtt channel, replacing the sync `PublishAlert`
   call). `RuleAlertNotificationDispatcher` is deleted; AirQ and Svantek
   register `AddDurableAlerts<TContext>` with their own context factories and
   no longer consume `IMessageService`. MyAtm's compat-only direct-delivery
   route (compat ctor, `ProcessRule`/`ProcessAlertForContacts`/
   `ProcessRulesV2`) is deleted too, so no monitor calls the sync path — all
   monitor RVT0001 NoWarns are gone. Ingress validation was also corrected to
   accept zero suppression windows (source-latched signals; the P3.c Omnidots
   handlers already passed `TimeSpan.Zero`, which the old positive-only check
   would have rejected at runtime). Both messaging-boundary allowlists are now
   empty. Step 5 is unblocked: the only remaining `IMessageService` surface is
   the contract + `MessageService` implementation inside `Rvt.Communication`.**
5. Then: delete `IMessageService`/`MessageService`, the namespace-squatting
   `RvtContactDto` + `LegacyMessageContracts`, consolidate on one contact DTO
   (~60 test files, mechanical).
   **Done 2026-07-29: `IMessageService`, `MessageService`,
   `LegacyMessageContracts`, `CommsException`, and the Abstractions-assembly
   `RvtContactDto` that squatted `Rvt.Monitor.Common.Notifications` are
   deleted, along with the Rules-side `ToNotificationDto` converter —
   `Rules.RvtContactDto` is the one contact surface. Omnidots'
   `ReadAlertContacts` query went with them (the durable stack plans contact
   deliveries itself; only stale test setups still referenced it). The ~60-file
   estimate was written before steps 3–4 migrated the monitor tests; the
   residue was 15 files. Note `Rvt.Communication.Abstractions` now holds only
   the delivery ports and notification contracts the durable stack composes.**
6. Dispatcher unification around a shared claim/lease/terminal/audit core
   (align the error-truncation divergence immediately regardless).
   **Done 2026-07-29 (scoped): `DeliveryDispatchPolicy` now owns the terminal
   decision and safe-error shaping for both dispatchers, and the alert
   dispatcher gained the 1024-character error truncation the monitor
   dispatcher already had — the divergence this step ordered fixed. The retry
   schedule was already shared (`DeliveryRetrySchedule`). A full merge of the
   two dispatch loops is withdrawn: the loops encode different product
   semantics (MyAtm's failure sink + configurable failure modes and
   fleet-level aggregate exceptions vs. the alert stack's dead-letter
   aggregation and adapter registry), each side is independently pinned by
   tests, and merging them would couple those semantics for no deletion win —
   the shared core (claim/lease fencing SQL, retry schedule, terminal/error
   policy) is already single-sourced.**
7. `RvtConfig` endgame: after the Omnidots token-seam decision and options
   binding for the remaining fields, delete the assembly-name sniffing.
   **Done 2026-07-29: the user ruled the Omnidots static-token escape hatch
   out — `OmnidotsStaticTokenClient`, `RVT__OMNIDOTS_USE_TOKEN`, and the
   `RVT__OMNIDOTS_TOKEN` credential are deleted; Omnidots always authenticates
   against the vendor. The entry-assembly/base-directory sniffing is deleted
   from `RvtConfig` and `MonitorRuntimeDefaultsResolver`: `RVT__MONITOR_KIND`
   (declared by every deployment, see `apps/monitors/docker-compose.yml`) is
   the only kind signal, and an unknown kind falls to neutral defaults.
   Converting the remaining static `RvtConfig` reads to per-host options
   binding is withdrawn: with the sniffing gone they are deterministic
   env/config lookups, and per-field options classes would churn every
   composition root for no behavioral gain.**

---

## Consolidated priority list

**P0 — guardrails (completed by PR #20)**
1. The PR test job runs the .NET and Portal client gates.
2. The five root guards run in CI. SonarQube remains intentionally manual;
   scheduling it is a separate product decision.
3. AirQ architecture tests and the shrinking Portal
   `Application → Spa.Api` guard are active.
4. `MonitorHost` threads the shutdown token through all one-shot paths.
5. Svantek has a bounded HTTP timeout with regression coverage.

**P1 — deletion sweep (zero-risk, large)**
6. Portal dead chains (~800+ lines) + small dead types + stale package refs.
7. Monitor dead code (`SmsSender.cs`, MyAtm dead wrappers, facade orphans,
   `manualtest/`, dead `RvtConfig` fields, dead lib members).
8. Frontend dead exports/component/state/CSS.
9. Repo hygiene: prune artifacts (1.4 GB), remove merged worktrees/branches,
   `git rm --cached` the tracked-but-ignored files, archive `project_state.md`
   history, delete the superseded Sonar docs/scripts.

**P2 — consolidation (the biggest debt payoff)**
10. Shared `Rvt.Monitor.TestKit`: one `TestDbClient` harness (+13% of all
    ratchet debt), shared `TestRuleActivity`/`TestUtil`/composition tests.
    **Done for `TestRuleActivity` (moved to CommonTests, policy-explicit) and
    the composition tests (shared contract driver). `TestDbClient` deferred to
    the §8 DTO retirement; the 13% debt figure is stale (now 1.4%). `TestUtil`
    stays per-monitor: its factory binds each monitor's own API/DB types.**
11. One vendor HTTP client in rvt-monitor-common (MyAtm's as the superset);
    move the Omnidots token seam to composition (needs the product decision).
    **Done: `VendorHttpTransport`/`VendorRequestPolicy` in Common; four thin
    wrappers; seam relocated to a composition decorator with behaviour
    preserved. Open: the product ruling on whether `RVT__OMNIDOTS_USE_TOKEN`
    should exist/default-on.**
12. ~~`AddRvtEmailProvider`~~ + `GetJobName` + job-map base into the library
    (kills the dual job lists and the ctor hack). **Done for the job parts
    (PR #22: `MonitorJobCatalog`, `MonitorJobArguments`). The email-provider
    part is withdrawn: it contradicts the guard-enforced provider split —
    hosts own provider selection; the ×5 block is pinned identical instead.**
13. Frontend `gridQuery.ts` + `format.ts` (pick ONE locale policy) + one
    request-lifecycle idiom; fix the Help Admin whitelist bug and regenerate/
    pin the OpenAPI schema.
14. Portal `InvalidSort` helper; shared test DbContext factory.

**P3 — convergence (needs decisions)**
15. Rule on field naming (`_camelCase` standard vs universal practice), then
    sweep; fix the intra-class mixes first.
16. Ports for Svantek/MyAtm; one job-service shape; reuse `RunFleetAsync` in
    Omnidots Veff/Vdv/Traces.
    **Done: `ISvantekVendorGateway` and `IMyAtmVendorGateway` mirror the
    AirQ/Omnidots ports; AirQ gained `IAirQMonitorJobs` so every monitor's job
    catalog binds an interface (Omnidots' provider shape stays — its durable
    jobs resolve scoped services); the four Omnidots fleet loops share
    `OmnidotsFleetImport`, which also fixes Veff/Vdv/Traces recording a
    mid-fleet cancellation as a monitor failure, and standardizes the traces
    failure message onto the `{job} serialId={id}` pattern.**
17. Execute the legacy-retirement map (§8).
18. Batch ratchet paydown: repo-wide IDE0008 fix + `--update-baseline`
    (59% of debt, mechanical), CHARSET sweep, prettier one-shot, DOC-002
    header removal on touch.
    **Done 2026-07-29 (recomputed first — this item's numbers were stale): the
    IDE0008 debt was already paid by earlier merges, and main's baseline now
    tolerates 534 violations, not 7,709. This slice ran the remaining
    mechanical dotnet-format sweep (file-scoped namespaces, primary ctors,
    collection expressions, blank-line/parenthesis prefs, unused usings,
    target-typed new), the portal CHARSET/BOM pass, a prettier one-shot, and
    renamed private fields to `_camelCase` in every file this remediation
    stack touches. Three tooling limits recorded: (a) Roslyn's naming code fix
    has no fix-all at any scope, so the repo-wide IDE1006 residue stays
    tolerated; (b) the IDE0072/IDE0010 switch-populating fixers are unsafe —
    their fix inserts throwing arms ahead of the default case (verified: it
    would have broken AuthController's status mapping) — never batch-apply
    them; (c) `--all --update-baseline` is currently blocked by main-side
    drift: a fresh scan of an untouched tree exceeds main's checked-in
    baseline (CA1067/CA1873 in portal command files this stack never touched),
    so the official baseline shrink needs a main-side pass once this stack
    lands.**
