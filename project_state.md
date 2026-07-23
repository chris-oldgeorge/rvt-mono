# Project State

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Documents/rvt-mono`
- Status: Documentation consolidation is complete. All 122 non-entry module
  Markdown documents are centralized under root `docs/`, with a guarded root
  index and valid retained repository/module entry points. The completed RVT
  common source-reference migration remains in effect: active monitor and
  portal consumers are source-referenced, their 12 tracked locks reflect the
  source graph, and the two package-validation consumers restore locally
  packed artifacts through artifact-scoped validation locks.
- Design: `docs/superpowers/specs/2026-07-22-rvt-mono-repository-design.md`
- Plan: `docs/superpowers/plans/2026-07-22-rvt-mono-repository-bootstrap.md`
- Requested outcome: fresh unified Git history and a shared root solution for
  `rvt-monitors`, `rvtportal-spa-alpha`, `rvt-reporting`, and
  `rvt-reporting-new`.
- Intended modules: `apps/monitors`, `apps/portal`,
  `libs/rvt-monitor-common`, and `services/reporting`.
- Root solution: `Rvt.Mono.slnx`.
- Approved design: `docs/superpowers/specs/2026-07-22-rvt-common-source-reference-design.md` changes active consumers to source project references, while package-validation remains package-based against locally packed artifacts. This is an explicit decision to review if independent package consumption becomes required again.
- Implemented plan: `docs/superpowers/plans/2026-07-22-rvt-common-source-reference-migration.md`.
- Implemented design: `docs/superpowers/specs/2026-07-22-documentation-consolidation-design.md` consolidates all module Markdown into root `docs/`, retaining root/module README and AGENTS entry points.
- Implemented plan: `docs/superpowers/plans/2026-07-22-documentation-consolidation.md`.
- Aggregate project count: 38 projects across all four module roots.
- Important boundary: active application consumers use the in-repository RVT
  source projects; only `libs/rvt-monitor-common/package-validation` consumes
  RVT packages. Do not merge reporting implementations or database schemas.
- Imported source snapshots:
  - `apps/monitors` from `chris-oldgeorge/rvt-monitors` at
    `5935f40614073afa6c4ef954db1308a72a5f8f2b`.
  - `apps/portal` from `chris-oldgeorge/rvtportal-spa-alpha` at
    `8355070f094a591297c9f8468057f44a6c876986`.
  - `libs/rvt-monitor-common` from `RVT-Group-LTD/rvt-reporting` at
    `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
  - `services/reporting` from `chris-oldgeorge/rvt-reporting-new` at
    `e602e8317e35bd94a1eb4dd017759b91713ea111`.
- Import staging directory: `/private/tmp/rvt-mono-import.2w115l` (retained
  through Task 3 final verification).
- Import verification: all staged repositories were checked out detached at
  their exact manifest revisions; imported trees checksum-match the staged
  content with `.git` excluded; no nested `.git` directory exists below the
  module roots.
- Known environment note: authenticated GitHub metadata access was available;
  source clone/restore access must be verified during implementation. Never
  record credentials in this repository.
- Task 1 guard: `.gitignore` excludes generated files, environment files, and
  `.superpowers/sdd/` controller state. `docs/imports/source-manifest.md` pins
  the four approved source snapshots. Repository bootstrap commits through the
  source import are design `1327b84`, plan `0abf895`, guard `ae65789`, and
  source import `31d168f`.
- Task 3 guard: `tests/verify-mono-solution.test.sh` runs
  `scripts/verify-mono-solution.sh`. It compares normalized, sorted module
  `*.csproj` paths with the normalized, sorted `dotnet sln Rvt.Mono.slnx list`
  paths, requires matching project counts and per-module representation, and
  enforces exact project placement under `Apps/Monitors`, `Apps/Portal`,
  `Libraries/RVT Monitor Common`, and `Services/Reporting`, with test projects
  in each module's corresponding `Tests` solution folder.
- Source-reference migration Task 1: `tests/verify-rvt-common-source-boundary.test.sh`
  invokes `scripts/verify-rvt-common-source-boundary.sh`. The guard declares
  the three shared source projects, requires the approved app/portal project
  references, rejects their common-package references, and preserves
  package-only validation consumers. Each package-validation project rejects
  source references to all three shared projects while retaining its required
  package references.
