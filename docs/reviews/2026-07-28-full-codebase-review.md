# RVT Monorepo — Full Codebase Review

Date: 2026-07-28
Scope: `apps/portal` (backend + React client), `apps/monitors` (all five hosts),
`libs/rvt-monitor-common`, `services/reporting`, repo-root governance.
Method: five parallel subsystem reviews against the documented hexagonal /
ports-and-adapters baseline (`docs/architecture/portal/ports-and-adapters-catalog.md`,
`docs/history/monitors/specs/2026-07-16-common-communications-ports-and-adapters-design.md`,
`docs/development/engineering-standards.md`). Every finding was verified in source;
file:line references were live at review time.

---

## Verdict

The architecture documentation is unusually good and roughly half the estate
genuinely lives on it. The portal's extracted slices, the communications
ports-and-adapters in `libs/rvt-monitor-common`, and the ReportingMonitor /
MyAtmMonitor hosts faithfully implement the documented hexagonal shape, with
real executable architecture guard tests. The problems are concentrated and
legible:

1. One organizational defect that dwarfs everything else — the **duplicated
   reporting stack** (`services/reporting` vs `apps/monitors/reportingmonitor`).
2. Two monitors that never completed the migration (**AirQ**, **Omnidots**).
3. A shared-library hub (**`Rvt.Monitor.Common`**) that is still the static
   grab-bag the design set out to dismantle.

---

## Critical findings

### C1. The reporting stack is maintained twice; the abandoned copy has the defects

`services/reporting/src/Rvt.Reporting.*` and
`apps/monitors/reportingmonitor/Rvt.Reporting.*` are parallel forks:

- Seven files byte-identical (`ReportModels.cs`, `OneTimeReportContracts.cs`,
  `ReportInsightBuilder.cs`, `ReportPeriodCalculator.cs`, `RVTlogo.svg`,
  `SpaCustomerLogoClient.cs`, `OllamaReportNarrativeProvider.cs`), plus a
  near-identical `QuestPdfReportRenderer.cs` and 8 duplicated Core test files.
- Where they diverge, only the monitor copy received the fixes (doc comments
  dated 2026-06-29): fail-closed constant-time API-key auth, per-rule error
  isolation, atomic report persistence, narrow ports
  (`IReportingRuleQueries` / `IReportingDataQueries` /
  `IReportingGenerationLocks` / `IReportingGenerationCommands`), batched
  recipient queries.
- Both copies are actively touched — the current uncommitted `RVTlogo.svg`
  change exists in **both** trees (dual maintenance happening right now).

Consequences retained in the `services/reporting` copy:

- **Auth fails open** — with the shipped default empty `RVT:INTERNAL_API_KEY`
  (`appsettings.json`), a request carrying an empty `X-RVT-Internal-Key`
  header passes `Rvt.Reporting.Service/Api/InternalApiKeyFilter.cs:23-35` in
  non-Development environments; the comparison is also `string.Equals`, not
  constant-time. The monitor fork fixed both (fail-closed +
  `CryptographicOperations.FixedTimeEquals`).
- **Duplicate emails on partial failure** — report insert, per-recipient send,
  and `last_generated` update run as separate auto-commit statements on
  separate connections (`ReportGenerationService.cs:125-147`,
  `PostgresReportingRepository.cs:156-203`); a crash mid-loop makes the rule
  due again with no idempotency check. The fork made this a single EF
  transaction.
- **One failing rule aborts the whole scheduled run** — no per-rule try/catch
  (`ReportGenerationService.cs:43-46`); fixed in the fork.

### C2. AirQ and Omnidots block on `.Result` with no timeout and no cancellation

- `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/http/OmnidotsHttpGateway.cs:42,68,75,82,89,99,109,115`
  — `.Result` on every core import call; `IHttpClient` accepts no
  `CancellationToken`; `.WaitAsync(ct)` abandons rather than cancels.
- `apps/monitors/airqmonitor/AirQMonitor/api/http/AirQHttpGateway.cs:28,41,50,68`
  — same pattern; `api/http/HttpWebClient.cs:15` is a raw `new HttpClient()`
  with the 100 s default timeout and no CT parameter.
