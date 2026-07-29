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
- `GetJobName` ×5; the dispatcher "parameterless ctor throws at runtime" hack
  is now ×5 (grew); each monitor maintains the job-name list twice
  (dispatcher set + runner switch = 10 hand-maintained lists).
- Four `IHttpClient`/`HttpWebClient` families — now all async+CT, so **more**
  consolidatable, but newly inconsistent: timeouts 30 s / 30 s / **none
  (Svantek)** / 15 s; only MyAtm has retry + bounded read; phantom `<T>` in 3
  of 4; Svantek's `GetByteArrayAsync` actually sends POST; the Omnidots
  fake-auth seam survived the rewrite verbatim.
- `ClearOlderErrorMessagesHandler` ×3 (same 7-day cutoff, now with sync/async
  signature drift, all reading the clock directly).
- Test scaffolding: `TestDbClient.cs` ×4 (~6,400 lines; also 13% of ratchet
  debt), `TestRuleActivity` ×4 (AirQ↔Svantek byte-identical after rename),
  `CommunicationsCompositionTests` ×5 (4 diff lines), plus **new** duplication
  from the July refactor: `TestAirQCancellation`/`TestOmnidotsCancellation`
  twins with identical private handlers, timeout-test twins, and a copy-pasted
  `#pragma warning disable IDE0130` block in both Ports files.

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
  registration naming drift across three conventions; the design spec's single
  `AddMonitorCommunications` was never built — five hosts duplicate the block.

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
   interface; generic startup-validation service; library-level
   `AddMonitorCommunications`.
2. One-file compat kills: retarget Omnidots' `AlertActivityTimeDto` alias;
   retarget AirQ/Svantek `NotificationDto` aliases; replace MyAtm's inverted
   adapter with the DI-registered service.
3. Omnidots offline/battery → durable alerts (cheapest retirement — the
   durable stack already runs in-process); delete the inline loop.
4. AirQ + Svantek alerting → durable stack; retires
   `RuleAlertNotificationDispatcher`, sync `NoiseRuleEvaluator`, sync
   `PublishAlert`.
5. Then: delete `IMessageService`/`MessageService`, the namespace-squatting
   `RvtContactDto` + `LegacyMessageContracts`, consolidate on one contact DTO
   (~60 test files, mechanical).
6. Dispatcher unification around a shared claim/lease/terminal/audit core
   (align the error-truncation divergence immediately regardless).
7. `RvtConfig` endgame: after the Omnidots token-seam decision and options
   binding for the remaining fields, delete the assembly-name sniffing.

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
11. One vendor HTTP client in rvt-monitor-common (MyAtm's as the superset);
    move the Omnidots token seam to composition (needs the product decision).
12. `AddRvtEmailProvider` + `GetJobName` + job-map base into the library
    (kills the dual job lists and the ctor hack).
13. Frontend `gridQuery.ts` + `format.ts` (pick ONE locale policy) + one
    request-lifecycle idiom; fix the Help Admin whitelist bug and regenerate/
    pin the OpenAPI schema.
14. Portal `InvalidSort` helper; shared test DbContext factory.

**P3 — convergence (needs decisions)**
15. Rule on field naming (`_camelCase` standard vs universal practice), then
    sweep; fix the intra-class mixes first.
16. Ports for Svantek/MyAtm; one job-service shape; reuse `RunFleetAsync` in
    Omnidots Veff/Vdv/Traces.
17. Execute the legacy-retirement map (§8).
18. Batch ratchet paydown: repo-wide IDE0008 fix + `--update-baseline`
    (59% of debt, mechanical), CHARSET sweep, prettier one-shot, DOC-002
    header removal on touch.