- Source-reference migration Task 2: the five monitor hosts now directly
  reference `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure`; the
  five current monitor test consumers reference `Rvt.Monitor.IntegrationTesting`
  (with `ReportingMonitorTests` retaining its direct Common edge); the reporting
  messaging/storage projects directly reference Common; and the portal host
  directly references Infrastructure. MSBuild now supplies build ordering for
  these active graphs.
- Monitor central package variables `RvtCommonVersion`,
  `RvtCommonInfrastructureVersion`, and `RvtIntegrationTestingVersion`, plus
  their three `PackageVersion` entries, were removed. Active monitor and portal
  NuGet configs now retain only nuget.org; the shared library NuGet config maps
  `Rvt.*` to the root `artifacts/packages` feed for package validation.
- Package-validation remains intentionally package-based at `0.2.0-rc.1`.
  `scripts/build-mono.sh` packs exactly `Rvt.Monitor.Common`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting` to `artifacts/packages`, validates the two
  package consumers from an isolated `artifacts/nuget-packages` cache, restores
  `Rvt.Mono.slnx`, builds with `--no-restore`, and tests with `--no-build`.
  Its legacy package-validation compatibility path is deterministically replaced
  on each run, so a stale directory or symlink from a temporary test feed cannot
  block the next build.
  Normal builds opt those two consumers into per-project locks under
  `artifacts/validation-locks`; freshly emitted NuGet archives have different
  content hashes, so this keeps their committed `0.2.0-rc.1` package-policy
  locks and all other tracked locks unchanged. The shared library NuGet
  configuration maps `Rvt.*` only to the root local feed and retains nuget.org
  for third-party packages; GitHub Packages and credentials are not used.
- Verification results:
  - `tests/verify-mono-solution.test.sh` and
    `tests/verify-mono-layout.test.sh` pass.
  - `dotnet sln Rvt.Mono.slnx list` reports all 38 module projects.
  - The source-boundary guard passes after the active-consumer conversion.
  - Both active module solutions restore successfully and their restore graphs
    reach the shared source projects. Verbose network traces contacted only
    nuget.org; the preserved shared-library config still appears in NuGet's
    configured-feed summary.
  - Portal restore reports four existing NU1903 high-severity advisories for
    `System.Security.Cryptography.Xml` 10.0.7; remediation is outside Task 2.
  - During Task 2, `dotnet restore Rvt.Mono.slnx` was blocked by private package access:
    GitHub Packages returns HTTP 401 for the RVT organization feed. Cached RVT
    `0.2.0-rc.1` packages also produce NU1403 content-hash validation errors.
  - During Task 2, `dotnet build Rvt.Mono.slnx --no-restore --nologo` exited
    with 16 errors from the same NU1301/NU1403 package state; unaffected
    projects compiled.
  - Package feeds and dependency declarations were not changed in Task 2.

## RVT Common Local Package Validation - 2026-07-22

- The missing-artifact regression check records the expected pre-restore
  failure and names `Rvt.Monitor.Common.0.2.0-rc.1.nupkg`; its mutation RED run
  catches `RuntimeConsumer` restore before artifact verification. Its GREEN run
  proves neither package-validation consumer nor aggregate restore can occur
  before all three packages exist.
- The local package sequence restores and packs the three shared projects,
  restores all 38 aggregate projects from nuget.org plus
  `artifacts/packages`, and builds the aggregate solution with 0 errors. The
  existing four NU1903 advisories for `System.Security.Cryptography.Xml`
  10.0.7 remain outside this task.
- The package artifact suite passes 8/8. RuntimeConsumer and TestConsumer stay
  package-based at `0.2.0-rc.1`; build-time artifact locks are generated under
  `artifacts/validation-locks`. The 12 active monitor consumer locks were
  regenerated from their source-reference graphs: none retain a direct RVT
  package, and normalized comparison proves all non-RVT dependency data is
  unchanged.
- Focused source-boundary architecture verification passes 12/12 for monitors
  and 7/7 for the portal. The monitor suite now enforces the approved source
  matrix, source-consumer lock shape, local validation boundary, version, and
  feed policy. The portal suite now requires the Infrastructure source project
  and a nuget.org-only credential-free configuration.
- A full build-sequence diff fingerprint is identical before and after restore,
  pack, package validation, aggregate restore/build, and the nonzero aggregate
  test stage. A normal successful run therefore introduces no tracked lockfile
  changes.
- The aggregate test stage remains nonzero for imported test assumptions that
  are outside this migration. Database-backed tests report exactly:
  `System.InvalidOperationException: Set RVT__POSTGRES_INTEGRATION_CONNECTION
  to run PostgreSQL integration tests.` Other imported architecture and
  migration-contract test assumptions still
  resolve pre-mono paths, including
  `/Users/oldgeorge/Documents/rvt-mono/reportingmonitor/ReportingMonitor/api`
  and `/Users/oldgeorge/Documents/rvt-mono/rvt-monitors.sln`, which do not exist
  in the aggregate layout. No package versions or test behavior were changed
  to mask these failures.

## Documentation Consolidation Task 1 - 2026-07-22

- Current state: the documentation move guard and exhaustive manifest are
  defined; no documentation has moved and no links have been rewritten yet.
- File structure added by this task:
  - `docs/documentation-move-manifest.md` maps all 122 tracked non-entry module
    Markdown sources to unique destinations below the root `docs/` hierarchy.
  - `scripts/verify-documentation-layout.sh` enforces the manifest, retained
    entry points, destination presence, absence of module documentation, and
    absence of stale links to moved sources.
  - `tests/verify-documentation-layout.test.sh` is the strict-mode executable
    wrapper for the guard.
- Retained entry points are the root `README.md`, the four module-root
  `README.md` files, and `apps/monitors/AGENTS.md` plus
  `apps/portal/AGENTS.md`.
- Guard variables: `repo_root` is derived from the script location;
  `manifest_path` is `docs/documentation-move-manifest.md`;
  `expected_manifest_entries=122`; `failures` counts issue groups; `sources`
  and `destinations` hold parsed manifest rows; `retained_paths` holds the seven
  required entry points; and `missing_sources` scopes stale-link checks to
  documents that have actually moved.
- Expected verification state: `tests/verify-documentation-layout.test.sh`
  exits nonzero with exactly two issue groups—122 non-entry Markdown sources
  remain below module roots and all 122 manifest destinations are absent.
  Task 2 is responsible for resolving those expected failures with Git-aware
  moves.
- Review follow-up: the exact old-path stale-reference scan is repository-wide
  text scanning, with `.git`, `.superpowers/sdd`, and the move manifest
  excluded. The Markdown link resolver remains scoped to Markdown. The
  `tests/verify-documentation-layout-regression.test.sh` fixture moves all 122
  manifest documents and proves a source-code reference in the MyAtm monitor
  architecture-test path is reported as the sole stale reference.

## Documentation Consolidation Task 3 - 2026-07-22

- Current state: all 122 manifest documents are present exactly once below the
  root documentation hierarchy, and the seven retained root/module README and
  AGENTS entry points remain beside their code.
- Root navigation: `docs/index.md` is the central documentation hub, grouped
  into architecture, development, operations, release, database, modules,
  history, and imports. Root and module READMEs link into that hub or directly
  to their current module documentation.
- Repaired references: the portal development-guideline references, the
  monitor ReportingMonitor README link, the MyAtm architecture-test document
  path, portal/monitor AGENTS state paths, and the portal development-secrets
  script link now resolve from their current locations.
- Guard variables added to `scripts/verify-documentation-layout.sh`:
  `documentation_index` names `docs/index.md`; `index_targets` holds one
  required link for each guarded documentation category. The regression
  fixture uses `stale_document_path` and the `STALE_DOCUMENT_PATH` environment
  variable to inject its intentional old path only into the temporary test
  repository, preserving the repository-wide production scan.
- Verification: `tests/verify-documentation-layout.test.sh` passes with 122
  moves and seven retained entry points;
  `tests/verify-documentation-layout-regression.test.sh` passes while proving
  source-code stale references are rejected. The final stale-link scan and
  whitespace check are clean. The obsolete untracked suffixed copy
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests 2.cs`
  was removed after the final repository push; the tracked mono-repository
  boundary test remains authoritative.