- The Quartz/one-shot cancellation token is accepted then **discarded**:
  `airqmonitor/.../MonitorJobDispatcher.cs:33-42` (all AirQ jobs),
  `omnidotsmonitor/.../MonitorJobRunner.cs:28-51` (8 of 11 Omnidots jobs).
  SIGTERM in containers hard-kills mid-write instead of stopping cleanly.

### C3. AirQ/Svantek alert delivery: one attempt, no retry, one bad contact aborts the rest

- Both use the legacy synchronous path through
  `libs/.../Rules/RuleAlertNotificationDispatcher.cs`: single send attempt per
  contact; only `CommsException` caught; failure audited then dropped forever.
- A contact with SMS enabled but a **null phone number** throws
  `ArgumentException` — not a `DeliveryException` —
  (`Rvt.Communication/MessageService.cs:39-49` →
  `NotificationDeliveryService.cs:15`), so a plain data condition escapes the
  dispatcher (`RuleAlertNotificationDispatcher.cs:134-137` catches only
  `CommsException`), skipping all remaining contacts with no audit. No test
  covers this path.
- MyAtm and Omnidots already use the durable outbox/dead-letter dispatcher.

### C4. Transient Entra ID outages dead-letter alerts as "Permanent"

`libs/.../Rvt.Communication.MicrosoftGraphMail/AzureIdentityGraphAccessTokenProvider.cs:37-46`
— an `AuthenticationFailedException` wrapping a network error (inner not a
`RequestFailedException`) is classified Permanent; the adapter catch-all
(`MicrosoftGraphEmailAdapter.cs:337-343`) does the same. The design spec
requires network failures to be transient; an AAD blip dead-letters every
in-flight alert with no retry.

### C5. PDF heatmap clips for the product's headline report lengths

`Rvt.Reporting.Pdf/Documents/QuestPdfReportRenderer.cs:348-392` (identical in
both copies): fixed 640×190 viewBox, rows at `y = 20 + dayIndex * 20` — day 9
onward falls outside the viewBox, so monthly/31-day reports silently lose most
of the heatmap. Related renderer issues:

- Culture-sensitive formatting at lines 122, 172, 465 (thresholds render as
  "42,5" under a comma-decimal container locale) while other call sites use
  `InvariantCulture`.
- `BuildReportChrome` (117-123) explicitly discards `reportName`, `fromUtc`,
  `toUtc` — the PDF never shows the report name or period.
- `QuestPDF.Settings.License = LicenseType.Community` hardcoded at line 26 in
  the render hot path — needs an explicit compliance decision (Community is
  lawful only below the $1M revenue threshold) and belongs in composition.
- `FindRvtLogoPath` (125-140) walks upward from the base directory to the
  filesystem root — environment-dependent; should be an embedded resource.

---

## High-priority findings

### Portal backend (`apps/portal` .NET)

- **[H] Master-admin restore backdoor on every boot** —
  `RvtPortal.Spa/Data/SeedDatabase.cs:82-184`: on every start,
  `master@rvtGroup.com` is re-enabled if disabled, lockout cleared,
  `EmailConfirmed` forced, and the password reset whenever
  `RVT_PORTAL_SEED_MASTER_ADMIN` is set and differs. Deliberate operator
  lockouts are silently undone; a stale/leaked seed variable is a standing
  credential reset. Make recovery opt-in (second explicit flag) and log at
  Warning; seeding failures are also downgraded to warnings (75-78).
- **[M] Host application layer coupled to the inbound API DTO namespace** — 31
  files under `RvtPortal.Spa/Application/**` import `RvtPortal.Spa.Api` and
  return API DTOs directly (e.g. `Application/Data/DataApplicationService.cs:15`,
  `Application/Auth/AuthApplicationService.cs:17`); the outbound adapter
  `Adapters/Reporting/ReportGenerationClient.cs:10` also consumes inbound API
  DTOs even though `ReportGenerationRequestModel` exists in
  `RVT.BusinessLogic/Reports/ReportRuleApplicationModels.cs:114`. This is the
  one structural divergence the catalog doesn't admit — add it to the catalog
  follow-up list and mirror the existing guard tests.
