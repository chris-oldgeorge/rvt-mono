# Project State

## Authoritative checkpoint: PR #6 merged into main — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- PR #6, `Remediate Portal npm advisories`, was accepted and merged into
  `main` with merge commit `2542205ea5288e0804e2fa9b2443076dd0a515f2`.
  Its remote feature branch `codex/portal-npm-advisories` was deleted.
- Before merge, the feature branch incorporated `main` checkpoint `b47b498`,
  preserving the separate legacy Azure Pipelines cleanup. The only merge
  conflict was this state document; both workstream checkpoints were retained.
- Fresh Node 24 verification on the integrated head passed `npm ci`, ESLint
  with zero errors and the same two Fast Refresh warnings, all 78 Vitest
  tests, the Vite 6.4.3 production build, and `npm audit` with zero
  vulnerabilities at every severity.
- The changed-range engineering-standards ratchet and both workflow contract
  tests passed locally. Exact-head GitHub Engineering Standards run
  `30356087429` passed before merge.
- The primary `main` worktree was fast-forwarded to the merge while preserving
  its unrelated, unstaged
  `apps/portal/RvtPortal.Spa/Properties/launchSettings.json` change that sets
  `RVT__EMAIL_ENABLED` to `false`, plus an unrelated, unstaged
  `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj` edit that uses explicit
  Windows `cmd.exe`/`pushd` SPA commands. Neither local developer change was
  staged or committed.
- Remaining pending work is deliberately separate: adopt the additional
  ESLint 10 and React Hooks 7 recommended rules in a lint-modernization scope,
  and complete the documented Help Admin/R2 operator release-database audit
  before declaring that conditional release scope ready.
- Next improvement step: plan and execute the lint-modernization scope with
  deliberate React source changes and regression coverage. The Help Admin/R2
  production decision remains gated by its retained zero-finding audit
  receipt.

## Authoritative checkpoint: Portal npm advisories remediated — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Current branch: `codex/portal-npm-advisories`, synchronized with merged
  `main` checkpoint `b47b498`. The bounded implementation plan is
  `docs/superpowers/plans/2026-07-28-portal-npm-advisory-remediation.md`.
- The Node 24 `npm audit --package-lock-only` baseline reported six
  high-severity advisories: the ESLint 9 dependency graph reached vulnerable
  `minimatch`/`brace-expansion`, and Vite's lock entry held vulnerable PostCSS
  8.5.14.
- The direct lint toolchain now resolves `eslint` 10.8.0, `@eslint/js` 10.0.1,
  `typescript-eslint` 8.65.0, `eslint-plugin-react-hooks` 7.1.1, and
  `eslint-plugin-react-refresh` 0.5.3. The obsolete transitive overrides were
  removed. The lock resolves `minimatch` 10.2.6, `brace-expansion` 5.0.8, and
  PostCSS 8.5.24 while retaining Vite 6.4.3.
- ESLint 10 and React Hooks 7 expanded their recommended presets. To keep this
  security-only change behavior-neutral, `eslint.config.js` explicitly
  preserves the former Hooks rules (`rules-of-hooks` and `exhaustive-deps`)
  and leaves the three new ESLint core recommendations and React Compiler
  rules for a dedicated lint-modernization scope.
- Fresh `node:24-alpine` verification passed: `npm ci`, lint with 0 errors and
  the same two Fast Refresh warnings, all 78 Vitest tests, the Vite production
  build, and `npm audit` with zero vulnerabilities at every severity.
- The committed changed-range engineering-standards ratchet passed against
  `origin/main`. Both workflow contract tests passed, and `git diff --check`
  is clean.
- Draft PR #6, `Remediate Portal npm advisories`, is open at
  `https://github.com/chris-oldgeorge/rvt-mono/pull/6`. Exact-head Engineering
  standards run `30355269311` passed every step.
- No application runtime dependencies, application source behavior, database
  configuration, secrets, or Help Admin/R2 release-audit conditions changed.
- PR #6 has been accepted. Next step: complete the refreshed post-integration
  verification, merge it into `main`, and synchronize the local worktrees. A
  separate lint-modernization task may adopt the new ESLint 10 and React Hooks
  7 recommended rules with deliberate React source changes and regression
  coverage.

## Legacy Azure Pipelines removed — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Removed all eight imported Azure Pipeline definitions: paired main and
  pull-request YAML files from the AirQ, MyATM, Omnidots, and Svantek monitor
  module roots.
- Removed the six matching Visual Studio solution-item entries from
  `airqmonitor.sln`, `myatmmonitor.sln`, and `omnidotsmonitor.sln`.
  `svantekmonitor.sln` contained no pipeline-file reference.
- Repository-wide search now finds no Azure Pipeline filename or Azure
  Pipelines/Azure DevOps reference outside this historical state record.
- The three changed module solutions parse successfully and each still lists
  its production and test project. The monorepo solution and layout guards
  pass, the working-tree engineering-standards ratchet exits successfully,
  and `git diff --check` passes.
- CodeGraph synchronized the deletions. The refreshed index contains 1,130
  files, 20,234 nodes, and 48,999 edges, with the indexed YAML count reduced
  from 23 to 15.
- The GitHub Engineering standards and on-demand SonarQube workflows remain
  unchanged. Any Azure DevOps pipeline registered externally to a deleted YAML
  path must also be deleted or repointed in Azure DevOps.
- The pre-existing uncommitted local edit to
  `apps/portal/RvtPortal.Spa/Properties/launchSettings.json` remains untouched.

## Authoritative checkpoint: PR #5 merged into main — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- PR #5, `Harden Help asset release auditing and configure Portal email
  delivery`, was marked ready and merged into `main` with merge commit
  `2eebf3defa6bd820bb220aa324b73d1d8010e51f`. Its remote feature branch
  `codex/help-asset-url-release-audit` was deleted.
- The primary `main` worktree was fast-forwarded to the merge while preserving
  its unrelated, unstaged
  `apps/portal/RvtPortal.Spa/Properties/launchSettings.json` change that sets
  `RVT__EMAIL_ENABLED` to `false`. That local developer setting was not staged,
  committed, or included in PR #5.
- Exact-head GitHub Engineering standards run `30353814819` passed every step,
  including the pinned Node 24 setup and changed-range ratchet.
- Pre-merge full verification used a disposable
  `timescale/timescaledb:2.28.3-pg17` container on localhost port `55432`;
  the production/local PostgreSQL database on port `5432` was not touched.
  With command-scoped `RVT__POSTGRES_INTEGRATION_CONNECTION`, the complete
  `Rvt.Mono.slnx` suite passed 2,266 tests with 11 opt-in Portal database tests
  skipped. The disposable container was stopped and automatically removed.
- Portal verification in `node:24-alpine` passed npm install, lint with the
  same two pre-existing Fast Refresh warnings and no errors, all 78 Vitest
  tests, and the production Vite build.
- Remaining pending work: remediate the six high-severity npm dependency
  advisories in a dedicated dependency-change scope. Help Admin/R2 remains
  conditional on the documented operator release-database audit and retained
  zero-finding receipt; this merge does not constitute that production audit.
- Next improvement step: open a dedicated dependency-remediation task for the
  Portal npm advisories, review transitive upgrade impact, and verify the Node
  24 lint/test/build suite before proposing dependency changes.

## Authoritative checkpoint: Node 24 and engineering-standards CI hardening — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Current branch: `codex/help-asset-url-release-audit`, based on the existing
  Help audit and Portal email-toggle delivery scope. This checkpoint
  supersedes lower current-state and next-step statements while retaining them
  as historical evidence.
- CI hardening commits after the prior feature checkpoint are:
  `b9d663c` (remove the runner's undeclared ripgrep dependency),
  `743005c` (standardize the Node 24 toolchain), and `b33e86e`
  (accept deletion-only changed ranges).
- All first-party workflow references now use SHA-pinned Node 24 action
  implementations: `actions/checkout` v6 at
  `d23441a48e516b6c34aea4fa41551a30e30af803`,
  `actions/setup-dotnet` v5 at
  `26b0ec14cb23fa6904739307f278c14f94c95bf1`, and
  `actions/setup-node` v6 at
  `249970729cb0ef3589644e2896645e5dc5ba9c38`. The governed workflows and
  contract tests were updated together.
- Portal client development and build declarations now converge on Node 24:
  `apps/portal/RvtPortal.Client/.nvmrc` contains `24`,
  `package.json` requires `>=24 <25`, `@types/node` is on major 24, and the
  lockfile was regenerated with the pinned `node:24-alpine` image used by the
  Portal Dockerfile.
- The GitHub Engineering standards check exposed a verifier defect: a retained
  source file whose range change only deleted lines was rejected for lacking a
  new-side range. `scripts/engineering-standards/verify.mjs` now records
  zero-length new-side hunks as deletion-only paths, so those valid changes
  can be verified while metadata-only changes and empty added source files
  remain rejected.
- GitHub Actions run `30352635145` confirmed the Node 24 transition itself:
  checkout v6, setup-dotnet v5, setup-node v6, npm install, restore, standards
  model, engineering configuration, and workflow-contract steps all passed.
  The remaining changed-range failure was unrelated to Node. It exposed style
  diagnostics in the full PR diff that a local check had missed because local
  branch `main` pointed at `b9d663c` while `origin/main` and the PR base were
  `129419a`. For this branch, committed-range verification must use
  `--base origin/main` or the CI `--base auto`, not the local `main` label.
- The full-PR style remediation formats the release-audit CLI and option
  predicates, simplifies Portal registration and test collection creation,
  removes two unused imports, orders test imports, renames the private test
  timestamp field to camel case, and annotates intentional JSON literals.
  The shared Help URL corpus keeps its neutral namespace with a documented
  `IDE0130` suppression because the same source is linked into two differently
  named test projects.
- `tests/verify-engineering-standards.test.sh` contains the committed-range
  regression: it deletes a member from `src/Clock.cs`, verifies the range, and
  asserts successful analysis. The full verifier scenario suite passed.
  Mode-only changes still fail closed.
- After the style remediation, targeted Roslyn style and whitespace checks
  passed. `RvtPortal.Application.Tests` passed 75/75 tests and
  `RvtPortal.Spa.Tests` passed 546 tests with 11 opt-in PostgreSQL tests
  skipped. The committed remote-base ratchet passed against `origin/main`
  with baseline decreases and no policy violation. Replacement GitHub Actions
  run `30353535675` passed every step, including Node 24 setup and the
  changed-range engineering-standards check.
- Node 24 container verification passed: ESLint completed with the same two
  pre-existing Fast Refresh warnings and no errors, all 78 Vitest tests passed,
  and the production Vite build passed. The current dependency tree still
  reports six high-severity npm advisories; dependency remediation remains
  separate pending work.
- No secrets, application variable definitions, or deployment behavior changed
  in this hardening. The Portal email setting remains `RVT:EMAIL_ENABLED`,
  supplied by environment variable `RVT__Email_ENABLED`, defaulting to `true`.
- PR #5 remains a draft. Next step: review the complete PR scope and, when
  approved, mark it ready for review. The separate six-high-severity npm
  advisory remediation remains pending and is not part of this toolchain
  alignment.

## Authoritative checkpoint: Help audit and Portal email-toggle scopes — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Current authoritative head: `55cba79` (`feat: configure portal email
  delivery`). This checkpoint supersedes every lower current-state or
  next-step statement while preserving all lower sections as historical
  evidence. The current branch remains
  `codex/help-asset-url-release-audit`.
- Help asset URL release-audit scope is implemented and remains `CONDITIONAL`:
  Help Admin/R2 cannot be marked ready until an operator runs the deployed
  audit against each target release database and retains a complete
  zero-finding receipt. The Help audit checkpoint through `8f69d82` remains
  accurate as historical implementation detail; commits `48f4121`, `400490d`,
  `e5ef12d`, and `55cba79` follow it.
- Portal email-toggle scope is complete. The changed source and test files are
  `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs` and
  `apps/portal/RvtPortal.Spa.Tests/SendGridConfigurationTests.cs`.
  `RVT:EMAIL_ENABLED` controls `SendGridMailOptions.Enabled`; its environment
  variable spelling is `RVT__Email_ENABLED` (configuration keys are
  case-insensitive). An absent value defaults to `true`, and `false` disables
  the SendGrid provider.
- Portal registration preserves the existing `EmailConfiguration` API-key and
  sender metadata behavior. It registers the same resolved options both
  directly with `AddSendGridMail(sendGridMailOptions)` and through
  `IOptions<SendGridMailOptions>` using `Options.Create`, so direct-provider
  delivery and options consumers agree on the enabled state. It does not alter
  credentials, deployment secrets, `EmailConfiguration`, `UseDebugEmail`, or
  `Auth:SkipPasswordResetEmail` behavior.
- Reported final test totals using
  `/private/tmp/rvt-dotnet-sdk-10.0.302/dotnet` with `--no-restore --nologo`:
  Application tests: 75 passed, 0 skipped, 0 failed (75 total); SPA tests:
  546 passed, 11 opt-in PostgreSQL tests skipped, 0 failed (557 total);
  combined: 621 passed, 11 skipped, 0 failed (632 total). `git diff --check`
  passed after this checkpoint update.

## R2 Help asset URL release audit implemented; operator receipt pending — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- This top checkpoint is the current authority for the Help asset URL release
  audit. It supersedes the provisional Task 7 checkpoint and every lower R2
  implementation/next-step statement while retaining the lower sections as
  historical evidence.
- Current branch: `codex/help-asset-url-release-audit`. The implementation
  sequence after approved base `129419a` is:
  `325074a` (implementation plan), `9dfafef` (transaction test seam),
  `e4dd080` (shared URL policy), `3c102ba` (behavioral policy tests),
  `a38e4f8` (mutation-policy delegation), `f574299` (audit behavior-test
  refinement), `369052e` (URL-limit behavior cleanup), `88c018e` (audit core),
  `d29f0ec` (read-only audit runner), `e5aac08` (receipt-path hardening),
  `9f307b2` (namespaced entry-point clarification), `76fde37` (transaction
  proof), `a61d580` (solution registration and SQL retirement), `c3534a7`
  (artifact publishing and operator documentation), `d989f66` (Task 7 stale
  SQL-guidance correction), and `8f69d82` (solution-membership ratchet
  correction).
- `apps/portal/RvtPortal.Application/Help/HelpAssetUrlPolicy.cs` is the sole
  BCL-only URL authority.
  `apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs` delegates
  mutation validation to it. The shared corpus is
  `apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyCases.cs`;
  pure policy coverage is in
  `apps/portal/RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyTests.cs`,
  and mutation/audit parity, receipt secrecy, exit behavior, classification,
  and opt-in transaction coverage are in
  `apps/portal/RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs` and
  `apps/portal/RvtPortal.Spa.Tests/HelpApplicationServiceTests.cs`.
- The isolated audit adapter is
  `apps/portal/RVT.ReleaseAudit/RVT.ReleaseAudit.csproj`, with its entry point,
  option parser, reader/classifier, and receipt writer in `Program.cs`,
  `ReleaseAuditOptions.cs`, and `HelpAssetUrlAudit.cs`. It reads the fixed
  `public.help_asset` relation in a repeatable-read, read-only transaction,
  classifies every row through the application policy, rolls the transaction
  back, and writes a deterministic receipt containing IDs and stable violation
  codes but no URLs or credentials.
- `RVT.ReleaseAudit` is registered in both `Rvt.Mono.slnx` and
  `apps/portal/RvtPortal.Spa.sln`.
  `apps/portal/scripts/verify-backend.sh` publishes and asserts
  `artifacts/release-audit/RVT.ReleaseAudit.dll` alongside
  `artifacts/backend/RvtPortal.Spa.dll`. These local artifact directories are
  generated verification output, not tracked release evidence. Audit receipts
  belong in the release evidence store outside source control.
- Operator and maintenance contracts are recorded in
  `apps/portal/README.md`,
  `docs/release/portal/CUTOVER_RUNBOOK.md`,
  `docs/development/portal/development-guidelines.md`,
  `docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md`, and
  `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`.
  The required operator sequence is: deploy the Portal and audit artifacts
  from one revision; apply EF migrations and `RVT.SchemaDeploy`; provide a
  least-privilege read-only connection through the environment; run
  `help-asset-urls`; and retain the receipt outside the repository.
- Variable definitions contain no values:
  - `RVT_RELEASE_AUDIT_CONNECTION` is the only runtime source for the targeted
    release database connection;
  - `RVT_TEST_POSTGRES_CONNECTION` enables only the opt-in disposable
    PostgreSQL transaction test, which uses `pg_temp.help_asset` and otherwise
    skips.
- Stable policy violation kinds are `required`, `too_long`, `not_canonical`,
  `unsafe_character`, `unsupported_relative_path`,
  `absolute_https_required`, `host_required`, `user_info_forbidden`, and
  `malformed_uri`. Stable process outcomes are exit `0` for a complete
  zero-finding audit, `10` for a complete audit with findings, `2` for invalid
  input or a missing runtime connection, and `3` for database/read,
  cancellation, or receipt-write failure. Exit `10`, `2`, `3`, a missing
  receipt, or any finding blocks Help Admin enablement.
- The obsolete
  `apps/portal/docs/release/validate-help-asset-urls.sql` is deleted. Active
  Portal/release documentation no longer invokes it. Its remaining text
  references are confined to superseded implementation plans and historical
  checkpoints; the executable audit is the live production-query authority.
- Fresh final verification used
  `/private/tmp/rvt-dotnet-sdk-10.0.302/dotnet`, ran serially with both database
  variables unset, and produced:
  - solution restore: all projects up-to-date in `0.59s`, 0 warnings and
    0 errors;
  - Release solution build: success in `10.54s`, 0 warnings and 0 errors;
  - Application tests: 75 passed, 0 skipped, 0 failed;
  - SPA tests: 544 passed, 11 opt-in database tests skipped, 0 failed;
  - combined tests: 619 passed, 11 skipped, 0 failed, 630 total;
  - `tests/verify-mono-solution.test.sh`,
    `tests/verify-rvt-common-source-boundary.test.sh`, and
    `scripts/verify-engineering-standards.sh --working-tree`: passed, with the
    standards ratchet reporting baseline decreases and no increase; and
  - `git diff --check`: passed.
- Verification limitation: the approved Task 8 plan named the nonexistent
  `tests/verify-rvt-mono-solution.test.sh`. Repository inventory and history
  confirmed the tracked aggregate ratchet is
  `tests/verify-mono-solution.test.sh`; the owner authorized that exact
  substitution. VSTest and the engineering-standards disposable-worktree
  check required minimum sandbox escalation for local socket/worktree access;
  the commands and scope were otherwise unchanged.
- The no-secret audit reviewed all 50 matches. They are explicit CI,
  local-test/local-development sentinel values or documentation placeholders;
  no release credential is committed. The retired-SQL audit reviewed all nine
  matches in the pre-checkpoint tree and the new checkpoint reference (ten
  final-tree matches); they are historical/superseded records, not an active
  script or operator instruction.
- No production or release database or credential was used. Final verification
  made no database connection. The earlier transaction proof used only a
  disposable local PostgreSQL container and a connection-scoped
  `pg_temp.help_asset` table.
- Help Admin remains `CONDITIONAL` and R2 remains unchecked. Tool
  implementation and local test evidence cannot make it `READY`; every target
  release database still requires one complete zero-finding receipt produced
  from the deployed revision.
- R4 Portal blob client/service unification remains approved future pending
  work and is not part of this implementation.
- Next step: an operator must run the deployed audit against each target
  release database and preserve each zero-finding receipt. Do not start R3 or
  blob unification as the next action.

## R2 implementation plan authored; execution not started — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- This top checkpoint is the sole current authority for the Help asset URL
  release-audit work. It supersedes the lower R2 pause/next-step wording while
  preserving the integrated R1 state.
- The configured workspace
  `/Users/oldgeorge/Documents/rvt-mono` resolves to
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/repository-engineering-standards`.
  That clean historical worktree was switched to the plan-only branch
  `codex/help-asset-url-release-audit-plan`, based exactly on synchronized
  `main`/`origin/main` commit `129419a`
  (`docs: approve Help asset URL release audit design`). The main checkout
  remains `/Users/oldgeorge/Developer/rvt-mono`.
- The implementation plan is now authored at
  `docs/superpowers/plans/2026-07-28-help-asset-url-release-audit.md`.
  It follows the approved design in
  `docs/superpowers/specs/2026-07-28-help-asset-url-release-audit-design.md`
  and defines eight independently reviewable, test-first tasks with focused
  RED/GREEN commands and commits.
- Planned source structure:
  - `RvtPortal.Application/Help/HelpAssetUrlPolicy.cs` will become the sole
    BCL-only URL authority;
  - `RVT.ReleaseAudit/{RVT.ReleaseAudit.csproj,Program.cs,ReleaseAuditOptions.cs,HelpAssetUrlAudit.cs}`
    will form the isolated Npgsql console adapter;
  - `RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyCases.cs` will be the
    single test-corpus source linked into the SPA audit/parity tests;
  - `RvtPortal.Application.Tests/Help/HelpAssetUrlPolicyTests.cs` and
    `RvtPortal.Spa.Tests/HelpAssetUrlAuditTests.cs` will cover pure policy,
    mutation/audit parity, receipt/exit secrecy, and opt-in PostgreSQL behavior;
  - both solution catalogs and the Portal backend publish script will include
    the audit artifact; and
  - the obsolete `apps/portal/docs/release/validate-help-asset-urls.sql` and
    its two source-contract tests will be deleted during implementation.
- Planned variable and contract definitions:
  - `RVT_RELEASE_AUDIT_CONNECTION` is the only runtime release connection
    source;
  - `RVT_TEST_POSTGRES_CONNECTION` is the only opt-in disposable PostgreSQL
    test connection;
  - production reads are fixed to `public.help_asset`; the test-only relation
    is fixed to the connection-scoped `pg_temp.help_asset`;
  - URL violation codes remain `required`, `too_long`, `not_canonical`,
    `unsafe_character`, `unsupported_relative_path`,
    `absolute_https_required`, `host_required`, `user_info_forbidden`, and
    `malformed_uri`;
  - process exit codes remain `0` pass, `10` complete findings, `2` invalid
    input, and `3` database/incomplete-read/receipt failure; and
  - mutation validation preserves its existing required, maximum-length, and
    unsafe-URL field/message contracts.
- No implementation source, project, test, SQL, workflow, release script, or
  runtime documentation has been changed. No production/release/shared
  database or credential was accessed. The historical production credential
  must not be used during implementation.
- Help Admin remains `CONDITIONAL` and R2 remains unchecked. Implementing the
  tool will not by itself make the capability `READY`; every targeted release
  database still needs a complete zero-finding receipt from the deployed
  revision.
- The blob storage client/service unification remains approved future pending
  R4 work and is explicitly outside this plan.
- Plan self-review confirmed coverage of every approved violation code, the
  four exit outcomes, mutation message compatibility, fixed production/test
  relations, read-only repeatable-read execution, deterministic secret-safe
  receipts, SQL deletion, artifact publishing, and the conditional release
  boundary. `git diff --check` and the engineering-standards working-tree
  ratchet passed with no increase.
- Next step: review the committed plan, then choose its execution mode:
  subagent-driven development (recommended) or inline execution. Do not begin
  the implementation branch `codex/help-asset-url-release-audit` until that
  choice is made.

## R1 integrated; R2 release-audit design approved and paused — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- This top checkpoint is the sole current authority for R1 and Help Admin. It
  supersedes every lower checkpoint, current-state heading, resume instruction,
  release gate, and next-step instruction concerning either item. Lower
  sections are preserved only as historical evidence and must not override or
  direct work away from this checkpoint.
- Current Git integration state: R1 was merged into local `main` by
  `54d59cb` (`Merge R1 architecture guards`). This checkpoint is committed
  after that merge and is included in the same integration push to
  `origin/main`; verify local and remote refs before any later mutation.
- This documentation-only R2 approval checkpoint is committed and pushed on
  `main`. At the recorded pause boundary, local `main` and `origin/main` are
  synchronized and the working tree is clean.
- R1 is integrated into `main`. The reviewed source branch remains
  `codex/r1-architecture-guards` at `151971b`. The implementation builds on `aaa20de`
  (`Repair monorepo test paths`) and `f59d5d1` (`Record monorepo path repair
  verification`) and is recorded in `0fd8921` (`test: add portable monitor
  repository layout`), `b1433eb` (`style: satisfy repository layout
  standards`), `f44bf24` (`test: share monitor repository layout discovery`),
  `a7b84c2` (`test: expose Mapperly architecture diagnostics`), and `74de1f8`
  (`test: prove R1 architecture guards reject mutations`). Final fail-closed
  portability hardening is recorded in `d459dbb` (`test: define fail-closed
  repository layout`), `e9c98d1` (`fix: fail closed on repository layout
  ambiguity`), `8584049` (`test: require actionable repository root
  guidance`), `81ef27f` (`fix: guide relocated test hosts to explicit root`),
  and `d1dfad6` (`style: use current MSTest string assertions`). Merge commit
  `c4b303b` reconciles the branch with its then-current `origin/main`; it is not
  an R1 implementation unit. State commit `cb5fd5d` records the synchronized
  pre-integration main/origin state. Merge commit `54d59cb` integrates the
  reviewed branch into `main`.
- `Rvt.Monitor.IntegrationTesting.RepositoryLayout` is the shared monitor
  test-support authority for the monorepo root and repository-relative paths.
  It independently evaluates four candidates: test output, the
  `[CallerFilePath]` source location, process current directory, and optional
  `RVT_MONOREPO_ROOT`. Every discovered or configured root must contain
  `Rvt.Mono.slnx` plus either a normal `.git` directory or a worktree `.git`
  file. A configured root is validated directly and fails without fallback
  when invalid. Existing path components are physically canonicalized so
  aliases of one checkout collapse; candidates for distinct checkouts fail as
  ambiguous instead of selecting one.
- `RepositoryLayout.GetPath` accepts one or more segments, but each segment
  must be a single non-empty, non-rooted repository-relative name: rooted
  input, separators, null/blank values, `.` and `..` traversal, and an empty
  segment collection are rejected. The normalized result is also required to
  remain below the canonical repository root.
- Eight MyATM and two Svantek repository-reading test files now use the shared
  helper. No AirQ, Omnidots, ReportingMonitor, Portal, shared-library,
  production monitor, database, package, or later-roadmap migration was
  absorbed.
- Focused normal proof passed 19/19 helper tests, 38/38 MyATM tests, and 5/5
  Svantek tests. The same 19/19, 38/38, and 5/5 suites passed with `CI=true`,
  `UseArtifactsOutput=true`, external disposable artifacts, and
  `RVT_MONOREPO_ROOT` set to the validated R1 checkout. Restore/build/test
  execution was deliberately serialized, then tests used
  `--no-build --no-restore`, because this host's parallel MSBuild child nodes
  are unreliable.
- The deterministic-CI proof also recorded one expected bounded RED without
  `RVT_MONOREPO_ROOT`: compiler `PathMap` mapped `[CallerFilePath]` to
  `/_/...`, VSTest moved the process current directory to the external
  test-assembly directory, and output was external. Discovery failed closed
  and named all candidates while instructing the operator to set
  `RVT_MONOREPO_ROOT` to a validated root. The identical explicit-root
  invocation passed; no hidden checkout inference was added.
- `tests/verify-r1-architecture-guards.test.sh` created and removed a detached
  disposable worktree. It proved that Mapperly in the MyATM test project and a
  forbidden `Rvt.Monitor.Common` package dependency in the MyATM production
  project both fail for their intended architecture diagnostics, then proved
  the restored baseline passes 2/2.
- Mono-layout, mono-solution, RVT common source-boundary normal/regression,
  engineering-standards, and `git diff --check` verification passed. No
  PostgreSQL integration credential or production/release database was used.
- Post-merge verification on `main` passed the disposable architecture
  mutation harness: baseline 2/2, both intended mutations rejected, and
  restored baseline 2/2. The engineering-standards working-tree gate and
  `git diff --check origin/main..HEAD` also passed, and the disposable
  worktree was removed.
- Current R1 file structure:
  - `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/RepositoryLayout.cs`
    — shared repository-root and path authority;
  - `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/RepositoryLayoutTests.cs`
    — 19 direct cases covering output, source, current-directory, and configured
    roots; invalid configuration; distinct-checkout ambiguity; physical alias
    collapse; `.git` file/directory markers; actionable no-candidate failure;
    and valid/rejected path composition;
  - eight repository-reading files under
    `apps/monitors/myatmmonitor/MyAtmMonitorTests/` and two under
    `apps/monitors/svantekmonitor/SvantekMonitorTests/` — shared-helper
    consumers;
  - `tests/verify-r1-architecture-guards.test.sh` — disposable two-mutation
    proof; and
  - `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
    — authoritative checklist, with R1 checked complete.
- Current R1 variable definitions:
  - `RepositoryRootEnvironmentVariable` names `RVT_MONOREPO_ROOT`;
    `_pathComparer` supplies the platform path comparison, and
    `_repositoryRoot` lazily resolves `Root`;
  - `FindRepositoryRoot` receives `outputDirectory`, `sourceFilePath`,
    `currentDirectory`, and `configuredRoot`; `sourceDirectory`, `candidates`,
    `normalizedConfiguredRoot`, and `rootsByCanonicalPath` hold discovery,
    validation, physical canonicalization, and ambiguity state;
  - `RepositoryCandidate.Label` identifies `RVT_MONOREPO_ROOT`, output, source,
    or current directory, while `RepositoryCandidate.Root` holds its canonical
    checkout; `repositoryRoot`, `combinedPath`, and `rootPrefix` enforce the
    `GetPath` below-root result;
  - the mutation harness uses `repo_root`, `temp_root`, `mutation_root`,
    `test_output`, `test_project`, `mapperly_project`, `consumer_project`,
    `mapperly_filter`, `source_filter`, and `baseline_filter`; and
  - `label`, `filter`, `expected_diagnostic`, and `status` define each
    mutation assertion.
- Help Admin's R2 implementation is merged to `main`, but its release status
  remains `CONDITIONAL` and R2 remains unchecked. The SQL preflight and the
  .NET application validator are demonstrably not equivalent: SQL accepts the
  malformed `https://:443/guide.pdf`, while its case-sensitive scheme match
  rejects `HTTPS://docs.rvt.test/guide.pdf`, which the application accepts
  case-insensitively. A zero-row SQL result is therefore not a sufficient
  release receipt.
- The dedicated R2 release-audit design is approved in
  `docs/superpowers/specs/2026-07-28-help-asset-url-release-audit-design.md`.
  It selects one shared BCL-only `HelpAssetUrlPolicy` plus a separate read-only
  Npgsql `RVT.ReleaseAudit` adapter. The audit must scan every Help asset,
  produce a secret-safe receipt, and fail closed on any finding or incomplete
  execution. The SQL regex is non-authoritative.
- Implementation has not started. No implementation branch/worktree, project,
  policy, test, SQL, or workflow change has been created for this design, and
  no implementation plan has been authored. This is the explicit pause point.
- The functionality matrix and Portal development guideline now record the
  approved audit requirement and retain Help Admin as `CONDITIONAL`.
  No production/release database or credential was accessed while recording
  the decision.
- The architecture review now marks R1 and R9 complete. R2 remains
  conditional/unchecked; R3-R8 and R10-R11 remain pending and unchanged.
- Next step after resumption: review the approved R2 release-audit
  specification, then author its implementation plan. Do not begin code from
  this checkpoint without that plan.

## Historical/superseded checkpoint — local main reconciliation after Help Admin merge — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Historical evidence only: this section records the intermediate state before
  the later synchronization recorded in the top checkpoint. Its ahead,
  unpublished, and push-next wording is superseded and must not be used as a
  current action instruction.
- At this checkpoint, the local integration checkout at
  `/Users/oldgeorge/Developer/rvt-mono` was on `main`, fast-forwarded to
  `origin/main` commit `1e2fbf3`, and had one intentionally retained local commit:
  `244816b` (`build: include Portal communication projects in solution`).
- The retained Visual Studio edit adds the in-repository
  `Rvt.Communication.Abstractions` and `Rvt.Communication.SendGridMail`
  projects, including their Debug/Release configuration mappings, to
  `apps/portal/RvtPortal.Spa.sln`. This matches the direct project references
  already used by `RvtPortal.Spa`.