## Documentation Consolidation Final Review Fix - 2026-07-22

- Manifest-derived stale-reference enforcement now derives each moved
  module's `docs/**` form from the 122 source paths and scans arbitrary tracked
  text with `git grep -I`. The scan excludes the move manifest, internal SDD
  review packages, and `docs/history/**`; historical documents therefore keep
  their original evidence while current docs, code, scripts, SQL, and
  configuration remain guarded.
- Guard variables added by the final fix: `module_relative_source` is the
  current manifest source with its module-root prefix removed, and
  `missing_module_relative_sources` contains only `docs/**` forms whose source
  document has actually moved. `stale_reference_count` includes both exact
  repository-root source forms and module-relative forms.
- Repaired references: 50 occurrences in non-Markdown tracked files and 27 in
  current Markdown now use their manifest destinations. This includes the
  shared release-automation documentation path, portal EF/database/Sonar and
  development-secrets references, monitor release-export configuration, SQL
  comments, and current database/onboarding/release documentation.
- Regression structure: the tracked non-Markdown fixture
  `tests/fixtures/documentation-layout-stale-source-reference/libs/rvt-monitor-common/scripts/release-documentation.txt`
  injects the old shared-library module-relative release path only in its
  temporary repository.
  The regression requires the guard to report both that form and the existing
  exact old source path.