- **[M] Swallowed catch-all** —
  `RvtPortal.Spa/Api/MonitorDetailSummaryService.cs:164-167`
  `catch { return null; }` converts any exception (including
  `OperationCanceledException`) into "no metric" with no logging.
- **[M] Dead `RVT.Utilities` project** — single class `AzureBlobService.cs`
  with no production consumer, self-built configuration from
  `Environment.CurrentDirectory`, unhandled enum case, sync blob I/O, and a
  `UploadTest` method. Delete it; `RvtPortal.Spa.csproj` and
  `RVT.BusinessLogic.csproj` still reference it. Also: `RVT.BusinessLogic`
  carries unused `Azure.Storage.Blobs` / `Microsoft.Extensions.Http` package
  refs; `RVT.Entities` an unused `Newtonsoft.Json`.
- **[M] Bounded N+1** —
  `Application/Monitors/MonitorAdministrationReadService.cs:394-398` awaits
  `impactReader.BuildAsync` per page row (~4 × pageSize sequential round
  trips; shared-connection design forbids parallelizing).
- **[L]** Dead lookup surface: 12 of 21 `ILookupService` methods have no
  callers; two return raw entities. `MonitorData.cs:222` null-forgives a
  nullable read. Legacy monitor-data chain lacks CancellationToken plumbing.
  InMemory-provider branching inside production classes
  (`MonitorRemovalImpactReader.cs:56-58`, `EfCoreUnitOfWork.cs:349-356`).
  Dev-only CORS heuristic over-matches (`Program.cs:612-620` — `172.2` prefix,
  `10.`/`192.168.` match DNS names, with credentials).
- **Positive**: systematic `[Authorize]`, constant-time internal-key compare,
  CSRF via `Sec-Fetch-Site`, fail-fast production config validation,
  parameterized SQL throughout, no hardcoded secrets, carefully engineered
  `EfCoreUnitOfWork`, strong executable architecture guards.

### Portal frontend (`RvtPortal.Client`)

- **[H] Help Admin Status/Type filters are silent no-ops** —
  `src/api/client.ts:576-581, 1006-1022`: `toSearchParams`'s whitelist omits
  `status`/`contentType`; both are discarded before the request, while the
  backend expects them (`HelpController.cs:87-88`). The whitelist serializer
  is a footgun (new fields silently fail); tests mock `queryAdminHelp`
  wholesale so nothing catches it.
- **[H] One-click permanent user deletion, no confirmation** —
  `src/admin/AdminPanels.tsx:531-534, 600`; company delete has a
  `ConfirmDialog`, user delete does not. Same for monitor-from-contract
  removal (`MonitorPanels.tsx:398-409`).
- **[H] `handleLogout` unhandled rejection** — `src/App.tsx:1073-1082`
  re-throws non-401 errors from an `onClick` handler; no UI feedback, session
  cookie stays live.
- **[M] Stale generated OpenAPI schema** — `schema.d.ts` has no Help endpoints
  at all; `dtos.ts` hand-writes "schema-gap extensions"; regeneration is
  unpinned `openapi-typescript@latest` against a running server. Type drift is
  possible everywhere except 8 sentinel keys.
- **[M] Detail/lookup effects lack abort/stale guards** (list effects do it
  correctly): `AdminPanels.tsx:284-291, 614-621`,
  `MonitorPanels.tsx:380-396`, `NotificationAlertPanels.tsx:332-342`,
  `ContractSitePanels.tsx:657-668`, `ReportPanels.tsx:414-458`. Options-fetch
  races (last-write-wins) in `ContractSitePanels.tsx:397-431, 881-916`.
- **[M] No debounce on list queries** — every keystroke fires a full query in
  every list panel, while the cheap suggestion lookups *are* debounced (180 ms).
- **[M] Copy-paste helper drift** — `parsePositiveInt` ×5 (two semantics),
  `normalizeSortDirection` ×5 (three variants), date/number formatters ×7
  (hardcoded `en-GB` vs browser locale — same timestamp renders differently
  per screen), `useGridSortHandler` ×3.