- The concurrent `RvtPortal.Spa.csproj` edit changed only the UTF-8 BOM and was
  deliberately discarded as non-semantic formatting churn.
- Recovery branch `codex/visual-studio-solution-reconcile` preserves the
  pre-pull reconciliation commit as `d5aa349`.
- Verification on reconciled `main`:
  - `dotnet sln apps/portal/RvtPortal.Spa.sln list` resolves both communication
    projects and all nine existing Portal projects;
  - restore completed successfully for all eleven solution projects;
  - the first parallel build exposed the host's known named-pipe permission
    restriction in MSBuild child nodes (`MSB4166`);
  - the identical serial Release build (`-m:1`) passed with 0 warnings and
    0 errors.
- At this historical checkpoint only, the local reconciliation had not yet
  been pushed and `main` was ahead of `origin/main`. That condition no longer
  applies: the top checkpoint records clean, synchronized refs and no pending
  `main` push.
- The Help Admin release remains `CONDITIONAL`; the zero-row release-database
  URL-validation receipt gate is unchanged.

## Historical/superseded checkpoint — Help Admin R2 conditional, R1 partial — 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Historical evidence only: the top checkpoint now supersedes this entire
  section. Its R1-partial status, SQL-only release gate, and references to the
  lower Help Admin active-state section must not be used as current action
  instructions.
- R9 is integrated into `main`: merge commit `1a48378` and integration-state
  commit `0675479`. Its final review returned `Ready—Yes`.
- At this checkpoint, R1 had started only as a separate worktree/branch:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/r1-architecture-guards` on
  `codex/r1-architecture-guards` at `0675479`. The worktree is clean and has no
  R1 implementation commit. The 17 known stale repository-layout/Mapperly
  failures recorded by the R9 aggregate remain the bounded R1 starting set;
  R1 was partial/not complete at that time.
- R2 implementation was on `/private/tmp/rvt-mono-help-admin`, branch
  `codex/help-admin-application-boundary`. This final-review wave began from
  `a86b041` (`fix: canonicalize materialized standards roots`) and retains the
  earlier Help application-boundary, adapter, client, HTTP, browser, and
  rollback evidence.
- Help Admin release status is `CONDITIONAL`, not `READY`. There is no recorded
  execution of
  `apps/portal/docs/release/validate-help-asset-urls.sql` against the release
  databases, and this checkpoint did not access a production or release
  database.
- Historical gate recorded at the time (superseded; do not use as release
  authority): execute the committed read-only script against every release
  database and require zero returned rows. The top checkpoint invalidates this
  as a sufficient gate because the SQL and .NET policies are not equivalent.
- Final-review code/test corrections:
  - the SQL now rejects whole-value whitespace as
    `HelpMutationValidator` does, including
    `https://docs.rvt.test/a b.pdf` and `/help-assets/a b.pdf`;
  - `CutoverReadinessTests` contains a non-live policy guard plus an opt-in
    PostgreSQL sample-row execution using only `pg_temp.help_asset`;
  - `HelpAdminPanel` holds publication-button refs by article ID, arms focus
    only after the refreshed overview resolves and commits, and selects the
    next, previous, first refreshed, or New FAQ fallback when a status filter
    removes the changed article.
- The canonical `main..HEAD` engineering-standards RED additionally exposed
  CSH-002 and WEB-001 formatting failures plus IDE0305 collection and IDE1006
  private-field naming failures on the Help branch. The scoped correction
  formats only the reported Help/client surfaces, uses collection expressions
  in the read/write adapters and API mapper, and renames private fields to
  `contentTypes`, `assetTypes`, `articleSummaryProjection`, and
  `articleProjection`. No verifier, baseline, exception, or standards-policy
  file was changed.
- Current file structure for this wave:
  - `apps/portal/docs/release/validate-help-asset-urls.sql` — read-only
    persisted-row release gate;
  - `apps/portal/RvtPortal.Spa.Tests/CutoverReadinessTests.cs` — static
    read-only/policy guard and opt-in PostgreSQL sample execution;
  - `apps/portal/RvtPortal.Client/src/admin/HelpAdminPanel.tsx` and
    `HelpAdminPanel.test.tsx` — post-refresh publication focus and fallbacks;
  - `apps/portal/RvtPortal.Client/src/api/client.ts`, `client.test.ts`,
    `src/dtos.ts`, and `tests/e2e/help-admin.spec.ts` — Prettier-only branch
    range compliance;
  - `apps/portal/RvtPortal.Application/Help/HelpMutationValidator.cs`,
    `RvtPortal.Spa/Adapters/Help/EfHelpReadAdapter.cs`,
    `EfHelpWriteAdapter.cs`, `RvtPortal.Spa/Api/Mappers/HelpApiMapper.cs`, and
    `RvtPortal.Spa/ServiceCollectionExtensions.cs` — formatter/naming-only
    branch range compliance;
  - `docs/release/portal/FUNCTIONALITY_READINESS_MATRIX.md` and
    `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
    — conditional release decision and unchecked R2 gate.
- Current Help Admin variable definitions:
  - `PendingFocus` includes `article`, `publication`, `asset`, `add-asset`, and
    `new-article` targets;
  - `articleEditRefs` and `articlePublicationRefs` are maps keyed by persisted
    article ID; `assetTitleRefs` is keyed by immutable client asset key;
  - publication refresh selection uses `articlesBeforeRefresh`,
    `changedIndex`, `refreshedOverview`, `refreshedIds`, `nextArticle`,
    `previousArticle`, and `fallbackArticle`;
  - cutover sample execution separates `acceptedUrls`,
    `rejectedUrls`, `expectedRejectedIds`, and `actualRejectedIds`.
- Fresh final-review verification:
  - RED client focus run: 3 intended failures and 6 passes;
  - RED cutover run: the non-live whitespace guard failed and the opt-in
    PostgreSQL sample execution skipped because
    `RVT_TEST_POSTGRES_CONNECTION` was unset;
  - GREEN focused client: 3 files and 60 tests passed;
  - GREEN focused backend/cutover: 59 passed and the same 1 opt-in PostgreSQL
    test skipped;
  - client lint exited 0 with 0 errors and the 2 pre-existing
    `DataViewPanels.tsx` Fast Refresh warnings;
  - client production build passed with 1,606 modules transformed;
  - scoped C# whitespace and IDE0305/IDE1006 formatter verification passed;
  - Prettier check passed for all seven changed Help client/e2e surfaces;
  - `git diff --check` passed.
- Historical next steps at the time (superseded; do not follow): obtain
  zero-row SQL receipts, complete R1, then continue to R3. Use the top
  checkpoint's shared-policy/.NET-audit R2 decision and completed R1 status
  instead.

## R9 integrated into main — R1 next 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Integration worktree: `/Users/oldgeorge/Developer/rvt-mono`; branch: `main`.
- R9 was merged with merge commit `1a48378`
  (`Merge repository engineering standards enforcement`). The reviewed feature
  branch remains `codex/repository-engineering-standards`, whose final recorded
  tip was `975d828` (`docs: align final readiness record`).
- The combined final audit found no Critical, Important, or Minor issue and
  returned `Ready—Yes`.
- Final-audit remediation includes trusted requested-head/base policy,
  monotonic baselines, exact current-and-trusted exception intersection,
  automatic least-privilege pull-request CI, locked restores, fail-closed
  exact-path exceptions, exact workflow execution schemas, load-bearing
  documentation authority links, and tracked full-logical-unit review
  evidence.
- Post-merge verification passed on `main`:
  - model and module-policy tests: 50/50;
  - engineering configuration and full verifier scenarios;
  - automatic workflow, integration, and manual Sonar guards with zero
    accepted mutations;
  - working-tree ratchet, documentation mutations, source-boundary and runner
    guards;
  - locked restore and a full serial build with 0 warnings and 0 errors;
  - `git diff --check` and clean-worktree checks.
- Integration is complete locally. Push `main`, then start the separate,
  test-first R1 plan for stale repository-layout and Mapperly assumptions.
- Residual classified work is unchanged: 2,050 baselined identities / 8,024
  diagnostics; and backend aggregate results of 1,896 passed, 203 failed,
  10 skipped (186 dedicated-test-DB cases and 17 existing R1 layout/Mapperly
  cases). The five historical NU1903 advisories recorded below are no longer
  present in the post-merge build.

## Historical checkpoint — R9 implementation recorded before final audit

- Resume instruction: `Read project_state.md to get up to speed`.
- Physical worktree:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/repository-engineering-standards`
  (also exposed through `/Users/oldgeorge/Documents/rvt-mono`).
- Branch: `codex/repository-engineering-standards`.
- Implementation head before the Task 7 documentation commit:
  `22865329d41e28f7d1f60b52bc6bd6a4bc01ff82`
  (`test: tokenize shell punctuation in workflow guards`).
- Tasks 1–7 of
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`
  are implemented. The design status is `Implemented`, R9 is checked complete
  in the architecture/code-quality review, and the operating guide and
  evidence report are linked from `docs/index.md`.
- R9 commits by area:
  - standard/plan: `e083eda`, `9bd194b`, `1b05562`, `170b4d8`;
  - root configuration: `2212185`;
  - model: `3d01096`, `4f23da2`, `e7757de`;
  - verifier: `988c0e9`, `6bc3b26`, `f5f1472`, `8d50a96`, `20c3bde`,
    `5c850cc`, `30acaca`;
  - frontend policy: `e8759f6`, `d0d659c`;
  - baseline/module policy: `604912d`, `71a84a0`, `20d89b0`, `331c181`,
    `d3ab241`, `76eecc4`;
  - local/CI integration: `bfa78d0`, `dd3910d`, `299d971`, `c03ba2e`,
    `2286532`;
  - Task 7: `docs: record engineering standards enforcement` (the commit
    containing this checkpoint).
- Baseline generated 2026-07-27: 2,050 identities / 8,024 diagnostics.
  By tool:
  - `dotnet-format-analyzers`: 27 identities / 61 diagnostics;
  - `dotnet-format-style`: 1,933 / 7,872;
  - `dotnet-format-whitespace`: 59 / 59;
  - `eslint`: 1 / 2;
  - `prettier`: 30 / 30.
  The exception register is empty.
- Final standards evidence:
  - model/policy: 50/50 passed;
  - configuration hierarchy, verifier scenarios, integration sequence, and
    working-tree ratchet passed with no increase;
  - all 12 root shell guards passed;
  - the temporary real mutation proof exited 1 for exact C# `CSH-002` and
    TypeScript `@typescript-eslint/naming-convention` path/rule evidence;
  - the disposable mutation worktree was removed and production source was
    untouched.
- Aggregate evidence:
  - locked restore passed;
  - build passed with 0 errors and 5 existing NU1903 advisories for
    `System.Security.Cryptography.Xml` 10.0.7;
  - backend tests: 2,109 total, 1,896 passed, 203 failed, 10 skipped;
  - 186 failures require the deliberately absent
    `RVT__POSTGRES_INTEGRATION_CONNECTION`;
  - 17 failures are stale monorepo-layout/Mapperly expectations assigned to
    R1;
  - Portal SPA passed 425 with 10 PostgreSQL-dependent skips;
  - frontend lint passed with 0 errors and 2 existing Fast Refresh warnings,
    Vitest passed 68/68, and Vite built 1,606 modules;
  - `git diff --check` passed.
- No production database credential was used, no failing test was hidden, and
  no enforcement gate was weakened. R9 completion describes the enforcement
  deliverable; the classified backend outcomes remain visible work.
- Exact commands and requirement traceability are in
  `docs/reviews/2026-07-27-engineering-standards-enforcement-report.md`.
- Historical next step at the time was to write and execute a separate R1 plan
  for stale MyAtm/Svantek and related monitor architecture paths plus the
  shared repository-layout helper. The top pending-final-review checkpoint
  supersedes that instruction until R9 is independently accepted.
- Remaining sequence after R1: R2 Help Admin; R3 reporting lineage; R4 dead
  Portal infrastructure/blob unification; R5 Portal vertical extraction; R6
  monitor narrow ports; R7 async compatibility removal; R8 selectable Common
  infrastructure; R10 structural reduction; R11 ambient configuration.

## R9 latest checkpoint — Task 6 complete, Task 7 next 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Branch: `codex/repository-engineering-standards`.
- Committed `HEAD`:
  `22865329d41e28f7d1f60b52bc6bd6a4bc01ff82`
  (`test: tokenize shell punctuation in workflow guards`).
- Tasks 1–6 of the repository engineering-standards enforcement plan are
  implemented. Task 5 remains independently approved at `76eecc4`; Task 6
  production integration is at `bfa78d0`.
- Task 6 integrates the working-tree ratchet into `scripts/build-mono.sh`
  after restore and before compile, and integrates the base/head ratchet into
  `.github/workflows/sonarqube.yml` after .NET restore and Portal `npm ci`
  and before Release build. Portal coverage does not reinstall dependencies.
- Task 6 guard-hardening commits are:
  - `dd3910d` — initial workflow guard mutation coverage;
  - `299d971` — wrapper, option, and executable classifier coverage;
  - `c03ba2e` — conservative npm and dotnet phase classification;
  - `2286532` — shell control/redirection punctuation tokenization.
- The final punctuation correction added ten real bypass regressions. RED
  proved exactly 20 acceptances across the integration and manual workflow
  guards; GREEN rejects all ten in both guards. Reverting only the tokenizer
  reopens exactly those 20 acceptances.
- Final independent Task 6 review found no Critical, Important, or Minor
  issue. It additionally proved rejection of `>>`, `<`, `<<<`, `|&`, `&>`,
  `>|`, and grouped-redirection forms. Full-line command comments remain
  ignored, and the duplicated classifier blocks are byte-identical.
- Fresh controller evidence passed Bash syntax, diff hygiene, the complete
  engineering-standards integration mutation harness, the manual Sonar
  workflow guard, and the Rvt.Common source-boundary suite.
- The original Task 6 aggregate build reached the test phase. Its failures
  were classified outside Task 6: environment-sensitive monitor tests require
  unset PostgreSQL configuration, and two untouched Portal assertions remain
  reproducible (`NotificationLists` expected 2/actual 1 and
  `CompanyUserClose` expected 200/actual 404). No gate was weakened and no
  production database credentials were used.
- Required next step: execute Task 7 from
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`.
  Complete the enforcement guide/status/report, run the complete standards
  and root-guard matrices plus backend/frontend verification, prove real
  ratchet increases fail in temporary copies, obtain independent review, and
  only then mark R9 complete.
- `project_state.md` remains the only tracked working-tree modification. It
  must be incorporated deliberately by Task 7 and not staged before that
  documentation/evidence commit.

## R9 latest checkpoint — Task 5 complete, Task 6 next 2026-07-28

- Branch: `codex/repository-engineering-standards`.
- Committed `HEAD`:
  `76eecc4d8f0710a00826386e0884a925b42329d6`
  (`test: revalidate module policy inputs`).
- The prior user-authorized ESLint structural-ignore exception is complete at
  `30acaca` and independently approved. Fresh controller syntax, model 26/26,
  real selector, and expanded verifier harness evidence passed.
- Task 5 has committed exactly:
  - `eng/standards/baseline.json`;
  - `eng/standards/exceptions.json`;
  - `eng/standards/module-policy.json`;
  - `tests/verify-engineering-standards-policy.test.mjs`.
  The production baseline contains 2,050 unique identities and 8,024 actual
  diagnostics. No convenience exceptions were created.
- Task 5 is complete and independently approved. Its review-driven hardening
  commits are:
  - `20d89b0` — initial framework/package/exact-shape guards;
  - `331c181` — replace textual XML inference with evaluated MSBuild metadata
    and exact filesystem-versus-solution inventory;
  - `d3ab241` — enforce module-root central-policy provenance, fail-closed
    governed roots/symlinks, and regular lock files;
  - `76eecc4` — reject dangling/unknown links and remove stale cross-call
    central metadata caching.
- The final policy suite evaluates all governed projects with MSBuild, proves
  exact module and override shape, handles framework package IDs
  case-insensitively, distinguishes central from central-locked behavior,
  validates central declaration origin, and prevents projects from being
  omitted from the solution census.
- Final independent review found no Critical, Important, or Minor issue.
  Fresh controller evidence passed syntax, model/policy 50/50, the focused
  process-lock harness, the working-tree ratchet with no increases, and diff
  hygiene.
- Required next step: execute Task 6 from the written plan—wire the ratchet
  into the aggregate build and CI—using a fresh implementer, TDD evidence,
  independent scoped review, and controller verification.
- `project_state.md` remains the only tracked working-tree modification.
  Tasks 6–7 and final branch review have not started.

## R9 repository engineering standards — blocked checkpoint 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Physical worktree:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/repository-engineering-standards`
  (also exposed through `/Users/oldgeorge/Documents/rvt-mono`).
- Branch: `codex/repository-engineering-standards`.
- Committed `HEAD`:
  `5c850cc5c88e3dfc86e6e64def782fe2a4851cef`
  (`fix: route only supported files to Prettier`).
- The only tracked working-tree modification is this state file. Preserved
  untracked Task 5 work:
  - `eng/standards/exceptions.json`;
  - `eng/standards/module-policy.json`;
  - `tests/verify-engineering-standards-policy.test.mjs`.
  `eng/standards/baseline.json` does not exist and no partial baseline or lock
  was left behind.
- Task 3 reached its fifth and final planned fix round. Commit `5c850cc`
  keeps SVG in the validated Portal source surface but routes only the
  explicit Prettier 3.9.6 parser allowlist to Prettier. Fresh controller
  evidence passed Node/Bash syntax, model 26/26, and the 79-repository focused
  harness. A real all-scope Prettier probe now reaches normal policy exit 1
  with 30 formatting-debt entries instead of invocation exit 2. Independent
  scoped review approved the fix with no new Critical/Important finding.
- Task 4 is complete through `d0d659c`: Prettier is pinned exactly to 3.9.6,
  the prescribed configuration and structural generated-schema ESLint ignore
  are committed, and the naming policy is active. The two unchanged
  `react-refresh/only-export-components` warnings remain intended Task 5
  baseline inputs.
- Task 5 captured its strict RED: 4/4 tests failed only because the three
  production policy documents were absent. Exact exceptions/module policy and
  the 333-line behavior test are preserved. Required SDK 10.0.302 is installed
  only under `/private/tmp/rvt-dotnet-sdk-10.0.302`; serial restore and
  `npm ci` succeeded. Sandbox Roslyn named-pipe denial was proven
  environmental and avoided by the approved unsandboxed run.
- Current load-bearing blocker: the real all-scope verifier selects 36 tracked
  TypeScript paths and explicitly passes generated
  `apps/portal/RvtPortal.Client/src/api/schema.d.ts` to ESLint. Task 4
  structurally ignores that generated file, so ESLint exits 0 but emits an
  ignored-file informational message with `ruleId: null`, severity 1, and
  `fatal: false`. Task 3 rejects the null rule ID and exits 2 before baseline
  initialization.