- Verification: both documentation guards pass; the explicit manifest-derived
  scan reports zero current stale alias groups; Bash syntax checks pass; the
  shared release-document destination exists; and
  `git diff --check` is clean. The obsolete untracked suffixed C# copy was
  subsequently removed, leaving the tracked boundary test as the sole copy.

## Immediate Blockers Task 2 - 2026-07-22

- Portal startup now explicitly registers `TimeProvider.System`, satisfying
  report-generation client and report-rule dependency resolution without a
  framework-service assumption.
- Vibration traces use the mapped `OmnidotsTrace` entity from
  `RVTSearchContext.OmnidotsTraces` end-to-end. `IMonitorService`,
  `MonitorData`, graph dataset mapping, and the data-view test fake all carry
  `SearchQueryResult<OmnidotsTrace>`; the unmapped `OmnidotsTraces` DTO is no
  longer on the execution path.
- Regression coverage: a host scope resolves `IReportGenerationClient`; a
  SQLite-backed test inserts a mapped `omnidots_trace` row and verifies
  `MonitorService.GetVibrationTraces` reads it. The focused run passes 9/9;
  the portal test project passes 316/319 with three intentional opt-in
  PostgreSQL skips. Restore continues to report existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7.

## Immediate Blockers Task 3 - 2026-07-22

- Site and monitor company-user authorization now share
  `Application/Sites/ActiveSiteAssignment.cs`. Its `ForUser(userId, nowUtc)`
  expression requires `StartDate <= nowUtc` and no `EndDate` or
  `EndDate >= nowUtc`, so both boundaries are inclusive.
- `SiteApplicationService` now receives the registered `TimeProvider`; its
  detail and list paths evaluate the shared assignment predicate at
  `timeProvider.GetUtcNow().UtcDateTime`. Monitor detail, picture, inventory,
  and option paths reuse the same assignment expression for company users.
- Installer monitor reads require an assigned row with non-null actor/row
  company ids that match. The protected picture endpoint therefore preserves
  its existing `404` response for missing and unauthorized monitors while
  same-company pictures remain readable.
- `IMonitorAdministrationReadService.OptionsAsync` now accepts the
  `PortalUserContext` actor. Admin option behavior remains global; installers
  receive contracts/sites for their company; company users receive contracts
  and non-archived sites reached through currently active assignments.
- Regression coverage fixes the site-authorization clock at
  `2026-07-22T12:00:00Z` and covers expired, future, exact-boundary active,
  same-company, and cross-company controls. The focused workflow run passes
  26/26; the initial portal test project run passed 319/322 with three expected opt-in
  PostgreSQL skips. Existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7 remain unchanged.
- Task 3 review follow-up closes the remaining specified consumers:
  `DashboardApplicationService`, `AlertLevelApplicationService`,
  `NotificationApplicationService`, and both notification-close handlers now
  receive the registered `TimeProvider` and reuse `ActiveSiteAssignment.ForUser`.
  Future and expired assignments cannot expose dashboard/alert data or mutate
  notification close state; exact `StartDate == nowUtc` / `EndDate == nowUtc`
  remains authorized. The fixed test instant is `2026-07-22T12:00:00Z`.
- Monitor option contract scope is now the intersection of visible site ids
  and the actor's company id for installer/company-user callers. This prevents
  a second company's contract leaking when both contracts point to one site;
  admin option behavior remains global.