- **[M] `safeReportLink`** (`ReportPanels.tsx:972-989`) reimplements `safeHref`
  minus the protocol-relative check; installer deployment save coerces blank
  coordinates to 0,0 (`MonitorPanels.tsx:750-755`) while the admin path
  deliberately preserves null.
- **[L]** `authError` dead state (`App.tsx:326-336`); module-top-level config
  throw → blank white screen (`client.ts:119`); `requestJson` breaks on
  204/empty bodies; dev script binds `0.0.0.0`; prefix routing matches
  `/helpxyz`; `react-hooks/exhaustive-deps` downgraded to warn; hardcoded
  `rememberMe: true`; CSRF on mutating requests rests entirely on server
  cookie config (verify backend — it checks `Sec-Fetch-Site`).
- **Positive**: no `dangerouslySetInnerHTML`, no `any`, no tokens in
  localStorage; single API adapter (`client.ts`) genuinely used by all calls;
  URL/href sanitization with tests; help bodies rendered as plain text.

### Monitors (`apps/monitors`)

Maturity spectrum: ReportingMonitor & MyAtm ≈ target architecture; Svantek
mostly there (alerting still legacy sync); Omnidots split-brain; AirQ
essentially pre-migration (sync core, static config, no cancellation, no
architecture tests).

- **[H] On-by-default auth bypass in a production adapter** —
  `omnidotsmonitor/.../api/http/HttpWebClient.cs:45-51` intercepts
  `/api/v1/user/authenticate` and fabricates a `TokenResponse` from
  `RVT__OMNIDOTS_TOKEN` when `RVT__OMNIDOTS_USE_TOKEN` (default **true**) is
  set — a test seam in the request path, selected by global static config.
- **[H] `RvtConfig` used pervasively from monitor code** (`AirQService.cs:23`,
  all `*MonitorServices.cs`, Omnidots `HttpWebClient.cs:45`) — bypasses host
  `IConfiguration`; MyAtm's `MyAtmVendorOptions` shows the intended pattern
  yet still falls back to `RvtConfig`.
- **[H] AirQ timezone bugs** — `StoreNoiseLevelsHandler.cs:60` seeds with
  server-local `DateTime.Now` then compares against UTC;
  `CheckForOfflineMonitorsHandler.cs:50` applies `.ToUniversalTime()` to
  unspecified-kind DB timestamps (Unspecified = local); correct only while
  containers run UTC.
- **[M] Composition duplication** — `AddEmailProvider(...)` byte-identical ×5;
  `MonitorJobRunner.GetJobName` ×5; the "parameterless ctor throws at runtime"
  Quartz hack ×4; four diverging `IHttpClient`/`HttpWebClient` families (only
  MyAtm has CT + bounded read + retry + 15 s timeout); three hosts declare a
  phantom generic `HttpWebClient<T>`; `ClearOlderErrorMessagesHandler` ×3.
- **[M] Facades hand-`new` their handler graphs** (`AirQApi.cs:31-52`,
  `SvantekApi`, `OmnidotsApi`), defeating DI; MyAtm registers handlers
  individually (the intended shape).
- **[M] Error persistence assumes the DB is up** — every catch funnels into
  `IDBClient.HandleException` which opens a context and saves; when the DB is
  the failure, the handler throws a second exception masking the original. No
  log-only fallback anywhere.
- **[M] One-shot jobs can't shut down gracefully** —
  `reportingmonitor/.../Program.cs:9` passes `CancellationToken.None`;
  `MonitorHost.RunAsync` offers no shutdown-linked token for the one-shot path.
- **[M]** Omnidots webhook catch blocks log fixed strings without the
  exception; `SpaCustomerLogoClient` silently maps every failure to null with
  zero logging. AirQ `NotifySiteAverages` uses local `DateTime.Today` with an
  acknowledged `// fixme` about the 00:05 run, and has no per-monitor error
  isolation. AirQ `DBClient.InsertNoiseDtos` does one existence query per
  sample (N+1 fleet-wide, 4×/hour). Omnidots puts session tokens in URL query
  strings and re-authenticates per `ListMeasuringPoints` call. Svantek
  `Program.cs:25-28` hardcodes `LogLevel.Trace`.
