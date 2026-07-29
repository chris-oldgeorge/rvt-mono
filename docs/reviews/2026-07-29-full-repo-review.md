# RVT Monorepo — Full Repository Review

Date: 2026-07-29 (third full review, after PR #30, main @ `2a35068`)
Focus, as commissioned: legacy code no longer used, different functions doing
the same thing, and bad coding practices, across the entire repository.
Method: five parallel subsystem reviews (portal backend, portal frontend,
monitors, shared libs, repo-wide hygiene), each briefed with the
2026-07-28 review and the PR #20–#29 remediation so only still-open or new
findings are reported. Every dead-code claim was grep-verified repo-wide
(zero references outside the definition unless stated), and the highest-impact
claims were independently re-verified.

---

## Executive summary

The July 28–29 remediation held up under verification: **all five P0
guardrails landed** (PR test job, wired guard scripts, AirQ architecture
tests, portal boundary guard with a shrinking baseline, Svantek timeout), the
~800-line portal dead-code list is fully deleted, SIGTERM cancellation now
genuinely reaches vendor requests, and the job-catalog/vendor-HTTP/ports
consolidations are real.

What this review adds:

- **A second dead-code sweep is ready to go** (~1,200+ lines): 13 dead EF
  view-entity models with their DbSets and fluent config, the production-dead
  `MyAtmApi` facade with its shadow handler graph, a dead repository
  registration (`IAlertlevelRepository` — the same "registered, consumed by
  nothing" pattern as the previously deleted Omnidots repo), a dead
  `IMonitorRuntimeDefaultsResolver` registration ×3, ~10 dead service/port
  members, and three fully dead legacy enum members.
- **The recent consolidation PRs are each ~80% complete**, and the stragglers
  are now the sharpest duplication findings: `DataController` missed the
  InvalidSort unification (divergent error shape), ContractSitePanels kept
  private date formatters (different rendering per screen), five hand-rolled
  sort handlers and two hand-rolled request-lifecycle re-implementations
  bypass the shared hooks, and the shared `format.ts` **lost the invalid-date
  guard** the local copies had — a malformed date now crashes the panel.
- **Three small production bugs**: the Sites list passes the sort-direction
  default as the *value* instead of the fallback (`?sortDir=garbage` →
  Ascending only on that screen); a missing deployment becomes a 500 via a
  null-forgiving operator; and user delete is still a single un-confirmed
  click while company delete gets a ConfirmDialog.
- **Timezone handling is the highest-risk systemic practice issue**: AirQ's
  server-local `DateTime.Now.AddYears(-1)` seed plus two same-named
  `DateTimeUtil` classes with *opposite* UTC semantics; Svantek DB cutoffs on
  `DateTime.Now`; and the portal's own `DateTime.Today` business-window seeds
  beside an injected time provider.
- **The legacy sync messaging path (retirement steps 3–5) remains the
  dominant duplication cluster**, exactly as mapped — and the `RVT0001`
  NoWarn hygiene missed the shared libraries themselves, so every lib build
  emits undocumented obsolete warnings that erode the retirement signal.

---

## 1. Dead / legacy code (grep-verified)

### Portal backend — new deletion sweep (~600+ lines, zero behavior change)

| # | Finding | Evidence |
|---|---------|----------|
| D1 | **13 dead EF view-entity models + DbSets + fluent config (~409 lines)** — registered in `RVTSearchContext` but never queried anywhere (no DbSet access, no `Set<T>()`, no raw SQL, no test seeding): `AdminDashboardDatum`, `CustomerDashboardMonitorDatum`, `CustomerDashboardNotificationDatum`, `MonitorCurrentSearch`, `MonitorUserSearch`, `NotificationSearch`, `NotificationUserSearch`, `ReportRuleUserSearch`, `ReportUserSearch`, `SiteUserSearch`, `UsersForReportSearch`, `UsersForSiteSearch`; plus orphan class `OmnidotsTraces` (the DbSet of that name is of type `OmnidotsTrace`). | `RVT.DataAccess/Context/RVTSearchContext.cs:24-648`, `EntityModels/Models/OmnidotsTraces.cs:7` |
| D2 | **`IAlertlevelRepository`/`AlertlevelRepository` registered in DI, consumed by nothing** — identical pattern to the previously deleted `IOmnidotsBreachesAndAlertsRepository`. | `RvtPortal.Spa/ServiceCollectionExtensions.cs:181` |
| D3 | **Dead members across the legacy service/repository chain** (zero callers each): `IMonitorService.ReadAllAsync` (+ the `IMonitorRepository.ReadAllAsync` it was the last caller of), `IMonitorRepository.ReadFilteredAsync`, `IDeploymentRepository.ReadAllAsync`/`ReadFilteredAsync`/`ReadCurrentForMonitiorAsync` ×2 (note the misspelling), `ICompanyService.CompanyExist`/`ReadAllAsync`/`ReadOneWithContractsAsync` (dragging `ICompanyRepository.GetByIdWithContractsAsync` and the `GenericRepository.GetByIdAsync(Guid, string)` include-overload), `MonitorDataSearchFilters` class. | `MonitorService.cs:32,63-70`, `CompanyService.cs:20-23`, `DeploymentRepository.cs:28,34`, `GenericRepository.cs:62-71` |
| D4 | **`SearchQueryResult`'s error channel can never fire** — every constructor passes `wasSuccessful: true`, so `CompanyApplicationService.cs:140-143` and the `ErrorMessage` mapping in `CompaniesController.cs:50-56` are dead branches and `IOperationResult` has no consumer as an abstraction. | `MonitorService.cs:379`, `SearchQueryExecutor.cs:55,66` |
| D5 | Residue: orphaned section comments in `MonitorService.cs:38-44,60`; empty `<ItemGroup>` in `RVT.Entities.csproj`; vendored AForge `Complex.cs` (1,116 lines) of which the FFT path uses a fraction. | — |

### Monitors

| # | Finding | Evidence |
|---|---------|----------|
| D6 | **`MyAtmApi` facade (188 lines) is production-dead and maintains a shadow handler graph.** `GetRequiredService<MyAtmApi>` has zero matches; the singleton registration at `MyAtmMonitorServices.cs:138` is resolved by nothing; production uses only the static constant `MyAtmApi.JAN1_1970`. The facade hand-news its own copies of the gateway, reader, rule processor, and all six handlers (with a hardcoded `TimeProvider.System` and fallback URL) — a second composition graph that can drift silently from the DI-registered one. Constructed only by `MyAtmMonitorTests/TestUtil.cs:60`. Move `JAN1_1970`, delete the registration, demote or remove the facade. | `api/MyAtmApi.cs:80-124` |
| D7 | **MyAtm's last `GetAwaiter().GetResult()` sits in a compat-dead legacy route** (`ProcessRule`/`ProcessAlertForContacts`, guarded by `RequireLegacyDependencies()`, whose legacy deps are null in production DI — the route throws if reached; an architecture test *enforces* that nothing calls it). Deleting the two methods removes MyAtm's last sync-over-async site and its `IMessageService` dependency ahead of the retirement schedule. | `MyAtmRuleProcessor.cs:108-135,275` |
| D8 | **`OmnidotsRuleProcessor.portalBaseUrl` is injected, stored, never read** — line 63 reads `RvtConfig.PORTAL_BASE_URL` directly instead; the constructor parameter is a decoy seam. One-line fix. | `OmnidotsRuleProcessor.cs:19,25,30,63` |

### Shared libs

| # | Finding | Evidence |
|---|---------|----------|
| D9 | **`IMonitorRuntimeDefaultsResolver` registered in all three `MonitorHost` modes, resolved by nothing** anywhere in the repo (test-only direct construction aside). Looks like the intended DI replacement for `RvtConfig` statics that was never adopted — either adopt it or delete the registrations and fold it into the retirement-step-7 endgame. | `Hosting/MonitorHost.cs:101,123,155` |
| D10 | **Three fully dead legacy enum members deletable today with no test edits**: `LegacyMessageKind.Password_Forgotten`/`Report_Weekly`/`Report_Monthly` (zero references outside the declaration). `Password_Set` and `LegacyMessageChannel.Both` are rejection-test-only, slated for step 5. | `Rvt.Communication.Abstractions/LegacyMessageContracts.cs:6,12,13` |

### Frontend

| # | Finding | Evidence |
|---|---------|----------|
| D11 | The prior review's frontend dead-code list is fully resolved (8 dead client exports, `ErrorSummary`, `authError`, `.metrics` CSS — all gone). Remaining: `downloadFile` is exported solely for tests. | `src/api/client.ts:930` |
| D12 | `@vitejs/plugin-react` sits in `dependencies` instead of `devDependencies`. | `package.json:22` |

### Legacy sync messaging path — status (retirement steps 3–5, expected open)

Confirmed intact and unchanged in shape: the Omnidots **inline dispatcher
loop** (`OmnidotsRuleProcessor.cs:33-119`, reached from offline + battery
handlers — Omnidots remains split-brain: durable alerts for measurements,
legacy sync for offline/battery); AirQ (`AirQRuleProcessor.cs:183`) and
Svantek (`SvantekRuleProcessor.cs:163`) via `RuleAlertNotificationDispatcher`.
With it survive `MessageService` (including the `Sendmessage`/`SendMessage`
typo-twins), sync `MonitorEventPublisher.PublishAlert` (sync-over-async), sync
`NoiseRuleEvaluator`, and the twin contact DTOs bridged by a one-way
converter. Step 3 (Omnidots offline/battery → durable, the cheapest
retirement — the durable stack already runs in that process) is untouched.

---

## 2. Different functions doing the same thing

### Stragglers from the July consolidations (cheap, restores claimed invariants)

| # | Finding | Evidence |
|---|---------|----------|
| C1 | **`DataController` missed the InvalidSort consolidation** — two inline hand-rolled `ProblemDetails` with a *different* title ("Unsupported sort field" vs canonical "Invalid sort field"), no `correlationId`, no `allowedSortFields`; plus the two `ToProblemResult` overloads duplicating each other's failure mapping in the same file. PR #25 touched ten controllers but not this one. | `Api/DataController.cs:152-157,171-177` |
| C2 | **ContractSitePanels kept private `formatDate`/`formatDateTime`**, bypassing `format.ts` with a *different* rendering (numeric `dd/mm/yyyy` vs shared `medium`) — the "same timestamp renders differently per screen" complaint survives between Sites/Contracts and everything else. | `ContractSitePanels.tsx:1797-1817` |
| C3 | **`useGridSortHandler` adoption incomplete**: five identical hand-rolled copies remain (AdminPanels ×2 — which doesn't even import the hook — DashboardPanels, MonitorPanels ×2). | `AdminPanels.tsx:259-263,754-758`, `MonitorPanels.tsx:301-305,1016-1020`, `DashboardPanels.tsx:211-215` |
| C4 | **Request-lifecycle consolidation partial**: only 4 panels adopted `useRequestLifecycle`; `HelpAdminPanel` (`currentExecutionRef`) and `AdminPanels` (`refreshVersion` counter) re-implement exactly what the hook provides; two more idioms coexist elsewhere. Also: the one-line execution type is re-declared 7× under two names and three shapes. | `HelpAdminPanel.tsx:97,116-160`, `AdminPanels.tsx:83-86,578-580` |
| C5 | One test file missed by the DbContext-factory consolidation (hand-built options character-identical to `TestDbContexts.ModelOnlyNpgsql`). | `RvtPortal.Spa.Tests/MonitorListReaderSqlTests.cs:47-48` |

### Durable dispatchers (retirement step 6 — the largest remaining shared-lib duplication)

`DurableAlertDispatcher` vs `MonitorDeliveryDispatcher` still duplicate:
`IsTerminal` (identical expressions), the whole batch claim/lease/timeout/
ownership-loss skeleton, dead-letter audit construction, and ownership-loss
logging. Divergences that are *behavioral*, not just stylistic:

- **Error truncation is now three-way**: delivery dispatcher caps at 1,024
  (`MonitorDeliveryDispatcher.cs:11,310-316`); alert dispatcher doesn't
  truncate but its store caps at 256 (`EfAlertOutboxStore.cs:21,99-101`) —
  and the alert dead-letter *audit* gets the untruncated string while its own
  outbox row is cut to 256. Align the policy now even if unification waits.
- **Identity hashing has three schemes** (SHA256→Guid plain-UTF8 no version
  bits / SHA256→hex strict / SHA256→RFC-4122 Guid strict big-endian):
  `MonitorDeliveryIdentity.cs:8-12`, `AlertDeliveryIdentity.cs:12-27`,
  `AlertIdentity.cs:24-45`.
- Alert side uses injected `TimeProvider`; delivery side reads
  `DateTime.UtcNow` directly. `.ConfigureAwait(false)` on one side only.
- The `AlertType → NotificationMessageKind` switch is copied verbatim
  (`EmailAlertDeliveryAdapter.cs:37-45` vs `MonitorDeliveryDispatcher.cs:289-297`);
  the portal notification-URL is built in **four** homes with a
  slash-handling split (durable paths `TrimEnd('/') + "/…"`, legacy paths
  depend on the default's trailing slash).
- Claim semantics implemented twice with different mechanisms *and status
  literals*: raw SQL `FOR UPDATE SKIP LOCKED` + `'Leased'`
  (`AlertOutboxClaimSql.cs`) vs MyAtm's EF conditional-update loop +
  `"InProgress"` (`MyAtmMonitor/api/db/DBClient.cs:503-570`).

### Cross-monitor (still open)

| # | Finding | Evidence |
|---|---------|----------|
| C6 | `ClearOlderErrorMessagesHandler` ×3 — AirQ/Omnidots byte-identical async; MyAtm drifted to sync `void Run()` with **no cancellation token** (its catalog entry accepts one and can't pass it down); all three hard-code the 7-day cutoff and read the clock directly despite injecting `TimeProvider` elsewhere. | three `UseCases/ClearOlderErrorMessagesHandler.cs` |
| C7 | **AirQ dual `DateTimeUtil` with opposite UTC semantics** — local `ToUtc` treats `Unspecified` as server-local and *shifts* it; Common `AsUtc` stamps it as already-UTC. Bound in exactly two files (`NoiseDto.cs:23`, `AirQHttpGateway.cs:203`), so AirQ vendor timestamps are interpreted differently from every other timestamp in the same monitor, dependent on container timezone. Merge after ruling which semantic is right for the vendor's field. | `AirQMonitor/common/DateTimeUtil.cs:4` vs `Rvt.Monitor.Common/Utilities/DateTimeUtil.cs:7` |
| C8 | Battery thresholds duplicated Svantek↔Omnidots with clashing naming conventions (PascalCase consts vs SCREAMING_SNAKE). Same values, same cascade. | both `NotifyBatteryLevelsHandler.cs` |
| C9 | Refactor-era test twins: HTTP-timeout tests ×3 and cancellation tests ×3 are near-identical after vendor-name substitution — candidates for the contract-driver treatment `CommunicationsCompositionContract` got, now that all four clients delegate to one `VendorHttpTransport`. `TestDbClient.cs` ×4 (6,394 lines) stays deferred per the standing ruling. | `Test*HttpTimeout.cs`, `Test*Cancellation.cs` |
| C10 | New instances of "same concern, N shapes": API-key validation in three unrelated shapes (AirQ validator / Omnidots guard+options / Reporting filter); `LivenessText()` one-liner ×5. | `AirQApiKeyValidator.cs`, `OmnidotsApiSecurityGuard.cs`, `InternalApiKeyFilter.cs` |
| C11 | Three communication options classes carry byte-identical `Get`/`ParseBoolean`/`Require` helpers (~30 lines ×3); the dependency-free guard on Abstractions blocks the obvious home, but a shared source file or pinning would work. | `TransmitSmsOptions.cs:46-74`, `SendGridMailOptions.cs:46-74`, `MicrosoftGraphMailOptions.cs:45-70` |

### Portal backend / frontend (still open)

| # | Finding | Evidence |
|---|---------|----------|
| C12 | Paging models now **three-way**: `Paging` (Entities) plus two **byte-identical** `sealed record PageRequest` in `RVT.BusinessLogic` and `RvtPortal.Application`, each with live consumers. | `Global.cs:14`, both `PageRequest.cs` |
| C13 | Report-frequency logic ×2 with a semantic divergence: `FrequencyLabel` byte-identical, but `MatchingFrequencies` includes `Off` in one copy and excludes it in the other — text search for "off" behaves differently per screen. | `ReportRuleApplicationService.cs:759-770`, `ReportApplicationService.cs:166-177` |
| C14 | Repeated literals: `"Auth:SkipPasswordResetEmail"` ×5, `"Site not found"` ×4 in two shapes. | `AuthApplicationService.cs:224,534` etc. |
| C15 | Frontend: `DetailItem` ×4 (one drifted: renders `'None'` vs empty) + a fifth sibling (`ReadOnlyRow`); `'https://rvt.local'` ×24 with two verbatim idioms that a two-function helper in `navigation.ts` would erase; per-resource `build<X>Url` ×11 with two divergent URL conventions (defaults omitted vs always written); `GridSortDirection` duplicating `SortDirection`. | see file list in §frontend agent notes |
| C16 | CI: setup preamble repeated across 5 jobs/3 workflows (composite-action candidate); two contract tests run twice per PR (explicit steps + glob). | `.github/workflows/*` |
| C17 | `MonitorHost` triplicates its per-mode composition block (which is exactly how the dead resolver registration got cloned ×3 — D9). | `MonitorHost.cs:99-103,122-126,154-157` |

---

## 3. Bad coding practices

### Production bugs (fix now, all small)

| # | Finding | Evidence |
|---|---------|----------|
| P1 | **Sites list sort-direction bug**: `normalizeSortDirection(initialParams.get('sortDir') ?? 'Descending')` passes the intended default as the *value*, so the fallback stays `'Ascending'` — `?sortDir=garbage` yields Ascending on this screen, Descending on its siblings that pass the default correctly as arg 2. One-line fix. | `ContractSitePanels.tsx:613-614` |
| P2 | **Shared `formatDate`/`formatDateTime` throw on malformed input** — the invalid-date guard the pre-consolidation local copies had (`Number.isNaN(date.getTime())`) was lost in PR #25's `format.ts`; a malformed non-empty string now throws `RangeError` in a render path and takes down the panel via the error boundary. | `src/format.ts:11-24` |
| P3 | **Missing deployment → 500**: `(await monitorService.DeploymentReadOneAsync(DeploymentId))!` then immediate dereference — unknown/unauthorized id is an NRE instead of a 404, four lines from an explicit `monitor == null` check. (Same method: 12-parameter static signature.) | `MonitorData.cs:221` |
| P4 | **User delete is one un-confirmed click** — the confirmation UX has three tiers: `ConfirmDialog` (company/help/contract/site-archive/monitor-remove), `globalThis.confirm` (alert-level, report-rule), and *nothing* for the most consequential: user delete (row action → `deleteUser` directly), remove-user-from-site, remove-report-recipient, remove-monitor-assignment. | `AdminPanels.tsx:669-670,799` |

### Timezone hazards (systemic — one theme, both sides of the repo)

| # | Finding | Evidence |
|---|---------|----------|
| P5 | AirQ first-import seed uses **server-local** `DateTime.Now.AddYears(-1)` against a UTC pipeline; beside it a no-op `catch (AggregateException) { throw; }`. Both survived the async rewrite. Combined with C7 (dual `DateTimeUtil`), AirQ timestamp handling depends on the container's timezone. | `StoreNoiseLevelsHandler.cs:61,131-134` |
| P6 | Svantek: `DateTime.Now.AddHours(-12)` cutoffs in DB queries against UTC data; `ListedAtTime = DateTime.Now` persisted; local-time sentinel `DateTime.Now.AddDays(1)` compared to UTC values. | `SvantekMonitor/api/db/DBClient.cs:731,763`, `StoreMonitorsHandler.cs:64`, `SvantekRuleProcessor.cs:94` |
| P7 | Portal twin: `DateTime.Today.AddDays(±1).LocalToUtc(dateTimeProvider)` — server calendar day reinterpreted as business-timezone ticks; on a UTC container with non-UTC `TimeZones:Local` the default data window shifts by the offset. ×5 sites. | `MonitorData.cs:223-224,306`, `DashboardController.cs:176`, `DashboardApplicationService.cs:779` |
| P8 | Omnidots: three sibling handlers kept the `DateTime.Now` stopwatch idiom while `StoreTracesHandler` was converted to `timeProvider.GetTimestamp()` in the same PR #28 refactor — intra-refactor drift. | `StorePeakRecordsHandler.cs:162-164`, `StoreVdvRecordsHandler.cs:69-71`, `StoreVeffRecordsHandler.cs:68-70` |

### Swallowed errors / logging gaps

| # | Finding | Evidence |
|---|---------|----------|
| P9 | `catch { return null; }` with no OCE filter and no logging — cancellation and real failures both degrade silently to "no metric". (The sibling `SiteArchiveAdapter` got the OCE filter; this didn't.) | `MonitorDetailSummaryService.cs:165-168` |
| P10 | `SiteArchiveAdapter` maps any failure to a fixed message with no logger injected — improved (OCE rethrown) but still undiagnosable. | `SiteArchiveAdapter.cs:27-30,50-53` |
| P11 | Omnidots endpoints: `catch (Exception)` logs *without the exception object* — a 500 leaves no stack trace. | `MonitorApiEndpoints.cs:91-97,167-174` |
| P12 | Delivery dispatcher's failure-sink catch discards the second-order exception — precisely the diagnostic needed when audits silently stop appearing. | `MonitorDeliveryDispatcher.cs:250-255` |
| P13 | `MonitorHost` one-shot failure path writes only `exception.Message` to `Console.Error` — stack trace and type never reach the logger/OTel pipeline the host just set up; `MonitorJobCatalog.cs:59` likewise reports errors via `Console.Error` from library code. | `MonitorHost.cs:80-84` |
| P14 | Frontend: unaborted status fetch with silent catch (stale response can win; API failure renders as "no status"); initial `getCurrentAuth()` treats API-unreachable as "logged out". | `MonitorPanels.tsx:441-443`, `App.tsx:329-337` |

### Behavioral inconsistencies & config hygiene

| # | Finding | Evidence |
|---|---------|----------|
| P15 | **"OmniDots guest" filter still inconsistent**: guest skip present in Peak/Vdv/Veff (magic string ×3) but absent from `StoreTracesHandler` — guest traces are still fetched and stored. The `OmnidotsFleetImport` convergence was the natural moment to unify and didn't. Behavioral, not stylistic. | `StoreTracesHandler.cs:133` |
| P16 | Personal email `haakan.eriksson@cellsoftware.co.uk` and `AllowedSerialIds: ["23423"]` + `MaxMonitorsPerRun: 1` still committed as Omnidots defaults. | `OmnidotsMonitor/appsettings.json:13,21-24` |
| P17 | Unexplained `libgssapi-krb5-2` still in Svantek + MyAtm Dockerfiles — a Kerberos dependency left over from the previous database engine, before the guard-enforced PostgreSQL-only cutover. airq is still the only compose service without a port mapping (8081 absent). | both `Dockerfile:10`, `docker-compose.yml` |
| P18 | Monitor Dockerfiles use floating `sdk:10.0`/`aspnet:10.0` tags while the client and runner images are digest-pinned; two PR workflows use `dotnet-version: 10.0.x` while sonarqube pins via `global.json` (10.0.302) — CI can silently build with a different SDK than the repo pins. | five monitor Dockerfiles, `tests.yml`, `engineering-standards.yml` |
| P19 | Averaging-period magic numbers (900/3600/86400) still inline ×6 in both rule processors; the same vocabulary is separately hand-encoded in the portal (`MonitorData.cs:387-448` + `MonitorDetailSummaryService.cs:191-199`) — two layers, three encodings of one protocol. | `AirQRuleProcessor.cs`, `SvantekRuleProcessor.cs` |
| P20 | InMemory-provider branching in production code **grew** (2 → ≥5 sites, including an entire alternate non-transactional code path in `AuthApplicationService.ConfirmEmailChangeWithoutTransactionAsync`) — test infrastructure shaping production control flow. | `EfHelpReadAdapter.cs:103,205`, `AuthApplicationService.cs:375-400`, etc. |
| P21 | Obsolete-plumbing hygiene: the shared libs themselves lack the documented `RVT0001` NoWarn (7 undocumented warnings per full build — trains people to ignore the retirement diagnostic); the sync *members'* `[Obsolete]` lacks a `DiagnosticId`, so tests suppress `CS0618` while the interface uses `RVT0001` — two warning IDs for one legacy path. | `Rvt.Communication`, `RuleAlertNotificationDispatcher.cs:11,16` |
| P22 | New shared-code traps: `IMonitorEventPublisher.PublishAlertAsync`'s default implementation delegates async→sync (any minimal implementation silently gets blocking semantics and a discarded token; the inversion should run the other way); `VendorHttpResponse.ReadStringAsync` honors the response charset unbounded but hard-codes UTF-8 when a byte bound is set. | `Mqtt/MonitorEventPublisher.cs:20-29`, `Http/VendorHttpResponse.cs:29-38` |
| P23 | Timing-based negative assertion in a portal test (`Task.Delay(250)` as a "nothing happened" window) — same family as the help-admin flake fixed in `c913c12`. | `SiteArchiveServiceSecurityTests.cs:114` |

### Architecture & style (needs rulings, not sweeps)

| # | Finding | Evidence |
|---|---------|----------|
| P24 | **Field-naming ruling still missing** — the single largest baseline rule (IDE1006, 1,066 of 2,072 diagnostics = 51%). The standard says `_camelCase`; 0 of 95 portal instance-field files comply; the static-only pass created intra-class mixes in the portal (the exact defect it created in the monitors last time); new lib files split both ways (Http files comply, dispatchers don't). Until someone amends the standard or escalates the `.editorconfig` rule past `suggestion`, every new file deepens the split and the debt cannot ratchet down. | `docs/development/engineering-standards.md:178`, `.editorconfig:17-31` |
| P25 | The boundary guard's blind spot: `HostApplicationLayerBoundaryTests` scans only `Application/` (27-file shrinking baseline — down from 31, working as designed), but `Adapters/` can regress freely — and the known `ReportGenerationClient`/`ReportGenerationGateway` inbound-DTO leak lives exactly there. Extend the guard with a two-file baseline. | `Adapters/Reporting/ReportGenerationClient.cs:9` |
| P26 | `RvtConfig` endgame (step 7) still open: assembly-name sniffing now in **two** places (`RvtConfig` + `MonitorRuntimeDefaultsResolver`), and `RvtConfig.BuildConfiguration` builds a second private config root that can disagree with `MonitorHost`'s (env-vars/command-line included on one side only). Facades still hand-new handler graphs in 3 of 4 vendor monitors; Omnidots facade builds a second gateway from `RvtConfig` bypassing options binding. | `RvtConfig.cs:14-17,35-46,104-141`, `OmnidotsApi.cs:95` |
| P27 | The copy-pasted `#pragma warning disable IDE0130` block has spread from 2 to **17 files** and is growing with every new file — a per-folder `.editorconfig` override would delete all 17 and stop the growth. Dated "Major updates:" headers grew 384 → ~424 files and were copied into every brand-new 2026-07-29 lib file; `// Function summary:` noise is flat at ~2,150. | grep counts, `VendorHttpTransport.cs:6-9` |
| P28 | Guardrail residuals: workflows are `pull_request`-only — nothing runs on a direct push to main (relies entirely on branch protection); the ratchet never tightens automatically (regressing back to a stale baseline entry still passes); no `BannedApiAnalyzers` (3 production `GetAwaiter().GetResult()` sites remain, only AirQ has a source-scan guard); no `no-console` ESLint rule (currently moot, unguarded); remaining `.editorconfig` rules at `suggestion`. | `tests.yml:3`, `verify.mjs:1174-1178` |
| P29 | **OpenAPI schema is stale**: `schema.d.ts` (6,367 lines) has zero Help entries while `HelpController` exists; nothing in CI regenerates or diff-checks it (generation needs a live server). The Help Admin whitelist bug this caused is fixed, but the root cause — an unguarded generated artifact — remains. | `src/api/schema.d.ts` |

---

## Consolidated priority list

**P0 — production bugs & one-line fixes**
1. Frontend sort-direction default bug (P1) — one line.
2. Restore the invalid-date guard in `format.ts` (P2) + malformed-input test.
3. Null-forgiving deployment dereference → 404 (P3).
4. Add confirmation to user delete (and the other tier-3 destructive actions) (P4).
5. `DataController` → `ApiProblems.InvalidSort` (C1).
6. Align dispatcher error-truncation policy (step-6 down-payment).
7. `RVT0001` NoWarn hygiene in the libs + `DiagnosticId` on the sync members (P21).

**P1 — deletion sweep #2 (zero behavior change, ~1,200+ lines)** — **DONE 2026-07-29** except item 11 (see below)
8. Portal: 13 dead EF view entities + `OmnidotsTraces` + config (D1), `IAlertlevelRepository` (D2), dead service/port members (D3), dead result-channel branches (D4).
   **Done.** ~1,600 lines removed in total across the sweep. D1 also required updating
   the current model snapshot and the `CanonicalNames.approved.txt` golden file
   (approval test unchanged, removals only). D4 was executed narrowly: only the
   unreachable branches went; `SearchQueryResult.WasSuccessful`/`ErrorMessage`
   stayed because `SearchQueryReader` still propagates them and removing them
   would have been a signature change rippling through tests, not a deletion.
   Cascade found and closed during the sweep: `ICompanyRepository.ReadAllAsync`
   and `GenericRepository.ReadAllAsync` became dead once `ICompanyService.ReadAllAsync`
   went. `ICompanyRepository.ReadFilteredAsync` deliberately kept — it is test-only
   but the tests are genuine filter/sort-validation coverage.
9. Monitors: `MyAtmApi` facade + registration (D6), compat-dead legacy route in `MyAtmRuleProcessor` (D7), `portalBaseUrl` decoy (D8).
   **Partially done — and the D6 premise was wrong.** The *registration* is deleted
   and `JAN1_1970` moved to the existing `DateTimeUtil.JAN1_1970`, so `MyAtmApi` now
   has zero production references. The **facade itself was kept**: re-verification
   showed it is the test subject of 7 files / 1,449 lines across ~34 call sites and
   4 constructor overloads, exercising real vendor-paging and watermark behaviour
   that `MyAtmService` cannot express (it hardcodes `customerId` and `Period`).
   Retiring it is a test-migration exercise, not a deletion. D7 done — including
   `ProcessRulesV2` (it called `ProcessRule`), the `portalBaseUrl` cascade, MyAtm's
   last production `GetAwaiter().GetResult()`, and its `RVT0001` NoWarn; the
   architecture invariant was **strengthened**, not weakened (a new test pins that
   `MyAtmRuleProcessor` itself contains no synchronous delivery route, and the
   `.SendMessage(` allowlist narrowed to Omnidots only). D8 fixed rather than
   deleted, per the smaller-correct-change rule.
10. Libs: `IMonitorRuntimeDefaultsResolver` registrations (D9), three dead enum members (D10).
    **Done.** The resolver *types* were kept (still exercised by `RvtConfigTests` and
    part of the pending `RvtConfig` retirement); only the three unused DI
    registrations went. The three enum members needed no mapper change — they
    already fell through to the existing throwing default arm.
11. Config hygiene: personal email + serial pin in Omnidots appsettings (P16), Kerberos lib ×2, airq port mapping (P17). **Not done — deliberately deferred.** These change runtime configuration (alert recipient, device allowlist, image contents, port exposure) rather than delete dead code, so they need a product/ops decision, not a sweep.

Verification of the sweep: full `Rvt.Mono.slnx` build clean (0 errors; the 13
`RVT0001` warnings are the pre-existing P21 finding, none in `apps/`); **2,362
tests pass across all 15 projects with zero failures and zero skipped**, run
against real PostgreSQL — including the search aggregate-view query test and
startup schema validation, which is what makes the EF view-entity deletion
safe; all five repository guards pass; 15/15 contract tests pass; and the
engineering-standards ratchet passes with no policy violations.

Two notes for whoever runs this next:

- **The two integration suites read differently-named connection variables.**
  Monitor tests require `RVT__POSTGRES_INTEGRATION_CONNECTION` and *fail* when
  it is unset (by design); portal tests read `RVT_TEST_POSTGRES_CONNECTION` and
  *skip* when it is unset. Set both, or a run can look green while 11 portal
  integration tests silently sat out. Only `RVT__POSTGRES_INTEGRATION_CONNECTION`
  is documented in the verification-environment section above.
- **Touching a file that carries a UTF-8 BOM trips the ratchet.** `CSH-002` is an
  immediate violation, so pre-existing `charset` debt in an otherwise
  deletion-only diff blocks the gate; three files needed their BOM stripped
  (content untouched). Worth a repo-wide BOM sweep so this stops ambushing
  unrelated changes.
- **`tests/verify-engineering-standards.test.sh` is load-dependent, and the
  failure message misdescribes it.** The "same numeric PID without the sentinel
  token was treated as the owner" assertion is really a **0.4-second wall-clock
  budget**: it backgrounds `verify --all --update-baseline`, sleeps 0.4 s, and
  fails if that process is still alive. On an idle machine it passes; under load
  it fails deterministically (measured 0/8 here, and **0/3 on unmodified
  `origin/main`**, so it is pre-existing and independent of any change). Because
  `tests/*.test.sh` runs as a glob in the `Tests` workflow, this makes CI
  sensitive to runner speed. The fix is to assert lock *reclamation* rather than
  elapsed time.

**P2 — finish what the July PRs started**
12. Frontend: adopt `useGridSortHandler` ×5, `useRequestLifecycle` in HelpAdmin/AdminPanels, delete ContractSitePanels' private formatters, `routeSearchParams`/`routePathname` helpers, shared `DetailItem` (C2–C4, C15).
13. Guest-trace filter decision + unify in `OmnidotsFleetImport` (P15).
14. Swallowed-error fixes: P9–P13 (mechanical, ~6 sites).
15. Timezone ruling: provider-supplied "business today"/UTC seeds at the 10+ `DateTime.Now`/`Today` sites (P5–P8) — decide once, apply everywhere; merge AirQ's dual `DateTimeUtil` (C7).
16. Extend the boundary guard to `Adapters/` (P25); Dockerfile/SDK pinning (P18).

**P3 — the standing programs (already mapped, reaffirmed)**
17. Legacy retirement steps 3–5 (kills the dominant duplication cluster: the sync path, the inline Omnidots loop, the contact-DTO twins, `ClearOlderErrorMessagesHandler` drift).
18. Dispatcher unification (step 6) around a shared claim/lease/terminal/audit core; one identity-hashing scheme; one URL builder.
19. `RvtConfig` endgame (step 7): adopt-or-delete the defaults resolver, one config root, facade composition through DI.
20. Field-naming ruling (P24) — the 51%-of-debt decision; then the IDE0130 pragma strategy (P27) and DOC-002 header sweep.
21. OpenAPI schema regeneration + CI diff-check (P29); test-twin contract drivers (C9); ratchet auto-tighten + push-to-main gate (P28).