- Follow-up verification: the four new exploit/control tests pass 4/4, the
  four covering workflow classes pass 41/41, and the portal test project
  passes 323/326 with the same three opt-in PostgreSQL skips. The duplicate
  `SiteApplicationService` file header was consolidated.

## RVT Portal AI Review Analysis - 2026-07-22

- Source review: `/Users/oldgeorge/Desktop/RvTPortal AI Review.docx` was read
  structurally and rendered as 14 pages. It contains two overlapping technical
  review passes and eight reviewer comments; there are no tracked insertions or
  deletions.
- Action plan:
  `docs/superpowers/plans/2026-07-22-rvt-portal-review-remediation.md` contains
  the normalized finding register, comment disposition, five implementation
  phases, 16 test-driven tasks, and the final release gate.
- Highest-priority confirmed current defects are: inactive/future site
  assignments granting access, installer cross-company monitor-picture reads,
  missing `TimeProvider` DI registration, an unmapped vibration-trace query,
  request-host-derived password-reset links, unspecified contract dates written
  to `timestamptz`, omitted existing-database repair SQL, unscoped monitor
  options, and the absence of an active root GitHub workflow after the monorepo
  import.
- The disputed timestamp finding remains a validation-first item: current code
  passes UTC bounds to PostgreSQL `timestamp without time zone` telemetry and
  returns values without restoring UTC kind. The plan requires a real-Postgres
  test to distinguish throwing paths from return-shifted paths before repair.
- Reviewer-comment disposition: the monitor-picture dismissal confuses
  admin-only upload with installer-enabled read access; the schema-deploy issue
  labelled "Hallucination" is confirmed because the repair file is absent from
  `ScriptRunner` and publish content; What3Words requires a retain-or-remove
  product decision; Help Admin remains deferred if it is excluded from release;
  the destructive dev-restore defect is real but lower production priority.
- Superseded observations: root `project_state.md` exists; the current workspace
  is not the reviewed SMB checkout; Word/AppleDouble/build debris is absent;
  SendGrid uses a singleton client factory; and the runtime client container
  already uses `nginx-unprivileged`.
- Planned CI variable: `RVT_TEST_POSTGRES_CONNECTION` is the portal-specific
  real-PostgreSQL test connection. It is distinct from monitor-suite integration
  variables recorded elsewhere in this file.

## Immediate Blockers Task 4 - 2026-07-22

- Public account-action links now use only the bound `SpaOptions.PublicBaseUrl`
  through `SpaPublicLinkBuilder`; neither `AuthApplicationService` nor the
  sibling `UserAccountNotificationService` can fall back to request scheme,
  host, or path base. The existing request-origin records remain at controller
  boundaries for API compatibility, with auth also carrying the internal
  correlation id used for safe provider-failure logging.
- Outside Development/Testing, `Program.cs` requires `Spa:PublicBaseUrl` to be
  an absolute HTTPS base URI without credentials/query/fragment. `AllowedHosts`
  must be nonempty, contain no wildcard, and contain that URI's exact host.
  Checked-in `appsettings.json` leaves the public base blank and limits local
  hosts to `localhost;127.0.0.1`; deployments must override both settings.
- Forwarded-header variables are `ForwardedHeaders:KnownProxies` (individual IP
  addresses) and `ForwardedHeaders:KnownNetworks` (CIDR ranges). Framework trust
  defaults are cleared, `ForwardLimit` is one, only `X-Forwarded-For` and
  `X-Forwarded-Proto` are enabled, and `UseForwardedHeaders` runs before HTTPS
  redirect, correlation/observability, CSRF, rate limiting, and authentication.
- Profile email edits update name, phone, and company role independently while
  leaving `ApplicationUser.Email`, `UserName`, and `EmailConfirmed` unchanged.
  A `GenerateChangeEmailTokenAsync` token is delivered to the requested address;
  `GET /api/auth/change-email` applies it with `ChangeEmailAsync` and then aligns
  the username. `AccountMessageKind.EmailChange` supplies the dedicated message.
- Anonymous forgot-password paths return the same generic 200 response for
  unknown, unconfirmed, missing-origin, delivery-failure, and provider-exception
  cases. Provider diagnostics stay in structured internal logs with the API
  correlation id.
- Regression files are `SecurityHardeningTests.cs` and `SpaHostSmokeTests.cs`;
  they cover malicious/configured host controls, the sibling notification path,
  pending-to-confirmed email change, provider-failure uniformity, production
  startup validation, configured proxy/network trust, untrusted peers, cleared
  defaults, and disabled forwarded-host processing.