- **[L]** Raw `throw new Exception(...)` in `SvantekHttpGateway.cs:45,96,124`;
  `map[name]!` null-forgiveness in `OmnidotsQueryProcessor.cs:27`; magic
  averaging periods `900/3600/86400` and vendor field switches inline in
  AirQ/Svantek rule processors (MyAtm extracted these); ~6,400 lines of
  quadruplicated test scaffolding (`TestDbClient.cs` ×4 at 1,285–1,967 lines
  each); AirQ has **no** architecture guard tests (the others have 1–4).
- **Positive**: docker-compose secrets hygiene is clean; both API-key filters
  in monitors use `CryptographicOperations.FixedTimeEquals`; the Omnidots
  webhook path (bounded JSON reader, signature validation, rate limiter,
  typed problem results) is genuinely good.

### Shared libs (`libs/rvt-monitor-common`)

- **[H] Storage adapters don't honor the port's error contract uniformly** —
  S3 translates only `AmazonS3Exception`
  (`S3ObjectStorageClient.cs:84,119-126,162-169`; `AmazonClientException`
  escapes raw); Azure only `RequestFailedException`
  (`AzureBlobObjectStorageClient.cs:77,112,165`;
  `AuthenticationFailedException` leaks); Local translates **nothing**. Three
  failure surfaces for one `IObjectStorageClient` contract; mock-based
  contract tests structurally cannot catch this.
- **[H] `GetObjectUri` missing from the port** — implemented identically by
  all three adapters; ReportingMonitor binds to concrete adapter classes to
  reach it (`ReportingStorageComposition.cs:41,59,75`).
- **[H] `RvtConfig` grab-bag** — monitor identity sniffed from entry-assembly
  name/base directory (`RvtConfig.cs:14-17,94-131`); shared business rule
  `AlertActivityTimeDto.IsActive` branches on `RvtConfig.IsMyAtmMonitor`
  (`AlertActivityTimeDto.cs:20-25`); dead public-static secrets
  (`SENDGRID_API_KEY`, `SMS_API_KEY`, `SMS_API_SECRET` — zero consumers,
  delete); every monitor's credentials in the shared class.
- **[M] Secret-bearing options are records** — generated `ToString()` prints
  `ApiKey`/`ClientSecret`/`ApiSecret`/`ConnectionString`
  (`SendGridMailOptions.cs:5-9`, `MicrosoftGraphMailOptions.cs:5-11`,
  `TransmitSmsOptions.cs:5-11`, `AzureBlobStorageOptions.cs:5-13`). Override
  `ToString()` or use classes.
- **[M]** Same email request produces different mail per provider (SendGrid
  sends text+HTML; Graph discards plain text); Graph large-attachment flow
  orphans drafts on failure (`MicrosoftGraphEmailAdapter.cs:104-176`);
  TransmitSMS in-body error codes always Permanent
  (`TransmitSmsClient.cs:52-56` → `TransmitSmsAdapter.cs:106-117`) so
  throttling dead-letters; two parallel durable dispatcher stacks
  (`Delivery/MonitorDeliveryDispatcher.cs` vs `Alerts/DurableAlertDispatcher.cs`)
  with already-diverged `RetryDelay` math; duplicate DTO families +
  namespace squatting (`Rvt.Communication.Abstractions/RvtContactDto.cs:1`
  declares `namespace Rvt.Monitor.Common.Notifications`); `[Obsolete]` on
  `MessageService` but not `IMessageService` so no caller sees the warning;
  MQTT stack uses static config + static `RvtLogger` service locator +
  `GetAwaiter().GetResult()` (`RvtMqttClient.cs`, `MonitorEventPublisher.cs:50,55`).