- The five-round breaker has therefore tripped. No sixth correction was
  attempted. User direction is required to authorize an exception fix round
  or revise the workflow. The likely bounded correction is to make Task 3
  honor ESLint's structural ignore without weakening supported-file lint
  failures (for example, the pinned CLI's `--no-warn-ignored` behavior), with
  RED/GREEN, a dedicated commit, and scoped re-review.
- Tasks 6 and 7 and the final whole-branch review have not started. Task 5
  cannot resume until the blocker is corrected and reviewed.
- The SDD ledger and reports remain under
  `.superpowers/sdd/2026-07-27-repository-engineering-standards-enforcement/`;
  the plan remains
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`.
- A separate task temporarily switched this physical worktree to
  `codex/help-admin-application-boundary`; that branch has since advanced
  independently through `c015d0b` and retains the earlier `96fa359` commit.
  The worktree was restored to the R9 branch; do not switch branches or reuse
  this physical worktree for another task while R9 resumes.

## Repository engineering standards enforcement — saved 2026-07-27 (Task 3 awaiting final review)

- Resume instruction: start the next session with
  `Read project_state.md to get up to speed`.
- Worktree:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/repository-engineering-standards`.
  Branch: `codex/repository-engineering-standards`.
- Current committed `HEAD` is
  `20c3bde` (`fix: secure verifier assets and lock ownership`). The only
  tracked working-tree modification is this state file; there are no
  uncommitted verifier changes.
- Completed and independently approved commits:
  - `e083eda` — engineering standards specification;
  - `9bd194b`, `1b05562`, `170b4d8` — enforcement plan and approved
    evaluated-test/Ratchet corrections;
  - `2212185` — shared EditorConfig/MSBuild hierarchy;
  - `3d01096`, `4f23da2`, `e7757de` — diagnostic/exception model and its
    reviewed boundary/date fixes.
- Task 1 is complete and independently approved. All 10 then-existing root
  guards passed; restore passed; default Ratchet build passed with 73 warnings
  and 0 errors. `RvtEngineeringStandardsMode` defaults to `Ratchet`, where
  `EnforceCodeStyleInBuild=false`; `Strict` evaluates it to `true`. Promotion
  remains gated on zero code-style baselines and a strict root build.
- Task 2 is complete and independently approved. Its focused suite passes
  26/26 and covers POSIX/Windows/UNC paths, escapes, realistic .NET/ESLint
  reports, fatal ESLint identity, fail-closed one-based lines, baseline and
  exception validation, strict `reviewOn > introducedOn`, exact exception
  scope, deterministic ordering, baseline-only deltas, and non-mutation.

### Task 3 review state

- Task 3's original implementation and four remediation rounds are committed:
  - `988c0e9` — changed-scope standards ratchet;
  - `6bc3b26` — verifier hardening, review round 1;
  - `f5f1472` — range assets and recoverable verifier locks, round 2;
  - `8d50a96` — prerequisite and lock identity checks, round 3;
  - `20c3bde` — secure asset provenance and collision-resistant lock
    ownership, round 4.
- Task 3 is **not yet complete or independently approved**. The round-4
  implementer reported passing syntax, the 26/26 model suite, five consecutive
  focused harness runs, two traced runs under CPU load, all 11 isolated root
  guards, and diff hygiene. Those results have not been independently rerun
  after commit `20c3bde`.
- Round 4 addresses the two findings left after round 3:
  1. exact-head asset provisioning could link dependency inputs from a dirty,
     staged, deleted, or untracked caller working tree;
  2. verifier lock ownership used locale-sensitive, second-resolution process
     start timestamps.
- The round-4 implementation rejects contaminated documented dependency
  inputs, uses random-token sentinel child ownership, and guards ownership
  immediately before the atomic baseline write. Its harness includes
  dirty/staged/deleted/untracked dependency inputs plus predecessor/successor
  lock safety.
- No verification process or subagent is currently running. The Task 3
  progress ledger/report is one round behind and still needs the round-4
  implementation and review outcome recorded.
- Resume with:
  1. inspect `git status` and confirm `HEAD` is `20c3bde`;
  2. update the Task 3 report and ledger for round 4;
  3. independently rerun syntax, the 26/26 model suite, the focused verifier
     harness, relevant root guards, and `git diff --check`;
  4. prepare the exact review package for `8d50a96..20c3bde`;
  5. dispatch a fresh scoped review against only the two open round-3
     findings;
  6. if approved, mark Task 3 complete; otherwise use the fifth and final
     permitted remediation round and then adjudicate any remaining blocker.

### Remaining R9 execution

- Task 4 — add deterministic frontend formatting and naming policy: not
  started.
- Task 5 — capture the legacy baseline and module policy: not started.
- Task 6 — wire the ratchet into the aggregate build and CI: not started.
- Task 7 — complete R9 documentation and end-to-end verification: not started.
- After Tasks 3–7, run the mandatory whole-branch review, one bounded final fix
  wave if necessary, update this state file and the final documentation, and
  use the finishing-development-branch workflow for the integration decision.
- The broader remediation phases R1–R8 and R10–R11 remain Future Pending Work;
  they are not part of this R9 branch.
- Codegraph is unavailable (`unable to open database file`); use focused local
  inspection. The relocated Developer worktree is outside the app's stale
  Documents writable root, so use the narrow escalated worktree patch
  workaround when direct `apply_patch` cannot resolve it. The Task 3 worker's
  `/bin/zsh` launch failed while `/bin/bash` worked.
- Local Node is `v22.22.3`; the planned CI/runtime pin is Node 24.

### Task 3 interfaces and variable definitions

- CLI modes: `--working-tree`, `--base REV --head REV`,
  `--base auto --head HEAD`, and `--all`. Baseline lifecycle flags are
  `--initialize-baseline` and `--update-baseline`, valid only with `--all`.
  Exit codes are 0 compliant, 1 policy violation, and 2 invocation/tool error.
- Command overrides are JSON arrays of non-empty tokens:
  `RVT_STANDARDS_DOTNET_COMMAND`, `RVT_STANDARDS_ESLINT_COMMAND`, and
  `RVT_STANDARDS_PRETTIER_COMMAND`; they are never evaluated as shell text.
- Policy path overrides are `RVT_STANDARDS_BASELINE_PATH` and
  `RVT_STANDARDS_EXCEPTIONS_PATH`; defaults are
  `eng/standards/baseline.json` and `eng/standards/exceptions.json`.
- Core constants are `ignoredPrefixes`, `portalPrefix`, `dotnetTools`, and
  `portalSourceExtensions`; error classes are `PolicyError` and
  `InvocationError`.
- Resolution/validation functions are `parseArguments`, `resolveRevision`,
  `resolveRange`, `resolveScope`, `finalizeChangedScope`,
  `parseUnifiedDiffRanges`, `validateGitPath`, `validatePatchPath`,
  `validateSourceBoundary`, `validateSourceFile`, `isIgnoredPath`, and
  `isSourcePath`.
- Tool/policy functions are `loadPolicy`, `commandOverride`,
  `collectDiagnostics`, `runDotnetPhase`, `runPortalTools`,
  `validateToolPath`, `printRatchet`, `baselineDocumentFromResult`, and
  `writeBaselineAtomically`.
- Harness variables are `source_root`, `temp_root`, `fake_bin`, `case_number`,
  `last_repo`, `last_output`, and `last_status`. Scenario overrides use
  `RVT_TEST_BASELINE_PATH`, `RVT_TEST_EXCEPTIONS_PATH`, and `RVT_FAKE_*`.
- Ledger/brief/report files are under
  `.superpowers/sdd/2026-07-27-repository-engineering-standards-enforcement/`.
  The plan is
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`.

## Provider package release migration Task 1 - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Worktree: `.worktrees/release-platform-hardening`. Task 1 starts from the
  approved provider source-split head `e8089dd`.
- `libs/rvt-monitor-common/release/package-catalog.tsv` is the release source of
  truth for exactly eleven ordered package IDs and their real project paths:
  Common, IntegrationTesting, five Communication packages, and four Storage
  packages.
- The clean-split `PackageVersion` default is `1.0.0-rc.1`; `Version` derives
  from it. `PinSynchronizedRvtProjectReferenceVersions` runs for packable
  projects after `_GetProjectReferenceVersions` and before `GenerateNuspec`,
  updating every `_ProjectReferencesWithVersions` item whose `Filename` begins
  `Rvt.` so `ProjectVersion` is exact `[$(PackageVersion)]`.
- Test variables are `PackageRoot`, `Artifacts`, `rows`, and
  `expectedPackageIds`. `PackageRoot` resolves
  `libs/rvt-monitor-common`; the TSV parser requires exactly two literal
  tab-separated columns, exact ordered IDs, and an existing project for every
  path.
- Strict TDD evidence: focused RED compiled and failed 0/1 with
  `DirectoryNotFoundException` while the catalog was absent. The identical
  no-restore slice passes 1/1 after the minimal catalog implementation. The
  required SendGrid MSBuild property probe prints `1.0.0-rc.1`.
- A no-restore SendGrid pack probe in `/private/tmp` succeeds and emits the real
  `Rvt.Communication.Abstractions` dependency as exact `[1.0.0-rc.1]`.
  SendGrid remains `9.29.3`.
- `Directory.Packages.props` was reviewed and intentionally remains unchanged:
  no obsolete infrastructure-only entry is present, every entry is referenced,
  and `AWSSDK.S3` `4.0.100.3`, `Azure.Identity` `1.15.0`,
  `Azure.Storage.Blobs` `12.25.0`, and `SendGrid` `9.29.3` are retained.
- No lockfile or active-consumer reference changed. All unrelated untracked
  files remain preserved. Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-provider-package-release-migration/task-1-report.md`.

## Storage provider split merge-blocker fix - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- This corrective scope starts from storage final-review commit `7804962`.
  Microsoft Graph large-attachment upload chunks now distinguish caller
  cancellation from provider/HTTP timeouts exactly like authenticated Graph
  requests: a cancelled caller token still propagates cancellation, while an
  `OperationCanceledException` with an active caller token becomes the
  secret-safe `EmailDeliveryException("MicrosoftGraph",
  DeliveryFailureKind.Transient, "Timeout")`.
- Strict Graph TDD evidence:
  - corrected RED ran the two upload-chunk cases with production unchanged:
    caller cancellation passed and the timeout test failed because exact
    `EmailDeliveryException` was expected but raw
    `OperationCanceledException` escaped;
  - after the single non-caller cancellation catch, the identical two-test
    slice passed 2/2;
  - the complete Microsoft Graph adapter project passed 37/37 and the bounded
    neutral Communication project passed 31/31.
  Test variables are `providerMessage`, `cancellation`, and `attachment`;
  `UploadChunkCancellationHandler` reaches the real PUT chunk boundary after
  draft and upload-session creation.
- The dependency license table now classifies
  `Microsoft.Extensions.Configuration.Abstractions` 10.0.9 as direct. Its
  version, approval, license metadata, and the 101-pair inventory are
  unchanged.
- No package policy, project file, or lock changed. All unrelated untracked
  files remain preserved. The first corrected RED retry inside the restricted
  sandbox was aborted because vstest could not bind its local communication
  socket; the bounded rerun outside that restriction produced the required
  1-pass/1-fail RED evidence.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/merge-blocker-fix-report.md`.

## Storage provider split final review fix - 2026-07-26 (complete)

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- This corrective scope starts from reviewed storage documentation head
  `0539691`. It resolves the whole-plan findings for Azure/S3 non-caller
  cancellation, source-split lock/catalog scope, and the 20-project dependency
  inventory. It does not push or expand any Future Pending Work.
- `AzureBlobObjectStorageClient` and `S3ObjectStorageClient` now translate a
  provider-thrown `OperationCanceledException` to the secret-safe neutral
  `ObjectStorageException` with `StorageFailureKind.Unavailable`, the configured
  resource name, the requested `StorageObjectKey`, and the provider exception
  retained only as `InnerException`. The existing first catch filter still
  rethrows cancellation unchanged when the supplied caller token is cancelled.
  Status classification, not-found handling, streaming leases, and S3
  disposal are unchanged.
- Strict provider TDD evidence:
  - focused RED compiled and failed all 6 new Azure/S3
    `WriteAsync`/`OpenReadAsync`/`DeleteIfExistsAsync` cases because exact
    `ObjectStorageException` was expected and raw
    `OperationCanceledException` escaped;
  - after the minimal catches, the same 6 cases plus both existing
    caller-cancellation controls passed 8/8;
  - the full storage suite passed 154/154.
  Test variables are `providerMessage`, `cancellation`, and `expectedKey`.
- Source-split scope is restored: the five locks formerly tracked under
  `Rvt.Storage.Abstractions`, `Rvt.Storage.Local`,
  `Rvt.Storage.AzureBlob`, `Rvt.Storage.S3`, and `Rvt.Storage.Tests` are
  deleted. No other repository lock is modified. Complete atomic lock
  regeneration remains delegated to the provider-package release migration.
- The conditional `Microsoft.CodeAnalysis.CSharp` catalog entry and private
  storage-test reference are removed. The package-free boundary analyzer uses
  BCL regular expressions and a lexical sanitizer to discard comments and
  literal text, retain executable interpolation holes, resolve local/global
  aliases across source files, distinguish the covered user-defined
  filesystem lookalikes, and report source paths for real dependencies.
  Existing dependency-boundary regressions all remain and the focused boundary
  slice passes 13/13. Analyzer variables are `AliasUsingPattern`,
  `NamespaceUsingPattern`, `QualifiedNamePattern`, `DeclaredTypePattern`,
  `ImplicitSystemIoTypes`, `sanitizedSources`, `aliases`, and
  `filesByDependency`.
- Temporary restore classification:
  - the planned `RestorePackagesWithLockFile=false` solution attempt was
    bounded and failed immediately with `NU1005`, because .NET 10 rejects that
    setting while other solution projects retain checked-in locks;
  - a serial retry with `RestorePackagesWithLockFile=true`,
    `RestoreLockedMode=false`, and `NuGetLockFilePath` redirected to the
    isolated `/private/tmp/rvt-storage-final-fix-locks.ks9vR8` location
    restored all 20 projects without a tracked lock write;
  - refreshed assets contain 101 distinct package/version pairs and no
    CodeAnalysis compiler package.
- `dependency-license-review.md` now exactly matches those 101 pairs and
  attributes Azure/AWS dependencies to `Rvt.Storage.AzureBlob` and
  `Rvt.Storage.S3`. The refreshed vulnerability-audit attempt was bounded but
  DNS-blocked before a result because `api.nuget.org` was unavailable; no
  current audit-clean claim is made.
- Repository verification passes mono layout, mono solution, RVT common source
  boundary, and its mutation regression companion. No storage lock exists in
  the working tree, no `Microsoft.CodeAnalysis.CSharp` occurrence remains in
  project/package catalog source, and `git diff --check` is clean.
- Existing MSTest analyzer warnings remain outside this correction.
  Microsoft Graph large-attachment upload-chunk non-caller timeout translation
  remains the external carry-forward merge blocker; this storage fix does not
  make the overall provider branch merge-ready.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/final-review-fix-report.md`.

## Storage provider Task 10 - final verification and documentation - 2026-07-26 (complete)

- Storage operator documentation now names the provider-neutral
  `Rvt.Storage.Abstractions` contract and the `Rvt.Storage.Local`,
  `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3` adapters. Consumers resolve
  `IObjectStorageClient` through the named `IObjectStorageClientFactory`
  contract; Svantek uses `svantek-sound-recordings` and ReportingMonitor uses
  `reporting-reports`.
- Svantek and ReportingMonitor reference all three adapter projects only to
  retain deliberate deployment-time selection among Local, Azure Blob, and
  S3. Each host composes exactly one provider for its named resource.
  Existing `BlobStorage:*`, `RVT:*`, and literal `RVT__*` configuration aliases,
  defaults, and legacy container fallbacks are unchanged.
- ReportingMonitor continues to resolve its persisted report URI outside the
  generic storage port. `report.report_link` retains Local `file:`, Azure HTTPS,
  and S3 `s3:` absolute formats.
- `Rvt.Monitor.Common` owns no storage implementation and references none of
  `AWSSDK.S3`, `Azure.Identity`, or `Azure.Storage.Blobs`. The dependency
  license review now attributes Azure SDK dependencies to
  `Rvt.Storage.AzureBlob` and AWS SDK dependencies to `Rvt.Storage.S3`;
  Abstractions and Local remain cloud-SDK independent.
- No behaviorally useful documentation-assertion test exists for these
  operator statements. The repository documentation tests cover move-manifest
  layout and release-automation instructions; extending either would be the
  prohibited grep-only source-text coupling. Task 10's planned documentation
  RED prerequisite is therefore inapplicable, and no test logic changed.
- Fresh shell verification passes
  `verify-mono-layout.test.sh`, `verify-mono-solution.test.sh`,
  `verify-rvt-common-source-boundary.test.sh`, and the new
  `verify-rvt-common-source-boundary-regression.test.sh`. Final
  `git diff --check` has no output.
- Bounded, no-restore, single-node verification passes storage 148/148,
  Common 340/340 excluding only the two known
  `MonitorDeliveryMigrationContractTests`, Svantek 93/93 excluding only its
  unavailable PostgreSQL/repository-root fixtures, and ReportingMonitor 74/74
  excluding only `TestReportingDbClient`. Reporting uses the preserved
  untracked Logging.Abstractions/Options 10.0.9 verification override.
- The established ordinary full-suite classifications remain unchanged:
  Common has 340 passes and two missing-migration-path failures; Svantek has
  93 passes and 40 absent-PostgreSQL/repository-root-sensitive failures;
  ReportingMonitor has 74 passes and ten tests requiring
  `RVT__POSTGRES_INTEGRATION_CONNECTION`. Those known blockers were not
  retried as green gates.
- A bounded root build imported a temporary targets file that removed exactly
  the two preserved untracked Portal C# copies. It succeeds with 76 existing
  analyzer/advisory warnings and 0 errors. The ordinary root build remains
  blocked by those two `* 2.cs` files and is not claimed green.
- The brief's raw legacy-symbol regex matches only the provider-owned
  replacement identifier `AzureBlobStorageOptions`; an exact whole-symbol
  search returns no legacy symbol matches. The Common project vendor-SDK
  search also returns no matches.
- Package release verification remains pending rather than green. Task 10
  changes no package policy or lock, and the complete eleven-package
  release/lock migration still owns atomic lock regeneration and the clean
  ReportingMonitor locked-restore reconciliation.
- Future Pending Work remains pending and outside this implementation:
  1. Migrate Portal `MonitorPictureStorage` and `SiteArchiveService` from
     `BlobStorageClientFactory` to `IObjectStorageClientFactory`, preserving
     protected streaming, Local fallback, atomic writes, existing `blob://`
     monitor references, persisted archive URLs, and report/archive container
     boundaries.
  2. Decide whether Portal customer-logo storage should use the shared
     named-client contract.
  3. Decide whether
     `services/reporting/src/Rvt.Reporting.Storage/AzureBlob/AzureBlobReportStorage.cs`
     should adopt `Rvt.Storage.AzureBlob`; it remains an independent adapter.
  4. Make an independent deprecation/removal decision for
     `apps/portal/RVT.Utilities/AzureBlobService.cs`.
  5. Consider dynamic provider discovery only if deployments require installing
     a provider without rebuilding a host.
  6. Consider external-consumer migration tooling only if coordinated
     major-version adoption proves insufficient.
  7. Review database, MQTT, scheduling, and observability dependencies as
     separate boundary projects after the communication and storage splits.
- Microsoft Graph large-attachment upload-chunk non-caller timeout translation
  remains the carry-forward merge blocker. Storage completion does not make
  the overall branch merge-ready.
- Full Task 10 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-10-report.md`.

## Storage provider Task 7 boundary-guard follow-up - 2026-07-26 (complete)

- `Rvt.Reporting.Storage` was already correctly migrated in Task 7 to reference
  `Rvt.Storage.Abstractions` only for storage. The shared
  `verify-rvt-common-source-boundary.sh` guard incorrectly retained its former
  `Rvt.Monitor.Common` requirement.
- The guard now requires the Abstractions project and rejects a Common project
  reference. Review fix round 1 also rejects Local, Azure Blob, and S3 provider
  references, enforcing that Reporting Storage depends only on Abstractions for
  storage. Its isolated behavioral regression first failed on the stale Common
  requirement, then on the missing Local-provider rejection, and now passes all
  four forbidden-reference mutations; the normal source-boundary test also
  passes.
- This corrective guard/test-only change does not alter Task 10's four pending
  documentation edits, consumer source, package policy, locks, or future
  pending work. The Graph upload-chunk timeout translation remains the
  carry-forward merge blocker.
- Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-7-boundary-fix-report.md`.

## Storage provider Task 9 - source solution wiring - 2026-07-26 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 9 starts from the
  Task 8 commit `6b678a5`. The four production storage projects and
  `Rvt.Storage.Tests` are represented exactly once in both
  `libs/rvt-monitor-common/rvt-common.sln` and `Rvt.Mono.slnx`.
- `apps/monitors/rvt-monitors.sln` contains the four production storage
  projects, but not `Rvt.Storage.Tests`. This reflects the active Svantek and
  ReportingMonitor hosts, which each directly reach Abstractions, Local,
  Azure Blob, and S3.
- Strict guard evidence: before solution wiring,
  `tests/verify-mono-solution.test.sh` failed because the solution had 46
  projects while the repository module inventory had 51. After the solution
  edits, the unchanged guard passes. The three `dotnet sln ... list` commands
  show each intended storage project once.
- Restore verification redirected every lock to
  `/tmp/rvt-storage-task9-locks.gI98g4` with
  `RestorePackagesWithLockFile=true`, `RestoreLockedMode=false`, and
  `--disable-parallel`. Both `rvt-common.sln` and `Rvt.Mono.slnx` restored
  successfully without rewriting a tracked repository lock.
- `rvt-common.sln` builds with 0 errors and 64 existing analyzer warnings.
  The first unmodified root build reached and compiled all five new storage
  entries but was blocked by duplicate types from the preserved untracked
  Portal `* 2.cs` copies. A temporary MSBuild import under `/tmp`, excluding
  only those future-pending copies, allowed `Rvt.Mono.slnx` to build with 0
  errors and 76 existing analyzer/advisory warnings.
- No package catalog, permanent central version, repository lock, solution
  guard, Portal/reporting-service source, or ReportingMonitor override is part
  of Task 9. The untracked ReportingMonitor `Directory.Packages.props` remains
  a verification-only override, and complete lock regeneration remains owned
  by the later provider-package release migration plan.
- The Graph large-attachment upload-chunk non-caller timeout translation
  remains the carry-forward merge blocker. Portal/reporting-service storage
  migration and every other documented future-pending item remain unchanged.
- Full Task 9 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-9-report.md`.

## Storage provider Task 8 - Common legacy removal - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 8 starts from the
  Task 7 commit. The 12 legacy files under
  `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Storage/` and their four
  Common storage test files are removed.
- `Rvt.Monitor.Common.csproj` no longer references `AWSSDK.S3`,
  `Azure.Identity`, or `Azure.Storage.Blobs`. Central versions remain in
  `libs/rvt-monitor-common/Directory.Packages.props`; the S3 and Azure Blob
  provider projects retain their SDK references.
- The new persistent guard loads
  `StorageProjectSnapshot.Load("Rvt.Monitor.Common")`.
  `Common_ReferencesNoCloudProviderSdkPackages` forbids the three SDK package
  identities above, while
  `Common_ProductionSourceUsesNoCloudProviderNamespaces` forbids the semantic
  dependency markers `Amazon.` and `Azure.Storage`.
- Strict TDD evidence: before deletion, the focused boundary command failed
  the two new assertions for `AWSSDK.S3` and the Common S3 source namespace,
  with four existing boundary tests passing. After deletion, the identical
  command passes 6/6. Review hardening added a root-namespace regression that
  failed before `Amazon.` also matched `using Amazon;`, then passed after the
  semantic matcher fix.
- The exact legacy-symbol/Common-namespace scan and the Common vendor scan both
  return no matches. The brief's raw substring regex still matches only
  provider-owned replacement names containing `BlobStorageOptions` or
  `AzureBlobStorageService`; this is a command-pattern false positive, not a
  residual legacy Common API use.
- Complete storage tests pass 148/148. The bounded Common set passes 340/340,
  Svantek storage composition/upload passes 11/11, and ReportingMonitor's
  non-DB set passes 74/74.
- Full Common reports 340 passed and two pre-existing missing-migration-file
  failures. Full Svantek reports 93 passed and 40 absent-PostgreSQL/stale-root
  fixture failures. Full ReportingMonitor reports 74 passed and ten failures
  that explicitly require `RVT__POSTGRES_INTEGRATION_CONNECTION`.
- Restore-capable verification temporarily rewrote tracked locks; those exact
  diffs were restored to `HEAD`. No central version, provider SDK reference,
  tracked lock, Portal storage, independent reporting-service source, or
  unrelated file is included in Task 8.
- The untracked ReportingMonitor `Directory.Packages.props` override remains
  preserved. Its clean locked-restore conflict remains release-plan work and
  is not claimed green.
- Full Task 8 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-8-report.md`.

## Storage provider Task 7 - ReportingMonitor report storage - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 7 starts from the
  Task 6 commit `ab7e5e0`. ReportingMonitor now writes through the named
  provider-neutral `IObjectStorageClientFactory` resource
  `reporting-reports`; report bytes, filename/key, content type, cancellation,
  and the provider-returned normalized key are preserved.
- The ReportingMonitor host deliberately references and composes exactly one
  Local, Azure Blob, or S3 provider. Selection retains the Svantek precedence,
  blank-value fallback, case-insensitive names, Local default, and exact safe
  unsupported-provider message.
- Reporting defaults remain container `pdfreports` and prefix `rvtreports`,
  including the legacy `BLOB_REPORT_CONTAINER_NAME` alias. A concrete-client
  `IReportObjectUriResolver` preserves Local `file:`, Azure blob, and S3 `s3:`
  URI formats outside `IObjectStorageClient`; `StorageWriteResult` still
  exposes only a key.
- Persisted URI behavior is unchanged: `ReportGenerationService` forwards the
  resolved URI and `ReportingDbClient` stores
  `request.ReportUri.ToString()` in `report.report_link`.
- `Rvt.Reporting.Storage` references only `Rvt.Storage.Abstractions` for
  storage. Its broad interim `Microsoft.AspNetCore.App` reference was removed;
  the existing logo client now receives `IOptions<T>` through a narrow
  `Microsoft.Extensions.Options` package reference.
- Strict TDD evidence includes the initial focused compile failure against the
  legacy blob interface followed by the provider-neutral GREEN slice. Final
  focused storage/architecture verification passes 10/10, and the complete
  non-environment ReportingMonitor set passes 74/74.
- The unfiltered ReportingMonitor suite compiles and runs 84 tests: 74 pass;
  10 PostgreSQL integration tests remain unavailable because
  `RVT__POSTGRES_INTEGRATION_CONNECTION` is not set.
- Verification used an untracked ReportingMonitor `Directory.Packages.props`
  override for Logging.Abstractions/Options 10.0.9 and per-project
  `NuGetLockFilePath` output under `/tmp/rvt-storage-task7-locks`. No tracked
  lock or central package policy is changed, and the repository locked-restore
  gate is not claimed green.
- Legacy Common storage removal remains Task 8; solution/packaging and final
  verification remain Tasks 9 and 10. Portal and the independent reporting
  service remain out of scope.
- Full Task 7 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-7-report.md`.

## Storage provider Task 6 - Svantek sound recordings - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 6 starts from the
  Task 5 review baseline commit `56fbe64`. The previously completed
  `MonitorHost` configuration callback migration was preserved unchanged.
- Svantek now references `Rvt.Storage.Abstractions`, Local, Azure Blob, and S3
  directly. `AddSvantekMonitor(IConfiguration)` composes exactly one provider
  for the named `svantek-sound-recordings` resource.
- Provider selection preserves the existing precedence and blank-value
  fallback across `BlobStorage:Provider`, `RVT:BLOB_PROVIDER`,
  `RVT__BLOB_PROVIDER`, then Local. Values are case-insensitive. Unsupported
  values fail at composition with the exact allowed-provider message. Local
  defaults remain `/data/rvt/blobs/audiofiles` with an empty prefix.
- `CheckForSoundRecordingsHandler` accepts `IObjectStorageClientFactory`,
  resolves its named client once during construction, and writes the existing
  `{NotificationId}.wav` key through a read-only `MemoryStream`. Bytes,
  `audio/wav`, cancellation, and the database recording-link update remain
  unchanged. Direct `SvantekApi` construction without a storage dependency
  retains a lazy, explicit missing-storage failure through a missing
  factory/client pair.
- Strict TDD evidence: composition first failed compilation on the absent
  storage references/composition root, then passed 6/6. A follow-up blank-key
  precedence regression failed 1/7 before its minimal fallback fix, then the
  composition slice passed 7/7. The streaming test rewrite failed compilation
  with five expected factory-to-legacy-port mismatches, then passed 4/4 after
  production migration.
- The shared Omnidots host scheduling guard passed 13/13. Svantek
  communication/options/cancellation composition passed 20/20, and the
  complete runnable non-environment Svantek set passed 93/93. The unfiltered
  suite compiled and ran 133 tests: 93 passed; 40 remain blocked by the absent
  PostgreSQL integration connection and pre-existing repository-root-sensitive
  schema/boundary fixtures.
- The Svantek host builds with zero warnings and errors. `git diff --check`
  passes.
- No package lock, Common legacy storage source, Portal/reporting-service
  source, or host callback source changed. Common storage removal remains Task
  8 work; ReportingMonitor migration and solution/packaging/final verification
  remain Tasks 7, 9, and 10.
- Full Task 6 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-6-report.md`.

## Storage provider Task 5 - contract parity and dependency isolation - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 5 starts from Task 4
  commit `65608a4`. The storage test project now has one reusable eight-case
  `IObjectStorageClient` contract inherited by real Local, Azure Blob, and S3
  provider fixtures.
- The Local contract fixture uses a unique real temporary filesystem root.
  Azure and S3 instantiate the concrete provider clients and use strict,
  stateful SDK-boundary doubles backed by ordinal in-memory object
  dictionaries; no client under test is replaced and no network is used.
- Shared parity covers non-seekable writes and normalized returned keys,
  streamed bytes/content type/content length, missing reads, overwrite,
  idempotent delete, and pre-cancelled write/read/delete. Each cancellation
  case performs a later uncancelled read to prove the original provider object
  did not mutate.
- Dependency guards parse the four storage project files and scan only their
  production C# sources, explicitly excluding `obj`/`bin`. They keep
  Abstractions provider/framework/filesystem independent, Local cloud-SDK
  independent, Azure limited to its Azure SDK dependencies, S3 limited to its
  AWS SDK dependency, and all providers directly referenced only to
  Abstractions.
- Review hardening makes Azure raw-response disposal directly observable and
  proves S3 response-lease disposal through the response stream's distinct
  second disposal. MSBuild dependency parsing handles `Include`, `Update`, and
  `Remove` identities in order. Round 2 replaces the interim lexer with one
  project-wide Roslyn compilation: semantic symbols include interpolation
  holes and cross-file global aliases while excluding comments, literal text,
  and user-defined filesystem lookalikes.
- Test-first evidence: the shared fixture shells failed 24/24 before their
  real implementations and then passed 24/24. The boundary snapshot shell
  failed 4/4 before implementation and then passed 4/4. The complete storage
  suite initially passed 137/137. Review hardening failed its five focused
  regressions before implementation, then passed 5/5; the expanded contract,
  boundary, and full suites passed 26/26, 7/7, and 142/142 respectively.
  Round 2 then failed three of six focused semantic regressions before
  implementation and passed 6/6 after it; the final boundary, contract, and
  full suites passed 10/10, 26/26, and 145/145. `git diff --check` passed.
- `Microsoft.CodeAnalysis.CSharp` 5.0.0 is centrally conditioned to the
  `Rvt.Storage.Tests` project and referenced with `PrivateAssets="all"`.
  Its direct/transitive graph is recorded only in the storage test lock; no
  provider project or provider lock changed.
- No real provider inconsistency surfaced, so no provider production code,
  provider package version, or provider lock changed. The test-only Roslyn
  central entry and `Rvt.Storage.Tests` lock are the only package/lock changes.
  Tasks 6-10 remain pending: consumer migrations, legacy Common storage
  removal, solution/packaging integration, and final
  verification/documentation. Portal storage and the independent
  `services/reporting` Azure adapter remain future work.
- Full Task 5 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-5-report.md`.

## Storage provider Task 4 - S3 adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 4 starts from Task 3
  commit `e7e6e5b`. `Rvt.Storage.S3` is now a separate packable net10 provider
  project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` S3 implementation remains present and unchanged for
  Task 8.
- `S3StorageOptions` binds `Bucket`, `Prefix`, `Region`, `ServiceUrl`, and
  `ForcePathStyle` from the current provider-neutral, `RVT:`, and literal
  `RVT__` aliases. `Bucket` is required when the named client resolves;
  prefixes use the shared traversal-safe key normalization; service URLs must
  be absolute. No access-key, secret-key, credential, or token option exists.
- `S3ObjectStorageClient` constructs `AmazonS3Config` with the current exact
  endpoint behavior: region-only configuration sets `RegionEndpoint`;
  compatible-S3 configuration sets normalized `ServiceURL` and, when supplied,
  trimmed `AuthenticationRegion`. It uses `new AmazonS3Client(config)` so the
  SDK default credential chain remains in effect.
- `AddRvtS3Storage` registers one keyed singleton client, one named
  `ObjectStorageClientRegistration`, the shared client factory, and startup
  validation through `IHostedService`, matching the Local and Azure provider
  composition pattern.
- Writes pass the original request stream to `PutObjectAsync` with
  `AutoCloseStream = false`, copy optional content type, and prefix provider
  keys. Reads return the raw `GetObjectResponse.ResponseStream`, content type,
  and length without buffering and retain the response as the shared disposal
  lease. Deletes probe metadata first, return `false` for `NoSuchKey` or 404,
  and otherwise delete the same bucket/key and return `true`.
- S3 failures classify 403 as `AccessDenied`, 409 as `Conflict`, 408, 429, and
  5xx as `Unavailable`, other 4xx as `InvalidRequest`, and the remainder as
  `Unknown`. Caller cancellation propagates. Shared messages omit provider
  response and inner exception text. Provider URIs escape each prefix/key path
  segment independently.
- Strict TDD evidence: options/registration first failed compilation because
  the S3 provider was absent, then passed 18/18. Client behavior then failed
  13/15 on the deliberate unimplemented operation shell (URI and disposal
  behavior already passed), then passed 15/15. The complete S3 filter passed
  33/33, the complete storage project passed 109/109, and the provider built
  with zero warnings and errors. All AWS operation tests use strict
  `IAmazonS3` doubles and no network.
- Existing central `AWSSDK.S3` 4.0.100.3 and Microsoft.Extensions 10.0.9
  versions were reused unchanged. Locked restore added only the new S3
  provider lock and the S3 project/AWS SDK graph in the storage test lock.
- Tasks 5-10 remain pending. Provider parity and dependency-boundary tests,
  consumer migrations, legacy Common storage removal, solution/packaging work,
  Portal storage, the independent `services/reporting` Azure adapter, and all
  other future-pending work remain excluded and unchanged.
- Full Task 4 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-4-report.md`.

## Storage provider Task 3 - Azure Blob adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 3 starts from Task 2
  commit `406f057`. `Rvt.Storage.AzureBlob` is now a separate packable net10
  provider project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` Azure storage implementation remains present and
  unchanged for Task 8.
- `AzureBlobStorageOptions` preserves provider-neutral, `RVT:`, literal
  `RVT__`, audio-folder, and custom reporting-container aliases and precedence.
  A connection string takes precedence over the service URI; otherwise the
  provider requires an absolute service URI and uses
  `DefaultAzureCredential`. Containers are required and trimmed, and prefixes
  use the shared traversal-safe key normalization.
- `AddRvtAzureBlobStorage` follows the Local provider pattern: one keyed
  singleton client, one named registration, the shared client factory, and
  startup validation through `IHostedService`.
- `AzureBlobObjectStorageClient` streams the original request stream to Azure
  after `CreateIfNotExistsAsync`, applies optional content type, and returns
  only the provider-neutral key. Reads stream `DownloadStreamingAsync` content
  and metadata without buffering, return `null` for 404, and retain the raw
  Azure response as the shared disposal lease. Deletes return the SDK boolean;
  `GetObjectUri` remains concrete-provider API.
- Azure failures classify 403 as `AccessDenied`, 409 as `Conflict`, and 408,
  429, and 5xx as `Unavailable`; caller cancellation propagates. Shared
  exception messages never copy Azure response or inner exception text.
- Strict TDD evidence: options/registration first failed compilation because
  the Azure provider was absent, then passed 18/18. Client behavior then failed
  14/14 on the deliberate unimplemented operation shell, then passed 14/14.
  The complete Azure filter passed 32/32, the complete storage project passed
  76/76, and the provider built with zero warnings and errors. All Azure
  operation tests use strict SDK doubles and no network.
- Existing central Azure/Microsoft.Extensions versions were reused unchanged.
  Locked restore added only the new Azure provider lock plus the Azure project
  graph and strict-test Moq graph in the storage test lock.
- Tasks 4-10 remain pending. S3 extraction, parity and architecture tests,
  consumer migration, legacy Common storage removal, solution/packaging work,
  Portal storage, and the independent `services/reporting` Azure adapter remain
  excluded and unchanged.
- Full Task 3 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-3-report.md`.

## Storage provider Task 2 - Local filesystem adapter - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; Task 2 starts from Task 1
  commit `da0dfd2`. `Rvt.Storage.Local` is now a separate packable net10
  provider project referencing `Rvt.Storage.Abstractions`; the legacy
  `Rvt.Monitor.Common` storage implementation remains present and unchanged for
  Task 8.
- `LocalStorageOptions` preserves the local defaults and provider-neutral,
  `RVT:`, literal `RVT__`, audio-folder, and reporting-container aliases.
  `AddRvtLocalStorage` registers a named keyed singleton client, one named
  registration, the shared client factory, and startup validation through
  `IHostedService`.
- `LocalObjectStorageClient` streams request content into same-directory
  create-new temporary files, flushes before atomic overwrite, removes
  temporary files on success/failure, and stores optional content type in an
  adjacent atomically replaced metadata file. Reads return async sequential
  file streams; delete reports existence and removes metadata.
- Local filesystem access retains rooted containment and pre/post-create
  reparse-point checks for object and metadata paths. Real-filesystem tests
  cover unsafe container/prefix values, traversal rejection at the validated
  key boundary, directory and target-file symlinks, failed-copy preservation,
  overwrite, cleanup, missing reads, idempotent delete, and cancellation before
  mutation.
- Strict TDD evidence: options/registration first failed compilation because
  `Rvt.Storage.Local` was absent, then passed 11/11. Client behavior then failed
  18/20 on the deliberate unimplemented operation shell (the two key-boundary
  rows already passed), then passed 20/20. The complete Local filter passed
  31/31.
- The required Microsoft.Extensions `10.0.9` central versions already existed
  and were not duplicated or changed. Locked restore added only the new Local
  project lock and the Local project edge in the storage test lock.
- Tasks 3-10 remain pending. Portal blob unification, the independent
  `services/reporting` Azure adapter, all communication release/lock work, and
  every other previously documented future-pending item remain excluded and
  unchanged.
- Full Task 2 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-2-report.md`.

## Storage provider Task 1 - provider-neutral streaming contracts - 2026-07-25 (complete)

- Worktree: `.worktrees/release-platform-hardening`; base before Task 1 was
  `0b655b6`. `Rvt.Storage.Abstractions` now provides validated normalized object
  keys, streaming write/read contracts, named logical-resource lookup, shared
  failure classification, and secret-safe storage exceptions under the
  `Rvt.Storage` namespace.
- `StorageObjectKey` trims and normalizes separators while rejecting empty,
  rooted, UNC, Windows-drive-rooted, dot-segment, and traversal names.
  `ObjectStorageClientFactory` uses ordinal resource matching, rejects blank
  names and duplicates, and does not enumerate registrations in unknown-name
  errors. `StorageReadResult` asynchronously disposes content before disposing
  its provider lease from a `finally` block.
- Strict TDD evidence: the key slice first failed compilation because
  `StorageObjectKey` was absent, then passed 8/8. The remaining abstraction
  slice first failed compilation because the client/request/result contracts
  were absent, then the complete abstraction filter passed 13/13.
- The net10 abstraction project has no direct or transitive packages and no
  configuration, dependency-injection, provider SDK, or filesystem APIs. The
  test project directly references only Microsoft.NET.Test.Sdk,
  MSTest.TestAdapter, MSTest.TestFramework, and the abstraction project.
- Tasks 2-10 remain pending: Local, Azure Blob, and S3 provider extraction;
  provider parity and dependency-boundary enforcement; Svantek and
  ReportingMonitor consumer migration; legacy Common storage removal; solution
  and packaging integration; and final verification/documentation. Portal blob
  unification and the independent `services/reporting` Azure adapter remain
  excluded future work, along with all previously recorded non-storage pending
  work.
- Full Task 1 evidence is in
  `.superpowers/sdd/2026-07-23-rvt-storage-provider-split/task-1-report.md`.

## Communication final-review HTTP hardening - 2026-07-25 (complete)

- Microsoft Graph mail and TransmitSMS keep their singleton delivery-port
  composition, while their DI-held adapters now retain `IHttpClientFactory`
  rather than transient typed clients. Each delivery creates and disposes one
  named factory-managed `HttpClient`, preventing process-long client capture
  without changing the singleton neutral workflow API. Public direct
  `HttpClient` adapter constructors remain available.
- Microsoft Graph non-caller HTTP cancellation now maps to a safe transient
  `EmailDeliveryException` with code `Timeout`; caller-requested cancellation
  still propagates. Malformed successful draft and upload-session JSON maps to
  the existing safe permanent `InvalidDraftResponse` and
  `InvalidUploadSession` codes without response content in exception text.
- Strict TDD RED: Graph focused tests failed 4/5 for the exact timeout,
  malformed-response, and retained-client symptoms while caller cancellation
  passed; TransmitSMS lifetime failed 1/1 with request client IDs `[1,1]`
  instead of `[1,2]`. GREEN: the same Graph tests passed 5/5 and TransmitSMS
  passed 1/1.
- Full suites passed: Graph 35/35, TransmitSMS 25/25, Abstractions 20/20, and
  workflow 31/31. The four runnable vendor monitor communication composition
  suites passed 12/12. Both provider projects and all five monitor hosts built
  sequentially with zero warnings and errors. The source-boundary/prerequisite
  guard and `git diff --check` passed.
- ReportingMonitor's focused test project remains blocked by the documented
  release-lock `NU1109` (central Logging.Abstractions 10.0.4 versus transitive
  10.0.9), although its host build is green. The five retained locks naming
  removed Infrastructure remain unchanged for the separate eleven-package
  release/lock plan.
- Storage, Portal, reporting behavior, central package versions, locks, and all
  future-pending work remain excluded. Full evidence is in
  `.superpowers/sdd/2026-07-23-rvt-communication-provider-split/final-fix-report.md`.

## Communication provider Task 9 - verification and documentation - 2026-07-25 (complete)

- The source-level communication split is verified. The project graph is
  `Rvt.Communication.Abstractions` at the base; the provider-neutral
  `Rvt.Communication` workflow and each of SendGridMail,
  MicrosoftGraphMail, and TransmitSms reference Abstractions directly.
  `Rvt.Monitor.Common` retains only an Abstractions reference for compatibility.
  `Rvt.Monitor.Common.Infrastructure` is removed and is not a facade.
- All five monitor hosts directly reference and compose Abstractions, the
  workflow, SendGrid, Microsoft Graph, and TransmitSMS. They select SendGrid by
  default or Microsoft Graph by exact case-insensitive
  `RVT:EMAIL_PROVIDER`/`RVT__EMAIL_PROVIDER` configuration. Portal remains
  explicitly SendGrid-only.
- Both reporting messaging projects now reference only Abstractions. The
  monitor Reporting host owns dynamic SendGrid/Microsoft Graph selection; the
  containerized reporting-service host explicitly owns its existing
  SendGrid-only selection.
- Fresh bounded library verification passed 126/126: Abstractions 20,
  workflow 31, SendGrid 20, Graph 31, and TransmitSMS 24. Fresh focused vendor
  monitor composition tests passed 12/12. Portal passed 381 with eight known
  provider-gated skips, and the containerized reporting service passed 14/14.
- The full monitor suites remain environment/baseline-gated rather than green:
  AirQ 87 passed/33 failed, MyAtm 139/69, Omnidots 337/64, and Svantek 86/40.
  The failures require the missing PostgreSQL integration connection and, for
  MyAtm/Svantek, include retained pre-monorepo path assumptions. No
  communication-focused regression was identified.
- Dependency isolation is green: both neutral `dotnet list --include-transitive`
  results exclude SendGrid, Azure Identity, Azure Storage, and AWS S3, and the
  source-boundary guard passed.
- The aggregate/locked gate is explicitly **not green**. ReportingMonitor is
  blocked by central Logging.Abstractions 10.0.4 versus transitive 10.0.9;
  RuntimeConsumer lacks the not-yet-packed `Rvt.Communication` 0.2.0-rc.1
  artifact; TestConsumer cannot resolve its expected RVT type; and five
  retained monitor/package-validation locks still name the removed
  Infrastructure project. These are owned by the dedicated eleven-package
  release/lock plan.
- Storage isolation and every future-pending item remain out of scope. Portal
  `BlobStorageClientFactory`/service unification through
  `IObjectStorageClientFactory`, customer-logo and reporting storage adoption,
  synchronous legacy message removal, dynamic plugins, external compatibility
  tooling, notification/business/API/persisted-record changes, database, MQTT,
  scheduling, observability, and the full eleven-package release pipeline all
  remain pending.
- Exact graph, command results, known failures, and the complete pending-work
  list are recorded in
  `docs/architecture/rvt-monitor-common/communications.md` and the Task 9
  report.

## Portal test-host SendGrid follow-up - 2026-07-25 (complete)

- Both Portal test-host paths now provide deterministic, non-secret legacy
  `EmailConfiguration:SENDGRID_API_KEY` and
  `EmailConfiguration:Sending_Email_Address` values through `UseSetting`.
  This makes the values available while minimal-host `Program.ConfigureServices`
  eagerly constructs `SendGridMailOptions`; the custom factory's later
  `ConfigureAppConfiguration` collection is intentionally unchanged for its
  other test settings.
- TDD RED: `SwaggerDocument_IsAvailable` and
  `HealthEndpoints_ExposeLivenessAndReadiness` each failed 0/1 because enabled
  SendGrid validation reported the missing API key and from-address. GREEN:
  both fixtures passed together 2/2, the Task 7 Portal composition/adapter
  tests passed 12/12, and the complete Portal suite passed 381 with eight known
  opt-in PostgreSQL integration skips (389 total). Output retained five known
  NU1903 advisories for `System.Security.Cryptography.Xml` 10.0.7.
- No production code or provider validation changed. Verification used a
  temporary MSBuild import that excluded only the preserved untracked
  `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs` files.
- All previously documented future-pending work remains out of scope,
  including Portal blob client/service unification through
  `IObjectStorageClientFactory`, customer-logo and reporting storage adoption,
  legacy storage utility migration, dynamic plugins and external compatibility
  tooling, synchronous `IMessageService` removal, and later notification,
  API/persisted-record, database, MQTT, scheduling, and observability work.

## Communication provider Task 7 - Portal and reporting mail migration - 2026-07-24 (complete)

- The Portal host now source-references only
  `Rvt.Communication.Abstractions` and `Rvt.Communication.SendGridMail` for
  communication. It retains the existing `PortalEmailOptions` binding and
  explicitly maps the existing `EmailConfiguration` keys into
  `SendGridMailOptions`; the manual Infrastructure, SendGrid factory, and
  concrete adapter registrations are removed.
- The Portal `RvtCommonEmailDelivery` adapter remains the translation seam
  between the business-layer `IEmailDelivery` result contract and
  `IEmailDeliveryPort`. Focused tests cover request mapping, the existing
  debug-recipient override, typed provider-failure translation, and caller
  cancellation.
- The monitor reporting messaging project now references only Communication
  Abstractions for email delivery. Its existing attachment mapping,
  disabled/test-recipient behavior, cancellation, and result semantics are
  preserved.
- The containerized reporting messaging project no longer references or
  constructs the SendGrid SDK. Its provider-neutral `ReportMessageSender`
  sends one `EmailAttachment` through `IEmailDeliveryPort`, while the service
  host explicitly registers SendGridMail from the existing `RVT:EMAIL_*` and
  `RVT:SENDGRID_API_KEY` settings. Disabled email remains a successful no-op
  even when the supplied cancellation token was already cancelled, preserving
  the legacy containerized sender behavior.
- The root source-boundary guard now enforces the Task 7 graph for the Portal,
  monitor reporting messaging, and containerized reporting projects. The two
  monitor lock files updated by restore reflect the active provider graph and
  removal of the old Infrastructure/Common messaging edges.
- Verification: Portal focused tests passed 12/12 through a temporary MSBuild
  import that excluded only the two preserved untracked duplicate `* 2.cs`
  files; monitor reporting sender tests passed 7/7; containerized reporting
  sender tests passed 7/7; the source-boundary guard and its regression harness
  passed; and scoped Portal, monitor-reporting, and service-reporting builds
  completed with zero compiler warnings and errors. Portal test restore/test
  output retained five existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7.
- Future pending work remains out of scope: unify Portal blob storage
  client/service use through `IObjectStorageClientFactory`; migrate
  customer-logo storage; decide the independent reporting-service Azure
  storage path; migrate the legacy Portal storage utility; dynamic provider
  plugins; external-consumer compatibility tooling; notification content or
  business changes; public API or persisted-record changes; removal of the
  legacy synchronous `IMessageService`; and later database, MQTT, scheduling,
  and observability dependency-boundary reviews.

## Communication provider Task 6 - monitor composition roots - 2026-07-24 (complete)

- `MonitorHost.RunAsync` now passes the effective `IConfiguration` to its
  service-composition callback in API, Quartz scheduler, and one-shot modes.
  All five monitor `Program.cs` entry points and every direct registration
  caller pass that configuration into `AddAirQMonitor`, `AddMyAtmMonitor`,
  `AddOmnidotsMonitor`, `AddReportingMonitor`, or `AddSvantekMonitor`.
- Each monitor composition root explicitly registers the neutral
  `Rvt.Communication` workflow, selects SendGrid by default or Microsoft Graph
  by a case-insensitive exact provider match, reads `RVT:EMAIL_PROVIDER` before
  literal `RVT__EMAIL_PROVIDER`, and always registers TransmitSMS. Invalid
  values fail at registration with the safe exact message
  `RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.` without echoing the
  configured value.
- The five active monitor host projects no longer reference
  `Rvt.Monitor.Common.Infrastructure`. Each retains `Rvt.Monitor.Common` and
  directly references Communication Abstractions, Communication, SendGridMail,
  MicrosoftGraphMail, and TransmitSms. The source-reference matrix and shell
  boundary guard enforce this graph while retaining the Portal/Infrastructure
  boundary for later tasks.
- Verification: all five focused communication suites passed 3/3 (15 total);
  `MonitorHostTests` passed 3/3; the source-reference matrix passed 12/12; the
  shell source-boundary guard passed; all five hosts built with zero warnings
  and errors; and `apps/monitors/rvt-monitors.sln` built with zero errors. The
  solution build retained the known NU1900 warning because NuGet vulnerability
  metadata was unreachable. No restore was needed.

## Communication provider Task 5 - TransmitSMS extraction - 2026-07-24 (complete)

- `Rvt.Communication.TransmitSms` now owns the TransmitSMS client and adapter,
  `TransmitSmsOptions`, its startup validation service, and `AddTransmitSms`
  overloads. It reads `RVT:` keys before literal `RVT__` aliases; defaults to
  disabled SMS, empty credentials, and `KrakenAlert` sender. Enabled validation
  names missing key/secret/sender settings without exposing configured secrets.
- The adapter preserves the existing form POST endpoint, Basic authorization,
  cancellation behavior, provider-code-only errors, retry-after parsing, and
  delivery-failure classifications. Provider registration adds one typed HTTP
  adapter, one SMS port, options, and startup validator; an existing SMS port
  fails with `An SMS delivery provider is already registered.`.
- `Rvt.Monitor.Common.Infrastructure` temporarily project-references the
  provider and resolves its provider-owned options/adapter/validator directly
  from `IConfiguration`. `CommunicationsOptions` is now email-only and no
  longer parses, exposes, or validates SMS settings. Its packed dependency is
  exactly pinned to `[$(PackageVersion)]` through the temporary bridge.
- The solution includes the provider and test projects in the RVT Monitor
  Common library/test folders. The temporary build/package graph is now eight
  artifacts, with TransmitSMS restored, packed, required in artifact checks,
  and cleared from the local package cache before package validation.
- Verification: TransmitSMS tests passed 24/24; Infrastructure tests passed
  17/17 after the ownership-boundary follow-up; source-boundary/package guard
  and solution inventory guard passed;
  Infrastructure package packing with `-m:1` produced a nuspec pinned to
  `[0.2.0-rc.1]`; and Infrastructure plus all five monitor hosts built with
  `--no-restore`. The provider test build still reports the existing MSTest
  parallelization/data-test analyzer warnings.

## Communication provider Task 3 - SendGrid mail extraction - 2026-07-24 (complete)

- `Rvt.Communication.SendGridMail` now owns the SendGrid adapter, client factory,
  `SendGridMailOptions`, its startup validation service, and explicit
  `AddSendGridMail` overloads. Its `RVT:` configuration keys take precedence
  over literal `RVT__` fallbacks. The provider registers one email port, one
  factory, one options instance, and one provider-specific hosted validator;
  duplicate email ports fail with the required exact message.
- `Rvt.Monitor.Common.Infrastructure` temporarily source-references and package
  depends on SendGridMail while keeping its existing SendGrid/Microsoft Graph
  selector. Its temporary composition resolves provider-owned SendGrid options.
  This bridge is scheduled for removal in Task 8 rather than becoming a facade.
- The portal has a direct SendGridMail source reference because it owns a
  separate SendGrid composition root. It maps its existing `EmailConfiguration`
  settings to `SendGridMailOptions` and retains its original service lifetimes.
- The temporary package graph is now six artifacts:
  `Rvt.Monitor.Common`, `Rvt.Communication.Abstractions`,
  `Rvt.Communication`, `Rvt.Communication.SendGridMail`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting`. `scripts/build-mono.sh` restores, packs,
  verifies, and clears cached copies of all six. Infrastructure packs with
  `-m:1`; its SendGridMail dependency is exactly pinned to `[$(PackageVersion)]`.
- Verification: provider tests 20/20; Infrastructure compatibility tests 52/52;
  package-artifact tests 14/14; Common communication boundary tests 3/3; source
  and temporary-six-package bridge guard passed; Infrastructure and all five
  monitor hosts built with `--no-restore`. A Portal build no longer reports a
  SendGrid reference error but remains blocked by the accepted unrelated
  untracked duplicate Portal source files.
- Follow-up review fix: `Rvt.Mono.slnx` includes SendGridMail under
  `/Libraries/RVT Monitor Common/` and its tests under
  `/Libraries/RVT Monitor Common/Tests/`. The solution inventory guard now
  recognizes all 44 module projects.

## Communication workflow Task 2 - 2026-07-24 (complete)

- Worktree: `.worktrees/release-platform-hardening`; base before Task 2 was
  `6b80074` (Task 1 communication abstractions).
- Commit `486748c` added `Rvt.Communication` and
  `Rvt.CommunicationTests`, moved the
  provider-neutral workflow types/tests from Common, added idempotent
  `AddRvtCommunication`, and made Infrastructure temporarily reference and
  package-depend on Communication.
- RED/GREEN evidence exists in `.superpowers/sdd/task-2-report.md`: workflow
  suite passed 31/31; bridge guard passed after requiring five temporary
  packages and a single-node Infrastructure pack; package artifact suite passed
  12/12 after synchronized dependency pinning.
- Confirmed local MSBuild issue: parallel Infrastructure `dotnet pack` stalls
  after Communication's `GetTargetFrameworks`; identical `-m:1` pack completes
  in 2.3 seconds. `scripts/build-mono.sh` now uses `-m:1` only for that pack.
- Verification constraint: Infrastructure restore from the bridge stalls after NU1900
  inability to reach nuget.org vulnerability data. Its last bounded command
  was stopped at 2m11s; audit-disabled and `--disable-parallel` retry restores
  also timed out after 55 seconds. A bounded `dotnet build Rvt.Mono.slnx
  --no-restore --nologo` compiled Task 2 projects but failed on eight duplicate
  Portal types from unrelated untracked `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs` files. Full aggregate build is not
  freshly proven.
- Preserve unrelated untracked `.codegraph/`, `apps/.nuget-packages/`, and
  `apps/portal/RvtPortal.Client/src/localDate 2.ts`, plus the two Portal C#
  suffixed files and `docs/superpowers/specs/2026-07-23-rvtportal-sites-application-boundary-design 2.md`; do not stage.
  Task 2 retained scoped verification evidence without modifying those files.
- Scoped handoff verification (all `--no-restore`) passed for Rvt.Communication,
  Infrastructure, and all five monitor hosts. The source-boundary guard passed;
  Communication passed 31/31; package validation passed 12/12; Common remained
  at its accepted 376 passed/2 missing-migration-path failures. The independent
  task review approved the implementation with aggregate verification excluded
  for the unrelated Portal duplicate files and restore environment issue.

## Release Platform Hardening - 2026-07-23

- The portal now maps explicit `/api/health/live` and `/api/health/ready`
  probes. Liveness has no dependency checks; readiness runs the three EF
  context checks plus schema validation and returns only statuses/check names.
  The cutover runbook designates readiness as the deployment gate.
- Existing proxy hardening remains configured before redirects, rate limiting,
  authentication, and CSRF: only forwarded client IP and scheme are accepted,
  defaults are cleared, and immediate peers must be configured in
  `ForwardedHeaders:KnownProxies` or `KnownNetworks`.
- Blob storage clients are constructed by the shared
  `IBlobStorageClientFactory` for both connection-string and managed-identity
  modes. Monitor pictures and site archives no longer independently construct
  blob service clients. Report generation and Omnidots adapters use bounded
  named client timeouts and translate malformed URLs, downstream timeouts, and
  connection failures without reflecting secrets or vendor response bodies.
- Calendar selections retain the full ISO date, local date-only defaults no
  longer serialize through UTC, and calendar/breach effects ignore responses
  after cancellation. Help Admin is explicitly excluded from the release
  surface until its temporary asset editor receives stable row-key coverage.
- Focused verification: `SpaHostSmokeTests` passed 5/5;
  storage/archive/report/Omnidots tests passed 18/18; the corrected host-filter
  controls passed 2/2; and the complete client suite passed 68/68 after a
  production build. The full portal backend suite passed 376 tests with eight
  explicit PostgreSQL/Timescale provider skips (384 discovered). Client lint completed with no errors and its two existing
  `DataViewPanels.tsx` fast-refresh warnings. The planned
  `DashboardRoutePanels`, `DashboardPanels`, and `ContractSitePanels` test
  files are absent in this checkout, so their exact scoped Vitest command has
  no matching files.
- Production-provider gate: an existing local TimescaleDB container was
  brought online after the first loopback connection attempt was refused.
  The second selected run reached PostgreSQL and reported 22 passed, 4
  failed (26 total). Root cause investigation found: the seed fixture passed
  UTC-naive values to Npgsql without an explicit `timestamp` type; seven
  aggregate view expressions were implicitly promoted to `timestamptz`; and
  PostgreSQL normalized the equivalent `created` default expression with a
  different spelling. The approved idempotent schema deploy applied all seven
  deploy scripts, including the corrected aggregate-view definitions. The
  final provider gate passed 26/26 against the database; the complete portal
  suite passed 376 tests with the eight opt-in provider tests skipped. Source
  remediation adds explicit view casts, typed test parameters, and a
  version-tolerant default assertion. Every temporary credential file was
  deleted immediately after use.
- Release-artifact verification: `RvtPortal.Spa` published successfully in
  Release with `wwwroot/index.html`, hashed SPA assets, and the host DLL.
  The client release gate then passed lint (two pre-existing fast-refresh
  warnings only), 68/68 Vitest tests, 4/4 Playwright browser smoke tests, and
  a production build. Targeted npm overrides update only the ESLint development
  dependency tree; a fresh full `npm audit` reports zero vulnerabilities. The
  repository has no portal production deployment manifest or target
  configuration. Before traffic can be deployed, an operator must supply the
  deployment target/secret store, the public HTTPS host (`Spa:PublicBaseUrl`
  and matching `AllowedHosts`), and the immediate reverse-proxy IP addresses or
  CIDR ranges for `ForwardedHeaders:KnownProxies`/`KnownNetworks`.

## Provider Adapter Project Split Design - 2026-07-23

- Approved direction: perform a clean major-version split with no temporary
  `Rvt.Monitor.Common.Infrastructure` facade. Provider-neutral communication
  and storage contracts become standalone projects; SendGrid, Microsoft Graph,
  TransmitSMS, Local, Azure Blob, and S3 each become individual adapter
  projects selected explicitly by application composition roots.
- Design specification:
  `docs/superpowers/specs/2026-07-23-rvt-provider-adapter-project-split-design.md`.
  Execution is divided into three implementation plans:
  `docs/superpowers/plans/2026-07-23-rvt-communication-provider-split.md`,
  `docs/superpowers/plans/2026-07-23-rvt-storage-provider-split.md`, and
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`.
  The release plan updates the hard-coded three-package validation, SBOM,
  solution, and package-consumer assumptions to the approved eleven-package
  graph.
- Target production package set: `Rvt.Monitor.Common`,
  `Rvt.Monitor.IntegrationTesting`, `Rvt.Communication.Abstractions`,
  `Rvt.Communication`, `Rvt.Communication.SendGridMail`,
  `Rvt.Communication.MicrosoftGraphMail`,
  `Rvt.Communication.TransmitSms`, `Rvt.Storage.Abstractions`,
  `Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3`.
  The clean-split release baseline is `1.0.0-rc.1`;
  `Rvt.Monitor.Common.Infrastructure` is removed rather than retained as a
  facade or meta-package.
- Storage abstraction definitions: `IObjectStorageClient` provides streaming
  write, open-read, and delete-if-exists operations;
  `IObjectStorageClientFactory.GetRequiredClient(resourceName)` resolves a
  named host-configured resource; `StorageObjectKey` protects provider-neutral
  object names; and provider adapters translate vendor failures into
  `ObjectStorageException` with `StorageFailureKind`.
- Communication abstraction definitions: `IEmailDeliveryPort` and
  `ISmsDeliveryPort` remain transport ports; `LegacyMessageKind` and
  `LegacyMessageChannel` replace implementation-owned nested message enums;
  `RvtContactDto` remains source-compatible in the
  `Rvt.Monitor.Common.Notifications` namespace but is compiled by
  `Rvt.Communication.Abstractions` to avoid a project cycle.
- TODO(storage): after the provider split, migrate Portal
  `MonitorPictureStorage` and `SiteArchiveService` from direct Azure
  `BlobContainerClient` construction to `IObjectStorageClientFactory`. Preserve
  protected streaming, local fallback and atomic writes, existing `blob://`
  references, persisted archive URLs, and report/archive container boundaries.
  Treat customer logos and the reporting service adapter as later explicit
  decisions.
- Future pending work is now explicit in the design: dynamic provider plugins;
  external-consumer compatibility tooling if coordinated major migration proves
  impossible; notification content/business changes; public API or persisted
  record changes; legacy synchronous `IMessageService` removal; and later
  database, MQTT, scheduling, and observability dependency-boundary reviews.

## RVT Mono-Repository Bootstrap - 2026-07-22

- Workspace: `/Users/oldgeorge/Developer/rvt-mono`
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

## Sites Application Boundary Task 3 - 2026-07-23

- Current state: the BCL-only `RvtPortal.Application` project owns common
  use-case results and paging primitives, the complete transport-neutral Sites
  contract set, the Sites read facade, and the materialized `ISiteReadPort`.
- Application structure:
  - `Common/UseCaseResult.cs`, `PageRequest.cs`, and `PagedResult.cs` define the
    application-owned result and paging types.
  - `Sites/SiteContracts.cs`, `ISiteApplicationService.cs`,
    `Ports/ISiteReadPort.cs`, and `SiteApplicationService.cs` define the read
    use cases and their persistence-neutral boundary.
  - `Sites/SiteApplicationService.cs` creates one UTC
    `SiteAuthorizationPolicy.ReadScope` per user-facing read, masks invisible
    sites as not found, joins materialized notification assignments with
    `IPortalUserDirectory`, and restricts company users to their own settings.
- Host adapter: `RvtPortal.Spa/Adapters/Sites/EfSiteReadAdapter.cs` owns the
  extracted EF Core read queries. Filtering, counts, sorting, paging, monitor
  and notification projections, options, details, operating hours, and
  notification-setting materialization remain database-side where they were
  before extraction.

## Sites Application Boundary Task 4 - 2026-07-23

- Current state: transactional site create, update, and notification-setting
  mutations are implemented in the BCL-only `RvtPortal.Application` boundary.
  Archive and customer-logo workflows remain in the host, and controllers
  still use the legacy host service pending their separate cutover task.
- Application structure:
  - `Common/IApplicationUnitOfWork.cs` defines transaction execution and the
    one-save command boundary.
  - `Sites/Ports/ISiteWritePort.cs` defines explicit staging operations for
    site create, site update, notification-setting upsert, and a conditional
    contract claim.
  - `Sites/SiteMutationValidator.cs` owns request-shape validation, exact
    `HH:mm` parsing, optional text normalization, legacy/seven-day operating
    hours normalization, and database-fact validation.
  - `Sites/SiteApplicationService.cs` validates shape before transaction
    entry, reads name/company/contract/ownership facts inside the transaction,
    authorizes inside the transaction before business reads, stages one
    mutation, saves once, and re-materializes the response.
  - Update preserves legacy site-not-found precedence by materializing site
    existence after its in-transaction management gate and before mutation-fact
    validation. A false write-adapter update still maps to not-found for the
    delete race between the existence read and entity materialization.
- Materialized read contracts added to `ISiteReadPort`:
  `SiteMutationValidationData` reports duplicate-name, company, and contract
  facts; `SiteNotificationSettingTarget` reports only assignment ownership
  identifiers. No EF, HTTP, host, vendor, or queryable types cross the port.
- Host adapters:
  - `EfSiteReadAdapter` supplies the focused mutation validation and
    notification-target lookups.
  - `EfSiteWriteAdapter` stages entities without parsing or validating. Create
    stages the site and seven operating-hour rows; after the unit-of-work save,
    `TryClaimContractAsync` issues one relational conditional update constrained
    by contract id, company id, and an unassigned `SiteiD`. Update replaces
    mutable values and operating-hour rows; notification writes upsert the
    settings row.
  - `EfCoreUnitOfWork` implements both the existing host `IUnitOfWork` and the
    application-owned `IApplicationUnitOfWork`. DI registers one concrete
    scoped instance and maps both interfaces to it.
- Time variables: mutation use cases receive `TimeProvider`; create passes
  `timeProvider.GetUtcNow().UtcDateTime` to the write port, and the EF adapter
  persists it with `DateTimeKind.Utc`.
- Authorization variables: create/update call
  `SiteAuthorizationPolicy.CanManage(user)` inside their transaction before
  any business read or write. Notification updates construct
  `SiteAuthorizationPolicy.ReadScope(user, timeProvider.GetUtcNow().UtcDateTime)`
  inside the transaction and call `ISiteReadPort.ExistsAsync` before resolving
  a notification target or evaluating ownership, so expired, future, and
  otherwise invisible assignments are masked as site-not-found.
- Atomic-claim variables: `contractId`, `companyId`, and the staged `siteId`
  form the compare-and-set predicate. A zero-row claim throws within the
  shared transaction, rolling back the saved candidate site and hours; the
  application catches that private signal outside the unit of work and returns
  the existing contract-assigned validation error.
- Verification: focused application mutation tests pass 18/18; focused
  relational write-adapter and existing unit-of-work tests pass 11/11; all
  application tests pass 24/24; application-boundary, site-read-adapter,
  site-write-adapter, unit-of-work, and CQRS regressions pass 42/42; the SPA
  host build succeeds with
  zero warnings and zero errors. The existing `System.Security.Cryptography.Xml`
  NU1903 advisories remain outside this task.
- DI state: `ISiteReadPort` resolves to scoped `EfSiteReadAdapter`. The legacy
  `RvtPortal.Spa.Application.Sites.ISiteApplicationService` remains registered
  for controllers; HTTP cutover is deferred to Task 6.
- Access variables: `VisibleSites` supports `All`, inclusive-window `Assigned`,
  and empty `None` scopes. Assignment comparisons use `scope.NowUtc`.
- Sort variables: `DefaultSort = "createDate"`,
  `MonitorSort = "fleetNumber"`, and
  `NotificationSort = "notificationTime"`. Open-notification pages and
  site-detail notification rows retain the existing limit of 20.
- Detail facts: `CanManage` is enriched by the application service;
  `HasCustomerLogo` remains false until Task 5 adds the storage port.
- Tests:
  - `SiteReadUseCaseTests` verifies assigned-scope masking and exact materialized
    query forwarding with a reusable, extensible `FakeSiteReadPort`.
  - `SiteReadAdapterTests` resolves the adapter through DI and verifies inclusive
    active and expired assignment windows for existence and paged visibility.
  - `SiteMutationUseCaseTests` verifies create/update manage gates, notification
    visibility-before-ownership masking for expired/future assignments, and
    stale-claim result mapping.
  - `SiteWriteAdapterTests` uses relational SQLite and the real shared
    `EfCoreUnitOfWork` to prove a stale conditional claim rolls back the
    candidate site and its seven operating-hour rows without replacing the
    existing contract assignment.
  - Application boundary architecture checks continue to prove
    `RvtPortal.Application` has no package/project references or forbidden
    framework/adapter imports.
- Known environment concern: portal builds continue to report the pre-existing
  NU1903 high-severity advisories for `System.Security.Cryptography.Xml` 10.0.7.
  No dependency versions changed in this task.

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

## Immediate Blockers Task 5 Review Completion - 2026-07-23

- Task 5 implementation is complete through `0bd22c2` and passed independent
  re-review with no Critical, Important, or Minor findings.
- The approved contract rejects non-UTC API inputs, converts client
  `datetime-local` values to UTC ISO instants, deliberately strips Kind only at
  PostgreSQL `timestamp without time zone` query boundaries, restores UTC on
  grid/graph/monitor-summary reads, and persists contract calendar dates as UTC
  midnight without workstation-local conversion.
- Checked-in PostgreSQL aggregate view SQL, EF runtime mappings, and the search
  model snapshot now agree. UTC-naive aggregate fallbacks use
  `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'`; only genuine daily aggregates remain
  `date`. Deterministic tests cover UTC and Europe/London display without skips.
- Verification passed 347 backend tests with six provider-gated skips, all 68
  client tests, both builds, and diff checks. The provider-gated metadata/query,
  telemetry JSON, and contract-persistence tests are authored but were not run
  because `RVT_TEST_POSTGRES_CONNECTION` is unset. Do not claim live Npgsql or
  deployed-schema closure until those tests execute successfully.

## Immediate Blockers Task 6 Implementation - 2026-07-23

- `RVT.SchemaDeploy` now resolves and executes one deterministic sequence:
  `create_unmapped_schema.sql`, `restore_unmapped_column_defaults.sql`, then
  ordinally sorted `post-load/*.sql`. Dry-run and actual execution use the same
  resolver; publish output includes the repair exactly once under `sql/`.
- `ScriptRunner.RunAsync(NpgsqlConnection, CancellationToken)` accepts a
  caller-owned open connection so provider tests can execute two deployments
  inside one rollback-owned transaction. The provider-gated idempotency test
  verifies canonical defaults and fingerprints seeded row values/counts before
  and after the second run.
- `share-dev-database.sh` no longer suppresses `pg_restore`. It returns the
  exact restore status, stops before post-restore success output on failure,
  and requires valid nonzero public-table and TimescaleDB hypertable counts
  before printing `Restore complete.`
- TDD evidence: RED was 4 expected failures, 4 passes, and 2 provider skips;
  GREEN is 8 focused passes with 2 provider skips. The full portal project
  passes 352 tests with seven provider-gated skips. Shell syntax, a fake-Docker
  failure/zero/success harness, actual publish contents, the portal solution
  build, and diff checks pass.
- A pre-commit implementation review found no Critical, Important, or Minor
  issues. Live PostgreSQL double-run/idempotency remains unverified because
  `RVT_TEST_POSTGRES_CONNECTION` is unset; it must run against a dedicated test
  database before deployed-schema closure is claimed.

## Immediate Blockers Task 6 Review Fix - 2026-07-23

- Canonical schema deployment is now complete-or-fail. `ResolveScripts`
  requires `create_unmapped_schema.sql`,
  `restore_unmapped_column_defaults.sql`, and at least one non-AppleDouble
  `post-load/*.sql` file before returning a list. Missing stages throw a clear
  `DeployException` before dry-run output or any PostgreSQL connection attempt.
- The canonical order remains create, repair, then ordinally sorted post-load
  scripts, each exactly once. Both public execution paths still use the same
  validated resolver.
- Review-fix test variables are `dryRun`, `verificationCounts`,
  `expectedStage`, and `expectedError`. Six stage cases cover dry-run and real
  execution mode for missing create, missing repair, and sidecar-only
  post-load. Shell cases independently cover `5|0`, `x|2`, and `5|x` in
  addition to the existing `0|0`, failure-status, and success controls.
- Review-fix RED was `6 failed, 11 passed, 2 provider skips`; GREEN was
  `17 passed, 2 provider skips`. The full portal project passes `361` with
  seven provider skips (`368` total), publish contains the repair exactly once
  and dry-runs seven scripts in order, Bash syntax and diff checks pass, and the
  portal solution builds with zero errors plus the five existing NU1903
  advisories.
- Live PostgreSQL remains deliberately unclaimed because
  `RVT_TEST_POSTGRES_CONNECTION` is unset.

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

## Immediate Blockers Final Whole-Branch Review Fix - 2026-07-23

- Trace-index timestamps now follow the same explicit UTC application/plain
  PostgreSQL timestamp boundary as other search telemetry. Both
  `MonitorDataSource.GetTraceIndexesAsync` range bounds and
  `MonitorService.GetVibrationTracesIndex` point-in-time bounds pass through
  `SearchTimestampPolicy.ToDatabase`, preserving ticks, converting UTC Kind to
  Unspecified only at the EF query boundary, and rejecting Local or
  Unspecified application inputs.
- Trace list `StartTime`/`EndTime` and trace detail `FromDate`/`ToDate` pass
  through `SearchTimestampPolicy.FromDatabase` before DTO serialization.
  Database-style Unspecified values therefore leave the API as explicit UTC
  JSON instants ending in `Z`; routes and DTO property names are unchanged.
- Timestamp controls added to
  `apps/portal/RvtPortal.Spa.Tests/DataViewTests.cs` inspect real EF command
  parameter Kinds, reject both non-UTC Kinds, and drive the authenticated HTTP
  trace list/detail routes with database-style values. The PostgreSQL contract
  test covers runtime and snapshot `OmnidotsTracesIndex.StartTime`/`EndTime`
  mappings. The provider-gated `TraceIndexes_UtcBounds_QuerySuccessfullyAndReturnUtcJson`
  test inserts a real trace index, queries listing/detail with UTC bounds, and
  checks list/detail JSON `Z` output when
  `RVT_TEST_POSTGRES_CONNECTION` is configured.
- `share-dev-database.sh` now always makes a best-effort
  `timescaledb_post_restore()` call after `pg_restore` fails. Cleanup failure
  emits a distinct manual-cleanup diagnostic, while both cleanup-success and
  cleanup-failure paths return the original `pg_restore` status exactly and
  skip ANALYZE, verification queries, and completion output. The ordinary
  successful restore sequence is unchanged.
- Final-review variables are `databaseFromDate`, `databaseToDate`,
  `cleanup_status`, `cleanupStatus`, `FAKE_POST_RESTORE_STATUS`,
  `TraceBoundCommandProbe.DateTimeParameters`, `databaseStartTime`,
  `databaseStart`, and `databaseEnd`.
- TDD evidence: the first focused RED run produced six intended failures, one
  existing mapping pass, and one PostgreSQL skip; its focused GREEN run passed
  seven with one PostgreSQL skip. The point-in-time trace-index follow-up RED
  failed all three intended cases and its GREEN passed all three.
- Verification: the owning backend/schema slice passes 37 tests with three
  explicit PostgreSQL skips; the dedicated fake-Docker restore harness passes
  7/7; `bash -n` passes; the full portal project passes 369 tests with eight
  PostgreSQL skips (377 total); and the portal solution builds with zero errors
  plus the five pre-existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisory warnings. `git diff --check` is clean.
- Live PostgreSQL remains the sole provider evidence gap because
  `RVT_TEST_POSTGRES_CONNECTION` is unset. The new trace listing/detail test is
  discovered and explicitly skipped; no deployed-schema closure is claimed.
  The generated untracked `apps/.nuget-packages/` cache remains excluded from
  the commit.
- Final independent whole-branch re-review of `5048052..b5e5bd1` found no
  Critical, Important, or Minor issues. It confirmed both prior Important
  findings are resolved and assessed the five-area immediate-blocker tranche as
  ready to merge, subject to running the eight provider-gated tests against a
  dedicated PostgreSQL/TimescaleDB database before production rollout.

## Immediate Blockers Main Integration - 2026-07-23

- Local `main` was fast-forwarded from `5048052` to the reviewed immediate-
  blockers tip `0b30293`. The pending integration commit only corrects the
  password-reset security tests described below; production behavior is
  unchanged.
- The former worktree produced a false-green result because its
  `apps/portal/RvtPortal.Spa/appsettings.json` carried the macOS `hidden` file
  flag. ASP.NET Core's physical configuration provider omitted that file, so
  `AllowedHosts` was absent. The normal checkout correctly loads
  `AllowedHosts=localhost;127.0.0.1` and rejects `Host: attacker.example`
  before the authentication controller runs.
- `SecurityHardeningTests` now separates three contracts:
  a disallowed request host returns `400` without delivery; an allowed request
  with no `Authentication:PublicBaseUrl` returns the generic `200` response
  without delivery; and an allowed request with a configured public base URL
  sends a callback rooted at that configured origin.
- Current verification from the normal checkout:
  portal backend `370 passed, 8 provider-gated skips, 378 total`; client
  `68 passed`; client production build succeeded; monorepo solution/layout,
  RVT source-boundary, local-package sequencing, and documentation guards all
  passed. Four existing `System.Security.Cryptography.Xml` 10.0.7 NU1903
  advisories remain.
- Generated local state is intentionally untracked:
  `.codegraph/` in the normal checkout and
  `apps/.nuget-packages/` in the former immediate-blockers worktree.
- Next approved branch: `codex/sites-application-boundary`. Its scope is to
  introduce `RvtPortal.Application` and extract the complete Sites slice
  incrementally, retaining route and payload compatibility and avoiding a
  broad rewrite. The design must be written and reviewed before implementation.

## Sites Application Boundary Design - 2026-07-23

- Active branch: `codex/sites-application-boundary`, created from and tracking
  the pushed `main` integration commit `f7db3dd`.
- Approved design:
  `docs/superpowers/specs/2026-07-23-rvtportal-sites-application-boundary-design.md`.
- The new `RvtPortal.Application` project is BCL-only for the first slice: no
  NuGet or project references, ASP.NET Core, EF Core, DataAccess, host, or
  vendor SDK dependencies.
- `RvtPortal.Application` owns Sites use cases, contracts, results,
  `PortalUserContext`, the explicit UTC active-assignment policy, and focused
  read/write/archive/logo/user-directory/unit-of-work ports.
- `RvtPortal.Spa` retains controllers, API mapping, DI composition, optimized
  EF projections, Identity, archive/storage implementations, and the existing
  three-context `EfCoreUnitOfWork`.
- The full Sites surface includes list/options/detail, monitors, open
  notifications, create/update/archive, notification settings, authorization,
  and all customer-logo operations. Routes and payloads remain unchanged.
- Implementation order is scaffold, policies/contracts, reads, writes,
  controller cutover, and documentation. The design review and a task-level
  implementation plan are required before production code changes.

## Communication Abstractions Extraction - 2026-07-24

- `Rvt.Communication.Abstractions` now owns provider-neutral delivery ports,
  requests, failure exceptions, notification contracts, `LegacyMessageKind`,
  `LegacyMessageChannel`, and the source-compatible
  `Rvt.Monitor.Common.Notifications.RvtContactDto` type. The abstractions
  project has no project or provider dependencies.
- `Rvt.Monitor.Common` now references Abstractions; its active callers use the
  moved contracts and top-level legacy enums. `MessageService`,
  `NotificationDeliveryService`, and `NotificationMessageComposer` remain in
  Common for the next communication implementation-move task.
- Focused verification: abstraction tests pass 20/20. Common tests pass
  405/407; the only two failures are the accepted
  `MonitorDeliveryMigrationContractTests` path baseline. The former three
  `CommunicationsBoundaryTests` path failures are green.
- Aggregate source compilation reported no C# errors after the split. The
  immediate four-package validation bridge is in place for the Common
  package's new `Rvt.Communication.Abstractions` dependency; the broader
  package-release migration remains a later task.

### Task 1 Package Validation Bridge Fix

- The immediate four-package bridge now restores, packs, asserts, and evicts
  `Rvt.Communication.Abstractions` alongside the prior three packages. The
  RuntimeConsumer artifact lock was regenerated and includes the Common
  package's transitive abstraction dependency.
- The focused prerequisite guard passes, `dotnet build Rvt.Mono.slnx
  --no-restore --nologo` succeeds with zero errors, and Task 1's focused
  abstraction/Common results remain 20/20 and 405/407 respectively. The two
  Common migration-path failures and the unrelated aggregate imported-suite
  path/provider failures remain baseline exceptions.

## Communication Task 4: Microsoft Graph Mail Extraction - 2026-07-24

- `Rvt.Communication.MicrosoftGraphMail` owns Microsoft Graph email delivery:
  the Graph adapter, token-provider port and Azure Identity implementation,
  source-generated Graph models/context, upload sessions, provider options,
  registration, and startup validation. Its only project dependency is
  `Rvt.Communication.Abstractions`; Azure Identity is provider-local.
- `MicrosoftGraphMailOptions` maps `RVT:...` before literal `RVT__...` aliases
  for `EMAIL_ENABLED`, tenant ID, client ID, client secret, and sender address.
  Disabled providers permit absent credentials; enabled validation reports all
  missing key names without including secret values.
- The temporary `Rvt.Monitor.Common.Infrastructure` selector retains the
  `EmailProvider` runtime choice for the five monitor hosts, but constructs the
  moved Graph adapter from provider-owned Graph options. It directly references
  GraphMail and its compatibility package pins all four internal dependencies
  (Common, Communication, SendGridMail, and MicrosoftGraphMail) exactly.
- Root `Rvt.Mono.slnx` inventory contains the GraphMail source project under
  `/Libraries/RVT Monitor Common/` and its tests under `/Tests/`; the temporary
  compatibility bridge packs exactly seven packages and keeps Infrastructure
  packing at `-m:1`.
- Focused results: GraphMail tests 31/31, Infrastructure tests 31/31,
  package-artifact tests 16/16, solution inventory and source-boundary bridge
  guards pass, and all five monitor hosts build with `--no-restore`. Existing
  MSTest analyzer warnings and unrelated untracked Portal duplicate C# files
  remain outside this extraction.

## Communication Task 8: Legacy Infrastructure Removal - 2026-07-24

- The tracked `Rvt.Monitor.Common.Infrastructure` source and test projects are
  removed. Both solutions now list the five extracted communication projects
  and their five test projects; active monitors deliberately reference all
  three providers, while Portal remains limited to Abstractions and SendGrid.
- Communication ownership guards now confine SendGrid, Microsoft Graph, and
  TransmitSMS implementation markers to their provider projects and reject a
  reintroduced Infrastructure project. The transitional `build-mono.sh` graph
  now packs seven packages and no longer builds or validates Infrastructure.
- Provider projects no longer depend on the broad `Microsoft.AspNetCore.App`
  framework. They declare exact 10.0.9 Configuration, DI, Hosting, and HTTP
  dependencies as required; SendGrid and Azure Identity remain provider-local.
- Verification: ownership 7/7, SendGrid 20/20, Graph 31/31, TransmitSMS 24/24,
  MyAtm boundary 12/12, both shell boundary tests pass, and `rvt-common.sln`
  restores with temporary delegated locks and builds with zero errors.
- Lock regeneration remains delegated to the eleven-package release plan.
  Portal testing remains externally blocked by the preserved untracked
  duplicate `* 2.cs` files. Blob client/service unification and every other
  storage, compatibility, plugin, database, MQTT, scheduling, observability,
  notification, API, and persisted-record item remain future pending work.
## Sites Application Boundary Plan - 2026-07-23

- The approved design is expanded into
  `docs/superpowers/plans/2026-07-23-rvtportal-sites-application-boundary.md`.
- The plan contains seven independently reviewable tasks: compile-time
  scaffold, shared identity/UTC policies, read extraction, transactional
  writes, archive/logo workflows, controller cutover, and documentation/full
  verification.
- Key planned types are `UseCaseResult<T>`, `PortalUserContext`,
  `SiteAccessScope`, `ISiteApplicationService`, `ISiteReadPort`,
  `ISiteWritePort`, `ISiteArchivePort`, `ISiteLogoPort`, and
  `IApplicationUnitOfWork`.
- The application project remains BCL-only. Host adapters keep EF Core,
  Identity, archive, logo storage, and the existing shared-connection unit of
  work.
- No production implementation has started. The next action is selecting
  subagent-driven or inline plan execution.

## Sites Application Boundary Task 2 - 2026-07-23

- Active branch: `codex/sites-application-boundary`; Task 1 scaffold commit is
  `5246c4390aa0c73072c98afbe6bb8867da60b8c4`.
- `RvtPortal.Application.Identity` now owns the six-fact
  `PortalUserContext`, `PortalRoleNames`, `PortalUserProfile`, and
  `IPortalUserDirectory`. The superseded BusinessLogic identity files are
  removed, and all SPA host consumers resolve the application-owned types.
- `RvtPortal.Application.Sites` now owns `SiteAccessScopeKind`,
  `SiteAccessScope`, `SiteAssignmentWindow`, `ActiveSiteAssignment`, and
  `SiteAuthorizationPolicy`. Site access timestamps and assignment comparison
  timestamps must have `DateTimeKind.Utc`; assignment windows use inclusive
  start and end bounds.
- Policy variables and facts: `NowUtc` in the focused tests is
  `2026-07-23T12:00:00Z`; `SiteAccessScope.UserId` is populated only for
  `Assigned`; admins receive `All`; company users with a user id receive
  `Assigned`; all other users receive `None`.
- Focused verification passes: 4/4 application policy tests and 30/30 SPA
  CQRS/company-user site-access compatibility tests. The SPA restore continues
  to report existing high-severity NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7.
- Generated untracked `.codegraph/` and `apps/.nuget-packages/` remain
  untouched and excluded from the task commit. Task 3 Sites read extraction
  has not started.

## Sites Application Boundary Task 5 - 2026-07-23

- Active branch: `codex/sites-application-boundary`; Task 5 started from the
  accepted Task 4 commit `1e8a261753440162d2b9d02c1b6f1e992898b9be`.
- `RvtPortal.Application.Sites.Ports` now owns `ISiteArchivePort`,
  `SiteArchiveExportResult`, `ISiteLogoPort`, `SiteLogoUpload`,
  `SiteLogoFile`, `SiteLogoSaveOutcome`, `SiteLogoSaveResult`, and
  `SiteArchiveState`. The logo upload/download records intentionally use only
  BCL `Stream` payloads; the application project still has no package or
  project references and no host, HTTP, EF, configuration, or vendor SDK
  types.
- `ISiteApplicationService` and `SiteApplicationService` now cover archive,
  logo save/delete, and protected logo reads. Archive export completes before
  `IApplicationUnitOfWork.ExecuteInTransactionAsync`; an export failure opens
  no transaction and writes no archive state. Successful archive metadata uses
  `timeProvider.GetUtcNow().UtcDateTime`.
- Logo save/delete call storage only after `CanManageSiteAsync`; protected
  reads call storage only after `CanReadSiteAsync`. Inaccessible sites remain
  masked as not found, and storage validation messages are returned as the
  `logo` validation error.
- All successful detail reads now pass through `ReadDetailAsync`, which sets
  `SiteDetailModel.CanManage` and obtains `HasCustomerLogo` from
  `ISiteLogoPort.ExistsAsync`. `HasCustomerLogo` is mutable only so the
  application orchestration can enrich materialized EF projections.
- Host adapter structure added under
  `apps/portal/RvtPortal.Spa/Adapters/Sites`: `SiteArchiveAdapter` wraps
  `ISiteArchiveService`, maps non-cancellation failures to the compatible
  archive error, and rethrows cancellation; `SiteLogoAdapter` wraps
  `ICustomerLogoStorage` and maps BCL streams through an `IUploadedContent`
  implementation without disposing the caller-owned upload stream.
- `EfSiteReadAdapter.GetArchiveStateAsync` materializes only site id/archive
  state. `EfSiteWriteAdapter.MarkArchivedAsync` marks the tracked site and
  stages UTC `SiteArchived` metadata. DI now registers the four
  application-facing read, write, archive, and logo ports; controller cutover
  remains Task 6.
- Recording-test variables include `Events` (`export`, `transaction`,
  `archive`), `TransactionCount`, `ArchiveCount`, `ExportCount`, `SaveCount`,
  `DeleteCount`, `OpenReadCount`, `ArchiveUrl`, `ArchivedUtc`, and logo
  `Exists`/`SaveResult`. The fixed application clock is
  `2026-07-23T12:00:00Z`.
- TDD evidence: the first RED failed on the absent archive/logo contracts; the
  second RED failed on the absent use cases and seven-argument service
  constructor. The adapter/EF RED failed because the host did not implement
  `GetArchiveStateAsync` or `MarkArchivedAsync`. GREEN verification passes
  7/7 focused application workflow tests, 8/8 prescribed archive/storage
  tests, 4/4 focused adapter/EF additions, all 31 application tests, and 16/16
  application-boundary/read/write/unit-of-work regressions.
- The existing `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories
  remain outside Task 5. Generated `.codegraph/` and
  `apps/.nuget-packages/` remain untracked and must not be staged.

## Sites Application Boundary Task 6 - 2026-07-23

- Active branch: `codex/sites-application-boundary`; Task 6 started from the
  accepted Task 5 commit `7235e3d9ea5dc3ed9c5d3c08ffd4524722410bd7`.
- `SitesController` now depends on
  `RvtPortal.Application.Sites.ISiteApplicationService`,
  `ICurrentUserContextFactory`, and `IApiResultMapper` only. Its route,
  authorization, request-size, and response metadata are byte-for-byte
  unchanged from the Task 5 base.
- `SiteApiMapper` consumes the application-owned Sites contracts. The
  `ToApplicationPage` boundary copies the host-normalized legacy `PageRequest`
  fields (`SearchText`, `Page`, `PageSize`, `Sort`, and `SortDir`) into the
  application-owned page contract. Fixed-sort panels still use
  `GetNormalizedPage`, `GetNormalizedPageSize`, and `GetNormalizedSortDir`.
- `IApiResultMapper` retains its legacy `ApplicationResult<T>` overload and
  adds the six-kind `UseCaseResult<T>` mapping. `SitesController` keeps
  `CreatedAtAction` for create, maps `HasCustomerLogo` to
  `/api/sites/{id}/customer-logo` at the HTTP edge, and returns successful logo
  reads with `File(...)` so streams are never JSON-wrapped.
- DI now maps the application-owned `ISiteApplicationService` to the
  application-owned `SiteApplicationService`. The duplicate host
  `SiteApplicationService`, host `SiteCommands`, and
  `RVT.BusinessLogic.Sites` application models are removed.
- Intentional brief variance: the host
  `Application/Sites/ActiveSiteAssignment.cs` remains. The prescribed
  namespace scan found seven live non-Sites consumers in notification,
  dashboard, monitor, and alert-level slices. Repointing or relocating those
  EF expression consumers is outside Task 6, so deleting the helper would
  break accepted behavior. Current live variables remain `userId`/`nowUtc`,
  and the inclusive `StartDate <= nowUtc` plus nullable `EndDate >= nowUtc`
  assignment window is unchanged.
- Boundary-drift fix: the host contract suite uses EF InMemory, which does not
  implement `ExecuteUpdateAsync`. `EfSiteWriteAdapter.TryClaimContractAsync`
  now uses a conditional tracked claim plus `SaveChangesAsync` only when
  `Database.IsRelational()` is false. Relational providers retain the accepted
  atomic conditional `ExecuteUpdateAsync`; its `affected == 1` result is
  unchanged.
- TDD evidence: the architecture RED failed because the constructor exposed
  the legacy Sites service and `ICustomerLogoStorage`; its GREEN passed 1/1.
  The first compatibility run passed 35, skipped one PostgreSQL-gated test,
  and failed two create flows. Detailed logs traced both failures to the
  unsupported InMemory `ExecuteUpdateAsync` call. After the provider-specific
  compatibility branch, the prescribed architecture/contract slice passes
  38 with one PostgreSQL-gated skip, `SiteWriteAdapterTests` passes 4/4, and
  the complete SPA host suite passes 381 with eight provider-gated skips.
- Contract assertions explicitly preserve create `Location` as
  `/api/sites/{siteId}` and the protected post-delete logo response as 404.
  Independent-review regressions additionally preserve the legacy customer-logo
  upload failure ordering: current-user creation and masked site-manage
  visibility/existence precede null-logo validation, so a missing site with no
  logo returns the endpoint's `Site not found` 404 response. Invalid logo
  application validation is handled only at this HTTP edge as plain
  ProblemDetails with title `Invalid customer logo`, the storage error in
  `detail`, HTTP 400, and no ValidationProblemDetails `errors` member. The
  generic `IApiResultMapper` remains unchanged.
- Delete-logo missing-site translation is also endpoint-specific:
  `DeleteCustomerLogo` returns the controller's legacy `SiteNotFound(id)`
  ProblemDetails for `UseCaseResultKind.NotFound` before delegating all other
  result kinds to `IApiResultMapper`. The regression asserts HTTP 404, title
  `Site not found`, and detail `Site '{id}' was not found.`.
- Independent-review TDD RED failed both contracts with the former HTTP 400
  null-logo ordering and generic ValidationProblemDetails title. GREEN passes
  both regressions. The delete-logo re-review RED received HTTP 404 but exposed
  the generic mapper title `Resource not found.`; its GREEN passes. The
  architecture/contract slice now passes 39 with one PostgreSQL-gated skip,
  and the full SPA host suite passes 382 with eight provider-gated skips. The
  complete `SitesController` action-attribute sequence still has an empty
  metadata diff against Task 5 base
  `7235e3d9ea5dc3ed9c5d3c08ffd4524722410bd7`.
  Existing `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories remain
  outside Task 6. Generated `.codegraph/` and `apps/.nuget-packages/` remain
  untracked and excluded from the task commit.

## Sites Application Boundary Task 7 - 2026-07-23

- Active branch: `codex/sites-application-boundary`. The accepted application
  slice ends at Task 6 commit
  `13aa5ff4c678df3a70c9576ae73fccc067f56ac7`; the initial Task 7 documentation
  commit is `52e5a83d03132a96e80f50d29c651d4c93abd29a`
  (`docs: record sites application extraction`), and the accepted archive
  authorization repair is
  `8bf2f18afa0c33e3bf2749cbb4e8f01e097e90c4`
  (`fix: enforce archive management authorization`).
- Extraction decision: the Sites slice moved incrementally into the BCL-only
  `apps/portal/RvtPortal.Application` project. `RVT.BusinessLogic` remains the
  legacy application boundary for slices not yet extracted and must not be
  moved opportunistically during unrelated work.
- New ownership:
  - `RvtPortal.Application/Common` owns `UseCaseResult<T>`, `PageRequest`,
    `PagedResult<T>`, and the unit-of-work port.
  - `RvtPortal.Application/Identity` owns transport-neutral portal user facts,
    role names, profiles, and the user-directory port.
  - `RvtPortal.Application/Sites` owns the complete Sites contracts, pure UTC
    authorization policy, validation, application service, and focused ports.
  - `RvtPortal.Application.Tests` owns pure application use-case and policy
    tests. `RvtPortal.Spa.Tests` owns the executable filesystem architecture
    guards plus host adapter and HTTP compatibility coverage.
  - `RvtPortal.Spa/Adapters/Sites` owns the EF read/write, archive-export, and
    customer-logo adapters. `SitesController` and `SiteApiMapper` remain the
    HTTP/DTO edge.
- Composition registrations:
  - application `ISiteApplicationService` resolves to application
    `SiteApplicationService`;
  - `ISiteReadPort` resolves to `EfSiteReadAdapter`;
  - `ISiteWritePort` resolves to `EfSiteWriteAdapter`;
  - `ISiteArchivePort` resolves to `SiteArchiveAdapter`;
  - `ISiteLogoPort` resolves to `SiteLogoAdapter`;
  - `IPortalUserDirectory` resolves to the host `PortalUserDirectory`;
  - `IApplicationUnitOfWork` and the legacy host `IUnitOfWork` both resolve to
    the same scoped `EfCoreUnitOfWork`, retaining the shared domain, search,
    and Identity transaction.
- Public application boundary interfaces are
  `ISiteApplicationService`, `ISiteReadPort`, `ISiteWritePort`,
  `ISiteArchivePort`, `ISiteLogoPort`, `IApplicationUnitOfWork`, and
  `IPortalUserDirectory`.
- Approved Task 6 variance: the host
  `RvtPortal.Spa.Application.Sites.ActiveSiteAssignment` remains for seven live
  EF-expression consumers in notification close authorization, dashboard
  visibility, monitor list/read authorization, two monitor-administration
  queries, and alert-level authorization. Relocating those consumers is not
  part of the Sites extraction. The host helper and the application-owned pure
  policy retain explicit UTC input and inclusive start/end semantics.
- Final verification:
  - `RvtPortal.Application.Tests`: 32 passed, 0 failed, 0 skipped.
  - `RvtPortal.Spa.Tests`: 382 passed, 0 failed, 8 skipped, 390 total.
    Every skip is explicitly gated on real PostgreSQL/TimescaleDB:
    contract calendar-date persistence, UTC site insert, three search
    timestamp/view checks, dashboard-breach `timestamptz`, and two schema
    deploy/default checks.
  - `RVT_TEST_POSTGRES_CONNECTION` was unset. The eight provider tests were
    discovered but not executed, so provider closure is not claimed.
  - `RvtPortal.Spa.sln` built with 0 errors and 5 known NU1903 warnings.
  - `RvtPortal.Client` passed 68/68 tests and its production build completed.
  - Mono-solution, mono-layout, and RVT common-source boundary guards passed.
  - The ordinary documentation-layout run was polluted solely by the
    pre-existing untracked `apps/.nuget-packages/` cache and reported 180
    package-owned Markdown files in two issue groups. A clean isolated clone
    containing the exact tracked tree plus the Task 7 documentation
    finalization diff
    passed with 122 moves and 7 retained entry points; the cache was not moved,
    deleted, edited, or staged.
  - `git diff --check` completed with no output.
- Known advisories: `System.Security.Cryptography.Xml` 10.0.7 continues to
  report five existing high-severity NU1903 advisories:
  `GHSA-23rf-6693-g89p`, `GHSA-8q5v-6pqq-x66h`,
  `GHSA-cvvh-rhrc-wg4q`, `GHSA-g8r8-53c2-pm3f`, and
  `GHSA-mmjf-rqrv-855v`. No dependency version changed in this extraction.
- Remaining slice candidates, without selecting the next one, include monitor,
  report/report-rule, company/user, contract, notification, dashboard, and
  alert-level boundaries. Each requires its own behavior-preserving design and
  verification scope.
- Independent full-branch review of
  `1eeb6c71922b98dd7928330879a6813247c0a7e8..4a1f5e8f4360b2b24a0ab44719d00bec9e99bfe2`
  found no Critical issues and one validated Important authorization defect:
  `SiteApplicationService.ArchiveAsync` read archive state, exported, and
  persisted archive metadata without first applying
  `SiteAuthorizationPolicy.CanManage(user)`. The repair now places that policy
  check before every archive-state, export, transaction, write, save, or detail
  operation and returns the established `Forbidden` result for a direct
  non-manager application caller. The focused regression records zero archive
  state reads, detail-enrichment reads, logo-existence reads, exports,
  transactions, archive writes, saves, and workflow events. Authorized callers
  retain export-before-transaction ordering. Root final reviews and the
  implementation-plan review checkbox remain open, so this branch is not yet
  declared merge-ready.
- Authorization-repair TDD evidence: the focused RED failed 0/1 with expected
  `Forbidden` and actual `Success`; focused GREEN passed 1/1; the complete
  external-workflow slice passed 8/8; all application tests passed 32/32; and
  the complete SPA host suite passed 382 with eight PostgreSQL-gated skips.
  `RvtPortal.Spa.sln` built with 0 errors and the same five known NU1903
  warnings. The Task 7 ownership wording remains accurate: application tests
  own application use-case/policy coverage, while SPA tests own executable
  filesystem architecture guards plus host adapter and HTTP compatibility
  coverage. Review-hardening variables are `ArchiveStateReadCount`,
  `DetailReadCount`, `ExistsReadCount`, and `Events`; the mutation run without
  the management guard fails the focused control on all three read counters and
  the nonempty workflow event log.
- The exact accepted repair diff
  `52e5a83d03132a96e80f50d29c651d4c93abd29a..8bf2f18afa0c33e3bf2749cbb4e8f01e097e90c4`
  received a read-only review with no Critical, Important, or Minor findings.
  This closes the previously validated archive-authorization blocker only.
  Task 7 implementation-plan Step 7 remains unchecked because root still owns
  the final independent whole-branch review from `main` through the
  documentation-finalized head; merge or push readiness is not claimed here.
- Generated `.codegraph/`, `apps/.nuget-packages/`, and the progress ledger
  remain unmodified and excluded from the Task 7 commit.

## Sites Application Boundary Final Review Compatibility Repair - 2026-07-24

- This final-review repair started from
  `78d6addd70c851e4c845b1ae7b2629edd47f2baf` on
  `codex/sites-application-boundary`. Its scope is limited to validation and
  authorization precedence, validation-response compatibility, regression
  coverage, and restoration of an unrelated tracked report. Archive and
  notification-upsert concurrency, UTC-constructor hardening, and unrelated
  refactors remain outside this repair.
- `SiteApplicationService.UpdateAsync` still computes the pure `shape` result
  before opening the transaction, but now returns shape errors only after
  `SiteAuthorizationPolicy.CanManage(user)` and the scoped materialized
  `ExistsAsync` check. Shape errors therefore cannot displace direct-caller
  `Forbidden` or masked missing-site `NotFound`, while they still precede
  `GetMutationValidationDataAsync`, `UpdateAsync`, and `SaveChangesAsync`.
  The adapter's false update result remains the delete-race `NotFound` path.
- `UpdateNotificationSettingAsync` similarly computes `timePair` before the
  transaction but returns its errors only after scoped site visibility,
  `GetNotificationSettingTargetAsync`, and target-ownership authorization.
  Missing/inaccessible sites and missing targets stay masked as `NotFound`;
  foreign targets remain `Forbidden`; every rejected combination records zero
  notification writes and saves.
- `SiteMutationValidator.ValidateTimePair` now takes distinct `startField` and
  `endField` parameters. Parsing uses the corresponding field, while missing
  pair members and reversed-order errors remain on `startField`. Explicit
  daily operating-hour rows pass the same indexed key for both fields,
  preserving that legacy contract.
- When `SiteMutation.OperatingHours` is absent or empty, the validator no
  longer reparses synthesized rows. The already parsed `weekday`, `saturday`,
  and `sunday` results are passed to `LegacyOperatingHours` and expanded into
  seven `ValidatedSiteOperatingHours` rows. `parsedByDay` and `seenDays` are
  now used only for explicitly supplied daily rows.
- Application test structure:
  - `Sites/SiteMutationUseCaseTests.cs` covers malformed update precedence and
    invalid notification times combined with missing, inaccessible,
    expired/future, missing-target, and foreign-target resources. The fixture
    counters `ExistsCallCount`, `MutationValidationReadCount`,
    `NotificationTargetReadCount`, `UpdateCount`,
    `NotificationSettingCount`, and `SaveCount` establish the exact gate and
    zero-write behavior.
  - New `Sites/SiteMutationValidatorTests.cs` uses the theory variables
    `endField` and `startField` to assert exact legacy error field, message,
    order, and count for `EndTime`, `SatEndTime`, and `SunEndTime`, plus the
    single `StartTime` error for a reversed legacy weekday pair.
- `ContractSiteOperationsTests` adds host-level masking and
  `ValidationProblemDetails` compatibility coverage. The local `errors`
  dictionary materializes the serialized `errors` object so tests assert exact
  property counts and arrays for site and notification end fields, and prove
  that reversed weekday input emits no synthesized `OperatingHours[*]` keys.
- Strict TDD evidence:
  - precedence application RED: 8 failed, 0 passed; every failure expected
    `NotFound`/`Forbidden` and received `Validation`;
  - precedence HTTP RED: 5 failed, 0 passed; every failure expected
    `NotFound`/`Forbidden` and received `BadRequest`;
  - precedence GREEN: application 8/8 and HTTP 5/5;
  - validator application RED: 5 failed, 0 passed, exposing start-field end
    parse errors, synthesized-row duplicates, and six reversed-weekday errors;
  - validator HTTP RED: 5 failed, 0 passed, exposing missing end-field keys and
    serialized key counts of six instead of the legacy one or two;
  - validator GREEN: application 5/5 and HTTP 5/5;
  - independent-review coverage control: the authorized existing-site
    malformed-update test failed 1/1 against the pre-fix early-return mutation
    (`TransactionCount` expected one and was zero), then passed 1/1 after the
    repaired ordering was restored; its exact HTTP validation-body
    characterization passed 1/1.
- Fresh broad verification passes:
  - `RvtPortal.Application.Tests`: 40 passed, 0 failed, 0 skipped;
  - `RvtPortal.Spa.Tests`: 393 passed, 0 failed, 8 provider-gated skipped,
    401 total;
  - `RvtPortal.Spa.sln`: build succeeded with 0 errors and the five existing
    `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories.
  The client and API DTO/contract files are unchanged, so the conditional
  client gate was not triggered.
- `.superpowers/sdd/task-6-report.md` is restored byte-for-byte from
  `1eeb6c71922b98dd7928330879a6813247c0a7e8`; the exact historical diff is
  empty. The displaced Sites Task 6 report is retained only at ignored,
  unstaged `.superpowers/sdd/sites-application-task-6-report.md`.
- `RVT_TEST_POSTGRES_CONNECTION` remains unavailable, so the same eight
  explicitly provider-gated PostgreSQL/TimescaleDB tests were discovered but
  not executed. Generated `.codegraph/`, `apps/.nuget-packages/`, and
  `.superpowers/sdd/progress.md` remain outside the repair commit.

## Site Write Concurrency Repair - 2026-07-25

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Active branch: `codex/sites-application-boundary`. This repair started from
  and was verified against base
  `19e8dbe0e98664b4bb05c2dd571dfca7c41abf5e`. Its required single commit is
  named `fix: serialize site archive and notification writes`. The later root
  whole-branch review and any push remain separate; merge or deployment
  readiness is not claimed here.
- Public Sites HTTP routes, response envelopes, authorization behavior, and the
  one-archive-per-site domain contract are unchanged.
  `RvtPortal.Application` remains BCL-only.
- Approved design and implementation plan:
  - `docs/superpowers/specs/2026-07-24-site-write-concurrency-repair-design.md`;
  - `docs/superpowers/plans/2026-07-24-site-write-concurrency-repair.md`.
- Repair file structure:
  - `apps/portal/RVT.DataAccess/Context/RVTDbContext.cs`,
    `Migrations/20260723234806_EnforceSiteWriteUniqueness.cs`,
    its generated `.Designer.cs`, and `RVTDbContextModelSnapshot.cs` own the
    relational uniqueness model and one-time duplicate cleanup;
  - `apps/portal/RvtPortal.Application/Sites/Ports/ISiteReadPort.cs`,
    `ISiteWritePort.cs`, `ISiteArchivePort.cs`, and
    `Sites/SiteApplicationService.cs` own canonical archive state, atomic
    claim results, cleanup outcomes, and unknown-commit orchestration;
  - `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteReadAdapter.cs`,
    `EfSiteWriteAdapter.cs`, and `SiteArchiveAdapter.cs`, plus
    `Adapters/Archive/SiteArchiveService.cs` and
    `SiteArchiveWorkspaceFactory.cs`, own materialized canonical URL reads,
    provider-native DML, guarded blob reconciliation, and archive workspaces;
  - `apps/portal/database/postgres/post-load/06_site_write_uniqueness.sql`
    owns the rerunnable, non-destructive PostgreSQL duplicate guard and unique
    index repair;
  - application coverage is in `SiteExternalWorkflowTests.cs`,
    `SiteMutationUseCaseTests.cs`, and `SiteTestDoubles.cs`; host/provider
    coverage is in `SchemaDeployTests.cs`,
    `SiteArchiveServiceSecurityTests.cs`,
    `SiteArchiveWorkspaceFactoryTests.cs`, `SiteConcurrencyTests.cs`,
    `SiteConcurrencyPostgresTests.cs`, `SiteReadAdapterTests.cs`,
    `SiteWriteAdapterTests.cs`, and `SpaTestApplicationFactory.cs`.
- New application state and port signatures:
  - `SiteArchiveState(Guid SiteId, bool Archived, string? ArchiveUrl)`;
  - `SiteArchiveClaimResult(bool Claimed, string? DurableArchiveUrl)` returned
    by `ISiteWritePort.TryClaimArchiveAsync(...)`;
  - `SiteArchiveCleanupResult(bool Succeeded, string? ErrorMessage)` returned
    by `ISiteArchivePort.CleanupSupersededAsync(Guid siteId,
    string durableArchiveUrl, CancellationToken)`;
  - the host archive boundary exposes
    `ISiteArchiveService.DeleteSupersededAsync(Guid siteId,
    string durableArchiveUrl, CancellationToken)`.
- Archive exports retain a unique local `archiveId` for `RootPath` and
  `ZipPath`, but every new export for one site uses the deterministic blob key
  exactly `<site-id-N>/site-archive.zip`. Concurrent new exporters therefore
  share one overwriteable candidate while relational archive metadata remains
  canonical. Existing legacy URLs remain valid and are not renamed.
- `EfSiteReadAdapter.GetArchiveStateAsync` is a no-tracking projection that
  returns the site's archived flag and the single canonical
  `site_archived.picture_link`. Relational archive claims use provider-native
  conflict handling and return the durable URL for either winner or loser.
- Archive cleanup is retryable. An already archived request reconciles the
  derived stable candidate before returning detail, so a failed loser cleanup
  is rediscovered without a second export. Cleanup failure maps to the existing
  external-service-unavailable result.
- Blob reconciliation fails closed before any delete unless the durable URL is
  an absolute HTTP(S) URL without credentials or a fragment and identifies the
  configured archive scheme, host, port, account, and container. If durable
  metadata identifies the stable candidate, cleanup returns without a storage
  delete. If verified legacy metadata identifies a different blob, cleanup
  deletes only the derived stable candidate with snapshots included. No path
  deletes the canonical URL.
- Unknown transaction outcomes are verified with
  `GetArchiveStateAsync(id, CancellationToken.None)`. A matching canonical URL
  proves durable success and retains the candidate. A different canonical URL
  is treated as a losing claim and reconciles only the stable candidate. When
  canonical metadata is absent or verification fails, the candidate is
  retained and the original persistence exception is rethrown with its stack.
- Notification writes now converge on one complete row per `site_user_id`.
  PostgreSQL and SQLite use `INSERT ... ON CONFLICT ... DO UPDATE`; SQL Server
  uses a locked `UPDATE WITH (UPDLOCK, HOLDLOCK)` followed by a conditional
  insert. Archive claims similarly use PostgreSQL/SQLite conflict handling or a
  SQL Server locked existence check. EF InMemory keeps its tracked
  compatibility path, and unknown relational providers fail explicitly.
- PostgreSQL/Npgsql is the canonical checked-in EF migration, designer, model
  snapshot, and generated-script provider. Migration
  `20260723234806_EnforceSiteWriteUniqueness` locks the owner tables, retains
  the smallest notification-setting UUID, retains the newest archive by
  `create_date DESC, id DESC`, reconciles `site.archived = true`, and then
  creates unique indexes on `notification_setting.site_user_id` and
  `site_archived.site_id`. Its down path relaxes uniqueness without restoring
  discarded duplicates.
- Deterministic row deletion occurs only in that one-time migration.
  Rerunnable `RVT.SchemaDeploy` SQL locks the same tables, rejects duplicate
  owner groups with an actionable migration hint, and repairs only the two
  named indexes on clean data. It performs no table-data insertion, update,
  deletion, merge, or truncation.
- SQL Server runtime DML is structurally covered, but a canonical SQL Server
  migration chain/snapshot and live migration deployment were not generated or
  validated. SQL Server migration-deployment closure remains separate work.
  Historical blob URLs discarded by relational deduplication also remain an
  operator/lifecycle audit item because database migration has no storage
  credentials.
- Fresh final integration verification:
  - `RvtPortal.Application.Tests`: 48 passed, 0 failed, 0 skipped;
  - `RvtPortal.Spa.Tests`: 408 passed, 0 failed, 9 skipped, 417 total;
  - `RvtPortal.Spa.sln`: build succeeded with 0 errors and the five existing
    `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories;
  - PostgreSQL `dotnet ef migrations has-pending-model-changes` reported no
    model changes since the last migration;
  - `git diff --check` completed with no output.
- `RVT_TEST_POSTGRES_CONNECTION` was unset. The new
  `SiteConcurrencyPostgresTests.AtomicSiteWrites_ConcurrentRequestsKeepOneValidRowPerOwner`
  case and the existing eight PostgreSQL/TimescaleDB cases were discovered but
  skipped, so live PostgreSQL concurrency/deployed-schema closure is not
  claimed. The UTC-midnight notification fixture did not fail during this run,
  so no baseline-only exception was applied.
- Existing NU1903 advisories remain
  `GHSA-23rf-6693-g89p`, `GHSA-8q5v-6pqq-x66h`,
  `GHSA-cvvh-rhrc-wg4q`, `GHSA-g8r8-53c2-pm3f`, and
  `GHSA-mmjf-rqrv-855v`; dependency versions are unchanged.
- `.superpowers/sdd/task-6-report.md` remains byte-identical to merge-base
  `1eeb6c71922b98dd7928330879a6813247c0a7e8`. Generated `.codegraph/`,
  `apps/.nuget-packages/`, `.superpowers/sdd/progress.md`, this plan's ignored
  SDD workspace, and historical reports remain outside the repair commit.

## Site Write Concurrency Final Review Fix - 2026-07-25

- The final review fix started from reviewed commit
  `c9295f0ff087275b8129e18bfeeb99357f430a1a`; its parent remains
  `19e8dbe0e98664b4bb05c2dd571dfca7c41abf5e`, and the concurrency repair
  remains one amended commit named
  `fix: serialize site archive and notification writes`.
- `EfCoreUnitOfWork.ExecuteInTransactionAsync` now captures the primary
  operation/commit exception with `ExceptionDispatchInfo`, attempts rollback
  with `CancellationToken.None`, and explicitly disposes application, search,
  and domain transaction wrappers in reverse creation order. Rollback and
  disposal faults are secondary; when possible they are retained under
  `Exception.Data` as an `AggregateException`, and diagnostic attachment itself
  is best-effort so it cannot replace the primary. The same primary-preservation
  and reverse-cleanup rules now cover caller-owned ambient enlistments.
  A disposal fault remains primary when the operation/commit path succeeded.
- `EfCoreUnitOfWorkTests` uses a real shared SQLite relational transaction and
  EF transaction interceptor. Its commit interceptor throws a stable exception
  instance and cancels the live request token; rollback then throws a distinct
  instance while proving cleanup received `CancellationToken.None`. The test
  asserts primary identity and stack plus retained secondary diagnostics.
  A second fault test proves a primary exception whose virtual `Data` getter
  throws still escapes unchanged.
- New `SiteSqlServerDmlTests.cs` constructs `RVTDbContext` with
  `UseSqlServer`, suppresses connection opening and async non-query execution,
  and records the real provider-generated `SqlCommand` and `SqlParameter`
  values without a live SQL Server. Archive coverage asserts the complete
  metadata insert, site-owner predicate, `NOT EXISTS`,
  `UPDLOCK, HOLDLOCK`, and winner-only site archived update. Notification
  coverage asserts the locked complete update, `site_user_id` predicate,
  `@@ROWCOUNT` gate, and complete conditional insert.
- The SQL Server archive claim is now one batch: after the locked conditional
  metadata insert, `IF @@ROWCOUNT > 0` performs a parameterized
  `[site].[archived]` update for the same site. A winner therefore needs no
  follow-up site read; the zero-row loser path still reads and returns the
  canonical durable archive URL. This is runtime DML coverage only. A canonical
  SQL Server migration chain and live migration deployment remain separate and
  unclosed.
- `SiteArchiveServiceSecurityTests` now explicitly prove that a percent-encoded
  stable blob name and a stable URL carrying query/SAS parameters are equivalent
  to the candidate and cause no delete. The same configured account/container
  on a wrong effective port fails closed, and a loopback observer proves no
  delete request is sent.
- Strict RED/GREEN controls:
  - original UoW RED: 1 failed/1 total because rollback's exception replaced
    the exact commit exception; GREEN passed 1/1;
  - EDI mutation RED: 1 failed/1 total because a normal throw erased the commit
    interceptor stack; restored EDI GREEN passed 1/1;
  - throwing-`Data` RED: 1 failed/1 total because diagnostic attachment
    replaced the commit exception; the guarded attachment joined the focused
    UoW GREEN at 10/10;
  - SQL Server initial RED: notification passed, while archive failed 1/2 on
    the unsuppressed follow-up reader; compound archive DML GREEN passed 2/2.
    Removing `HOLDLOCK` from both runtime branches then failed 2/2, and the
    restored lock hints passed 2/2. Swapping archive owner/URL and notification
    email/SMS bindings then failed 2/2 on exact placeholder-to-column
    assertions before restoration;
  - URL mutation RED failed 3/3 by attempting deletes for encoded/query
    equivalents and accepting the wrong port; restored production passed 3/3.
- The final read-only re-review reported no remaining Critical, Important, or
  Minor findings.
- Fresh final verification:
  - combined UoW, SQL Server DML, and archive-security slice: 28 passed,
    0 failed, 0 skipped;
  - `RvtPortal.Application.Tests`: 48 passed, 0 failed, 0 skipped;
  - `RvtPortal.Spa.Tests`: 415 passed, 0 failed, 9 provider-gated skipped,
    424 total;
  - `RvtPortal.Spa.sln`: build succeeded with 0 errors and the five existing
    `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories;
  - canonical PostgreSQL
    `dotnet ef migrations has-pending-model-changes` reported no model changes
    since the last migration;
  - `git diff --check` completed with no output.
- `RVT_TEST_POSTGRES_CONNECTION` remains unset, so the nine explicit
  PostgreSQL/TimescaleDB cases were discovered but not executed. Live
  PostgreSQL concurrency and deployed-schema closure are not claimed. Live SQL
  Server execution and SQL Server migration deployment are also not claimed.
  Generated `.codegraph/`, `apps/.nuget-packages/`, SDD ledgers/workspaces,
  old progress files, and historical reports remain outside the amended commit;
  only the required ignored final-fix report is written in the SDD task folder.

## Sites Application Boundary Final Whole-Branch Review - 2026-07-25

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Current branch is `codex/sites-application-boundary`; its merge base with
  `main` is `1eeb6c71922b98dd7928330879a6813247c0a7e8`. The reviewed implementation
  head was `e3dba33d203180496b790ca0302749c86bbf4f58`.
- The independent read-only review of
  `1eeb6c71922b98dd7928330879a6813247c0a7e8..e3dba33d203180496b790ca0302749c86bbf4f58`
  reported no Critical or Important findings and assessed the branch as ready
  to merge.
- One Minor follow-up remains: the public positional constructors and `with`
  support on `SiteAccessScope` and `SiteAssignmentWindow` can bypass their UTC
  naming/invariants. `ActiveSiteAssignment.IsActive` checks `nowUtc` but not
  assignment bounds. Production scope construction currently flows through
  `SiteAuthorizationPolicy.ReadScope` and the validated factories, while the
  application assignment helper currently has test-only callers. A future
  slice should replace the positional records with immutable validated
  construction, enforce valid scope kind/user combinations and UTC assignment
  bounds, and add bypass regression tests.
- Fresh release verification on the reviewed tree:
  - `RvtPortal.Application.Tests`: 48 passed, 0 failed, 0 skipped;
  - `RvtPortal.Spa.Tests`: 415 passed, 0 failed, 9 provider-gated skipped,
    424 total;
  - `RvtPortal.Client`: 68 passed, 0 failed, and the production Vite build
    succeeded;
  - `RvtPortal.Spa.sln`: build succeeded with 0 errors and the five existing
    `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories;
  - PostgreSQL `dotnet ef migrations has-pending-model-changes` reported no
    changes;
  - mono-solution, mono-layout, and RVT common-source boundary guards passed;
  - the documentation-layout guard passed against a clean archive of the
    tracked tree with 122 moves and 7 retained entry points;
  - `git diff --check` completed with no output.
- The ordinary documentation-layout run remains polluted only by the generated,
  untracked `apps/.nuget-packages/` cache. That cache and `.codegraph/` remain
  untouched and outside Git.
- `RVT_TEST_POSTGRES_CONNECTION` is unset, so the nine live
  PostgreSQL/TimescaleDB tests remain explicit skips. Live SQL Server DML and
  SQL Server migration deployment also remain unclosed. These are deployment
  verification gaps, not hidden merge claims.

## PostgreSQL-Only Final Handoff - 2026-07-26

This section supersedes every earlier current-state or compatibility statement
in this file. Earlier dual-provider descriptions are retained only as
historical audit evidence. The current solution has no runtime dual-provider
fallback: PostgreSQL is the only supported relational database, with
TimescaleDB extensions where the schema requires them.

### Branch, handoff identity, and structure

- Worktree:
  `/Users/oldgeorge/Documents/rvt-mono/.worktrees/postgresql-only`.
- Branch: `codex/postgresql-only`.
- Verified Tasks 1-12 pre-state head:
  `b0d0ecb55f22308cb5e81a3ecc716b3c6dba7e60`.
- Design base:
  `a07f6019fc492531a2f7d67294dd17ace47058db`.
- Task 13 enforcement commit:
  `12c0efbf98eac9d5d702d9eb3e76c5558fcc5270`
  (`chore: enforce PostgreSQL-only solution`).
- The final-review handoff commit is the commit containing this amended
  section, with subject `fix: close PostgreSQL-only review gaps`. A file cannot
  contain its own final hash; the exact hash is recorded after commit in the
  ignored `.superpowers/sdd/2026-07-25-postgresql-only/task-13-report.md`.
- `Rvt.Mono.slnx` contains 40 projects: 14 under `apps/monitors`, 9 under
  `apps/portal`, 9 under `libs/rvt-monitor-common`, and 8 under
  `services/reporting`.
- The four primary module roots remain:
  - `apps/monitors`: AirQ, MyATM, Omnidots, ReportingMonitor, and Svantek
    applications and tests;
  - `apps/portal`: the application, Npgsql data access, schema deployer, host,
    tests, and the `RvtPortal.Client` Vite client;
  - `libs/rvt-monitor-common`: shared Common, Infrastructure, integration-test
    support, tests, and the two package-validation consumers;
  - `services/reporting`: PostgreSQL reporting core/data/messaging/PDF/storage,
    service host, and tests.
- Compared with the design base, 76 tracked paths were deleted. This includes
  all 36 retired-provider-named paths, the complete
  `apps/portal/database/sqlserver/` tree, the Omnidots `sqlserver/` tree,
  MyATM/shared `*.sqlserver.sql` migrations and rollbacks, the Portal provider
  package/assets/registries, the retired DML test, and 34 obsolete
  `docs/history/` documents. Git history is the audit source for deleted
  artifacts.

### Canonical architecture and package boundary

- Portal configures `RVTDbContext`, `RVTSearchContext`, and
  `ApplicationDbContext` only with Npgsql. Their migration histories remain
  separate: the domain default, `__EFMigrationsHistorySearch`, and
  `__EFMigrationsHistoryIdentity`.
- `UtcTimestampGuardInterceptor` remains active for `timestamptz` writes.
  Domain persistence stays UTC; search/telemetry plain `timestamp` mappings
  retain their intentional `DateTimeKind.Unspecified` contract.
- `RVT.SchemaDeploy` owns canonical PostgreSQL/TimescaleDB schema objects that
  EF does not own. Active SQL uses canonical PostgreSQL identifiers and
  syntax. The site-write uniqueness migration is unconditional canonical
  PostgreSQL SQL. The repository guard rejects `ActiveProvider`,
  `ProviderName`, `IsNpgsql`, or `IsSqlServer` selection code in every tracked
  Portal `**/Migrations/*.cs` history, including domain, search, and Identity.
  Its script contract requires both exact unique indexes after deduplication
  and the site-state update.
- Shared monitor persistence creates only `NpgsqlConnection`/
  `NpgsqlParameter`, uses PostgreSQL binary `COPY`, and maps canonical
  PostgreSQL tables, columns, constraints, and indexes. Runtime T-SQL
  rewriting and provider enums are deleted.
- AirQ, MyATM, Omnidots, Svantek, ReportingMonitor, and
  `services/reporting` use PostgreSQL/TimescaleDB only.
- Active application consumers use `ProjectReference` entries to the shared
  RVT source projects. Only
  `libs/rvt-monitor-common/package-validation/{RuntimeConsumer,TestConsumer}`
  consume the locally packed `0.2.0-rc.1` packages.
- There are 23 tracked package locks. The supported aggregate path generates
  ignored validation locks under `artifacts/validation-locks`. Relative to the
  design base, 17 intentional Task 11 lock changes remain; there is zero
  unexpected post-Task11 verification lock drift. Neither project files nor
  locks contain the retired provider packages.

### Configuration contract

- Portal runtime connection: `ConnectionStrings:DefaultConnection` or
  `Database:ConnectionString`.
- Monitor runtime connection:
  `ConnectionStrings__DefaultConnection`.
- Reporting service runtime connection:
  `ConnectionStrings:ReportingDatabase`.
- Portal design-time EF connection: `RVT_EF_CONNECTION`.
- Portal live verification: `RVT_TEST_POSTGRES_CONNECTION`.
- Monitor/TimescaleDB live verification:
  `RVT__POSTGRES_INTEGRATION_CONNECTION`.
- `RVT_EF_PROVIDER` is retired and removed.
- `RVT_ENFORCE_POSTGRESQL_ONLY` is retired and removed; every
  `scripts/build-mono.sh` run now executes
  `bash scripts/verify-postgresql-only.sh .` unconditionally.
- `Database:Provider`, `RvtDatabase:Provider`, `RVT__DATABASE_PROVIDER`,
  `DatabaseProvider`, and the equivalent `RVT:DATABASE_PROVIDER` configuration
  form are retired as selection keys and must be omitted from new deployment
  manifests, scripts, and examples. During transition, raw compatibility
  validators may still read a stale setting, accept only explicit
  PostgreSQL/Npgsql/Timescale aliases, and reject any other value before it can
  select a different provider.
- Real credentials remain outside Git in user secrets or the deployment secret
  store. Presence-only verification found `RVT_EF_CONNECTION`,
  `RVT_TEST_POSTGRES_CONNECTION`,
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, checked OpenAI/GitHub/NuGet secret
  variables, monitor API/vendor keys, and the checked default connection
  environment variable absent. No values were printed or recorded.

### Fresh Task 13 verification

- All 12 repository commands passed: PostgreSQL-only script/fixture,
  mono-layout script/fixture, mono-solution script/fixture, RVT common
  source-boundary script/fixture/regression, and documentation-layout
  script/fixture/regression.
- The documentation commands ran with normal Git config and the untouched
  untracked `apps/.nuget-packages/` cache. The initial guard reproduced 185
  generated Markdown discoveries and two issue groups. A focused regression
  now proves only that exact cache is pruned; the real guard passes with 86
  moves and 7 retained entry points.
- Plain `dotnet restore Rvt.Mono.slnx --locked-mode` was run once and exited 1.
  Before the supported repack, stale same-version local packages produced
  `NU1403` content-hash failures for `Rvt.Monitor.Common`,
  `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting`, plus `NU1101` for two retired transitive
  dependencies carried by the stale local artifacts. No committed lock was
  changed.
- The supported restore with `RvtUseArtifactValidationLocks=true` passed.
  The final 40-project no-incremental build passed with 0 errors and 66
  warnings: 5 `NU1903`, 2 `MSTEST0001`, 3 `MSTEST0032`, 8 `MSTEST0037`,
  47 `MSTEST0044`, and 1 `MSTEST0052`.
- The five existing `System.Security.Cryptography.Xml` 10.0.7 high-severity
  advisories reproduced unchanged:
  `GHSA-23rf-6693-g89p`, `GHSA-8q5v-6pqq-x66h`,
  `GHSA-cvvh-rhrc-wg4q`, `GHSA-g8r8-53c2-pm3f`, and
  `GHSA-mmjf-rqrv-855v`.
- Individually green .NET suites:
  - `RvtPortal.Application.Tests`: 48 passed, 0 failed, 0 skipped;
  - `RvtPortal.Spa.Tests`: 414 passed, 0 failed, 9 live skips, 423 total;
  - `Rvt.Monitor.CommonTests`: 423 passed, 0 failed, 0 skipped;
  - `Rvt.Monitor.Common.InfrastructureTests`: 64 passed, 0 failed, 0 skipped;
  - `Rvt.Monitor.PackageValidationTests`: 8 passed, 0 failed, 0 skipped;
  - `Rvt.Reporting.Core.Tests`: 26 passed, 0 failed, 0 skipped;
  - `Rvt.Reporting.Service.Tests`: 7 passed, 0 failed, 0 skipped;
  - focused `SchemaDeployTests`: 17 passed, 0 failed, 0 skipped;
  - post-review `CutoverReadinessTests`: 13 passed, 0 failed, 0 skipped;
  - generated uniqueness-migration script contract: 1 passed, 0 failed,
    0 skipped;
  - repaired Reporting deployment contracts: 3 passed, 0 failed, 0 skipped.
- Full monitor-suite evidence, before filtering:
  - AirQ: 89 passed, 33 failed, 0 skipped, 122 total;
  - MyATM: 155 passed, 53 failed, 0 skipped, 208 total;
  - Omnidots: 326 passed, 64 failed, 0 skipped, 390 total;
  - Svantek: 87 passed, 40 failed, 0 skipped, 127 total;
  - ReportingMonitor: 72 passed, 12 failed, 0 skipped, 84 total.
  Failures classify as the absent
  `RVT__POSTGRES_INTEGRATION_CONNECTION` guard and known imported pre-mono
  filesystem/solution assumptions.
- With only `TestCategory!=PostgreSqlIntegration` excluded:
  - AirQ passed 89/89 and Omnidots passed 326/326;
  - MyATM reported 155 passed and 10 known imported-assumption failures;
  - Svantek reported 87 passed and 5 known mono-path failures;
  - ReportingMonitor reported 72 passed, 10 missing-variable failures whose
    xUnit fixture lacks that category, and 2 remaining known mono-path
    failures. Its prerequisite, mono-solution, Compose, testlocal, and active
    documentation path/configuration contracts now pass.
- Narrow controls excluded only the named known failing classes after the broad
  results were recorded: MyATM passed 141/141, Svantek passed 87/87, and
  ReportingMonitor passed 68/68. These controls do not claim the excluded live
  or imported-path cases passed.
- The plan's obsolete `apps/portal/RvtPortal.Spa/ClientApp` prefix does not
  exist and both prescribed npm commands exit 254 with `ENOENT`. At the tracked
  `apps/portal/RvtPortal.Client` path, `test:run` passed 68/68 across 8 files
  and the production build succeeded after transforming 1,605 modules.
- `RVT_EF_CONNECTION` was absent, so the three EF
  `has-pending-model-changes` checks were not run.
  `RVT_TEST_POSTGRES_CONNECTION` was absent, so the 9 Portal live cases were
  discovered as skips rather than executed.
  `RVT__POSTGRES_INTEGRATION_CONNECTION` was absent, so live monitor suites and
  `Rvt.Monitor.IntegrationTesting.Tests` were not run as live verification.
- The final aggregate `scripts/build-mono.sh` run passed its unconditional
  guard, package repack, artifact-validation restore, and build (5 warnings,
  the five advisories above; 0 errors), then exited 1 at its unfiltered test
  stage with the same monitor missing-variable/mono-path totals recorded
  individually above. The three integration-test-support cases also failed
  closed because `RVT__POSTGRES_INTEGRATION_CONNECTION` was absent. Portal,
  Common, package-validation, and reporting-service aggregate results remained
  green.
- Whole-branch review from `a07f6019fc492531a2f7d67294dd17ace47058db`
  found and repaired one real escaped provider-conditional migration through a
  RED/GREEN guard mutation, then broadened that guard through independent
  Contains, StartsWith, equality, Identity-history, and provider-name mutation
  fixtures. Final review reports zero forbidden packages in projects/locks,
  zero tracked retired-provider paths, zero production legacy SQL tokens, zero
  provider-selection tokens in tracked Portal migration histories, 17
  intentional Task 11 lock changes with zero unexpected post-Task11
  verification lock drift, zero changed authorization production files, zero
  added `DateTime.Now`/`DateTime.Today` calls, zero added production
  `SaveChanges` calls, and a clean whole-branch `git diff --check`.

### Deployment, rollback, and known limitation

- Before deployment, take and verify a PostgreSQL/TimescaleDB backup, provision
  extensions and privileges, configure secret-store connections, apply all
  three Portal EF migration chains plus `RVT.SchemaDeploy`, apply the required
  monitor/reporting PostgreSQL prerequisites, and run the environment-gated
  pending-model and live integration suites against the target-compatible
  database.
- Remove stale provider-selection settings from deployment manifests. The raw
  compatibility validators remain only to fail closed on unsupported legacy
  values during the transition; they are not rollout configuration.
- Rollback is a coordinated Git/application rollback plus restoration of the
  verified database backup or the supported PostgreSQL rollback scripts for the
  deployed change. There is no runtime dual-provider rollback and no retained
  reader/conversion path for the retired database.
- Monitor source builds are supported. Checked-in monitor container builds are
  currently unsupported: their `apps/monitors` build context cannot reach
  repository-root shared source projects, and obsolete package-feed secret
  plumbing remains. A monorepo-root context, realigned Dockerfile paths, secret
  cleanup, and verified clean image builds are required before container
  support can be claimed.

## PostgreSQL-only main integration - 2026-07-26

- `main` at `d86c82bc6fb4e8808e328e9d09e062e0e2ed2868` was merged with
  `codex/postgresql-only` at
  `adf732af4bd03318645a452092f0631773f38afb` using an explicit merge commit.
  The integration preserves the Sites application boundary, PostgreSQL-only
  database contract, communication provider split, storage provider split, and
  eleven-package release catalog.
- Conflict resolution keeps `RvtPortal.Application`, Portal's explicit
  Abstractions/SendGrid host references, the removed legacy Infrastructure
  project, PostgreSQL test connections, SendGrid test settings, and allowed
  host filtering. Shared Common owns neither database-provider packages nor
  Azure/AWS storage SDKs.
- Merge repairs route superseded site-archive cleanup through
  `IBlobStorageClientFactory`, preserve Azure blob URL validation, pass
  `IConfiguration` into ReportingMonitor composition, and use monorepo-root
  path resolution in Common and ReportingMonitor architecture tests.
- Temporary package validation now matches the seven packages actually emitted
  by `scripts/build-mono.sh`: Common, IntegrationTesting, Abstractions,
  Communication, MicrosoftGraphMail, SendGridMail, and TransmitSms. The release
  catalog remains the source of truth for the incremental eleven-package
  `1.0.0-rc.1` train.
- All 12 PostgreSQL/layout/solution/source-boundary/documentation commands
  pass. The full tracked solution builds with zero errors; five existing
  `System.Security.Cryptography.Xml` 10.0.7 `NU1903` advisories remain.
- Green merged suites: Application 48/48; Portal 425/425 with 9 live
  PostgreSQL skips; Common 340/340; Storage 154/154; Communication 133/133;
  package validation 17/17; ReportingMonitor non-live 83/83; reporting
  core/service 40/40.
- Local verification used
  `/private/tmp/rvt-exclude-untracked-duplicates.targets` only to exclude the
  preserved untracked duplicate Portal `* 2.cs` files from compilation. No
  tracked build rule was weakened. Generated obsolete Infrastructure package
  artifacts were removed from the ignored `artifacts/packages` output.
- Preserved untracked state includes `.codegraph/`, `apps/.nuget-packages/`,
  `apps/monitors/reportingmonitor/Directory.Packages.props`, the two duplicate
  Portal C# files, `localDate 2.ts`, and the duplicate Sites design Markdown
  file. Restore also generated untracked `packages.lock.json` files for the
  four Storage provider projects and `Rvt.Storage.Tests`; their lock migration
  remains delegated to the eleven-package release plan.

## Direct internal project references - 2026-07-26

- Work is on `codex/direct-project-references`, based on merged `main`
  `ef9fca4`. The shared Common, communication, storage, and integration-testing
  projects remain separate projects under `libs/rvt-monitor-common`, but all
  monorepo consumers now use direct `ProjectReference` dependencies.
- The active architecture decision is
  `docs/superpowers/specs/2026-07-22-rvt-common-source-reference-design.md`.
  It supersedes internal package delivery while preserving provider/project
  separation as a decision that may be reviewed later.
- The root build sequence is now serial restore, single-node build, then test:
  `dotnet restore Rvt.Mono.slnx --disable-parallel`,
  `dotnet build Rvt.Mono.slnx --no-restore --nologo -m:1`, and
  `dotnet test Rvt.Mono.slnx --no-build --nologo`.
- All eleven internal shared projects have `IsPackable=false`; their internal
  `PackageId` metadata has been removed. `NuGet.config` retains nuget.org only
  for third-party dependencies. No internal feed, package version, or package
  credential variable is part of the build contract.
- Package-validation consumers/tests, package catalog, pack/release scripts,
  package-oriented CI workflows, private-feed monitor scripts, and generated
  internal package output directories were removed. The source-boundary guards
  reject reintroduced `Rvt.*` `PackageReference` entries, packable shared
  projects, internal feeds, package-validation directories, and Docker NuGet
  credential plumbing.
- Monitor containers build from the monorepo root. Compose uses `context: ../..`
  and the five Dockerfiles publish projects through their full
  `apps/monitors/...` paths, allowing shared source projects to remain within
  the Docker context.
- Verification completed during implementation: all seven repository shell
  guard suites passed; Compose configuration validated; a clean serial restore
  and full single-node solution build passed with zero errors; focused
  source-boundary architecture tests passed; 627 non-live Common,
  communication, and storage tests passed. The aggregate live provider suites
  still require `RVT__POSTGRES_INTEGRATION_CONNECTION`; their failures without
  that variable are environmental and unrelated to project references.
- Local full-build verification used
  `/private/tmp/rvt-exclude-untracked-duplicates.targets` only to exclude
  preserved untracked Portal files named `* 2.cs`. No tracked compile rule was
  weakened. Preserve the unrelated untracked `.codegraph/`,
  `apps/.nuget-packages/`, ReportingMonitor `Directory.Packages.props`,
  duplicate Portal source/design files, and restore-generated untracked storage
  lock files unless they are handled in a separately scoped change.

## Self-hosted SonarQube workflow implementation - 2026-07-26

- Branch: `codex/direct-project-references`; final repair implementation commit
  `e1c8def` (`Repair isolated Sonar runner workflow`) is based on
  `4ba98ae1532dee6c9ab1551ee28f1e972eceb7e0`, and validation state was recorded
  at `21963aa`. The runner stack was introduced by `29a1805` (`Add isolated
  self-hosted Sonar runner`); the approved design remains
  `docs/superpowers/specs/2026-07-26-manual-sonarqube-workflow-design.md`.
- `.github/runner/` contains `Dockerfile`, `entrypoint.sh`, and
  `docker-compose.yml`. The Dockerfile builds Ubuntu 24.04 with GitHub Actions
  Runner `2.334.0` for Linux ARM64 and verifies archive SHA-256
  `f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4` before
  extraction. Noble uses `libssl3t64` and `liblttng-ust1t64`, with
  `libkrb5-3` and `libgssapi-krb5-2`; it does not retain `libssl3` or install
  Docker.
- `docker-compose.yml` defines the `rvt-sonar-runner` project with
  `rvt-sonar-db` (`timescale/timescaledb:2.28.3-pg17`) and
  `rvt-sonar-runner`. `rvt_sonar_ci` is the Compose seed/admin database; it is
  not a test target. The hostname is `rvt-sonar-db` and the credentials are
  `postgres` / `postgres`. It has no published ports, bind mounts, privileged
  services or Docker socket mount. `runner-state` is the only Compose-declared
  named volume; the TimescaleDB base image may use Docker-managed anonymous
  storage, but each analysis creates and drops its own run-scoped database.
- Runner variables are `RUNNER_URL=https://github.com/chris-oldgeorge/rvt-mono`,
  `RUNNER_NAME=rvt-sonar-dev`, `RUNNER_LABELS=rvt-sonar`, and
  `RUNNER_BOOTSTRAP_ONLY=false` by default. The persistent Compose service has
  no `RUNNER_REGISTRATION_TOKEN`. A transient `docker compose run --rm`
  bootstrap container receives the short-lived token and
  `RUNNER_BOOTSTRAP_ONLY=true` only for first registration or replacement. The
  entrypoint accepts `RUNNER_DIST_ROOT`, `RUNNER_HOME`, `RUNNER_STATE_ROOT`, and
  `RUNNER_USER` overrides; it persists only `.runner`, `.credentials`, and
  `.credentials_rsaparams`, then exits in bootstrap-only mode. With persisted
  state and bootstrap-only false, it restores the symlinks, unsets any token,
  and starts the listener.
- `.github/workflows/sonarqube.yml` is named `SonarQube`, has
  `workflow_dispatch` as its only trigger, uses
  `[self-hosted, linux, ARM64, rvt-sonar]`, permits only `contents: read`, and
  applies a non-cancelling `sonar-${{ github.ref }}` concurrency group. Each
  run derives `rvt_sonar_${{ github.run_id }}_${{ github.run_attempt }}`, waits
  for `rvt-sonar-db` through the `postgres` database, terminates stale
  connections, force-drops/recreates that database, adds `timescaledb` and
  `pgcrypto`, and exports `RVT_TEST_POSTGRES_CONNECTION`,
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, `RVT_EF_CONNECTION`, and
  `RVT_DEPLOY_CONNECTION` through `GITHUB_ENV`.
- SonarQube Cloud identity is project `aileron-forward_rvt-mono` in organization
  `aileron-forward` at `https://sonarcloud.io`; `SONAR_TOKEN` remains a GitHub
  repository secret. The workflow installs JDK 17, .NET 10, Node.js 24,
  `dotnet-sonarscanner` `11.2.1`, `dotnet-coverage` `18.9.0`, and job-local
  `dotnet-ef` `10.0.7`.
- Analysis runs a Release `Rvt.Mono.slnx` restore/build/test and sends .NET XML
  coverage from `artifacts/coverage/coverage.xml` plus Portal Vitest LCOV from
  `apps/portal/RvtPortal.Client/coverage/lcov.info`. Before .NET coverage it
  applies the `RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext`
  migrations with canonical paths, then runs `RVT.SchemaDeploy`. An
  `if: always()` final step force-drops only the run-scoped database without
  Docker. It waits up to 600 seconds for the Sonar quality gate and fails closed
  for missing credentials, unhealthy database, build/test/coverage/upload
  failures, or a red/timed-out gate.
- Operator instructions are in
  `docs/operations/github-actions/self-hosted-sonar-runner.md`, indexed from
  `docs/index.md` and linked concisely from the root `README.md`. The guide
  keeps the registration token out of repository files, shell history, and the
  persistent runner container configuration; it exists transiently in the
  auto-removed bootstrap container. It documents log inspection, normal stop
  and restart, and replacement/recovery: remove the local persistent runner and
  stale GitHub record, then delete only `rvt-sonar-runner_runner-state`, obtain
  a fresh token, bootstrap, and start again. That named volume contains only
  runner registration state; deleting it requires re-registration.
- Strict repair TDD evidence: after adding the runner dependency contract, the
  unchanged Dockerfile failed with `Ubuntu Noble runner image must install
  libssl3t64`; after adding the job-database contract, the unchanged workflow
  failed with `database connections must be exported after a unique per-run
  database is created`. Both focused guards pass after the minimal repair. The
  workflow guard also rejects extension-only preparation and missing
  schema-deployment mutations.
- Validated after the final repair:
  `tests/verify-sonar-runner-stack.test.sh` PASS;
  `tests/verify-manual-sonarqube-workflow.test.sh` PASS; every
  `tests/verify-*.test.sh` PASS (documentation layout, PostgreSQL-only,
  RVT source-boundary, direct-project-reference, runner-stack, and workflow
  guards); `docker compose -f .github/runner/docker-compose.yml config --quiet`
  PASS; `bash -n .github/runner/entrypoint.sh
  tests/verify-sonar-runner-stack.test.sh` PASS; and `git diff --check` PASS
  with no whitespace errors.
- Docker Desktop was started for image and live-run validation.
  `docker info --format '{{.Architecture}} {{.ServerVersion}}'` reported
  `aarch64 29.4.3`;
  `docker compose -f .github/runner/docker-compose.yml build
  rvt-sonar-runner` completed successfully, including the pinned archive
  checksum; and a one-shot container running as UID/GID `1001` reported
  `Runner.Listener --version` as `2.334.0`.
- The repository runner `rvt-sonar-dev` is registered, online, and uses labels
  `self-hosted`, `Linux`, `ARM64`, and `rvt-sonar`. GitHub automatically updated
  the listener from `2.334.0` to `2.336.0`; the persistent runner and TimescaleDB
  containers remain running without a registration token.
- The first manual run, GitHub Actions run `30194905575`, reached the runner and
  always-on database cleanup, then failed at `Set up .NET 10` because
  `actions/setup-dotnet` defaults to `/usr/share/dotnet` on Linux while the
  listener deliberately runs as the unprivileged `runner` user. The workflow
  now sets `DOTNET_INSTALL_DIR=${{ runner.temp }}/dotnet` on that setup step.
  The regression guard fails when the override is absent, passes with the
  repair, and a live non-root write check against the runner temporary
  directory passed.
- The second manual run, GitHub Actions run `30195309477`, proved the non-root
  SDK repair and passed runner setup, database preparation, tool installation,
  and SonarCloud initialization. Restore then failed because the tracked monitor
  central-package catalog pinned `Microsoft.Extensions.Logging.Abstractions`
  `10.0.4` below EF Core's `10.0.9` dependency and omitted
  `Rvt.Reporting.Storage` from the `Microsoft.Extensions.Options` `10.0.9`
  condition. NuGet in SDK `10.0.302` masked the downgrade with a
  `PackagesLockFileUtilities.HasP2PDependencyChanged` null reference; SDK
  `10.0.203` exposed `NU1109`. The tracked catalog now uses Logging Abstractions
  `10.0.9` and covers both Reporting Messaging and Storage for Options. With no
  nested ReportingMonitor override, the exact SDK `10.0.302` serial restore and
  Release single-node build passed in the Linux ARM64 runner with zero errors.
- Runs `30196184422` and `30197086817` were abandoned after independent
  GitHub runner transport/session failures during SDK download and job-lease
  renewal. Restarting only the persistent runner service restored its existing
  registration; the database service and runner-state volume were preserved.
- Run `30198150365` then proved the repaired setup, clean restore, and complete
  Release build in the real workflow. All three Portal EF migration chains
  passed, but `RVT.SchemaDeploy` failed on
  `post-load/06_site_write_uniqueness.sql` with PostgreSQL `25P01` because
  `LOCK TABLE` was executed without an explicit transaction block. Its
  always-on run-database cleanup passed.
- `ScriptRunner.RunAsync()` now opens one explicit transaction for the complete
  ordered deploy, commits only after every script succeeds, and passes that
  transaction explicitly to its Npgsql commands. The already-open connection
  overload still participates in the caller-owned transaction used by existing
  rollback-based integration tests.
- Regression
  `Run_WithOwnedConnection_ExecutesLockingScriptInsideTransaction` uses a
  temporary table and minimal deploy fixture against real TimescaleDB. Before
  the repair it failed at the intended boundary with `25P01`; after the repair
  the identical test passed 1/1 without mutating the Portal schema. The focused
  Release build passed with zero errors; the five existing
  `System.Security.Cryptography.Xml` 10.0.7 `NU1903` advisories remain.
- The transaction repair is committed as `59d8efa` and pushed to both
  `codex/direct-project-references` and `main`. Manual Sonar run `30199164649`
  analyzes that exact commit.
- Attempt 1 of run `30199164649` was infrastructure-abandoned during .NET setup:
  the runner's first lease expired after no renewals and GitHub returned
  `TaskAgentJobNotFoundException`. Restarting only the runner listener preserved
  registration and database state.
- Attempt 2 passed .NET/Node setup, run-database preparation, tool installation,
  and Sonar initialization, then was infrastructure-abandoned during the clean
  restore/build. Lease renewal succeeded once per minute through 11:34:43 UTC,
  then DNS resolution for
  `run-actions-1-azure-eastus.actions.githubusercontent.com` hung past the
  11:44:43 lease expiry. The runner eventually reported `HostNotFound`, followed
  by `NotFound` because GitHub had already invalidated the job. This is not a
  build or schema-deploy failure; the workflow never reached the repaired
  database step.
- The runner-only DNS hardening is approved and implemented. The
  `rvt-sonar-runner` Compose service uses `1.1.1.1` and `8.8.8.8` with
  `timeout:2` and `attempts:3`; the rendered-Compose regression guard fails if
  either the explicit resolvers or bounded options are removed. The operator
  guide documents that DNS changes require recreating only the runner service.
- Strict DNS TDD evidence: with the Compose service unchanged, the focused
  guard failed with `runner must use explicit public DNS resolvers`; after the
  minimal Compose change the identical guard passed. Rendered Compose validation
  and shell syntax validation also passed.
- Only `rvt-sonar-runner` was force-recreated. The database container retained
  ID `c3da4a5afa806a49ceda5f090e422d936c5adea0b133de3471a7a5c935dfd2f3`,
  the `rvt-sonar-runner_runner-state` registration volume was preserved, and
  GitHub reported `rvt-sonar-dev` online and idle after startup. Docker reported
  live DNS `["1.1.1.1","8.8.8.8"]` with
  `["timeout:2","attempts:3"]`; broker, token, run, results-receiver, and the
  previously failing Azure East US Actions hostname each resolved 10/10 times
  from inside the new runner container.
- Next action: commit and push the DNS hardening to the feature branch and
  `main`, retry Sonar run `30199164649`, monitor job-lease renewal and every
  workflow step through the Sonar quality gate, then verify the runner is idle
  and only `rvt_sonar_ci` remains.

## Branch Integration Attempt - 2026-07-27

- User requested that merged branches be pushed and cleaned up.
- Remote refs were refreshed. `origin/main` and
  `origin/codex/direct-project-references` both point to `59d8efa` ("Make
  schema deployment transactional").
- `codex/sites-application-boundary` at `a07f601` is an ancestor of that
  `main` history; it has no unmerged commits. Local `main` was safely
  fast-forwarded from `ef9fca4` to `59d8efa` and currently matches
  `origin/main`.
- The owned auxiliary worktree remains at
  `.worktrees/release-platform-hardening` on
  `codex/direct-project-references`, also at `59d8efa`. Do not remove that
  worktree or either branch until validation is green.
- Integration validation ran all root `tests/*.test.sh` in sorted order.
  The documentation layout, manual SonarQube workflow, PostgreSQL-only main
  guard, PostgreSQL-only fixtures, and RVT source-boundary regression passed.
  `tests/verify-rvt-common-source-boundary.test.sh` then failed with:
  `FAIL: Package-validation consumers must be removed; internal RVT projects
  are source referenced.` This is inconsistent with the latest main history,
  which intentionally removed the package-validation consumers. The aggregate
  `scripts/build-mono.sh` validation was not run, and no push, branch deletion,
  or worktree cleanup was performed.
- Untracked generated directories currently present in the primary worktree:
  `.codegraph/` and `apps/.nuget-packages/`. They were not touched.

## Boundary Guard Repair - 2026-07-27

- Root cause of the branch-integration blocker: direct-project-reference commit
  `54d522c` removed `libs/rvt-monitor-common/package-validation`, but
  `scripts/verify-rvt-common-source-boundary.sh` retained an obsolete assertion
  that failed whenever the correctly removed directory was absent.
- The minimal repair removes only that directory-existence assertion. All
  source-project-reference, `IsPackable=false`, container credential, and
  internal-RVT-package-reference checks remain unchanged. The user referred to
  the wrapper `tests/verify-rvt-common-source-boundary.test.sh`; the stale
  assertion was located in the guard script it invokes.
- Verification: the focused wrapper and its source-only mutation regression
  pass. A fresh sorted run of all nine root `tests/*.test.sh` scripts passes,
  and `git diff --check` is clean. The documented aggregate
  `scripts/build-mono.sh` command also completed after its PostgreSQL boundary
  check without reporting restore, build, or test errors.

## Branch Integration Completion - 2026-07-27

- The boundary-guard repair and state record were committed to `main` as
  `ffdcafd` (`fix: remove obsolete package-validation guard`) and pushed to
  `origin/main`.
- `codex/sites-application-boundary` was confirmed merged, then deleted both
  locally and from `origin`.
- `codex/direct-project-references` remains in the owned
  `.worktrees/release-platform-hardening` worktree. Although it is merged,
  that worktree has a tracked `project_state.md` modification and multiple
  untracked generated/suffixed-copy files. It and its remote branch were
  deliberately preserved to avoid deleting that unsaved work.

## Sonar DNS hardening integration - 2026-07-27

- DNS hardening commit `ccf5688` was pushed to
  `origin/codex/direct-project-references`. Its initial `main` push was safely
  rejected because `origin/main` had advanced to `724d2a7` with the boundary
  guard repair and branch-integration documentation.
- The two remote-main commits were reviewed and merged without overwriting
  either line of work. Merge commit `a5624d0` contains the DNS hardening,
  transactional schema deployment, obsolete guard removal, and both state
  records.
- Pending actions are a fresh post-merge guard run, pushing the integrated
  branch and `main`, retrying Sonar run `30199164649`, and monitoring the job
  through database deployment, coverage upload, and the quality gate.

## Architecture and Code Quality Review Reference - 2026-07-27

- The authoritative project-by-project coding-practices, hexagonal-architecture,
  style-consistency, remaining-work, and dead-code review is:
  `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`.
- Security analysis was explicitly excluded from this review.
- Use the ordered `R1` through `R11` checklist in that document as the remaining
  remediation sequence. Update the checklist and this state record when a phase
  is completed or deliberately deferred.
- The new communication and storage adapter projects conform to the intended
  dependency direction. The main remaining boundaries are legacy monitor
  facades, the Portal's dual application architecture, duplicated reporting
  lineages, and infrastructure still collected in `Rvt.Monitor.Common`.
- The sidecar investigation identified the previous workspace as an iCloud
  Drive File Provider domain:
  `com.apple.CloudDocs.iCloudDriveFileProvider/E2494D5B-200D-4B93-8033-4F36D6975AE8`.
  The completed migration below removes that condition.

## Non-iCloud Workspace Migration - 2026-07-27

- The complete repository moved from
  `/Users/oldgeorge/Documents/rvt-mono` to
  `/Users/oldgeorge/Developer/rvt-mono`.
- The old path no longer exists. The new repository root has no
  `com.apple.file-provider-domain-id` extended attribute.
- The linked `release-platform-hardening` worktree moved with the repository.
  `git worktree repair` updated both absolute Git pointers to
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/release-platform-hardening`.
- The project architecture review was preserved in commit `9a19baf`
  (`docs: record architecture quality review`) before the move.
- Existing untracked package/configuration artifacts were preserved. A
  differing historical iCloud conflict report was retained as
  `.superpowers/sdd/task-7-sites-application-boundary-report.md`.
- All 4,609 remaining Finder-style numbered conflict files, which were confined
  to generated output, dependency-cache, and code-index paths, were removed.
- Operational documentation now uses the non-iCloud repository path.
- Both CodeGraph indexes were rebuilt at their new paths. The stale daemon
  serving `/Users/oldgeorge/Documents/rvt-mono` was stopped.
- Codex's persistent trusted-project entry, saved local-project root,
  current-task assignment, and writable-root entries now use
  `/Users/oldgeorge/Developer/rvt-mono`. The currently running desktop process
  may continue to display its cached pre-migration path until Codex is
  restarted; its persisted state is already updated for the next launch.
- Relocation initially invalidated absolute paths in existing NuGet
  `project.assets.json` files. A forced root restore regenerated that metadata
  successfully at the new path.
- Post-migration verification passed: the 50-project root solution built with
  zero errors and five pre-existing Portal package warnings; all nine root
  repository guards passed; Portal client tests passed 68/68; and the Vite
  production build passed. Client lint retained two known Fast Refresh warnings
  and no errors.
- After all builds and tests, repository-wide conflict scans reported zero
  Finder-style numbered files and zero numbered directories.
- Git lists both worktrees exclusively under `/Users/oldgeorge/Developer`, and
  the old iCloud path remains absent. The local main branch remains three
  commits behind `origin/main`; no unrelated branch integration was performed
  as part of the filesystem migration.

## Final main integration and cleanup - 2026-07-27

- `main` and `codex/direct-project-references` were reconciled at the same
  integrated history. The final provider metadata addition is commit
  `ff7fbda` (`chore: add storage package locks`).
- The five storage `packages.lock.json` files are intentional source artifacts:
  their project tree enables `RestorePackagesWithLockFile`, and peer projects
  already track equivalent lock files. The untracked nested
  `apps/monitors/reportingmonitor/Directory.Packages.props` was a stale
  dependency-version shadow and was removed.
- Final verification passed a locked root restore, a serial no-restore root
  build with zero errors, and all 154 storage tests. Existing analyzer and
  package-advisory warnings remain unchanged.
- Repository-wide scans found no Finder-style numbered conflict files or
  directories after verification.
- Generated `.codegraph` and `apps/.nuget-packages` trees are disposable local
  caches and are not source artifacts. The merged feature worktree and local
  and remote feature branches can be removed after `main` is published.
- Current file layout is a single repository rooted at
  `/Users/oldgeorge/Developer/rvt-mono`; the intended final branch is `main`.
  Start future sessions with: `Read project_state.md to get up to speed`.

## Post-move runner re-anchor - 2026-07-27

- Canonical repository root:
  `/Users/oldgeorge/Developer/rvt-mono`.
- Canonical linked validation worktree:
  `/Users/oldgeorge/Developer/rvt-mono/.worktrees/release-platform-hardening`.
- Git reports both worktrees only at the new location. The relocation
  validation base was `ff7fbda`, shared by `main` and
  `codex/direct-project-references`; both were four local commits ahead of
  their remote tracking refs at `cfab4a3` before this state-only update.
- Remote `origin` remains
  `https://github.com/chris-oldgeorge/rvt-mono.git`.
- Preserve the unrelated generated directories `.codegraph/` and
  `apps/.nuget-packages/`; both remain untracked.
- The persistent self-hosted runner was recreated from the new root with
  `--no-deps --force-recreate`. Its Compose `config_files` and `working_dir`
  labels now point to `/Users/oldgeorge/Developer/rvt-mono/.github/runner`.
- Runner registration volume `rvt-sonar-runner_runner-state` was preserved.
  GitHub reports `rvt-sonar-dev` online, idle, and labelled `self-hosted`,
  `Linux`, `ARM64`, and `rvt-sonar`.
- DNS hardening remains live on the recreated runner:
  `Dns=["1.1.1.1","8.8.8.8"]` and
  `DnsOptions=["timeout:2","attempts:3"]`. Resolution of the GitHub Actions
  broker hostname passed from inside the container.
- The TimescaleDB container was not restarted. It remains healthy with ID
  `c3da4a5afa806a49ceda5f090e422d936c5adea0b133de3471a7a5c935dfd2f3`.
  Its old Compose path label is inert metadata; normal Compose discovery from
  the new root works through the stable project and service labels.

## Sonar run after DNS hardening - 2026-07-27

- Manual Sonar workflow run `30229885735` analyzed exact commit
  `cfab4a3e795bced8a9dd6aaa697aa26cc91b2c26`.
- The DNS repair is live-proven: the runner renewed its job lease for the whole
  approximately eleven-minute run with no `HostNotFound` error and no abandoned
  job.
- Checkout, JDK, .NET, Node, database preparation, tool installation, Sonar
  begin, clean restore/build, Portal database deployment, and always-on
  database cleanup all passed. This also live-proves the transactional schema
  deployment repair, including `post-load/06_site_write_uniqueness.sql`.
- Coverage failed while running the full test solution. The runner image is
  missing the Ubuntu `tzdata` package and therefore lacks
  `/usr/share/zoneinfo/Europe/London` and
  `/usr/share/zoneinfo/Africa/Johannesburg`. Direct failures report
  `TimeZoneNotFoundException` or timezone options validation for
  `GMT Standard Time`, `Europe/London`, and `South Africa Standard Time`;
  remaining Portal HTTP 500 failures must be reclassified after the timezone
  prerequisite is restored.
- Proposed next independent CI repair, still requiring explicit approval under
  the CI-fix workflow: add `tzdata` to `.github/runner/Dockerfile`, add a
  focused regression assertion to
  `tests/verify-sonar-runner-stack.test.sh`, rebuild and recreate only the
  runner, verify both zone files/runtime lookups, and dispatch a fresh manual
  Sonar run.

## Sonar runner timezone repair and retry - 2026-07-27

- Approved repair branch:
  `codex/sonar-runner-tzdata`.
- Commit `b0e7fe6` (`Install timezone data in Sonar runner`) is pushed to
  `origin/codex/sonar-runner-tzdata`.
- `.github/runner/Dockerfile` now installs Ubuntu `tzdata` in the existing
  noninteractive `--no-install-recommends` apt transaction.
- `tests/verify-sonar-runner-stack.test.sh` retains a focused package contract.
  Strict TDD evidence:
  - RED failed with `AssertionError: runner image must install timezone data`;
  - after the single Dockerfile dependency change, the identical guard passed.
- All nine root `tests/verify-*.test.sh` guards passed and `git diff --check`
  was clean before commit.
- The ARM64 runner image rebuilt successfully. The recreated runner reports
  `tzdata 2026b-0ubuntu0.24.04.1`; both
  `/usr/share/zoneinfo/Europe/London` and
  `/usr/share/zoneinfo/Africa/Johannesburg` exist and `zdump` resolves them as
  BST and SAST.
- Only `rvt-sonar-runner` was recreated. The registration volume
  `rvt-sonar-runner_runner-state`, explicit DNS resolvers, and bounded DNS
  options were preserved. The database remained healthy with unchanged
  container ID
  `c3da4a5afa806a49ceda5f090e422d936c5adea0b133de3471a7a5c935dfd2f3`.
- Initial dispatch `30232046545` was created before the recreated listener had
  cleared its stale GitHub session. It remained unassigned and was cancelled.
  Replacement run `30232216236` was immediately accepted after the listener
  was stable and analyzed exact commit
  `b0e7fe68c6d4dd612aab1db114e2f050db0d39ef`.
- The replacement run passed checkout, JDK, .NET, Node, database preparation,
  tool installation, Sonar begin, root restore/build, Portal database
  deployment, and always-on database cleanup. Job-lease renewal remained
  healthy throughout.
- The timezone repair is live-proven. AirQ passed 124/124, Omnidots 392/392,
  ReportingMonitor 93/93, Portal Application 48/48, and Portal SPA 435/435.
  The run log contains no `TimeZoneNotFoundException`, invalid-timezone, or
  Europe/London/Africa/Johannesburg failure. Other shared and reporting suites
  also passed.
- Coverage still failed for a separate monorepo-topology baseline:
  - MyATM: 13 failed, 195 passed. Most failures construct stale paths under
    `<repo>/myatmmonitor/...` instead of
    `<repo>/apps/monitors/myatmmonitor/...`; the Mapperly analyzer policy still
    expects a three-segment pre-monorepo project path; and its migration
    documentation contract reports that the durable exact-commit archive
    retrieval instructions are missing.
  - Svantek: 5 failed, 131 passed. All five construct stale paths under
    `<repo>/svantekmonitor/...` instead of
    `<repo>/apps/monitors/svantekmonitor/...`.
- Post-run state: runner `rvt-sonar-dev` is online and idle, and database
  cleanup left only `rvt_sonar_ci`.
- Next independent repair requires approval: update the MyATM and Svantek test
  fixture paths and Mapperly path-shape policy to the monorepo layout, restore
  the durable exact-commit archive retrieval instructions in the MyATM module
  documentation using current monorepo paths, run the two focused test
  projects against the integration database, then retry Sonar.
- Preserve the unrelated unstaged `.gitignore` edit that ignores `.codegraph/`
  and `apps/.nuget-packages/`; it is not part of the timezone repair.

## Monorepo test-path repair - 2026-07-27

- Branch `codex/sonar-runner-tzdata` now contains pushed commit `aaa20de`
  (`Repair monorepo test paths`), exact SHA
  `aaa20de59e44189ee4857feed1261bfd9f66f709`.
- Reproduced the failures before repair. MyATM tests constructed obsolete
  `<repo>/myatmmonitor/...` paths, Svantek tests constructed obsolete
  `<repo>/svantekmonitor/...` paths, and the Mapperly analyzer policy assumed
  the pre-monorepo three-segment project shape. The mapper policy's repository
  scan also included the unrelated Portal data-access project.
- Updated seven MyATM test files and three Svantek test files to use
  `apps/monitors/<monitor>/...`. The Mapperly policy now checks the five-part
  monitor project shape and scopes discovery to `apps/monitors`.
- Updated `docs/modules/monitors/myatmmonitor/README.md` and its migration
  documentation contract to describe the checked-in PostgreSQL shared,
  forward, and rollback scripts under current monorepo paths. The contract no
  longer expects the removed external SQL Server archive.
- Focused verification passed:
  - MyATM path/policy suite: 22/22;
  - Svantek path suite: 5/5;
  - all nine root `tests/verify-*.test.sh` repository guards;
  - `git diff --check`.
- Manual Sonar workflow run `30239653729` analyzed the exact pushed repair SHA:
  <https://github.com/chris-oldgeorge/rvt-mono/actions/runs/30239653729>.
  Checkout, tool setup, restore/build, Portal database deployment, the full
  `.NET coverage` test command, Portal client coverage, and always-on database
  cleanup all passed. This is the full-run proof that the 13 MyATM and five
  Svantek monorepo-port failures are repaired.
- The analysis report uploaded successfully, after which SonarCloud returned
  `QUALITY GATE STATUS: FAILED`. This is a separate static-analysis gate result,
  not a build, test, database, timezone, DNS, or path failure. The Sonar MCP/CLI
  needed to enumerate the individual gate conditions was unavailable in this
  session, so no threshold details were inferred.
- The long Sonar end step was caused by local analysis, including a
  602.814-second JavaScript security sensor pass. The log also confirms that
  unignored files under the 730 MB `apps/.nuget-packages/` cache entered the
  scan and produced missing-blame warnings.
- Post-run state: self-hosted runner `rvt-sonar-dev` is online and idle; the
  temporary run database was removed and only `rvt_sonar_ci` remains.
- Preserve the unrelated unstaged `.gitignore` edit that ignores `.codegraph/`
  and `apps/.nuget-packages/`; it remains outside the path-repair commit and
  this state checkpoint.

## Local full-suite rerun - 2026-07-27

- Re-ran the full monorepo test sequence from
  `codex/sonar-runner-tzdata` after a fresh restore and serial Release build.
  Restore completed, and build completed with 0 errors and 73 existing
  analyzer/package warnings.
- The test environment used a dedicated disposable PostgreSQL login and
  database on the local TimescaleDB container. The database received
  `timescaledb` and `pgcrypto`, all three Portal EF migration contexts, and all
  eight `RVT.SchemaDeploy` scripts before the final test pass.
- A stale ignored
  `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure` directory
  contained only old `obj` output and `.DS_Store`. It caused the clean-layout
  architecture test to fail locally even though the directory is absent from
  Git and CI. It was moved intact to
  `/private/tmp/rvt-mono-stale-Rvt.Monitor.Common.Infrastructure-20260727`.
- Final verification command:
  `dotnet test Rvt.Mono.slnx --configuration Release --no-build --no-restore --nologo -m:1`.
  Result: 2,109 passed, 0 failed, 0 skipped across all 17 test projects.
  MyATM passed 208/208, Svantek passed 136/136, Portal SPA passed 435/435,
  and Common passed 340/340.
- The disposable database and login were removed after the run and their
  absence was verified. No tracked restore/build changes remain. Preserve the
  pre-existing unstaged `.gitignore` edit.

## Build-warning review - 2026-07-27

- A clean serial Release build with restore disabled and incremental compilation
  disabled succeeded in 12.16 seconds with 0 errors and 73 warnings. The
  warnings-only log is
  `/private/tmp/rvt-build-warnings-20260727.log`.
- The warning inventory is limited to five NuGet security warnings and 68
  MSTest analyzer warnings; there are no production compiler or nullable
  warnings:
  - `NU1903`: 5, all emitted by `RvtPortal.Spa.Tests` for the same transitive
    `System.Security.Cryptography.Xml` 10.0.7 package and five high-severity
    advisories.
  - `MSTEST0001`: 7 assemblies have not made an explicit test parallelization
    choice.
  - `MSTEST0032`: 3 assertions compare compile-time constants and therefore
    cannot fail.
  - `MSTEST0037`: 8 collection-count assertions should use `Assert.HasCount`.
  - `MSTEST0044`: 49 obsolete `[DataTestMethod]` attributes should be
    `[TestMethod]`.
  - `MSTEST0052`: 1 `DynamicDataSourceType.Method` argument is redundant.
- A current full transitive vulnerability audit confirmed that the cryptography
  package is the solution's only vulnerable dependency. It enters through
  `Microsoft.AspNetCore.DataProtection` 10.0.7. Portal uses Data Protection for
  persisted keys and optional Key Vault protection, so the path is
  operationally relevant.
- The repository has patch-version drift: 24 direct Microsoft package
  references remain at 10.0.7 and nine are at 10.0.9. The local SDK is
  10.0.203 with .NET and ASP.NET Core runtimes 10.0.7. The current supported
  security patch is .NET 10.0.10 with SDK 10.0.302; all five reported
  `System.Security.Cryptography.Xml` advisories are fixed in 10.0.10.
- Proposed remediation order, not yet implemented:
  1. Align the SDK, runtimes, all direct Microsoft ASP.NET Core/EF
     Core/Extensions package references, the Sonar workflow's `dotnet-ef`
     version, and rebuilt deployment images to 10.0.10/SDK 10.0.302.
  2. Regenerate affected lock files and prove the transitive audit is clean,
     then run restore, build, all 2,109 tests, schema deployment, and Portal
     authentication/Data Protection smoke checks.
  3. Mechanically replace the 49 obsolete attributes, remove the one redundant
     DynamicData argument, and convert the eight count assertions.
  4. Preserve the intended public-constant contract checks by reading each
     constant through reflection instead of deleting the three tests or
     suppressing `MSTEST0032`.
  5. Add an explicit per-assembly parallelization choice to the seven affected
     test projects, defaulting to method-level parallelization only after
     repeated focused runs confirm isolation; use `DoNotParallelize` for any
     genuinely shared-resource suite.
  6. Once the baseline is clean, gate high/critical NuGet advisories and these
     specific MSTest rules in CI. Centralize Microsoft patch versions and add a
     `global.json` roll-forward policy to prevent future drift.
- No warning remediation code, dependency version, workflow, or build-policy
  change was made during this review. Preserve the existing unrelated
  `.gitignore` edit.

## Security-warning remediation - 2026-07-27

- Implemented the approved security-only remediation on
  `codex/sonar-runner-tzdata`. Commit
  `de004947295fcef70fcee03bf24805399f351574`
  (`Resolve .NET dependency security warnings`) was pushed to the matching
  origin branch.
- Added root `global.json` selecting stable SDK 10.0.302, disallowing
  prereleases, and allowing forward movement to later stable feature bands.
  Verification used an isolated SDK at
  `/private/tmp/rvt-dotnet-10.0.302`, whose installed runtimes are
  `Microsoft.NETCore.App` 10.0.10 and `Microsoft.AspNetCore.App` 10.0.10.
- Aligned direct Microsoft ASP.NET Core, EF Core, and Extensions packages from
  10.0.4/10.0.7/10.0.9 to 10.0.10 across Portal, monitors, common libraries,
  and reporting messaging. Regenerated and locked all affected
  `packages.lock.json` files.
- The remaining vulnerable edge was caused by
  `RvtPortal.Spa.Tests` inheriting `Microsoft.AspNetCore.App` only through its
  Portal project reference. NuGet package pruning does not use a transitive
  framework reference, so Azure Data Protection dependencies remained at
  10.0.7 in the test restore graph. Added an explicit
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to
  `RvtPortal.Spa.Tests`; the vulnerable Data Protection and cryptography
  packages are now pruned rather than overridden or suppressed.
- Updated the manual Sonar workflow to install the SDK selected by
  `global.json` and updated its `dotnet-ef` tool from 10.0.7 to 10.0.10. The
  workflow regression test now enforces both selections.
- Added `NU1903` and `NU1904` to `WarningsAsErrors` in the Portal, monitor, and
  common `Directory.Build.props` files. Reporting already has
  `TreatWarningsAsErrors=true`. Evaluated MSBuild properties confirm every
  solution subtree now fails restore/build on high or critical NuGet
  advisories.
- Final verification:
  - fixed-SDK locked restore passed;
  - full current NuGet advisory query reported no vulnerable package in any of
    the 50 solution projects;
  - non-incremental Release build passed with 0 errors and 68 warnings, all
    previously inventoried MSTest analyzer warnings; `NU1903`, `NU1904`, and
    `NU1510` are absent;
  - all nine repository guard scripts passed;
  - all three Portal EF migration contexts and all eight schema-deployment
    scripts were applied to a disposable local TimescaleDB database;
  - full database-backed suite passed: 2,109 passed, 0 failed, 0 skipped.
- The disposable database and login were removed and their absence was
  verified. `RVT__POSTGRES_INTEGRATION_CONNECTION`,
  `RVT_TEST_POSTGRES_CONNECTION`, `RVT_EF_CONNECTION`, and
  `RVT_DEPLOY_CONNECTION` were command-scoped only; none remains active.
- Preserve the pre-existing unrelated `.gitignore` change for `.codegraph/`
  and `apps/.nuget-packages/`; it remains unstaged and was excluded from the
  security-remediation commit.

## Safe mechanical MSTest cleanup - 2026-07-27

- Applied only the warning-review plan's syntax-preserving MSTest cleanup
  under `libs/rvt-monitor-common/tests`; no production code, project file,
  package, lock, workflow, or build policy changed.
- Replaced the 49 active `[DataTestMethod]` attributes with `[TestMethod]`,
  removed the one redundant `DynamicDataSourceType.Method` argument, and
  replaced eight `Assert.AreEqual(expected, collection.Count)` assertions with
  `Assert.HasCount(expected, collection)`.
- A fresh fixed-SDK non-incremental Release build of `Rvt.Mono.slnx` passed
  with 0 errors and 10 warnings. `MSTEST0037`, `MSTEST0044`, and `MSTEST0052`
  are absent; the intentionally deferred baseline is seven `MSTEST0001`
  parallelization-choice warnings and three `MSTEST0032` constant-contract
  warnings.
- All seven affected test assemblies passed, totaling 627/627:
  `Rvt.Monitor.CommonTests` 340, Communication Abstractions 20,
  Communication 31, SendGrid 20, Microsoft Graph 37, Transmit SMS 25, and
  Storage 154.
- The umbrella `rvt-common.sln` command additionally discovered the separate
  `Rvt.Monitor.IntegrationTesting.Tests` project. Its three database-dependent
  tests failed solely because `RVT__POSTGRES_INTEGRATION_CONNECTION` was not
  set; the other three tests in that project passed. No database-dependent
  behavior was changed by this cleanup.
- Verification used
  `/private/tmp/rvt-dotnet-10.0.302/dotnet`; the warnings-only build log is
  `/private/tmp/rvt-mstest-mechanical-after-20260727.log`.
- This checkpoint accompanies the cleanup commit. The same `.gitignore`
  additions for `.codegraph/` and `apps/.nuget-packages/` are already present
  in the newer target-branch commit and remain outside the cleanup diff.

## MSTest constant contracts and explicit parallelization - 2026-07-27

- Work is implemented on branch
  `codex/mstest-contracts-parallelization`; this checkpoint travels with the
  branch. The execution plan is
  `docs/superpowers/plans/2026-07-27-mstest-contracts-parallelization.md`.
- The three former `MSTEST0032` assertions now resolve the public static fields
  with `BindingFlags.Public | BindingFlags.Static` and compare
  `FieldInfo.GetRawConstantValue()` with the frozen values
  `Alerts:DurableDelivery`, `MyAtm`, and `Svantek`. No warning suppression or
  production behavior changed.
- Each reflected contract received independent mutation proof:
  `DurableAlertOptions.SectionName`, `MonitorDeliveryProducers.MyAtm`, and
  `MonitorDeliveryProducers.Svantek` were temporarily changed one at a time;
  the relevant focused test failed with the mutation as the runtime actual
  value. Every production mutation was explicitly reversed, both focused
  tests then passed 2/2, and no production source file remains modified.
- Seven dedicated `AssemblyInfo.cs` files now declare
  `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` for:
  `Rvt.Monitor.IntegrationTesting.Tests`,
  `Rvt.Communication.AbstractionsTests`,
  `Rvt.Communication.MicrosoftGraphMailTests`,
  `Rvt.Communication.SendGridMailTests`,
  `Rvt.Communication.TransmitSmsTests`, `Rvt.CommunicationTests`, and
  `Rvt.Storage.Tests`.
- Every affected assembly passed five consecutive whole-assembly runs:
  35/35 runs and 1,465/1,465 accumulated test executions. Per-run counts were
  Integration Testing 6, Communication Abstractions 20, Microsoft Graph 37,
  SendGrid 20, Transmit SMS 25, Communication 31, and Storage 154.
  No shared-resource collision occurred, so no assembly required
  `DoNotParallelize`.
- PostgreSQL repeat runs used command-scoped
  `RVT__POSTGRES_INTEGRATION_CONNECTION`. The helper generated a unique
  `rvt_integration_<guid>` schema per database test; a post-stress database
  query found zero remaining generated schemas. The connection variable was
  scoped to test commands only and is not active in the repository or shell.
- The complete shared-library solution passed 633/633, including the six real
  PostgreSQL integration tests and the 340 existing
  `Rvt.Monitor.CommonTests`. All nine root repository guards passed.
- Verification uses
  `/private/tmp/rvt-dotnet-10.0.302/dotnet`. The pre-change warnings log is
  `/private/tmp/rvt-mstest-contracts-before-20260727.log` with seven
  `MSTEST0001` and three `MSTEST0032`; the post-declaration log is
  `/private/tmp/rvt-mstest-parallel-build-20260727.log` with zero warnings and
  zero errors. Stress logs are under
  `/private/tmp/rvt-mstest-parallel-stress-20260727`.

## Monitor MSTest baseline repair - 2026-07-27

- Work is implemented on branch `codex/monitor-mstest-baseline`. The execution plan is
  `docs/superpowers/plans/2026-07-27-monitor-mstest-baseline.md`.
- The unsuppressed baseline was 258 diagnostics across AirQ, MyATM, Omnidots,
  and Svantek: `MSTEST0037` 177, `MSTEST0044` 53, `MSTEST0052` 10,
  `MSTEST0032` 9, `MSTEST0001` 4, `MSTEST0017` 4, and `MSTEST0030` 1.
- `apps/monitors/Directory.Build.props` no longer defines the test-project
  `NoWarn`. Its test-project `WarningsAsErrors` now contains all seven rule
  IDs. Evaluated MSBuild properties for every affected project contain all
  seven IDs; inherited `NoWarn` contains only unrelated `1701;1702`.
- The mechanical repairs use current MSTest APIs: `TestMethod` replaces
  warned `DataTestMethod` use, redundant `DynamicDataSourceType` arguments are
  gone, purpose-built assertions replace generic assertions, and four
  expected/actual pairs are corrected. Active legacy attributes in Svantek
  that its older analyzer surface did not report were harmonized as well.
- The nine `MSTEST0032` assertions now test meaningful value-type state:
  seven timestamps must differ from `default` and two identifiers must differ
  from `Guid.Empty`. This preserves the original non-null intent without an
  assertion that the compiler can prove true.
- Four new test assembly files declare `[assembly: DoNotParallelize]`:
  `airqmonitor/AirQMonitorTests/AssemblyInfo.cs`,
  `myatmmonitor/MyAtmMonitorTests/AssemblyInfo.cs`,
  `omnidotsmonitor/OmnidotsMonitorTests/AssemblyInfo.cs`, and
  `svantekmonitor/SvantekMonitorTests/AssemblyInfo.cs`. Sequential execution
  is deliberate because the suites repeatedly replace the process-global
  `RvtLogger` and legacy database classes share mutable class fixtures.
- The lone `MSTEST0030` source,
  `omnidotsmonitor/OmnidotsMonitorTests/TestRules.cs`, was an obsolete imported
  file: it retained the wrong `MyAtmMonitorTests` namespace and
  `TestMyAtmApi` logger. Restoring discovery added 12 tests and all 12 failed
  against the retired direct rule/MQTT path while the supported 392-test suite
  passed. The stale 809-line file was removed rather than ignored or
  suppressed; current peak-import and durable alert commit/outbox tests retain
  the supported coverage.
- Enforcement mutation proof temporarily restored one AirQ
  `[DataTestMethod]`; the ordinary project build failed with exactly one
  `MSTEST0044` error and zero warnings. The mutation was reverted, and the
  enforced monitor build then passed with zero warnings and zero errors.
- Each affected project passed twice against a disposable PostgreSQL database:
  AirQ 124/124, MyATM 208/208, Omnidots 392/392, and Svantek 136/136. That is
  8 green assembly runs and 1,720 accumulated executions.
- Final fixed-SDK verification passes:
  - monitor non-incremental Release build: 0 warnings, 0 errors;
  - root `Rvt.Mono.slnx` non-incremental Release build: 0 warnings, 0 errors;
  - all nine `tests/verify-*.test.sh` repository guards;
  - `git diff --check`.
- Verification used `/private/tmp/rvt-dotnet-10.0.302/dotnet`. Relevant logs
  are `/private/tmp/rvt-monitor-final-nonincremental-20260727.log`,
  `/private/tmp/rvt-root-build-monitor-mstest-20260727.log`, and
  `/private/tmp/rvt-monitor-{airq,myatm,omnidots,svantek}-repeat-20260727.log`.
- Test variables were command-scoped:
  `RVT__POSTGRES_INTEGRATION_CONNECTION`, database
  `rvt_mstest_monitor_20260727_a91c4e`, and role
  `rvt_mstest_runner_a91c4e`. The disposable database and role were removed
  after verification; no test credential or environment variable persists.
## Repository engineering standards foundation - 2026-07-27

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Isolated worktree:
  `.worktrees/repository-engineering-standards`; branch:
  `codex/repository-engineering-standards`; base: `main` at `5cc977c`.
- `docs/development/engineering-standards.md` is the normative repository-wide
  contract for every new or modified logical unit and for all R1-R11
  remediation analysis.
- `docs/superpowers/specs/2026-07-27-repository-engineering-standards-design.md`
  records the approved governance and enforcement design.
- Governance variables are `standardsPath`, `designPath`, `baselineMode`, and
  `complianceUnit`. Their defined values are the two paths above,
  `ratcheted`, and `logical unit` respectively. A logical unit is a type,
  function, React component, migration, SQL script, or configuration block.
- Stable rule prefixes are `GOV`, `ARC`, `NAM`, `CSH`, `WEB`, `DAT`, `ASY`,
  `ERR`, `NET`, `RES`, `OBS`, `CFG`, `TST`, `BLD`, `DOC`, and `REV`.
- New files comply completely. Modified logical units comply completely.
  Untouched legacy violations remain baselined, may not increase, and are
  removed phase by phase. Exceptions require an ID, owner, exact scope,
  justification, review date, removal condition, and validation.
- The architecture review now makes the standard a prerequisite for every
  remediation phase. Its completed standards-foundation item does not complete
  R9; R9 still owns root tooling, machine-readable baselines, and automated
  ratchet enforcement.
- The clean isolated baseline passed all nine root repository guard scripts.
  No production code, build configuration, analyzer configuration, or package
  policy changed while authoring the standards.
- The approved enforcement implementation plan is
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`.
  It scopes this execution to R9; R1-R8 and R10-R11 remain separate future
  remediation plans.
- Planned enforcement variables are `baselinePath`, `exceptionsPath`,
  `modulePolicyPath`, `changedRanges`, and `newPaths`. Their canonical paths are
  `eng/standards/baseline.json`, `eng/standards/exceptions.json`, and
  `eng/standards/module-policy.json`; `changedRanges` is a repository-relative
  path-to-inclusive-line-range map and `newPaths` is a repository-relative set.
- The plan enforces three layers: zero diagnostics for new files, zero
  non-excepted diagnostics on changed lines, and non-increasing whole-path
  counts. Full changed-logical-unit compliance remains a mandatory review record.
- Baseline creation uses the one-time `--initialize-baseline` mode. Subsequent
  `--update-baseline` runs can only preserve or reduce counts. ReportingMonitor
  retains its explicit xUnit override under the otherwise-MSTest monitor module.
- The plan has seven tasks: configuration hierarchy; diagnostic model; verifier;
  frontend policy; baseline/module policy; local/CI integration; and final R9
  documentation/evidence. Implementation has not started and requires selection
  of the plan execution workflow.

## Engineering standards enforcement execution preflight - 2026-07-27

- The user selected the Subagent-Driven execution option for
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`.
- The plan-specific ledger is
  `.superpowers/sdd/2026-07-27-repository-engineering-standards-enforcement/progress.md`.
- The user ruled that normative TST-002 and TST-003 govern the Task 1 conflict.
  The plan now preserves the same configuration requirements but proves them via
  evaluated `dotnet msbuild` properties, `dotnet format` EditorConfig behavior, and
  representative mutations. No implementation commit existed at the ruling point.
- Resume by dispatching the Task 1 implementer from the plan-specific ledger.

Next-session instruction: Read project_state.md to get up to speed

## Help Admin application-boundary integration - 2026-07-28

- Resume instruction: start a future session with
  `Read project_state.md to get up to speed`.
- Worktree: `/private/tmp/rvt-mono-help-admin`; branch:
  `codex/help-admin-application-boundary`; approved design base:
  `96fa359`.
- Implementation commits, in order:
  - `8092c25` `feat: define Help application contracts`
  - `c015d0b` `feat: add transactional Help use cases`
  - `f9f0387` `feat: add Help read adapter`
  - `cb68dba` `feat: add Help write adapter`
  - `1f26e37` `refactor: route Help through application ports`
  - `09fe9c1` `feat: complete Help Admin client workflow`
  - `0d55d34` `docs: approve Help Admin release`
- New application structure:
  - `apps/portal/RvtPortal.Application/Help/HelpApplicationService.cs`
  - `HelpAuthorizationPolicy.cs`, `HelpContracts.cs`,
    `HelpMutationValidator.cs`, and `IHelpApplicationService.cs`
  - `Ports/IHelpReadPort.cs` and `Ports/IHelpWritePort.cs`
  This boundary remains BCL-only and imports no ASP.NET Core, EF Core,
  persistence entity, data-access, or host namespaces.
- New host adapters:
  - `RvtPortal.Spa/Adapters/Help/EfHelpReadAdapter.cs`
  - `RvtPortal.Spa/Adapters/Help/EfHelpWriteAdapter.cs`
  The former host-side
  `Application/Help/HelpApplicationService.cs` and
  `Application/Help/HelpArticleCommands.cs` are removed.
- `IHelpApplicationService` exposes authorized published overview/detail and
  administrative overview/detail/create/update/publication/delete use cases.
  Every operation receives `PortalUserContext` and `CancellationToken`.
  Mutations return `UseCaseResult<T>`, execute through
  `IApplicationUnitOfWork`, save exactly once, and use
  `TimeProvider.GetUtcNow().UtcDateTime`.
- `IHelpReadPort` exposes published/admin queries and
  `GetMutationValidationDataAsync`. `IHelpWritePort` stages create, update,
  publication, and delete changes with supplied UTC timestamps. EF adapters
  never call `SaveChanges`; `DataAccessWriteBoundaryTests` enforces this.
- Application authorization allows published Help to RVT administrators and
  Company Users, and administrative Help only to RVT administrators. HTTP
  authorization independently preserves both `RVTAdmin` and
  `RVTMasterAdmin`, and denies Company User and Installer on every admin
  endpoint.
- Canonical HTTP creation is
  `POST /api/help/admin/articles`. The old `POST /api/help/articles` no longer
  resolves. Published `GET /api/help` and
  `GET /api/help/articles/{slug}` remain unchanged.
- Validation limits are section title/slug 120, article title/slug 160,
  summary 512, body 100,000, asset title 160, and asset URL 512 characters.
  Slugs use `^[a-z0-9]+(?:-[a-z0-9]+)*$`; section/article/asset orders must be
  nonnegative. Canonical content types are `FAQ`, `Article`, `Document`,
  `Video`, and `Definition`; asset types are `Document`, `Video`, and `Link`.
- Assets remain URL metadata only. Accepted URLs are absolute HTTPS URLs or
  root-relative `/help-assets/` paths. Protocol-relative, HTTP,
  JavaScript/data/file schemes, user-info, control/whitespace, backslash, and
  malformed values are rejected. The browser no longer supplies
  `InternalPath`; the write adapter derives it only for `/help-assets/`.
- Updates preserve persisted asset IDs, create server IDs for new assets,
  remove omitted assets, and reject duplicate, empty, unknown, or foreign IDs
  before staging. The React form uses persisted IDs plus a client-only random
  `clientKey`; API submission strips `clientKey` and retains persisted `id`.
- `HelpAdminPanel` restores focus by article ID or asset `clientKey` after
  create, update, publication, delete, add, and remove. Delete chooses the next
  article, then previous, then New FAQ; asset removal chooses next, previous,
  then Add Asset. Reverting to title/index row keys made the focused regression
  fail and was restored.
- Release evidence:
  - `apps/portal/docs/release/validate-help-asset-urls.sql` is read-only and
    release requires zero returned rows;
  - `CutoverReadinessTests` verifies canonical table selection, HTTPS and
    `/help-assets/` checks, backslash/control rejection, read-only behavior,
    and deterministic ordering; removing the HTTPS check made it fail;
  - this historical checkpoint marked Help Admin `READY` in the functionality
    matrix and R2 resolved in the architecture review. The authoritative
    checkpoint above corrects that claim: release remains conditional until
    every release database has a recorded zero-row execution;
  - rollback disables admin navigation/route and admin endpoints while
    preserving published Help and persisted content.
- Fresh verification:
  - standalone application tests: 48 passed;
  - full Portal SPA backend: 474 passed, 10 explicitly PostgreSQL-gated skips;
  - focused complete Help/architecture slice: 57 passed;
  - Release solution build: succeeded with 0 errors and the five existing
    `System.Security.Cryptography.Xml` 10.0.7 NU1903 advisories;
  - client tests: 9 files and 76 tests passed;
  - client lint: 0 errors and two pre-existing
    `DataViewPanels.tsx` Fast Refresh warnings;
  - client production build passed;
  - Playwright: 5/5 passed, including the complete Help Admin lifecycle and
    Company User denial;
  - cutover-readiness class: 14/14 passed;
  - all nine root layout, solution, PostgreSQL-only, source-boundary,
    documentation, engineering-configuration, and engineering-standards
    fixture guards passed outside the restricted named-pipe sandbox;
  - Portal client-release export dry run passed with 460 tracked files
    included and 9 internal/tooling paths excluded;
  - bundle inspection contains `/admin/help`,
    `/api/help/admin/articles`, and `Help/FAQ Management`; the only
    `/api/help/articles` occurrence is the retained public detail GET.
- The first no-restore Release build stopped only because the isolated
  worktree lacked `RvtPortal.Application.Tests/obj/project.assets.json`.
  A solution restore generated it, and the identical no-restore Release build
  then passed.
- Known external integration dependency: the committed-range verifier command
  `scripts/verify-engineering-standards.sh --base 96fa359 --head HEAD` cannot
  execute on this branch because `eng/standards` is not committed here. Those
  policy files remain untracked in the separate
  `codex/repository-engineering-standards` worktree. Do not copy or commit them
  on this Help branch; rerun the range verifier after that branch is integrated.
  The verifier's full fixture suite already passes.
- Final self-review of `96fa359..0d55d34` found no application-boundary,
  authorization, URL-policy, transaction, asset-identity, focus, API
  compatibility, public Help, report-guideline, release-evidence, or unrelated
  change finding.
## Repository engineering standards final-audit remediation - 2026-07-28

- Active branch: `codex/repository-engineering-standards`.
- Final Ready is pending independent re-review. The current remediation set is
  `33e8c60` (materialized-policy trust), `ea6fd74` (pull-request CI
  enforcement), and `ed8f123` (exception and documentation hardening). Do not
  claim the branch final Ready until the combined review passes.
- Final-audit finding 5 is remediated in `ed8f123`: `validateExceptions`
  rejects every symbol-scoped exception because R9 has no registered validator
  that both proves and applies symbol scope. Exact repository-relative path is
  the active exception mechanism. The unused `validateExactSymbol` and
  `validateRuleSpecificValidator` helpers were removed.
- Final-audit finding 7 is remediated in `ed8f123`: the documentation layout
  guard requires both `README.md` and `docs/index.md` to link the normative
  standard, enforcement guide, and enforcement report, and it verifies that
  each required target exists. The tracked enforcement report now records the
  complete GOV-001 logical units reviewed across R9.
- The two minor documentation findings are remediated in `ed8f123`: all
  genuinely completed Task 1–7 plan steps are checked, and the design rule
  catalog includes the missing `RES` prefix.
- Strict TDD evidence for symbol scope: the model suite first failed 1 of 26
  tests because a symbol record was still accepted/validator-gated; after the
  fail-closed narrowing it passed 26 of 26.
- Strict TDD and load-bearing documentation evidence: the initial guard
  accepted removal of the root standard link. The completed disposable
  86-entry fixture rejects removal of each of the six authority links and
  rejects moving the normative standard target. The real repository layout
  also passes.
- Files in `ed8f123`: `README.md`,
  `docs/development/engineering-standards-enforcement.md`,
  `docs/superpowers/plans/2026-07-27-repository-engineering-standards-enforcement.md`,
  `docs/superpowers/specs/2026-07-27-repository-engineering-standards-design.md`,
  `scripts/engineering-standards/model.mjs`,
  `scripts/verify-documentation-layout.sh`,
  `tests/engineering-standards-model.test.mjs`, and
  `tests/verify-documentation-layout.test.sh`.
- New documentation-guard variables are `entry_point_paths`,
  `entry_point_targets`, `entry_point_path`, `entry_point_target`, and
  `resolved_entry_point_target`. The mutation-fixture variables are
  `fixture_root`, `relative_path`, `needle`, `replacement`, `target`, and
  `original`. Model scope variables remain `hasPath` and `hasSymbol`.
- Fresh focused verification before `ed8f123`: shell syntax passed; model tests
  passed 26/26; the documentation regression and real layout guard passed; all
  six removal mutations and the moved-target mutation were rejected; and
  `git diff --check` passed.
- Next step: request independent review of the combined final-audit commits,
  then run the final branch verification before declaring Ready.

Next-session instruction: Read project_state.md to get up to speed

## Help Admin application-boundary design - 2026-07-28

- Branch: `codex/help-admin-application-boundary`.
- Design commit: `96fa359` (`docs: design Help Admin application boundary`).
- Approved scope: Help Admin ships as a supported capability; linked assets
  remain URL metadata and binary upload/storage is excluded.
- Specification:
  `docs/superpowers/specs/2026-07-28-help-admin-application-boundary-design.md`.
- Selected architecture moves the complete published/admin Help vertical slice
  into BCL-only `RvtPortal.Application.Help`, with application-owned read/write
  ports and EF adapters under `RvtPortal.Spa.Adapters.Help`.
- Mutations use `IApplicationUnitOfWork`, injected `TimeProvider`, immutable
  asset IDs, application-level admin authorization, and safe HTTPS or
  `/help-assets/` URL validation.
- The approved first-release API normalizes creation to
  `POST /api/help/admin/articles` and removes the unapproved legacy mutation
  route `POST /api/help/articles`.
- Client work includes stable asset row keys and deterministic focus after
  article/asset mutations. Release work changes Help Admin from `EXCLUDED` to
  `READY` only after application, adapter, API, client, browser, export, and
  persisted-URL checks pass.
- Production implementation has not started. The next required gate is user
  review of the committed written specification, followed by a detailed TDD
  implementation plan.
- Existing untracked engineering-standard artifacts and prior state-file
  changes remain preserved and unstaged.

Next-session instruction: Read project_state.md to get up to speed

## `/admin/help` release-surface analysis - 2026-07-28

- Analysis only; no production code was changed.
- The authoritative Portal readiness matrix marks Help Admin `EXCLUDED`, and
  the 2026-07-27 architecture review records R2 as excluding it from the
  production route/import surface unless shipment is explicitly approved.
- Runtime behavior contradicts that decision. `RvtPortal.Client/src/App.tsx`
  statically imports `HelpAdminPanel`, advertises `/admin/help` to RVT admins,
  resolves `admin-help`, and renders the editor. The focused `App.test.tsx`
  Help Admin test passes and explicitly proves the admin CRUD route.
- A fresh `npm run build` passed. The main production bundle contains
  `/admin/help`, `admin-help`, `Help/FAQ Management`, `Linked assets`, and
  `/api/help/admin`, proving the feature is compiled into the release artifact.
- `apps/portal/docs/release/client-release-exclusions.txt` does not exclude Help
  Admin. The release exporter copies tracked client source, while
  `RvtPortal.Spa.csproj` always builds and publishes the complete client.
- There is no Help release feature flag. The only Vite environment settings are
  API-origin and allowed-host configuration.
- The client route and all Help mutation endpoints require RVT admin roles, so
  no authorization bypass was found. The impact is instead that excluded and
  unsigned-off functionality can mutate persistent Help content in production.
- Public `/help` and report-rule guideline reads depend on the Help CMS model.
  Preserve those reads, entities, and tables; isolate the temporary editor and
  admin mutation surface rather than deleting the whole Help subsystem.
- Recommended decision: retain the documented exclusion. Remove the production
  navigation, route resolution, render path, and static import; decide explicitly
  whether production admin mutation endpoints must also be disabled. If internal
  development access remains, gate the route and endpoints together using one
  default-off release capability and add production-negative regression tests.
- If the product instead approves shipment, update the readiness decision only
  after browser CRUD, immutable asset row identity/focus, authorization, rollback,
  and production bundle validation are complete.

Next-session instruction: Read project_state.md to get up to speed

## Repository engineering standards Task 1 completion - 2026-07-27

- Resume instruction: start a future session with Read project_state.md to get up to speed.
- Worktree: /Users/oldgeorge/Developer/rvt-mono/.worktrees/repository-engineering-standards; branch codex/repository-engineering-standards. Task 1 implementation follows plan-correction base 170b4d8.
- Root .editorconfig owns the repository formatting, CSharp style, private-field naming, migration, and generated-source defaults. The nested monitor, Portal, and reporting EditorConfigs no longer terminate inheritance; all existing module rules remain, with Portal braces aligned from silent to suggestion.
- Root Directory.Build.props owns Nullable enable, ImplicitUsings enable, AnalysisLevel latest-recommended, Deterministic true, and CI ContinuousIntegrationBuild. RvtEngineeringStandardsMode defaults to Ratchet; Ratchet evaluates EnforceCodeStyleInBuild false and an explicit Strict mode evaluates it true.
- Module Directory.Build.props files under apps/monitors, apps/portal, libs/rvt-monitor-common, and services/reporting import the root immediately after Project. Former module EnforceCodeStyleInBuild true overrides were removed so Ratchet is authoritative. Monitors and shared common retain stronger AnalysisLevel latest.
- File structure added or modified by Task 1: root .editorconfig and Directory.Build.props; three nested EditorConfigs; four module Directory.Build.props files; executable tests/verify-engineering-configuration.test.sh.
- Guard variables are root_dir, temp_dir, nuget_config, representative_projects, representative_project, project_relative_path, expected_analysis_level, strict_project, editorconfig_root, editorconfig_modules, module_dir, msbuild_root, project, property, expected, actual, output, report, and status.
- The guard canonicalizes macOS TMPDIR with pwd -P, clears temporary NuGet sources, keeps package and HTTP caches under the temporary root, and cleans through an EXIT trap.
- Strict TDD for the approved mode change: the amended guard failed because AirQMonitor evaluated RvtEngineeringStandardsMode as empty instead of Ratchet. After the minimal root conditions and removal of three module overrides, the focused guard passed.
- Evaluated behavior: four representative module projects expose the root properties and default Ratchet plus EnforceCodeStyleInBuild false. A property-only Portal evaluation with RvtEngineeringStandardsMode Strict exposes Strict plus EnforceCodeStyleInBuild true without compiling legacy source.
- EditorConfig behavior: dotnet format style observes a root-only IDE1006 under copied monitor, Portal, and reporting configs. Inserting a nested root terminator removes that diagnostic in each mutation. Removing a copied monitor root import removes evaluated Nullable.
- Verification: all 10 root guard scripts pass; root restore passes with five existing System.Security.Cryptography.Xml 10.0.7 NU1903 advisories and no lock drift; default Ratchet root build passes with 73 warnings and 0 errors, three below the recorded 76-warning baseline.
- No full Strict solution build was run. Strict becomes the default only after later changed-scope baseline work reaches zero and a strict root build passes. No Portal severity was lowered and no suppression was added.
- Codegraph status and context were retried before edits and failed with unable to open database file, so focused inspection was used.
- Full TDD, mutation, guard, restore, build, warning, file, and self-review evidence is in .superpowers/sdd/2026-07-27-repository-engineering-standards-enforcement/task-1-report.md.

## Historical/superseded active state - Help Admin - 2026-07-28

- Historical evidence only: the top checkpoint supersedes this section's
  current-state and release-gate language.
- Help Admin is integrated into `origin/main`; application-boundary merge
  commit `a4b8749` contains current `main`, the Help feature history, the final
  review corrections, and the standards-clean Svantek worktree-path repair.
- The published feature ref is
  `origin/codex/help-admin-application-boundary`. Pull request
  `chris-oldgeorge/rvt-mono#4` is
  `https://github.com/chris-oldgeorge/rvt-mono/pull/4`.
- Final integration verification on the merged tree:
  - exact committed-range engineering-standards verification passed;
  - the complete aggregate .NET suite passed against a fully migrated,
    post-load-deployed disposable TimescaleDB database, including 485 Portal,
    392 Omnidots, 208 MyATM, 136 Svantek, 124 AirQ, 93 ReportingMonitor, and
    all common/reporting/application test cases;
  - client lint completed with zero errors and the two established Fast
    Refresh warnings;
  - all 78 client unit tests passed;
  - the client production build passed;
  - all five Playwright scenarios passed.
- The disposable database and role `rvt_help_merge_9dfe74d` were removed after
  verification.
- R2 and Help Admin release are `CONDITIONAL`. No production/release database
  was accessed during this correction, and no receipt records execution of
  `apps/portal/docs/release/validate-help-asset-urls.sql` against every release
  database. The historical SQL-only receipt gate is superseded and is not
  sufficient release authority; use the shared-policy/.NET-audit direction in
  the top checkpoint.
- The host-owned feature worktree remains at
  `/private/tmp/rvt-mono-help-admin`.
- The local main checkout at `/Users/oldgeorge/Developer/rvt-mono` has been
  reconciled and fast-forwarded. Its retained solution change and validation
  are recorded in the top checkpoint.

Next-session instruction: Read project_state.md to get up to speed