### Task 4 review follow-up

- Admin `PUT /api/users/{id}` email edits now follow the same pending-confirmation
  contract as self-service profile edits. The update command applies name,
  phone, role, company role, and company assignment without replacing the
  confirmed `Email` or `UserName`; the workflow sends an Identity change-email
  token to the requested address using the configured public SPA base URL.
- `GET /api/auth/change-email` now treats email, username, confirmation state,
  and the token's security stamp as one logical transition. If username update
  fails after Identity accepts the email token, the original values are restored
  and persisted before a validation response is returned. Restoring the security
  stamp keeps that same confirmation link valid for a safe retry after the
  conflicting username is resolved.
- Regression controls are
  `AdminEmailChange_RemainsPendingAndResetUsesConfirmedAddress` and
  `EmailChangeConfirmation_WhenUserNameUpdateFails_RollsBackAndTokenCanRetry`.
  They prove non-email admin edits still apply, reset delivery stays on the
  confirmed address, confirmation reaches the requested address, failed
  username alignment leaves no partial Identity state, and the token can retry.

### Task 4 second review follow-up

- Confirmed-account `GET /api/auth/change-email` transitions now run inside an
  execution-strategy-aware `ApplicationDbContext` transaction. Both
  `ChangeEmailAsync` and `SetUserNameAsync` commit together; an Identity failure
  result or exception rolls back the database transaction and clears the stale
  change tracker. Compensation remains only for the non-relational EF InMemory
  test provider, which cannot begin a transaction; it is not the production
  atomicity guarantee.
- Admin edits now branch on the account's pre-update confirmation state.
  Confirmed users retain the pending change-email workflow. For an unconfirmed
  invited user, the transactional update command replaces email and username,
  explicitly keeps `EmailConfirmed` false, rotates the security stamp so the old
  invitation token fails, and sends the normal password-set confirmation link
  to the replacement address. Independent name/phone/role/company edits remain.
- Relational SQLite controls force both a duplicate-username Identity failure
  and a validator exception after email persistence. Both observe the original
  database state afterward and successfully retry the same change-email token.
  The unconfirmed-invite control proves the replacement address cannot log in
  or receive reset mail before confirmation, the old token is invalid, and the
  new recipient completes confirmation plus initial-password sign-in.

## Immediate Blockers Resume Checkpoint - 2026-07-22

- Resume instruction: start the next session with `Read project_state.md to get
  up to speed`, then work in
  `/Users/oldgeorge/Documents/rvt-mono/.worktrees/immediate-blockers` on branch
  `codex/immediate-blockers`. Do not resume in the root checkout on `main`.
- Base/planning commit: `5048052`. Task 2 is complete at `4173f8a` and passed an
  independent review. Task 3 is complete through `4bc2ac9` and passed an
  independent re-review after both tenant-authorization gaps were fixed.
- Task 4 initial auth hardening is committed at `1f3bcc4`; its first review
  follow-up is committed at `b9b6c46`. The second review then required real
  atomicity for confirmed email/username transitions and a separate onboarding
  path for unconfirmed admin-managed email edits.
- Those second-review fixes are implemented in the checkpoint after `b9b6c46`:
  an execution-strategy-aware `ApplicationDbContext` transaction protects the
  confirmed transition, relational SQLite tests cover result and exception
  rollback plus token retry, and unconfirmed replacements stay unconfirmed,
  invalidate the old invite, and use the normal initial-password onboarding
  link.
- Latest implementer evidence before the pause: 3/3 critical relational tests,
  64/64 owning-slice tests, and 337 portal tests passed; three opt-in PostgreSQL
  tests remained skipped. Resumed verification then passed 3/3 relational
  controls, 30/30 Task 4 tests, 337 full-project tests with the same three
  skips, and a zero-warning host build. The deterministic authorization-clock
  fixture correction is test-only commit `a6dda94`.
- Task 4 final review is complete at `a6dda94`. The reviewer found no remaining
  Critical, High, Medium, or Low issues: relational result/exception rollback,
  token retry, invited-user onboarding, origin enforcement, proxy trust,
  forgot-password uniformity, and legitimate route/DTO behavior are approved.