- **[L]** `SendGridEmailAdapter.cs:98` disposes nullable `response.Body`
  without null check; `CreateIfNotExistsAsync` on every Azure write; adapter
  exceptions discard inner cause; `LegacyMessageChannel.Both` always throws;
  naming drift across parallel DI registrations and `Enabled` default
  inconsistencies; startup validation ordering is registration-dependent;
  `DateTimeUtil.cs:11` static `TimeZoneInfo` at type-load →
  `TypeInitializationException` on bad TZ id; 4-char destination prefixes
  logged despite "never log destinations."
- **Positive**: `Rvt.Communication.Abstractions` verified provider-free;
  adapter tests are genuinely behavioral (request mapping, failure-kind
  matrices, redaction assertions, chunked-upload boundaries) with no live
  network; architecture boundary tests exist at every layer (though as
  text scans).

### Reporting service (`services/reporting`) — beyond C1/C5

- **[M]** `IReportingRepository` is a fat 8-method port mixing
  queries/commands/locking/health (`ReportGenerationContracts.cs:19-38`) — the
  fork split it; no architecture guard test here (the fork has one).
- **[M]** Connection leak in `TryAcquireGenerationLockAsync`
  (`PostgresReportingRepository.cs:136-153`): connection not disposed if
  `ExecuteScalarAsync` throws; failed unlock returns a pooled connection still
  holding the advisory lock.
- **[M]** `sendResult.Success` never inspected — failed deliveries recorded as
  text only; one-time endpoint returns 200 even if every email failed;
  "Sent ok" written into the `error_message` column.
- **[M]** `GetDueReportRulesAsync(triggerUtc.Date, …)` — implicit
  `DateTime`→`DateTimeOffset` conversion applies the local machine offset
  (both copies); benign only on UTC hosts. N+1 recipient hydration per rule
  (fork batches). `Off` rules fetched then discarded.
- **[M]** Azure blob adapter builds a new `BlobContainerClient` +
  `DefaultAzureCredential` per upload (`AzureBlobReportStorage.cs:27-50`);
  fork uses the shared `Rvt.Storage` abstraction.
- **[M] Test-coverage illusion** — every TimescaleDB integration test silently
  returns green when `RVT_REPORTING_TIMESCALE_TEST_CONNECTION` is unset
  (`TimescaleSchemaIntegrationTests.cs:44-48,70,100,147`);
  `PostgresReportingRepository` (619 lines incl. advisory lock and all SQL)
  has zero unconditional coverage; no `InternalApiKeyFilter` test (the fork
  tests its endpoints); `ServiceAssemblyTests.cs` is a placeholder; no
  end-to-end `RenderAsync` smoke test (why C5 went undetected).
- **[L]** Latent SQL-injection pattern in `ReadAveragePointsBySerialAsync`
  (`PostgresReportingRepository.cs:474-483` — interpolated table
  name/expression; all current callers constant); magic `frequency = 5` +
  hard dependency on a partial unique index with no readiness check; period
  end `midnight - 1ms` vs exclusive `<` predicates; endpoints use
  `DateTimeOffset.UtcNow` despite injected `TimeProvider`; missing site → 500
  instead of 404/422; Ollama/logo adapters swallow failures with no logging.
- **Positive**: Core has zero package refs; ports defined in Core, adapters
  point inward; clean composition root; SQL consistently parameterized;
  TimescaleDB predicates fit hypertable pruning.

### Repo root

- Cross-module wiring is clean: only monitors/reporting →
  `libs/rvt-monitor-common` via direct `ProjectReference` (the sanctioned
  direction); the portal is self-contained. Root governance
  (`Directory.Build.props`, PostgreSQL-only guard, engineering-standards
  ratchet, deterministic builds) is in good shape.

---

## Hexagonal architecture scorecard