- Tasks 5 and 6 remain untouched: establish the explicit UTC/search timestamp
  contract, then complete schema deployment and failure reporting. Both require
  TDD and independent task review. Real PostgreSQL verification still requires
  `RVT_TEST_POSTGRES_CONNECTION`.
- Known non-task state: `apps/.nuget-packages/` is an untracked generated cache;
  do not commit it. Existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisories remain outside this repair tranche.

## Immediate Blockers Task 4 Verification Resume - 2026-07-23

- Task 4 implementation checkpoint `74d8696` passed the three exact critical
  relational/onboarding controls, the 30-test security/host slice, and the host
  build. The full portal suite initially exposed an unrelated midnight-sensitive
  Task 3 test fixture: its authorization clock was fixed at July 22 while its
  contract/deployment seed dates came from the July 23 wall clock.
- `NotificationAlertWorkflowTests.SeedNotificationAlertScenarioAsync` now has
  optional variable `scenarioNowUtc`. The two fixed-clock active-assignment
  tests pass their existing `nowUtc.UtcDateTime`, keeping all scenario dates and
  authorization boundaries on the same deterministic instant. Production code
  and non-fixed scenario tests are unchanged.
- After the fixture correction, both affected boundary tests pass 2/2 and the
  full portal project passes 337 tests with the same three opt-in PostgreSQL
  skips. Existing NU1903 advisories and the untracked `apps/.nuget-packages/`
  cache remain outside Task 4.

## Immediate Blockers Task 5 - 2026-07-23

- Status: implementation and non-provider verification are complete on
  `codex/immediate-blockers`; final disposition is `DONE_WITH_CONCERNS` because
  real PostgreSQL evidence is unavailable. `RVT_TEST_POSTGRES_CONNECTION` is
  unset, the repository has no Testcontainers harness, and sandbox/approval
  failures prevented Docker image inspection or container startup.
- Timestamp contract:
  - application search bounds are UTC `DateTime` values;
  - `SearchTimestampPolicy.ToDatabase` accepts only `Kind=Utc`, preserves ticks,
    and changes the provider-bound value to `Kind=Unspecified` for PostgreSQL
    `timestamp without time zone`;
  - `SearchTimestampPolicy.FromDatabase` preserves ticks and restores
    `Kind=Utc` before telemetry rows and graph points reach JSON;
  - daily aggregate `SampleTime` values keep database `date` semantics;
  - contract `OnHireDate` and nullable `OffHireDate` persist as UTC midnight,
    preserving calendar dates without workstation-local conversion.
- File structure:
  - new policy:
    `apps/portal/RvtPortal.Spa/Application/Monitors/SearchTimestampPolicy.cs`;
  - query-boundary changes:
    `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorService.cs`;
  - API-return boundary:
    `apps/portal/RvtPortal.Spa/Application/Data/DataApplicationService.cs`;
  - complete EF mapping audit:
    `apps/portal/RVT.DataAccess/Context/RVTSearchContext.cs`;
  - contract persistence helper:
    `apps/portal/RvtPortal.Spa/Application/Contracts/ContractCommands.cs`;
  - backend controls:
    `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs`,
    `apps/portal/RvtPortal.Spa.Tests/SearchTimestampPostgresTests.cs`, and
    `apps/portal/RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`;
  - client contract seam/control:
    `apps/portal/RvtPortal.Client/src/operations/DataViewPanels.tsx` and
    `DataViewPanels.test.tsx`.
- EF provider mapping variables: `dateTimeColumnType` remains
  `timestamp without time zone` for PostgreSQL and `datetime` for SQL Server.
  The model test enumerates all twelve `SampleTime` properties. The approved
  daily/date entries are `NoiseLevel1dayAvg`, `NoiseLevelSiteAvg`, and
  `OmnidotsPeakLevel1dayPeak`; the other nine use `dateTimeColumnType`.
- Test variables and provider gate:
  `RequiresPostgresFactAttribute.ConnectionVariable` is
  `RVT_TEST_POSTGRES_CONNECTION`; the inserted provider-test timestamp is
  `2026-07-01 14:30:00`, queried with UTC bounds and expected in JSON as
  `2026-07-01T14:30:00Z`. The separate provider test persists contract date
  `2026-07-01` through `UtcTimestampGuardInterceptor`.
- TDD evidence: the focused pre-change run failed all seven backend cases for
  the intended Kind/mapping/guard/JSON reasons, and the Europe/London client
  control failed before the formatter contract was exposed. The corresponding
  focused backend run passes 7/7; the owning backend slice passes 20 with the
  two new PostgreSQL tests skipped; the full portal suite passes 344 with five
  PostgreSQL skips (349 total). The client test passes under both
  `TZ=Europe/London` and `TZ=UTC`; the full client suite passes 66 with the
  timezone-specific control skipped under the Athens host zone, and the client
  production build succeeds. The portal host build succeeds with zero warnings
  and zero errors.
- Provider concern: neither new PostgreSQL test has executed against Npgsql and
  a live schema. They are discovered and skip explicitly when the connection
  variable is absent; this task must not be treated as provider-closed until
  both run with `RVT_TEST_POSTGRES_CONNECTION` configured.
- Environment note: one exact provider-filter attempt without `-m:1` entered an
  MSBuild IPC retry loop after sandbox socket denial; a targeted process-stop
  escalation was rejected by the broken approval backend. Fresh serial
  (`-m:1`) focused, owning, full-suite, and build commands nevertheless
  completed successfully. The existing NU1903 advisories and untracked
  `apps/.nuget-packages/` cache remain outside Task 5.

## Immediate Blockers Task 5 Review Follow-up - 2026-07-23

- Status: the review findings are fixed on `codex/immediate-blockers`. This
  section supersedes the earlier Task 5 statements that treated
  `NoiseLevelSiteAvg.SampleTime` as `date` and described the client timezone
  control as conditional.
- PostgreSQL/EF contract:
  - `noise_level_site_avg.sample_time` and
    `air_q_noise_level_site_avg.sample_time` are non-daily
    `timestamp without time zone` values;
  - only `NoiseLevel1dayAvg` and `OmnidotsPeakLevel1dayPeak` remain `date`
    among the mapped noise/vibration aggregates;
  - the 8-hour dust, hourly AirQ/final noise, and 1/5/15/20-minute vibration
    `COALESCE` fallbacks now use
    `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'`, preventing PostgreSQL from
    promoting the UTC-naive aggregate expression to `timestamptz`;
  - `RVTSearchContextModelSnapshot` now matches the runtime PostgreSQL model
    for `NoiseLevel15minAvg` and `NoiseLevelSiteAvg`;
  - `SearchTimestampPostgresTests` compares runtime EF metadata, snapshot
    source, and checked-in view SQL, while the provider-gated
    `AggregateViews_HaveExpectedProviderTypesAndAcceptUtcNaiveBounds` variable
    `expectedViewTypes` inspects and queries all affected views plus the two
    genuine daily views.
- Request boundary:
  - `DataApplicationService.NormalizeUtc` was removed;
  - application workflows return `InvalidTimestamp` for Local or Unspecified
    `FromDate`/`ToDate` values instead of relabeling ticks;
  - `DataController.TimestampQueryFields` is `["fromDate", "toDate"]` and
    rejects wire values that are not explicit `Z` instants, preserving the
    distinction model binding loses for offset strings;
  - `DataViewPanels.fromDateToApi` converts browser `datetime-local` wall time
    through `new Date(value).toISOString()` before API calls.
- Response/display boundary:
  - `MonitorDetailSummaryService.BuildMetric` applies
    `SearchTimestampPolicy.FromDatabase` to dust, noise, and vibration metric
    timestamps, so detail JSON includes `Z`;
  - `formatDateTime(value, timeZone?)` defaults to the production local zone,
    while the ordinary client test exercises explicit `UTC` and
    `Europe/London` zones in one process with no conditional skip.
- Review-fix TDD:
  - RED backend: `0 passed, 5 failed, 2 provider skips`; RED client:
    `0 passed, 2 failed`;
  - focused GREEN: backend `5 passed, 2 provider skips`; client `2 passed`;
  - owning backend slice: `34 passed, 2 provider skips`;
  - full portal suite: `347 passed, 6 provider skips, 353 total`;
  - full client suite: `68 passed`, no skips;
  - client production build succeeded;
  - portal solution build succeeded after restoring the previously absent
    `RVT.SchemaDeploy/obj/project.assets.json`, with only the repository's
    existing five NU1903 advisory warnings.
- Provider concern remains: `RVT_TEST_POSTGRES_CONNECTION` is unset, so the
  expanded live metadata/query test and the existing telemetry/provider tests
  are discovered but not executed. No provider workaround was attempted after
  controller direction.