| Module | Verdict |
|---|---|
| Portal backend | **Faithful.** Zero-dependency application core, correct dependency direction, strong executable guards. Gap: host app layer + one adapter coupled to inbound API DTOs; unextracted slices are layering-by-convention (documented debt). |
| `libs` communications | **Better than spec.** Provider-free abstractions verified; adapters isolated; failure classification and redaction enforced by good tests. |
| `libs` storage | **Right shape, weak execution** — error contract partially honored; `GetObjectUri` escaped the port. |
| `Rvt.Monitor.Common` hub | **The anti-pattern the design targeted still stands**: static config, static logger, duplicated dispatchers, monitor-sniffing business rules. |
| ReportingMonitor / MyAtm | **Faithful** — narrow ports, options validation, durable delivery, cancellable async. |
| Svantek | Mostly there; alerting still on the legacy sync path. |
| Omnidots | Modern shell, blocking `.Result` core, auth-bypass seam in the adapter. |
| AirQ | **Pre-migration code wearing post-migration folder names.** |
| `services/reporting` | Clean hexagon internally, but it is the abandoned fork of itself. |

---

## Recommended remediation steps (priority order)

1. **Consolidate the reporting duplication.**
   Declare `apps/monitors/reportingmonitor` authoritative; delete or archive
   `services/reporting`. Until deletion, backport to any deployed
   `services/reporting` instance: fail-closed + constant-time
   `InternalApiKeyFilter`, per-rule error isolation, atomic report
   persistence, delivery-failure capture.
2. **Fix the shared PDF renderer** (whichever copy survives): heatmap viewBox
   sizing for >8-day periods, `InvariantCulture` formatting, restore report
   name/period in the chrome, move the QuestPDF license assignment to
   composition and settle the Community-license compliance question, embed the
   logo as a resource.
3. **Async-ify and cancellation-plumb AirQ and Omnidots imports**: add
   `CancellationToken` to their `IHttpClient` ports, replace `.Result` with
   await, set explicit HttpClient timeouts, thread the Quartz/one-shot token
   through the job dispatchers, and remove the `RVT__OMNIDOTS_USE_TOKEN`
   fake-auth seam from the production adapter (move to a test double at
   composition).
4. **Migrate AirQ/Svantek alerting onto the durable dispatcher** (outbox +
   retry + dead-letter). While there: fix the null-phone `ArgumentException`
   escape in `MessageService`/`NotificationDeliveryService`, and reclassify
   Graph token network failures as Transient.
5. **Portal fixes**: gate the master-admin seed restore behind an explicit
   opt-in flag with Warning-level logging; fix the frontend `toSearchParams`
   whitelist (or replace the whitelist with typed serialization) and
   regenerate/pin the OpenAPI schema; add confirmations to user deletion and
   monitor-from-contract removal; handle the logout rejection; fix the
   swallowed catch in `MonitorDetailSummaryService`.
6. **Shared-library cleanup**: unify the storage adapters' exception
   translation behind the `ObjectStorageException` contract and add
   `GetObjectUri` to the port; override `ToString()` on secret-bearing option
   records; delete the dead `RvtConfig` secret statics; collapse the two
   durable dispatcher stacks; put `[Obsolete]` on `IMessageService` itself.
7. **Sweep composition duplication into `rvt-monitor-common`**: one
   `AddRvtEmailProvider` extension, one cancellable/timeout-bounded vendor
   HTTP client (MyAtm's pattern), shared `GetJobName`/job-map abstraction,
   shared test DB fake. Add architecture guard tests to AirQ.
8. **Make failure paths honest**: log-only fallback when `HandleException`
   can't reach the DB; log the exception object in Omnidots webhook catches
   and the logo/Ollama adapters; convert env-gated integration tests to
   explicit `Skip` so CI coverage stops overstating; inspect
   `sendResult.Success` in report delivery.
9. **Dissolve the `RvtConfig`/static-state hub incrementally**: options-bound
   per-monitor config (MyAtm pattern), remove assembly-name sniffing and the
   `IsMyAtmMonitor` branch from `AlertActivityTimeDto`, retire `RvtLogger` and
   the MQTT static config.
10. **Smaller hygiene items** (batch opportunistically): delete
    `RVT.Utilities` and stale inner-layer package refs; debounce frontend list
    queries; extract shared frontend formatters/param parsers; fix
    `safeReportLink` and the installer 0,0-coordinate coercion; Svantek
    hardcoded Trace logging; portal dev CORS heuristic; AirQ insert N+1 and
    the `// fixme` daily-averages schedule.
