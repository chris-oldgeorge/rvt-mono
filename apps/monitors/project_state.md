# Project State

## RVT Common Package-Only Migration Merged - 2026-07-18

- Personal repository PR [chris-oldgeorge/rvt-monitors#5](https://github.com/chris-oldgeorge/rvt-monitors/pull/5) merged `codex/rvt-common-private-nuget-migration` into `main` with merge commit `0c73e81225fbf8f33be6274ae36ff94d0674eaee` on 2026-07-18. The final feature head is `50bf9914f2dc7ebaf4b8ecdbcfe1563d6174c103`; the migration checkpoint history was preserved with a merge commit rather than squashed.
- All active monitor consumers now use exact `0.2.0-rc.1` private packages from `RVT-Group-LTD/rvt-reporting`: runtime `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure`, plus test-only `Rvt.Monitor.IntegrationTesting` only where required. The retired six-project local `rvt-monitor-common/` source tree, solution entries, `ProjectReference` paths, and local-source switches are removed.
- The personal repository's trusted GitHub Actions gate authenticates only through repository secrets `RVT_PACKAGES_READ_USER` and `RVT_PACKAGES_READ_TOKEN`; the token value is not tracked. Untrusted pull-request policy remains credential-free. Verification scripts use runner-portable `find`, `grep`, and `/tmp` fallback behavior.
- Final GitHub Actions run [29634111370](https://github.com/chris-oldgeorge/rvt-monitors/actions/runs/29634111370) passed both jobs on `50bf991`: `untrusted-policy` in 31 seconds and `trusted-package-gate` in 5 minutes 31 seconds. The trusted gate passed locked private-package restore, formatter, build, all 929 solution tests, package/source policy, Compose validation, all five image builds, exact runtime package inventory, curated export, and retired-source exclusion.
- The native clone fetched remote `main` at `0c73e81`, but its local `main` remains intentionally unreconciled because it already contains unpushed state commit `1ed3da9` and additional portal-state edits in this file. Those local changes were preserved; do not reset or overwrite them when reconciling local `main` with `origin/main`.
- No deployment, database migration, or package release promotion was performed by this merge. Stable `0.2.0` promotion and portal adoption remain separate work.

## Superseding Current State: Package-Only RVT Common Boundary - 2026-07-18

- This section supersedes the earlier retained-source rollback-window status while preserving all historical sections below. The active branch is `codex/rvt-common-private-nuget-migration`; shared source authority is the private `RVT-Group-LTD/rvt-reporting` repository at `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
- Monitor consumers remain exactly pinned to immutable `0.2.0-rc.1` for `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and test-only `Rvt.Monitor.IntegrationTesting`. Their central bindings are singleton ranges. NuGet 10 force-evaluate canonically records the 18 direct requests as closed equal-bound `[0.2.0-rc.1, 0.2.0-rc.1]`; locked restore is stable and accepted lock hashes remain unchanged from the RC evidence.
- Source-removal commit `51d680311c63327e2580723860f37097c5d8ea25` deleted the six-project `rvt-monitor-common/` rollback tree. Package-boundary hardening is `6c409f8`; active-document, CI, export, and container-policy completion is `06c6aa6d26f284beefe30ab4517dcecd35f19e84`; final-review hardening is `2274bc73d8f1b6ef0609262b0b18cb997635df57`; solution test-discovery correction is `b5639963e7dc89b191f39b5ed588d8f7e93f5921`; exact guard hardening is `1af6012`. The checkout has no local Common project, `ProjectReference`, or source-switch fallback.
- The root solution has exactly 14 consumer projects. Each AirQ, MyATM, Omnidots, and Svantek vendor solution contains only its app and test project. Architecture tests enforce the approved direct package matrix, exact synchronized central bindings/lock singleton semantics, unrelated-project exclusion, active-document boundary, semantic local project identities/references, trusted CI isolation/action pins, durable migration retrieval, `PrivateAssets="all"` for IntegrationTesting in all five test projects, and exactly one unconditional top-level project declaration of `IsTestProject=true` in every active consumer test project. Conditional groups/properties, Target-scoped declarations, duplicates, and false overrides are rejected.
- Package-consumer CI gives every pull request a credential-free policy job and restricts the credentialed full gate to trusted pushes or same-repository pull requests. Both official actions are SHA-pinned and both checkouts use `persist-credentials: false`; package credentials occur only on restore and Docker build steps. MyATM migration recovery now retrieves the two scripts from exact authoritative commit `f00d5b8a320945ed08e248da8641ca0c3f7e3b82` with verified SHA-256 values rather than an expiring workflow artifact.
- The first personal-mirror trusted gate failed locked restore with HTTP 403 for all three `RVT-Group-LTD` packages because `chris-oldgeorge/rvt-monitors` cannot use its repository `GITHUB_TOKEN` to read organization-owned packages. On 2026-07-18 the repository received dedicated `RVT_PACKAGES_READ_USER` and `RVT_PACKAGES_READ_TOKEN` Actions secrets; the trusted restore and Docker build steps now use only those secrets. The credential-free policy verifier no longer assumes `rg` is installed on `ubuntu-latest`; it uses standard `find` and `grep`, and architecture tests guard both corrections.
- The next trusted gate proved the new credential path by completing locked restore and build, then exposed one Linux-only Omnidots test path mismatch: the tracked directory is `api/http`, while the source-policy test requested `api/Http`, which the case-insensitive macOS workspace had accepted. The test now names the tracked lowercase path; its exact focused test passes after an isolated authenticated restore. PR #5's final head must rerun the complete trusted gate before merge.
- After the Omnidots correction, the trusted gate passed locked restore, formatter, build, all 929 solution tests, package-policy verification, Compose validation, and all five image builds. Its final inventory step then exposed a second macOS-only assumption: `scripts/report-rvt-package-inventory.sh` fell back to `/private/tmp` when `TMPDIR` was unset on Linux. The fallback is now portable `/tmp`, with an architecture regression guard; the complete gate must rerun before merge.
- Independent finishing verification found that the .NET 10 solution test target silently skipped AirQ, Svantek, and Reporting under CI's exact `dotnet test rvt-monitors.sln --no-build --nologo` command because those project files had an empty evaluated `IsTestProject`. The focused architecture guard first failed with exactly those three paths; the explicit-property correction passed the same guard and ensures CI's root command now exercises all five suites. Reviewer follow-up then proved the first descendant predicate accepted conditional, Target-scoped, and true-then-false fixtures; the exact structural guard rejected all three after failing RED first.
- The refreshed reviewer gate used a disposable PostgreSQL 17 fixture. Formatter verification passed; the serial `-m:1` build passed in 3.48 seconds with 0 warnings/errors. Serial build is an environment-specific workaround for the shared desktop's previously stalled default-parallel build, not a source change.
- The exact unchanged root test command passed 929/929 tests with zero failed/skipped across all five consumer suites: AirQ 118, MyATM 213, Omnidots 399, Svantek 124, Reporting 75. The three-test increase over 926 is exactly the three executed data rows in the new reviewer regression.
- Compose configuration, package policy, all five container builds, runtime package inventory, active-document/solution scans, curated export, image credential scans, and diff checks passed. The export copied 593 tracked files plus its manifest and retained `NuGet.config` and `Directory.Packages.props` without the Common tree or blocked secret-bearing file types.
- Runtime inventory reads the exact expected central version and reports Common/Infrastructure `0.2.0-rc.1` with no IntegrationTesting in all five images: AirQ `cbaf22459cb3e57dc7d381973c62b51fd0cc1a10c34a8b3110eac477cb970ddd`, MyATM `ed4fc75e7790b3ecbd3feaa22d0c42670e9a16ff1935a27d198c36d2fb91af85`, Omnidots `365b15aa0ab94347c155f914baf87997348a876b16ce970affccca82067cb04e`, Svantek `abd057d72d235232ef9f3d76830f4c68d669380f697d420688a8e55579503a6f`, Reporting `7d9048bb18fe7b46bd34f1c0a42134a45d62eedf844b1eb9ab4d3bdb35bdb19e`.
- The approved source-removal design intentionally accepts deletion before stable promotion. `0.2.0-rc.1` remains a prerelease limitation; portal validation and stable `0.2.0` promotion remain separate future work. The recorded source-reference rollback commit is `924ed20deee37fba17452612ae40eae8e0fe6168`.
- Final evidence is `docs/superpowers/evidence/2026-07-17-rvt-common-monitor-source-removal.md`. The branch is published to `chris-oldgeorge/rvt-monitors` in PR #5 (`codex/rvt-common-private-nuget-migration` -> `main`) at `https://github.com/chris-oldgeorge/rvt-monitors/pull/5`. Merge into `main` was authorized on 2026-07-18 and must preserve the migration history with a merge commit after both package-consumer CI jobs pass. If this state commit is present on `main`, that CI-gated PR merge has completed; no deployment, database migration, or package release promotion is implied.

## Personal RVT Portal Mirror Refactor Application - 2026-07-17

- The confirmed personal mirror is the private repository `chris-oldgeorge/rvtportal-spa-alpha` (the originally supplied `chris-oldgeorge/rvt-spa-alpha` name does not exist). Its default branch is `master`. The mirror does not contain the organization repository's exact baseline commit history, so the refactor gate was compatibility-ported rather than blindly cherry-picked.
- Branch `codex/rvt-common-refactor` was created from exact mirror baseline `7c0564b1e688366899c1cba80434028f31d559fd`. Atomic mirror commit `0be874c7dee4b5eda8b134d12b4c062a77476ec8`, `build: port RVT common refactor gate to personal mirror`, maps source implementation orders `10,20,30,40,50,70` from `RVT-Group-LTD/RVT-Cloud@9a0b3c046a461516c8a0f27a8781b0c01ff0d2e2`. Its trailers are `Mirror-Apply: recommended` and `Mirror-Source-Orders: 10,20,30,40,50,70`.
- Draft PR [chris-oldgeorge/rvtportal-spa-alpha#41](https://github.com/chris-oldgeorge/rvtportal-spa-alpha/pull/41), `Port the RVT common refactor gate to the personal mirror`, is open, mergeable, unmerged, and targets `master` from `codex/rvt-common-refactor`. It contains one commit and 15 changed files. It was not marked ready and was not merged.
- Final-head GitHub Actions run [Verify #125](https://github.com/chris-oldgeorge/rvtportal-spa-alpha/actions/runs/29571386684) completed successfully. The mandatory [verify job](https://github.com/chris-oldgeorge/rvtportal-spa-alpha/actions/runs/29571386684/job/87855740613) passed with a Release build at 0 warnings/0 errors; 311 backend tests passed with 3 known environmental PostgreSQL skips (314 total); ESLint, 7 frontend test files/66 tests, and the production build passed; and the pinned unprivileged image returned exact health `ok`, served the SPA fallback, and reported runtime user `101`. The [analyze job](https://github.com/chris-oldgeorge/rvtportal-spa-alpha/actions/runs/29571386684/job/87856528723) was skipped at job scope with no steps for the pull-request event.
- The port adds root-layout and zero-package boundary guards, warning-free generated-regex/scalar theory-data fixes, backend/frontend verification scripts, the pinned unprivileged frontend image, and mandatory Linux CI. Optional Sonar analysis is restricted at whole-job scope to trusted pushes on exact ref `refs/heads/master` and pins `dotnet-sonarscanner` `11.2.1`; pull-request code cannot enter the analysis job.
- Mirror-specific evidence is `docs/release/rvt-common-refactor-mirror-application-2026-07-17.md` on the branch. The mirror's newer `docs/database/database-constraint-index-name-registry.csv` was deliberately preserved instead of overwriting it with the older organization snapshot. Existing EF contexts, migrations, providers, and the public-only NuGet path were not changed; no private feed, credential, package permission, SQL, migration, live database, provider call, or deployment was used.
- Local `gh` authentication remains invalid, HTTPS Git sees the private repository as not found, and no authorized SSH key is available. The connected GitHub integration provided admin/push access and was used to create the branch, atomic Git tree/commit, and draft PR. There is no local clone of the private mirror in the workspace. Future local CLI work requires `gh auth login -h github.com` or an authorized Git credential/SSH key.
- A signed-in attempt to grant `chris-oldgeorge/rvtportal-spa-alpha` Read access to the private organization-owned `Rvt.Monitor.Common` package made no mutation. The Manage Actions access picker only offered eligible `RVT-Group-LTD` repositories; filtering for the exact personal repository returned `No repositories found`, and the add button remained disabled. Current access remains `RVT-Group-LTD/rvt-reporting` Admin and `RVT-Group-LTD/rvt-monitors` Read. Repository-level Actions access therefore requires an eligible repository under `RVT-Group-LTD`; the personal-repository workflow uses the separately managed credential recorded below.
- A classic personal access token named `RVT portal package read` was created with only the `read:packages` scope and a 90-day expiration of 2026-10-15. Its value was stored as the encrypted Actions repository secret `RVT_PACKAGES_READ_TOKEN` in `chris-oldgeorge/rvtportal-spa-alpha`; GitHub confirmed `Repository secret added`, and the secret list showed the name updated on 2026-07-17. The token value was never written to this workspace or recorded in this file and was cleared from the browser automation session after submission. No SSO authorization control was offered for the new token. No NuGet source, workflow, package reference, restore, or code change was added, so the mirror remains intentionally zero-package until a later consumer change explicitly wires this secret into authenticated package restore.

## RVT-Cloud Refactor Zero-Package Replacement PR - 2026-07-17

- The refactor-native zero-package consumer work is published from branch `codex/rvt-common-package-consumer-refactor` at final head `9a0b3c046a461516c8a0f27a8781b0c01ff0d2e2`, exactly eleven commits after baseline `f2582bcf7d2b17ae69a40cc32e47b6d2e1685eea` on `rvt-portal-refactor`. The RVT-Cloud worktree is `/Users/oldgeorge/Documents/rvt-monitors/RVT-Cloud/.worktrees/rvt-common-package-consumer-refactor`; the unrelated untracked `.github/workflows/build 2.yml` remains preserved and excluded.
- Replacement draft PR [RVT-Group-LTD/RVT-Cloud#2](https://github.com/RVT-Group-LTD/RVT-Cloud/pull/2), `Verify the portal refactor zero-package boundary`, is open and remains draft. Its base is exactly `rvt-portal-refactor` at `f2582bcf7d2b17ae69a40cc32e47b6d2e1685eea`, its head is exactly `codex/rvt-common-package-consumer-refactor` at `9a0b3c046a461516c8a0f27a8781b0c01ff0d2e2`, and GitHub reported it mergeable with 11 commits, 19 changed files, 2,027 additions, and 115 deletions. It was not marked ready and was not merged.
- Final-head GitHub Actions run [Verify #3](https://github.com/RVT-Group-LTD/RVT-Cloud/actions/runs/29569359206) completed successfully. The mandatory [verify job](https://github.com/RVT-Group-LTD/RVT-Cloud/actions/runs/29569359206/job/87849248127) passed checkout, .NET/Node setup, backend verification, frontend verification, and the hardened frontend image build/smoke test. The [analyze job](https://github.com/RVT-Group-LTD/RVT-Cloud/actions/runs/29569359206/job/87850002929) concluded `skipped` with no steps for the pull-request event, confirming that the whole job was not admitted and no `SONAR_TOKEN` detection or consumption occurred.
- Former draft PR [#1](https://github.com/RVT-Group-LTD/RVT-Cloud/pull/1), which targeted `main` from `codex/rvt-common-package-consumer`, was commented with the replacement URL and closed unmerged only after PR #2's mandatory verification passed. Its histories/layouts are unrelated (`backend/` plus pnpm on `main` versus root solution plus npm on the refactor line), and its branch was intentionally retained.
- Local acceptance is green: backend Release build 0 warnings/0 errors; backend tests 311 passed, 0 failed, 3 known environmental PostgreSQL skips, 314 total; frontend frozen install, ESLint, all 66 tests, and the Vite production build passed; the pinned unprivileged frontend image passed `/healthz`, SPA fallback, and runtime-user `101`; and `git diff --check` passed. No SQL, migration, live database, provider call, private restore, or deployment was performed.
- RVT-Cloud remains a zero-package non-consumer of `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting`, including `0.2.0-rc.1`. Root `NuGet.config` permits only public nuget.org; there is no common-package reference, private NuGet feed, package credential, or package permission in RVT-Cloud. Signed-in package settings verification found all three private packages linked to `RVT-Group-LTD/rvt-reporting`; `rvt-reporting` retains Admin, `rvt-monitors` retains Read, and RVT-Cloud is absent from all three access lists. No package-access mutation occurred during the replacement-PR task.
- Order `70` hardens CI and container smoke verification. The complete `analyze` job has `needs: verify` plus the trusted-push-only condition `github.event_name == 'push' && github.ref == 'refs/heads/rvt-portal-refactor'`, so pull-request code cannot enter any job that detects or consumes `SONAR_TOKEN`. Trusted pushes install `dotnet-sonarscanner` at exact version `11.2.1`; all third-party action references remain commit-pinned and the workflow retains only `contents: read` permission.
- The inherited `dotnet format RvtPortal.Spa.sln --verify-no-changes` audit remains intentionally outside CI and is non-binding for this scoped change. Under SDK/dotnet-format 10.0.203 it exits 2 on pre-existing repository-wide `CHARSET`, `IMPORTS`, and `IDE1006` findings; the audit made no changes and no unrelated mass-format cleanup was included.
- Personal-mirror handoff is recorded in `docs/release/rvt-common-refactor-mirror-change-manifest.md`. The compatible replay range is `f2582bcf7d2b17ae69a40cc32e47b6d2e1685eea..9a0b3c046a461516c8a0f27a8781b0c01ff0d2e2`; marked orders are `00`, `01`, `10`, `20`, `30`, `40`, `50`, `51`, `60`, `70`, and owning refresh order `71`. The manifest's full command includes every marked commit through trusted CI/container hardening at order `70`; its dependency-closed minimal series remains exactly `01, 10, 20, 30`. Order `71` owns the refreshed generated manifest and is intentionally excluded from its own hash table and full command. Full replay verification includes backend, frontend, container smoke, and diff checks; the exact minimal command was replayed from the baseline in a disposable clone with clean cherry-picks, empty status, and both diff checks passing.
- Durable source evidence remains in `docs/release/rvt-common-non-consumer-acceptance-2026-07-17.md`, with the approved design and plan under `docs/superpowers/`. The next action is review/merge of draft PR #2 into `rvt-portal-refactor` only when separately authorized; do not retarget it to `main`, mark it ready, merge it, or delete either retained branch as part of this handoff.

## RVT-Cloud Zero-Package Consumer Gate - Paused 2026-07-17

- Active portal worktree: `/Users/oldgeorge/Documents/rvt-monitors/RVT-Cloud/.worktrees/rvt-common-package-consumer`; branch `codex/rvt-common-package-consumer`; clean HEAD `c70efb6ace4b56aaa5881da4452f4fc9ee188331` (`docs: clarify portal acceptance evidence`). The portal rollback baseline remains `64af3f0aea873a8e0d23113018ae114772573fee`.
- Design: `docs/superpowers/specs/2026-07-16-rvt-common-package-consumer-design.md`. Plan: `docs/superpowers/plans/2026-07-16-rvt-common-package-consumer.md`. Durable execution ledger: `.superpowers/sdd/progress.md`. Task 7 diagnostic report: `.superpowers/sdd/task-7-report.md`.
- Tasks 1-6 are implemented and independently approved. Key commits after the design/plan are `678d19e` (zero-package scanner), `6368871` (schema inputs/casing), `c849080` (layout tests and authoritative performance indexes), `4e1bf19` (pnpm 10/ESLint), `6b5d191` (unprivileged pinned frontend image), and `c13d3e8` (current CI). Task 7 has no commit; README and the final acceptance document remain unchanged.
- Current portal acceptance is green: backend build 0 warnings/0 errors; 311 passed, 0 failed, 3 explicitly environmental PostgreSQL tests skipped; frontend frozen install/lint/build passed with pnpm 10.0.0; formatter passed; pinned frontend Docker build passed; runtime user is `101`; zero-package boundary tests passed 4/4; all eight recovered SQL assets compare byte-for-byte with `f2582bcf7d2b17ae69a40cc32e47b6d2e1685eea`.
- Observed portal artifact values: backend DLL SHA-256 `1d8b0859fdfd1ca06b39994fccb46af70cf5299e427f1b5388f3d394c1150029`; frontend `index.html` SHA-256 `d5391dbf3b9f0e9e67858c9110573e1b961944e03a4feec7945c3c357147af89`; local frontend image ID/RepoDigest `sha256:e48d8e0dce338a28ea2c7ccdba2692500a01d0193eb370c15f657425390c82aa`. The image was not pushed.
- Package-access cleanup is paused before mutation. GitHub identity is `chris-oldgeorge`; portal repository ID is `1178718947`. All three private packages are readable and linked to `RVT-Group-LTD/rvt-reporting`, but Manage Actions grants cannot currently be enumerated: the undocumented repository-list probe returns 404, no authenticated package-settings browser surface was available, and the active token scopes are `gist`, `read:org`, `read:packages`, `repo`, and `workflow` (no `write:packages`). The device authorization attempt expired. No DELETE was attempted; no package visibility, version, link, RVT-Cloud grant, or `rvt-monitors` grant changed.
- Resume Task 7 by obtaining a supported authenticated package-admin surface, verifying RVT-Cloud and `rvt-monitors` access before mutation, removing only repository ID `1178718947` from the three named packages where present, and verifying RVT-Cloud absent while `rvt-monitors` remains. Then write/commit README plus `docs/release/rvt-common-non-consumer-acceptance-2026-07-16.md`, run the Task 7 review, execute Task 8 handoff/project-state work, perform the final whole-branch review, and use the finishing-development-branch workflow.
- No live database migration, private/common-package restore, external provider call, package access mutation, push, or pull request occurred in the paused Task 7 run. Public NuGet restore did occur. The native monitor checkout remains on `main`; its pre-existing untracked communications/outbox/webhook files were preserved and must not be discarded.

## RVT Common Private NuGet Migration Execution - 2026-07-16

- The authoritative private repository is now `RVT-Group-LTD/rvt-reporting`; the solution,
  package IDs, and namespaces intentionally retain the `rvt-common` / `Rvt.Monitor.*`
  compatibility names. Common implementation used
  `/private/tmp/rvt-reporting-private-nuget` on
  `codex/rvt-reporting-private-nuget`; monitor plan/consumer work uses
  `.worktrees/rvt-common-private-nuget-migration` on
  `codex/rvt-common-private-nuget-migration`.
- Tasks 1-11 of
  `docs/superpowers/plans/2026-07-16-rvt-common-private-nuget-migration.md` are complete.
  The extracted repository passed 425 common, 64 infrastructure, and 6 integration-testing
  tests; package validation passed 8/8; both package-only consumers, exact 56-dependency
  SPDX graph validation, official SBOM validation, and nine flat checksums passed.
- GitHub PR `RVT-Group-LTD/rvt-reporting#1` merged to protected `main` as
  `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`. `main` requires pull requests, the
  `build-test-pack` check, one CODEOWNER/last-push approval, resolved conversations, and
  forbids force pushes/deletion. The documented bootstrap administrator has the temporary
  review bypass. Ruleset `19044494` protects stable `v*.*.*` tag creation/update/deletion.
- Private packages `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and
  `Rvt.Monitor.IntegrationTesting` were published together at immutable version
  `0.2.0-rc.1` by release run `29496427667`. Duplicate run `29496593683` failed at the
  availability preflight before build or push. All three packages are private, linked to
  `RVT-Group-LTD/rvt-reporting`, and grant `RVT-Group-LTD/rvt-monitors` read-level GitHub
  Actions access.
- A clean `/private/tmp/rvt-common-rc-consumer` restored
  `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure` at exactly `0.2.0-rc.1`
  and built with zero warnings/errors. Authentication used only the process variable
  `NuGetPackageSourceCredentials_rvt` with username, token, and
  `ValidAuthenticationTypes=Basic`; no credential or package-source configuration was
  written into that consumer or either repository.
- Monitor package consumption is complete through commit `98a17ca` on
  `codex/rvt-common-private-nuget-migration`. Root `NuGet.config` maps `Rvt.Monitor.*`
  only to GitHub Packages; `Directory.Packages.props` pins all three packages to exact
  `0.2.0-rc.1`; 14 consumer lock files use hashes from the published artifacts. Twelve
  consumer projects no longer reference `rvt-monitor-common`; the six local common
  projects remain in the solution only as the tested rollback checkpoint until stable
  adoption authorizes Task 14 deletion.
- Five production Dockerfiles use Dockerfile frontend `1.10` and the BuildKit secret
  `nuget_credentials`, exposed only during publish as
  `NuGetPackageSourceCredentials_rvt`. Compose, CI, curated release exports, and the
  container policy verifier preserve `NuGet.config` and `Directory.Packages.props`
  without persisting credentials. All five images built and their history/runtime
  environments were clean.
- Monitor RC evidence is
  `docs/superpowers/evidence/2026-07-16-rvt-common-monitor-rc-validation.md`.
  PostgreSQL-backed verification passed 1,410/1,410 tests with zero failed/skipped;
  the full build had zero warnings/errors; all five images contain Common and
  Infrastructure `0.2.0-rc.1` and exclude IntegrationTesting; API, readiness where
  exposed, provider-disabled communications, Quartz-with-disabled-triggers, safe
  one-shot rejection, and source-reference rollback checks passed. Disposable
  containers, configuration, and rollback worktree were removed.
- Next work is Task 12. Portal source is still absent locally. A read-only organization
  inventory shows `RVT-Group-LTD/RVT-Cloud` as the sole portal-shaped candidate, but the
  plan forbids inferring identity. Obtain explicit user confirmation of that exact
  repository before cloning it, granting package access, or creating the portal-specific
  design, plan, implementation, and staging evidence. Stable `0.2.0` promotion and local
  common-source deletion remain blocked on accepted portal evidence.

## RVT Common Private NuGet Design Refresh and Implementation Plan - 2026-07-16

- The approved package strategy was refreshed and committed as `8739750` in `docs/superpowers/specs/2026-07-15-rvt-common-package-strategy-design.md`. The initial compatibility release now explicitly contains three synchronized packages: `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and test-only `Rvt.Monitor.IntegrationTesting`.
- The detailed implementation plan is `docs/superpowers/plans/2026-07-16-rvt-common-private-nuget-migration.md`. It contains 14 reviewable tasks covering the frozen extraction baseline, history-preserving private repository creation, deterministic/central package configuration, package-only smoke consumers, CI, immutable RC/stable releases, migration assets/checksums/SBOM, repository governance, monitor feed and reference conversion, BuildKit-secret Docker restores, staging and rollback evidence, the separate portal migration gate, stable promotion, source removal, and final architecture enforcement.
- The extraction baseline named by the plan is monitor commit `8739750`; execution must stop and refresh the baseline if `rvt-monitor-common/` changes after that commit. The user retargeted the authoritative repository to the existing private, empty `RVT-Group-LTD/rvt-reporting`; the solution, namespaces, and package IDs remain `rvt-common.sln` and `Rvt.Monitor.*`. The repository becomes authoritative only after the extraction task is executed and verified.
- Portal source is not present in this workspace. The plan requires its exact repository and usage inventory, then a portal-specific approved spec/plan and staging evidence before `0.2.0` promotion.
- No repository extraction, GitHub repository creation, package publication, consumer reference conversion, credential change, container build change, source removal, or deployment was performed while updating the design and writing the plan. GitHub authentication and organization/repository/package permissions remain execution prerequisites; no token or connection value is tracked.
- Execution is active on monitor branch `codex/rvt-common-private-nuget-migration` and common branch `codex/rvt-reporting-private-nuget`. Task 1 baseline evidence is approved at monitor commits `7b54756` and `fc67874`; the repository-only retarget amendment is `79a3f2f`. Task 2 pushed the history-preserving six-project extraction to private `RVT-Group-LTD/rvt-reporting` at `b73e8e7`, and the clean native clone is `/Users/oldgeorge/Documents/rvt-monitors/rvt-reporting`. Task 3 package metadata/central versions/lock files are approved at `08389cd`; no package has been published.
- The Task 3 extra suite exposed nine extraction-layout failures. Systematic debugging proved three families: the standalone repository lost the ignored integration-settings copy/rename rule, two migration tests retain the retired `rvt-monitor-common/database` prefix although both artifacts exist under `database/`, and four communications guards still assume the monitor solution and consumer directories. The design and plan now include Task 3A to repair the three common-owned files and Task 8 to relocate Reporting/MyATM/Omnidots guards before old source removal. No credential or connection value is tracked or printed.
- Task 3A is approved at common commit `0de1cf4` with 495/495 extracted tests passing. Initial Task 4 commit `a4744f5` produced exactly three package/symbol pairs and green package-only consumers, but review correctly rejected open NuGet ranges, incomplete archive assertions, a compile-time-constant smoke assertion, and four new MSTest warning sites. The plan now requires exact bracketed consumer ranges, locked default validation restores, an exact generated Infrastructure-to-Common dependency range, complete `lib/**/*.dll` and symbol-package sets, runtime assembly loading, and warning-free MSTest declarations before Task 4 can pass re-review.
- Task 4 is approved after fix commit `44f1936`: the Infrastructure nuspec pins exact Common `[0.2.0-rc.1]`, consumer locks contain normalized singleton ranges and pass locked restore, package/symbol and `lib/**/*.dll` sets are complete, package-only runtime/test consumers pass without `ProjectReference`, and Task 4 adds zero warnings. The remaining 63 MSTest analyzer warnings are inherited from extracted tests. Same-version iterative repacking left a stale local global-cache copy; isolated-cache proof passed, and published versions remain immutable.

## MyATM Omnidots-Alignment Remediation - 2026-07-16

- Implementation was completed on `codex/myatm-omnidots-alignment-remediation` and merged into `main`. The approved design and test-first plan are in `docs/superpowers/specs/2026-07-16-myatm-omnidots-alignment-remediation-design.md` and `docs/superpowers/plans/2026-07-16-myatm-omnidots-alignment-remediation.md`.
- MyATM now normalizes every vendor/database instant through tick-preserving UTC semantics: UTC remains unchanged, genuine local values convert once, and provider `DateTimeKind.Unspecified` values are marked UTC without shifting ticks. Site-hours-aware offline evaluation uses the active deployment's timezone and weekday/weekend schedule, including overnight intervals and DST-safe elapsed durations.
- `MyAtmMonitor` and `MyAtmVendor` are startup-validated option sections. Vendor variables include `BaseUrl`, `ApiKey`, `MaxResponseBytes`, `MaximumAttempts`, `MinimumRequestIntervalMilliseconds`, `FallbackRetryCapSeconds`, and `MaximumRetryDelaySeconds`; real values remain runtime-only. HTTP success bodies, retries, pacing, catalogue pages, and per-monitor measurement/accessory pages are bounded.
- Catalogue, dust, accessory, aggregate-rule, and offline fleet work uses typed immutable failure aggregation, preserving the primary error and any operational-recording error while continuing independent items. Requested cancellation still stops work immediately.
- Scheduled production work resolves focused handlers and narrow ports directly; `MyAtmApi` and `IDBClient` remain compatibility facades. Dust imports atomically commit measurements/rule state/outbox rows but never dispatch inline. `StoreDustLevels` remains exactly every 30 minutes and the independent durable `DispatchOutbox` job remains every minute, preserving suppression, escalation, retry, and dead-letter behavior without duplicate inline alert attempts.
- The Mapperly architecture guard is rule-first rather than an exact project allow-list: only non-test monitor app projects may reference the analyzer, each reference requires `PrivateAssets=all` and `OutputItemType=Analyzer`, and `rvt-monitor-common` is explicitly prohibited from referencing Mapperly.
- Folder-wide rule: monitor subprojects must remain consistent in code style and architecture. Prefer the shared host, focused handlers, narrow ports, async/cancellation conventions, UTC-safe boundaries, focused tests, and current deployment documentation; document vendor-specific deviations at their boundary.
- Strict review found no Critical defects. Its UTC persistence, fleet-failure coverage, project-state, and exact-cadence guard findings were remediated: outbox claim/completion/retry/dead-letter inputs normalize at the database boundary, catalogue updates normalize `ListedAtTime`, and focused tests cover successful continuation, failed operational recording, cancellation, Local/Unspecified outbox times, and the exact 30-minute/one-minute cron expressions.
- Fresh pre-merge verification: MyATM 193/193 including the runtime-only PostgreSQL fixture; Common 371/371; MyATM and root solution builds both succeeded with 0 warnings and 0 errors; repository-wide formatter verification and `git diff --check` passed. No live credential or connection value is tracked.

## Omnidots Merge and Post-Merge Verification - 2026-07-16

- Omnidots hardening was published and merged through GitHub PR #2 (`Harden Omnidots imports, webhooks, and trace collection`). Both the PR merge and formatter-baseline commit `e42b128` are ancestors of current `main`; the remote/local `codex/omnidots-strict-review-remediation` branch and its owned `.worktrees/omnidots-strict-review-remediation` worktree are removed, and stale worktree metadata was pruned.
- The formatter gate on the merged tree exposed one merge artifact in `myatmmonitor/MyAtmMonitorTests/TestUtil.cs`: the later shared-delivery branch inserted `Rvt.Monitor.Common.Delivery` after `Diagnostics`, while the formatter-baseline branch had sorted the pre-existing imports. The isolated correction only restores the root `.editorconfig` import order.
- Fresh post-merge verification passes: repository-wide `dotnet format --verify-no-changes`; root solution build with 0 warnings and 0 errors; IntegrationTesting 6/6, Common 367/367, Omnidots 393/393, AirQ 117/117, MyATM 155/155, Svantek 122/122, and Reporting 68/68 (1,228/1,228 total). The disposable PostgreSQL 17 container was stopped and removed, and no connection value or credential was persisted.
- Unrelated active worktrees and branches for common communications adapters and shared durable delivery were preserved.

## Common Communications Ports and Adapters Design - 2026-07-16

- The user approved the full email/SMS ports-and-adapters target in `docs/superpowers/specs/2026-07-16-common-communications-ports-and-adapters-design.md`.
- The self-reviewed test-first implementation plan is `docs/superpowers/plans/2026-07-16-common-communications-ports-and-adapters.md`. It has 12 independently reviewable tasks covering core contracts/composition, the compatibility facade, both durable dispatchers, infrastructure/configuration, TransmitSMS, SendGrid, Microsoft Graph small and 3-150 MiB attachment flows, all monitor composition roots, ReportingMonitor, Omnidots, architecture guards, documentation, and the release gate.
- Implementation completed on `codex/common-communications-adapters` in `.worktrees/common-communications-adapters`; the branch was reconciled with the latest integrated `main` through merge commit `3c1bb37`. The release target is `main`, after which the temporary worktree/branch is removed.
- The target keeps provider-neutral email/SMS ports, immutable requests, typed failure classification, notification composition, and the temporary `IMessageService` compatibility facade in `Rvt.Monitor.Common`. A new `Rvt.Monitor.Common.Infrastructure` project owns SendGrid, Microsoft Graph, TransmitSMS, configuration validation, and DI registration.
- Email selection uses `RVT__EMAIL_PROVIDER=SendGrid|MicrosoftGraph`, defaulting to SendGrid. Microsoft Graph uses app-only tenant/client/secret/sender configuration and supports report attachments below 3 MB directly and 3-150 MB through draft/upload sessions. The Entra app requires restricted `Mail.Send` and `Mail.ReadWrite` application access for full attachment support.
- Reporting keeps `IReportMessageSender` as its application port but bridges to the shared email port. The durable dispatcher moves from `IMessageService` to the async notification service. Omnidots' direct hardcoded email path becomes configured through `RVT__OMNIDOTS_MONITORING_ALERT_TO`.
- Transient provider failures retain bounded outbox retry; permanent/configuration failures dead-letter immediately. Requested cancellation propagates. Provider adapters do not hide retries, and safe errors/logs exclude credentials, tokens, destinations, bodies, attachments, raw responses, and Graph upload URLs.
- Tasks 1-12 are implemented: provider-neutral contracts and notification composition, typed durable failures, shared infrastructure/configuration, TransmitSMS, SendGrid, Microsoft Graph small and 3-150 MiB attachment flows, all monitor composition roots, the ReportingMonitor shared-email bridge, awaited Omnidots operational warnings, removal of legacy static/provider transports, operational documentation, and the release gate. Reporting and Common no longer own SendGrid packages or adapters; provider code is confined to Infrastructure by architecture tests.
- Runtime settings are documented in the root and monitor READMEs and represented in Compose without secret values: provider selection, SendGrid, Microsoft Graph tenant/client/secret/sender, TransmitSMS, Reporting test-recipient mode, and `RVT__OMNIDOTS_MONITORING_ALERT_TO`. Graph requires restricted app-only `Mail.Send`, plus `Mail.ReadWrite` for 3-150 MiB upload sessions. SendGrid rollback is environment-only; the temporary `IMessageService` facade and its three explicit synchronous rule-processor callers remain the compatibility limitation for this release.
- Fresh sequential tests on the branch reconciled with latest `main` passed 1,406/1,406 with zero failed/skipped: IntegrationTesting 6/6, Common 426/426, Infrastructure 64/64, AirQ 118/118, MyATM 194/194, Svantek 124/124, Omnidots 399/399, and ReportingMonitor 75/75. PostgreSQL tests used an isolated disposable PostgreSQL 17 container on a random loopback port with the connection supplied only to each test process; the container was removed afterward.
- `dotnet format` and `--verify-no-changes`, `docker compose config --quiet`, `git diff --check`, the provider-boundary scan, and the root solution build passed; the build reported zero warnings and errors. Release publishes for ReportingMonitor and Omnidots contained `Rvt.Monitor.Common.Infrastructure`, SendGrid, Azure.Identity, and Azure.Core. No live SendGrid, Graph, or TransmitSMS call was made, and no connection value or credential was persisted.

## RVT Common Source and Package Strategy - 2026-07-15

- The approved design is recorded in `docs/superpowers/specs/2026-07-15-rvt-common-package-strategy-design.md`. This is a design-only checkpoint; no repository extraction, package publication, consumer reference change, CI workflow, credential, or production deployment was performed.
- `RVT-Group-LTD/rvt-reporting` will become the one authoritative common source repository. The repository name does not rename the `rvt-common.sln` solution or `Rvt.Monitor.*` packages/namespaces. The separate .NET 10 monitor and portal repositories will consume immutable, exact-version private NuGet packages from GitHub Packages and retain independent release cadences.
- Migration is compatibility-first: preserve history and current APIs, publish `Rvt.Monitor.Common` and test support as `0.2.0-rc.1`, migrate and stage monitors, migrate and stage the portal, promote `0.2.0`, remove the old source only after all project references are gone, and split packages incrementally afterward.
- The target package family separates contracts, optional shared EF entities/mappings, shared infrastructure, storage, and monitor hosting. The current broad assembly remains only a migration facade so portal consumers do not permanently inherit monitor-only or unused provider dependencies.
- Releases use protected tags, synchronized SemVer initially, exact central package versions, package-source mapping, package/API baseline validation, immutable RCs, consumer update PRs, runtime version inventory, additive cross-repository evolution, one shared-schema migration authority, and expand-and-contract database changes.
- Emergency fixes publish a new patch version from the affected release tag and merge forward. Consumer rollback pins the previous stable package and rebuilds the container; published versions are never overwritten.

## Reporting Storage and P1 Remediation - 2026-07-15

- Active implementation worktree and branch: `.worktrees/reporting-storage-p1-remediation` on `codex/reporting-storage-p1-remediation`, based on the approved design and plan in `docs/superpowers/specs/2026-07-15-reporting-storage-p1-remediation-design.md` and `docs/superpowers/plans/2026-07-15-reporting-storage-p1-remediation.md`.
- ReportingMonitor now stores generated PDFs through the shared `IBlobStorageService` behind an app-local `MonitorBlobReportStorage` adapter. The retired reporting-only Azure SDK adapter and direct Azure package references were removed. Local, Azure Blob, and S3 providers now share the same common registration and validation path.
- Common blob binding supports monitor-specific defaults without changing Svantek's existing audio defaults. Reporting defaults to provider `Local`, container `pdfreports`, prefix `rvtreports`, and local root `/data/rvt/blobs`; `RVT__BLOB_REPORT_CONTAINER_NAME` remains only as a compatibility fallback when `RVT__BLOB_CONTAINER` is absent.
- Docker Compose mounts the dedicated `reporting-reportfiles` named volume at `/data/rvt/blobs`, so default local PDFs are written below `/data/rvt/blobs/pdfreports/rvtreports/`. The provider's absolute URI is persisted in `report.report_link`.
- Vibration report rules now match notifications using the persisted averaging period while continuing to omit that implementation detail from displayed vibration rule data. PostgreSQL integration coverage proves the matching alert is included and closed-note data is preserved.
- Delivery failures are durable per recipient. Provider-returned failures and non-cancellation provider exceptions are written to `report_delivery.error_message` with a safe 1,024-character bound, later recipients are still attempted, and successful rows store a null error. Report persistence and `LastGenerated` advancement continue after delivery failures, as explicitly selected for this workflow; requested cancellation still propagates.
- Scheduled generation isolates failures per due rule and continues with later rules. Direct rule and one-time generation retain their normal exception behavior, and requested cancellation always stops the batch.
- Verification: shared common tests passed 123/123; the complete ReportingMonitor suite passed 68/68 against the runtime-only PostgreSQL fixture; `dotnet build rvt-monitors.sln --no-restore --nologo -m:1` succeeded with 0 warnings and 0 errors after restoring worktree-local assets; `dotnet format`, `docker compose config --quiet`, `git diff --check`, and CodeGraph sync/status completed successfully. No connection value or credential was persisted.
## Shared Durable Delivery Foundation Verification - 2026-07-15

- The shared delivery foundation now provides provider-neutral delivery contracts and planner, a common outbox entity and mapping, idempotent PostgreSQL and SQL Server migrations, fenced dispatch, cancellation-aware MQTT delivery, and focused unit/contract coverage.
- MyATM now uses the shared outbox through the common dispatcher, with forward and rollback migration scripts retaining the legacy table for the compatibility window.
- Svantek reliability work now bounds import windows, propagates asynchronous job failures, accepts cancellation through the vendor gateway, and includes focused coverage for windowing, cancellation, and job failure semantics.

## Shared Durable Delivery Planning - 2026-07-15

- The user approved the shared durable-delivery architecture in `docs/superpowers/specs/2026-07-15-shared-durable-delivery-and-svantek-remediation-design.md`; committed as `62e471a` (`docs: design shared durable delivery migration`).
- The approved design reuses `notification`, `notification_sent`, the generic rule/notification contracts, and MyATM's existing `my_atm_alert_occurrence`. It adds exactly one shared delivery table (`monitor_delivery_outbox` / `dbo.MonitorDeliveryOutbox`) because pending/retry/lease/dead-letter state does not belong in the immutable audit table.
- MyATM will migrate from `my_atm_outbox_message` to the shared table with lossless forward/backfill and authoritative rollback synchronization. Legacy `Leased` maps to shared `InProgress`; version-1 payload JSON and deterministic IDs/keys remain compatible. The legacy table remains frozen for one release and is not dropped by this work.
- Svantek will use the same table/dispatcher and receive bounded async ingestion (7-day first backfill, 12-hour request slices, 5-minute overlap), aggregate job failures, atomic import/alert commits, site-hours/DST-aware offline behavior, async sound follow-up, readiness, scheduler parity, and restoration of all four excluded test suites.
- Three execution plans are written and self-reviewed:
  - `docs/superpowers/plans/2026-07-15-shared-durable-delivery-foundation.md`
  - `docs/superpowers/plans/2026-07-15-myatm-shared-outbox-migration.md`
  - `docs/superpowers/plans/2026-07-15-svantek-reliability-remediation.md`
- Required implementation order is shared foundation, MyATM migration/cutover, then Svantek remediation. Each plan uses TDD-sized tasks, frequent commits, provider migration contracts, and a full release gate.
- The shared foundation, MyATM migration, and Svantek remediation are implemented. Preserve unrelated untracked duplicate MyATM docs and `ConfigureMeasuringPointResult 2.cs` unless the user explicitly asks to remove them.

## Omnidots Reference Analysis and Svantek Verification - 2026-07-15

- Synchronized CodeGraph from the native macOS clone. `codegraph sync .` reported the index already current; the verified index contains 462 files, 6,670 nodes, and 13,292 edges with no pending changes.
- Fetched `origin` and confirmed `main` is not behind. Local `main` is two documentation commits ahead of `origin/main` (`53d611c`, `2199738`). Preserved the existing untracked MyATM duplicate design/plan files and `omnidotsmonitor/OmnidotsMonitor/model/dto/ConfigureMeasuringPointResult 2.cs`.
- Omnidots reference improvements now present include the shared `MonitorHost` modes, DI composition, focused use-case handlers and vendor gateway, EF Core/PostgreSQL persistence, app-local Mapperly mappings, narrow query/command ports, shared MQTT publishing, test-local filtering, PostgreSQL fixture coverage, log redaction, Veff/VDV job registration, and the safe configure response that no longer returns webhook/configuration secrets.
- The approved three-phase Omnidots remediation target adds explicit past-looking fetch windows, aggregate fleet-failure propagation, mandatory constant-time webhook authentication, timezone-aware active-site offline intervals, validated UK fleet-watchdog options, independent transactional Peak/Veff/VDV cursors, ordered all-or-nothing traces, dual-provider forward/rollback migrations, and validated/fair fleet trace selection. Except for the safe configure response, those plans are not yet implemented on `main`.
- Omnidots is not yet a safe reference for every reliability detail. Its strict-review defects remain except for the configure-response secret: the Veff/VDV runner still passes positive `120` into an additive fetch-window helper; Peak/Veff/VDV still share `LastDataTime1Min`; per-monitor import failures are swallowed; missing webhook signatures are acknowledged; same-day offline duration overcounts to site close; traces remain hard-coded/non-atomic; and the fleet watchdog retains date/time-zone weaknesses.
- Svantek is structurally aligned with the reference on shared hosting, DI, use-case handlers, EF Core/PostgreSQL-first mappings, Mapperly boundaries, narrow ports, shared alert publishing, test-local filtering, and provider-backed integration fixtures. Its sound-recording flow additionally uses the generic blob-storage port and a per-run vendor file cache.
- Svantek review findings to prioritize:
  1. `StoreNoiseLevelsHandler` catches monitor, project, and top-level failures and returns normally, so one-shot/Quartz execution reports exit code 0 even when ingestion fails.
  2. Noise request ranges are neither capped nor cursor-page bounded. The fallback `startDate >= endDate` path sets `endDate = startDate + 4h`, which can ask for future data after a new deployment.
  3. Noise rows, eight-hour aggregates, the latest timestamp, offline recovery, and rule evaluation are separate commits/contexts. A failure after the watermark update can permanently skip alert evaluation for that interval.
  4. `StoreMonitorsHandler` creates catalogue DTOs with `Offline=false`, and Mapperly's `UpdateMonitorEntity` maps that value, so an hourly catalogue refresh can clear a persisted offline state.
  5. Vendor HTTP remains synchronous-over-async (`.Result`) and does not accept cancellation. `SvantekMonitorJobDispatcher` also drops Quartz's cancellation token when calling `MonitorJobRunner`; only the blob write currently receives it.
  6. Noise ingestion does not publish the shared data-inserted MQTT event; the event publisher is used only by the rule processor for alerts.
  7. Svantek's offline check uses elapsed wall-clock time and does not apply site operating hours as the Omnidots target requires.
  8. Svantek exposes only `/liveness`, not a database-backed `/readiness` endpoint. Omnidots has the same current operational gap.
  9. Four core legacy test files are excluded in `SvantekMonitorTests.csproj`: `TestRules.cs`, `TestSvantekApi.cs`, `TestSvantekApiException.cs`, and `TestSvantekApiNoiseLevels.cs` (30 test methods before data expansion). Current green tests therefore do not exercise the main noise-ingestion/rule/failure paths. The architecture test scans only top-level `SvantekApi*.cs`, not `api/UseCases`, and there is no focused scheduler/dispatcher parity test.
- Fresh verification: `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-restore --nologo` passed 75/75 with 0 skipped; `dotnet build svantekmonitor/svantekmonitor.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors. The recurring Sonar bootstrap messages only report a missing local `.sonarqube` analysis-target file.
- No production code, secrets, runtime configuration, database state, or external services were changed during this review.
## Repository-wide Formatter Baseline Repair - 2026-07-16

- The user selected full repository baseline repair rather than waiving the final formatter gate. `dotnet format rvt-monitors.sln --no-restore --verbosity minimal` mechanically normalized 96 previously untouched C# files for the root `.editorconfig`: line endings/encoding, import ordering/removal, whitespace, and final newlines. No monitor behavior was intentionally changed by the formatter.
- Fresh `dotnet format rvt-monitors.sln --verify-no-changes --no-restore --verbosity minimal` now exits 0 with no diagnostics under .NET SDK 10.0.203. `git diff --check` is silent.
- The root solution build passes with 0 warnings and 0 errors. Sequential full suites pass against an isolated disposable PostgreSQL 17 instance: Common 280/280, Omnidots 393/393, AirQ 117/117, MyATM 124/124, Svantek 75/75, and Reporting 60/60. The focused Omnidots provider/migration/model/commit/outbox/end-to-end slice passes 47/47. The disposable container was stopped and removed; no connection value or credential was persisted.
- The broader verification exposed one stale repository-wide architecture assertion: ReportingMonitor legitimately uses the app-local Mapperly analyzer, but the MyATM allow-list still named only AirQ, MyATM, Omnidots, and Svantek. The existing failing test was corrected to include `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`, consistent with the project rule that Mapperly is permitted only in monitor app projects and never in `rvt-monitor-common`.
- The formatter-baseline release prerequisite is now satisfied. The only preserved untracked file in the remediation worktree remains `.superpowers/sdd/.gitignore`.

## Common Durable Alert Final Review Remediation - 2026-07-15

- Source and tests are committed in `48ca37b` (`fix(alerts): address final review findings`). API security validation now follows the `MonitorExecutionModeContext` selected by the shared host, so ambient `MonitorApi:Enabled` configuration cannot make one-shot or Quartz processes require endpoint secrets; direct API test hosts register API mode explicitly.
- Outbox claims resolve the occurrence-owned notification ID inside the claim transaction. MQTT, email, and SMS adapters reject an envelope ID that differs from that authority. Terminal email/SMS failures use the claimed ID for their atomic failure audit, including malformed, empty, and mismatched payloads, without persisting or printing raw payload text.
- Contact lookup canonicalizes GUID text and applies the case-insensitive filter in the provider-translated query before materialization. Live PostgreSQL coverage proves an uppercase `AspNetUsers.Id` still plans the requested email and SMS deliveries.
- PostgreSQL concurrency evidence is controlled rather than scheduler-dependent: an explicit row lock proves the claim skips the locked oldest row, and an uncommitted unique occurrence plus a verified PostgreSQL lock wait proves duplicate recovery. SQL Server `datetime2` claim fields are normalized to `DateTimeKind.Utc` without changing ticks, including nullable fields.
- Retry backoff is calculated only for nonterminal failures. A final failure persists the actual outcome instant as both `next_attempt_at` and `completed_at`, and uses the same instant for the audit.
- Fresh sequential verification passed: Common 280/280; Omnidots 393/393 using isolated generated PostgreSQL schemas; focused migration/model/commit/outbox/end-to-end provider coverage 47/47; and `dotnet build rvt-monitors.sln --no-restore --nologo` with 0 warnings and 0 errors. Roslyn formatter verification passed for all 17 C# files changed by this remediation, `git diff --check` was silent, and leakage triage found only documentation, configuration names, synthetic test sentinels, and pre-existing unrelated helpers. The quantified untouched repository-wide formatter baseline recorded below is unchanged.

## Common Durable Alert Ingress Implementation and Task 12 Verification - 2026-07-15

- Active branch/worktree: `codex/omnidots-strict-review-remediation` in `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors/.worktrees/omnidots-strict-review-remediation`. Task 12 started from `5ed06c2`; the verification-discovered context-member correction is isolated in `38777ba` (`fix(common): resolve alert context member collision`). The subsequent whole-branch review produced the final remediation recorded in the section above.
- `Rvt.Monitor.Common.Alerts` now owns normalized alert acceptance, event-time caution/alert suppression and escalation, atomic occurrence/notification/delivery planning, provider-aware EF stores, fenced delivery claims, MQTT/email/SMS adapters, exponential retry, dead letters, audits, and completed-row cleanup. Omnidots owns the bounded HTTP boundary, exact-byte HMAC, typed device configuration, vendor schema validation/translation, and calls the narrow `IAlertIngressPort`; the webhook path no longer sends providers or writes notifications directly.
- Omnidots durable-alert migrations are `2026-07-15-add-common-durable-alerts.sql` and `2026-07-15-rollback-common-durable-alerts.sql` in both `omnidotsmonitor/OmnidotsMonitor/postgres/` and `omnidotsmonitor/OmnidotsMonitor/sqlserver/`. The existing cursor/trace migrations remain `2026-07-14-add-import-cursors-and-trace-order.sql` and matching rollback assets for both providers.
- Provider evidence used an isolated disposable PostgreSQL 17 container on a random loopback port; the tests received the connection only at runtime, generated/dropped their own schemas, and the container was removed afterward. The focused 45/45 provider suite passed: PostgreSQL forward/rollback migration execution, atomic commit/outbox behavior, and concurrent end-to-end exact-body replay; SQL Server migration parsing, EF metadata/model mapping, and claim-contract coverage. No live SQL Server database was used.
- Fresh sequential suite evidence: Common passed 269/269; Omnidots passed 389/389; the focused provider/migration/store/end-to-end filter passed 45/45; and the MyATM model-mapping regression check passed 4/4. `dotnet build rvt-monitors.sln --no-restore --nologo` passed after the explicit compatibility member correction with 0 warnings and 0 errors. `git diff --check` was silent.
- The exact root `dotnet format rvt-monitors.sln --verify-no-changes --no-restore --verbosity minimal` command does not pass under the installed .NET SDK 10.0.203: after a successful asset-only restore it reports 1,609 diagnostics in 139 files that are all outside `git diff cfc977e..HEAD` (1,458 `ENDOFLINE`, 56 `IMPORTS`, 52 `WHITESPACE`, 30 `CHARSET`, and 13 `FINALNEWLINE`). Formatter verification restricted to the 102 existing branch-touched C# files passed silently. The repository has no `global.json`, and prior reports identify only a .NET 10 formatter, not an exact previous SDK, so an SDK-version difference cannot be established. The untouched repository-wide formatting baseline remains a final-gate prerequisite; it was not bulk-reformatted in this Task 12 commit.
- API mode requires an absolute HTTPS callback URL and distinct webhook/configuration secrets of at least 32 strict UTF-8 bytes. Both POST bodies are JSON-only, identity-encoded, limited to 64 KiB, and protected by zero-queue concurrency limits. Webhook success is only `{ "processed": true }`; configuration success is only `{ "configured": true }`. Exact authenticated bytes are durably idempotent for occurrence, notification, and per-destination delivery creation, including concurrent replay.
- External MQTT/email/SMS delivery is at least once. A provider can accept a message immediately before the process stops and before fenced completion commits, so a later retry can duplicate that external side effect; exactly-once provider delivery is not claimed.
- Operational prerequisites: apply the provider forward migration before application deployment; configure the API-only callback/secrets before enabling API mode; verify persistence and `--job DispatchAlerts`; enable either the one-minute API worker or Quartz `DispatchAlerts`, not both in one process; and retain the daily `CleanupAlerts` job. Before rollback, stop webhook writers and every dispatcher, roll back the application, and only then remove the schema after no deployed version uses it. Completed outbox rows retain for 90 days, dead letters remain for explicit resolution, and occurrence rows remain permanent unless replay protection is deliberately discarded.
- Leakage triage found only configuration names, synthetic test sentinels, negative architecture/bounded-reader assertions, documentation, test-only dictionaries, and unrelated legacy `FirstOrDefault()` calls outside the endpoint. It found no secret value, raw-body logger, production string webhook path, dynamic configuration request, or first-signature selection. Historical Task 10 evidence was corrected: the removed legacy tests supplied HMAC strings made with an empty configured key; the signatures themselves were not empty.

## Common Durable Alert Ingress Implementation Plan - 2026-07-15

- The approved design now has a self-reviewed, test-first implementation plan at `docs/superpowers/plans/2026-07-15-common-durable-alert-ingress.md`. Production implementation has not started at this checkpoint.
- The plan contains 12 reviewable RED/GREEN commits: Common contracts/policy, shared provider schema, ingress service, atomic EF acceptance, fenced outbox, delivery adapters/worker, Omnidots API security, typed measuring-point configuration, vendor alarm translation, bounded/rate-limited endpoints, host-mode composition, and final documentation/strict review.
- PostgreSQL live concurrency/transaction tests and SQL Server mapping/parsed migration/claim contracts are explicit. The plan includes exact-body concurrent replay, transaction rollback, contact/MQTT delivery planning, lease fencing, retry/dead-letter audits, API-only startup validation, safe HTTP status/leakage tests, one-shot/Quartz/API-worker scheduling, formatter/Roslyn verification, and a final independent review gate.
- The implementation plan preserves the unrelated untracked `.superpowers/sdd/.gitignore`, does not migrate AirQ/Svantek/MyATM alert behavior, and does not authorize merge or push.

## Common Durable Alert Ingress Design - 2026-07-15

- The recurring Omnidots webhook P0 has an approved Common-first design in `docs/superpowers/specs/2026-07-15-common-durable-alert-ingress-design.md`; implementation has not started at this checkpoint.
- Reusable alert acceptance, event-time suppression/escalation, atomic occurrence/notification/outbox persistence, fenced dispatch, MQTT/email/SMS adapters, retries, dead letters, audits, and cleanup move into `Rvt.Monitor.Common.Alerts`. Omnidots remains responsible for vendor HMAC, typed configuration, payload parsing, and translation through the driving `IAlertIngressPort`; the webhook does not implement `IMonitorEventPublisher`.
- API mode requires distinct webhook/configuration secrets of at least 32 UTF-8 bytes and an absolute HTTPS callback URL. Scheduler-only and unrelated one-shot runs may start without those API secrets, while handler and validator guards still fail closed when constructed directly.
- Both API endpoints are JSON-only, bounded to 64 KiB, protected by no-queue concurrency limits, and use safe status contracts. Webhook HMAC covers exact raw bytes, exactly one signature is required, and configuration authentication uses fixed-time digest comparison. Caller webhook overrides are rejected and configuration success returns only `{ "configured": true }`.
- Exact authenticated-body duplicates are durable and idempotent through a permanent shared occurrence record. Occurrence, existing shared notification, and MQTT/email/SMS outbox rows commit atomically. External delivery is explicitly at least once; provider acceptance immediately before a crash can cause a retry duplicate.
- Omnidots is the first adopter. AirQ/Svantek behavior and the MyATM-specific outbox are not migrated in this P0. The existing `RvtMqttMessage` wire format and topics remain unchanged.
- Planned dispatch paths are a one-minute Common API worker when Quartz is disabled, Quartz `DispatchAlerts` every minute, a one-shot `DispatchAlerts` command, and daily completed-outbox cleanup. Occurrences remain permanent; completed outbox rows are retained for 90 days.

## Omnidots P1 Remediation - 2026-07-15

- Active branch/worktree: `codex/omnidots-strict-review-remediation` in `.worktrees/omnidots-strict-review-remediation`.
- The P1 implementation covers mandatory raw-body webhook HMAC authentication, bounded past-only Veff/VDV windows, independent Peak/Veff/VDV cursors with atomic imports, aggregate fleet failure propagation, ordered transactional trace persistence, and validated configuration-driven fair trace selection.
- Trace rollout defaults to enabled for serial `23423` with one monitor per run. An empty allow-list enables fleet eligibility; monitors without a prior trace are prioritized before the oldest last-trace time, with deterministic five-minute-slot rotation to prevent starvation.
- The implementation was reconciled with current `origin/main`. Post-merge verification passed: the complete Omnidots suite ran 214/214, `dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo` completed with 0 warnings and 0 errors, strict `dotnet format --verify-no-changes` passed under the root `.editorconfig`, and `git diff --check` was clean. The merge-specific duplicate `Microsoft.AspNetCore.TestHost` reference was removed before the final gate.

## Omnidots Strict Review Remediation Phase 1 (Historical) - 2026-07-14

- Phase 1 is implemented on branch `codex/omnidots-strict-review-remediation`. It corrects Veff/VDV request windows, propagates partial fleet-import failures, secures the measuring-point configuration and webhook contracts, calculates offline duration in site-local active intervals, and makes fleet monitoring timezone-safe and configuration-driven. No database migration is part of this phase.
- `StoreVeffRecords` and `StoreVdvRecords` now take a positive `TimeSpan` lookback. The shared one-shot/Quartz runner supplies two hours; each handler captures one UTC end instant and requests from that instant minus two hours minus a five-minute overlap. The unchanged UTC Quartz crons are Veff `0 0 0/2 * * ?` and VDV `0 15 0/2 * * ?`.
- `POST /configure-measuring-point` returns only typed JSON `{ "serialId": "...", "configured": true }` after Omnidots returns `ok: true`. Validation failures return safe HTTP 400 Problem Details without echoing the request, vendor request, configured secret, or raw exception message.
- `POST /webhook` requires `x-omnidots-notifier-signature: sha256=<64 hex characters>`, calculated as HMAC-SHA256 over the exact raw HTTP body bytes with `RVT__OMNIDOTS_WEBHOOK_SECRET` and compared in constant time before decoding or JSON deserialization. Missing, malformed, or mismatched signatures return 401; authenticated malformed/invalid payloads return 400; 200 is returned only after successful processing. Secret, body, signature, and raw failure text are excluded from endpoint responses and webhook operational log messages.
- Peak, Veff, VDV, and Trace fleet loops record a monitor-specific failure, continue later monitors, and throw one `OmnidotsImportException` after the loop. The one-shot/Quartz job therefore faults after any partial import failure. Its printable aggregate contains only the operation, count, and serial IDs and has no raw printable inner-exception chain; typed failure properties retain diagnostic exceptions for code. A secondary operational-recording failure does not stop fleet continuation.
- Webhook authentication now hashes the raw HTTP request bytes before any decoding or UTF-8 BOM handling. After successful authentication, strict UTF-8 decoding accepts and removes one leading BOM for JSON processing; invalid UTF-8 becomes a safe authenticated HTTP 400 response instead of an authentication mismatch. The existing string service/facade entry points remain compatibility adapters over UTF-8 bytes, while the production endpoint uses the byte path.
- Omnidots authentication mode is selected by `RVT__OMNIDOTS_USE_TOKEN`, which defaults to `true`: token mode uses `RVT__OMNIDOTS_TOKEN` without sending a Honeycomb username/password request; setting it to `false` authenticates with `RVT__OMNIDOTS_USER_ID` and `RVT__OMNIDOTS_USER_AUTH` and uses the returned token.
- Trace collection remains restricted by a hard-coded serial `23423` allowlist; other monitors are skipped. The Trace continuation/aggregate behavior therefore applies only to eligible entries until Phase 3 replaces the guard with validated configuration-driven and throttled fleet activation.
- Offline checks now sum real elapsed UTC intersections with each monitor's weekday/Saturday/Sunday schedule in that monitor's configured timezone. Null schedule boundaries are closed, overnight intervals and `00:00`-`24:00` are supported, and missing/invalid timezone IDs are sanitized aggregate failures. Schedule boundaries in spring-forward gaps or fall-back ambiguous periods fail closed with a fixed safe error; valid DST-spanning intervals retain their real 23/25-hour elapsed duration.
- `Omnidots:Monitoring` now configures `Recipient`, `TimeZoneId`, `WindowStart`, `WindowEnd`, and positive `StaleAfter`. Options validate on host startup without including configured values in failures. Freshness compares complete UTC instants; `Utc` database timestamps are unchanged, `Local` timestamps convert to UTC, and SQL Server-style `Unspecified` timestamps retain their ticks and are explicitly treated as UTC. Timezone conversion is limited to the business-window check.
- `omnidotsmonitor/README.md` now documents the current ASP.NET Core/one-shot/Quartz host, secret-safe endpoint examples, exact HTTP and signature contracts, import failure semantics, UTC schedules, site/DST policy, monitoring options, startup validation, and timestamp normalization.
- Fresh Phase 1 review-follow-up verification: `dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo` passed 155/155. `dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors.

## Omnidots P0 Configuration Secret Response Fix - 2026-07-15

- The `POST /configure-measuring-point` endpoint no longer returns the serialized Omnidots vendor configuration, which contains the configured webhook HMAC secret. Successful requests retain HTTP 200 and now return only `{ "configured": true }`.
- Added a named `ConfigureMeasuringPointResult` response DTO and an ASP.NET Core TestHost regression test that exercises the real endpoint and proves the response contains only the safe acknowledgement.
- Verification: the complete Omnidots suite passed 91/91, changed-file `dotnet format --verify-no-changes` passed, and the Omnidots solution built with 0 warnings and 0 errors. The pre-existing project-wide Omnidots test formatting backlog remains outside this security-fix scope.

## EditorConfig and Roslyn Formatting Audit - 2026-07-15

- Added the root `.editorconfig` and configured shared builds with `AnalysisLevel=latest` plus `EnforceCodeStyleInBuild=true` in `Directory.Build.props`. The C# rules explicitly cover unused imports, formatting, and using-directive placement; the build proves the configuration is consumed.
- `dotnet format rvt-monitors.sln --verify-no-changes` passes. A mechanical trim removed trailing whitespace from 14 tracked C# files that the .NET 10 formatter did not change.
- Roslyn warning baseline was 80 `IDE0055` formatting diagnostics plus 19 `IDE0005` notices explaining that unused-import analysis on build requires `GenerateDocumentationFile=true`. The .NET 10 formatter does not apply those compiler formatting diagnostics, so the editor/formatter rules remain active as suggestions while build warnings remain reserved for actionable compiler/analyzer issues. Final forced root-solution rebuild (`--no-restore -t:Rebuild`) passed with 0 warnings and 0 errors. Existing Sonar “analysis targets file not found” messages remain external Sonar-bootstrap notices rather than Roslyn diagnostics.
- The native clone already contained two untracked MyATM planning/specification files; the audit left them untouched. `git diff --check` is clean.

## MyATM Hardening Verification and Test-Host Remediation - 2026-07-14

- Active implementation worktree and branch: `.worktrees/codex-myatm-review-remediation-implementation` on `codex/myatm-review-remediation-implementation`.
- The MyATM hardening work provides fenced, one-at-a-time durable-outbox claims/outcomes; event-time Alert/Caution suppression and escalation; atomic scheduled aggregate/offline alert commits; monitor-read failure propagation; and keyset-paged, transactional accessory imports. `IDBClient` remains the compatibility facade while new handler paths use narrow ports.
- The recorded unfiltered-suite stall was reproduced and diagnosed with preserved test-host dumps. The blocked code was test bootstrap (`WebApplication.CreateBuilder` / `Host.CreateDefaultBuilder`) starting the default configuration `FileSystemWatcher`; it was not the Task 4 evaluator or PostgreSQL suppression logic. Endpoint and one-shot-host tests now explicitly pass `--hostBuilder:reloadConfigOnChange=false`, appropriate for tests that only exercise route registration or one-shot dispatch. This removes file watching from those tests without changing production host behavior.
- A legacy synchronous dust-import exception test was updated to assert the Task 6 contract: the original monitor-query `IOException` propagates after the best-effort operational record attempt.
- Migration review: the PostgreSQL and SQL Server hardening forward scripts idempotently add nullable `lease_id` / `LeaseId` and the alert-occurrence lookup index after the durable-outbox schema. Their rollback scripts drop the index before the lease column, are idempotent, and retain the required deployment-order warning. Identifiers match the EF mappings (`my_atm_*` for PostgreSQL and `dbo.MyAtm*` for SQL Server).
- Verification in this worktree: endpoint hang regression 1/1; monitor-read assertion regression 1/1; MyATM non-PostgreSQL suite 84/84; shared-host hang regression 3/3; full common suite 119/119; `dotnet build rvt-monitors.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors after a local asset-only restore. The architecture boundary tests are included in the 84 MyATM non-PostgreSQL tests.
- Controller verification supplied the runtime-only PostgreSQL setting to the complete MyATM suite and passed 121/121. No connection value, credential, destination, or live data was added or tracked. Existing diagnostic dumps were preserved and not committed.
- Final review remediation corrected the shared async email delegate argument order, guarantees malformed Email/SMS payloads still take the fenced final-dead-letter path, and applies the same event-time Alert/Caution suppression policy to atomic aggregate commits. Aggregate field aliases now use the same normalized Alert-over-Caution precedence as normal dust ingestion.
- Final verification: MyATM PostgreSQL suite 124/124, MyATM non-PostgreSQL suite 86/86, shared common suite 120/120, and `dotnet build rvt-monitors.sln --no-restore --nologo` completed with 0 warnings and 0 errors. Full-branch review passed after the final remediation; no credentials, destinations, or live connection data were tracked.

## Reporting Monitor Integration - 2026-07-14

- ReportingMonitor is a first-class root-solution monitor under `reportingmonitor/`, with six projects: host, Core, Pdf, Storage, Messaging, and tests. The retained reporting domain/adapters originate from the prior reporting implementation; its former Data and Service projects remain intentionally retired, with monitor-specific EF Core code now in `ReportingMonitor/api/db`.
- The host uses the shared `MonitorHost`, `ReportingMonitorContext`, narrow reporting query/command/lock/health ports, and the standard `ConnectionStrings__DefaultConnection` setting with `RVT__DATABASE_PROVIDER=PostgreSql`.
- Root Compose exposes `reportingmonitor-api` at port `8085` with the API enabled and scheduler disabled. It declares no database service: deploy an existing PostgreSQL database and provide `ConnectionStrings__DefaultConnection` plus secrets externally.
- `/liveness` is unauthenticated process health and `/readiness` verifies database connectivity. `/internal/reports` routes use `X-RVT-Internal-Key` when `RVT__INTERNAL_API_KEY` is configured; a missing key is rejected outside Development.
- Apply `reportingmonitor/database/postgres/reporting_service_prerequisites_20260625.sql` once to each target database before enabling report persistence. The script idempotently enables `pgcrypto`, adds `report_rule.is_hidden_system_rule`, and creates the hidden one-time-rule uniqueness index.
- `ReportingMonitorTests` uses the shared `RVT__POSTGRES_INTEGRATION_CONNECTION` fixture convention. Database tests run in a unique generated `rvt_integration_*` schema and are tagged `PostgreSqlIntegration`; pass the connection only to the test process and never persist it.
- Verification for this integration: `dotnet sln rvt-monitors.sln list | rg "ReportingMonitor|Rvt.Reporting"`, `docker compose config`, and `dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo` (with the runtime-only integration connection) passed; the reporting suite ran 60 tests successfully.

## AirQ Reliability Remediation - 2026-07-14

- AirQ now has a fail-closed `testlocal` filter in `AirQTestLocalMonitorFilter`. Normal runs are unchanged; `testlocal=true` requires `AirQ__TestLocal__SerialId` and applies the selected serial to both vendor catalogue writes and database monitor reads.
- `StoreNoiseLevels` and date backfills record every per-monitor failure, then throw `AggregateException`; the shared `MonitorHost` therefore returns one-shot exit code `1` instead of reporting a false success. Top-level catalogue/read failures are recorded and rethrown.
- AirQ catalogue imports tolerate a valid empty metadata response by creating the monitor with empty optional metadata and continuing the import.
- `scripts/run-testlocal-suite.sh` now runs AirQ `StoreNoiseLevels` and requires the caller to provide `AIRQ_TESTLOCAL_SERIAL_ID`; it passes that value as the monitor configuration setting without tracking a live serial.
- Verification: focused regressions 20/20, full AirQ suite 117/117, AirQ solution build 0 warnings/errors, testlocal script syntax and dry-run passed, and `git diff --check` passed.

## Omnidots Strict Review Remediation Plans - 2026-07-14

- Detailed test-first implementation work is split into three independently deployable plans:
  - `docs/superpowers/plans/2026-07-14-omnidots-remediation-phase-1.md` for fetch windows, API security, offline calculations, failure propagation, and fleet monitoring.
  - `docs/superpowers/plans/2026-07-14-omnidots-remediation-phase-2.md` for PostgreSQL/SQL Server migrations, independent measurement cursors, atomic imports, and ordered transactional traces.
  - `docs/superpowers/plans/2026-07-14-omnidots-remediation-phase-3.md` for configuration-driven, throttled, fair fleet trace activation.
- Plan self-review confirmed coverage for all nine strict-review findings, exact cross-task interfaces, red/green test commands, migration rollback, verification gates, and commit checkpoints.
- Fair trace selection was tightened during plan review: equal-priority monitors rotate by five-minute UTC run slot so a monitor that repeatedly returns no traces cannot starve the rest of the fleet.
- Historical planning checkpoint: implementation had not started when this entry was written. Its status is superseded by the current Phase 1 implementation section above.

## MyATM Durable Import and Outbox - 2026-07-14

- Merged to `main` from `codex/myatm-durability-remediation`; the feature worktree can be removed after the merge verification and push.
- Normal dust-page ingestion now evaluates rules without side effects and invokes one narrow `IMyAtmDustImportCommands.CommitDustImportAsync` command. The EF transaction deduplicates measurements, preserves the newest watermark, conditionally mutates rule state, records a stable logical alert occurrence/portal notification, and writes per-destination alert plus data-insert MQTT outbox rows.
- MyATM-local PostgreSQL and SQL Server deployment scripts now live in `myatmmonitor/database/migrations/2026-07-14-add-durable-outbox.*.sql`; the PostgreSQL integration fixture creates and resets the same tables. No connection strings or credentials are tracked.
- The dispatcher is exposed as `DispatchOutbox` (one-shot and Quartz, every minute) and runs once immediately after a successful page commit. It serializably claims 50 rows for two minutes, awaits MQTT/email/SMS delivery, exponentially retries from 30 seconds (capped at 30 minutes), and dead-letters after eight attempts. Completed sibling destinations are not retried.
- `StoreDustLevels` has no direct MQTT send or notification write. Both data-inserted and alert MQTT are durable outbox messages. Legacy non-ingestion processors retain the synchronous compatibility publisher until their independent workflows are migrated.
- `IMonitorEventPublisher` now provides awaited async methods; its legacy synchronous methods block on the awaited implementation for untouched monitor paths.
- Legacy `TestRules` assertions that inspected individual insert/watermark/notification calls were replaced with import-commit contract tests. Added mapping, dispatcher, and PostgreSQL commit/replay coverage.
- Verified in this worktree: the supplied runtime-only PostgreSQL connection ran the focused database/mapping suite (31/31) and complete MyATM suite (91/91). The atomic commit test caught and fixed an optional-FK defect: data-insert outbox rows now persist a null occurrence key rather than an empty string. Earlier checks: common tests 116/116, solution build with 0 warnings/errors, and `git diff --check` passed. No connection string or credential is tracked.

## Omnidots Strict Review Remediation Design (Historical) - 2026-07-14

- Historical design checkpoint: the approved three-phase design is recorded in `docs/superpowers/specs/2026-07-14-omnidots-strict-review-remediation-design.md`. Its pre-implementation status is superseded by the current Phase 1 implementation section above.
- Phase 1 corrects request-window semantics, secret-safe configuration responses, mandatory constant-time webhook authentication, offline interval calculations, aggregate job failure reporting, and timezone-safe fleet monitoring without a database migration.
- Phase 2 adds independent Peak/Veff/VDV import cursors and ordered transactional trace persistence, with idempotent forward and rollback scripts targeting both PostgreSQL and SQL Server.
- Phase 3 replaces the hard-coded trace serial with validated allow-list, enablement, and per-run throttle options before fleet-wide activation.
- The design preserves the shared host, narrow ports, `IDBClient` compatibility facade, PostgreSQL-first integration coverage, and SQL Server runtime compatibility.

## MyATM Strict Remediation Implementation Checkpoint - 2026-07-14

- This checkpoint intentionally records a partial implementation because the user requested that the in-progress branch be committed, merged, and pushed before the broader remediation is finished.
- Implemented: configurable measurement/accessory page limits, deterministic page normalization, direct page gateway methods, shared retry coverage for HTTP 408 and 5xx responses, preserved catalogue `Offline` state, customer-scoped monitor queries, nullable average DTO fields, 30-minute dust scheduling, activity-window rule enforcement, and cancellation propagation from dust ingestion.
- Focused unit tests covering the changed gateway, retry policy, mapper, and reader passed 12/12.
- Not yet implemented: handler-level iteration across every measurement page, transactional measurement/watermark/rule persistence, notification occurrence/outbox schema and dispatcher, awaited shared MQTT publication, and full PostgreSQL fixture verification.
- The full non-database MyATM test run currently has 18 failures. They are legacy contract assertions that still expect the pre-pagination request URL or pre-activity-window behavior; they must be updated before the full remediation can be called complete. PostgreSQL fixture tests also require the local integration connection to be supplied at runtime; no credential is stored here.

## Omnidots Strict Code Review - 2026-07-14

- Reviewed the native macOS clone on `main` at `623e475`, synchronized exactly with `origin/main`; no Omnidots production code was changed during the review.
- Focused verification passed: `dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-restore --nologo` passed 90/90, and `dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore --nologo` succeeded with 0 warnings and 0 errors.
- Highest-priority functional findings:
  1. The new Veff/VDV one-shot jobs pass `120` into a helper that adds the value to `UtcNow`, so both scheduled imports request data beginning two hours in the future. Passing `-120` would still be clamped to only ten minutes and leave gaps on a two-hour recurrence.
  2. Peak, Veff, and VDV imports all read and update the same `LastDataTime1Min` watermark. A newer Veff/VDV sample can advance the peak cursor and permanently skip peak records.
  3. The configure endpoint returns the serialized vendor request containing the long-lived webhook HMAC secret.
  4. A webhook with no signature is logged but acknowledged with HTTP 200, dropping the alarm without asking the sender to retry.
  5. Same-day offline-duration calculation counts from the last sample through site closing time instead of stopping at the current time, causing premature offline alerts.
  6. Per-monitor import exceptions are recorded and swallowed, so the one-shot/Quartz job reports success even if every vendor request or database write fails.
- Additional material findings: trace collection is hard-coded to serial `23423`; trace samples have no persisted ordinal and are not written atomically; the fleet watchdog compares only `TimeOfDay` and treats host-local time as UK time, which breaks stale-data detection across dates, midnight, and non-UK host time zones.
- Existing Veff/VDV dispatch tests authenticate against an empty monitor list and do not assert request timestamps, which is why the future-window defect passes the complete suite.

## MyATM Strict Review Remediation Design - 2026-07-14

- Approved design recorded in `docs/superpowers/specs/2026-07-14-myatm-strict-review-remediation-design.md`; implementation has not started.
- Dust polling is to run every 30 minutes while importing all missing one-minute samples through server-filtered keyset pagination.
- Alert durability will use Omnidots-style persistent `IsActive` plus `LimitOn`/`LimitOff` hysteresis, Caution-to-Alert escalation, a recent-notification guard for rapid oscillation, and a MyATM-owned per-destination transactional outbox.
- The implementation scope covers all nine strict-review findings: complete telemetry pagination, catalogue offline-state preservation, activity-window enforcement, atomic measurement/watermark/rule commits, cancellation propagation, awaited MQTT delivery, customer scoping, nullable averaged fields, and sustainable scheduling.
- No credentials, live database state, or runtime container configuration were changed while writing the design.

Last updated: 2026-07-15 Europe/Athens

## MyATM Reliability Refactor - 2026-07-14

- Active implementation worktree and branch: `.worktrees/codex-myatm-reliability-refactor` on `codex/myatm-reliability-refactor`; it starts from the reviewed and documented main branch state.
- `MyAtmMonitorOptions` now owns `CustomerId`, explicit catalogue `DevicePageSize`, and `PortalBaseUrl`; the values are bound from `appsettings.json` and validated at composition time. The customer ID is no longer a hard-coded service constant.
- The catalogue request now sends `$top`, imports all complete pages, and has regression coverage for a non-default page size. Accessory collection is registered for both one-shot and Quartz dispatch and runs daily.
- Vendor HTTP, the scheduled job path, and import handlers use `Task` plus `CancellationToken`; the supported legacy synchronous facade is retained only for compatibility. Import failures are recorded operationally and then fault the caller, so scheduled jobs cannot silently succeed after vendor failures.
- Measurement and accessory response handling is order-independent: readings are filtered by watermark and processed chronologically. Accessory imports now read their own persisted watermark from `my_atm_accessory_info`, rather than reusing the dust timestamp.
- Rule state transitions from normal ingestion are persisted with `UpdateAlertRule`; the eight-hour processor advances `Accessed` even when an eligible aggregate is missing. The MyATM-specific configured portal base URL is used in rule notification links.
- Added a database-backed `GET /readiness` endpoint (200/503) in addition to `GET /liveness`. README instructions now describe the current .NET host, scheduler, configuration, health endpoints, and PostgreSQL test setup.
- Added narrow measurement and health query interfaces and registered all MyATM narrow data-access interfaces in the composition root. `IDBClient` remains the compatibility facade.
- Folder-wide `AGENTS.md` now requires code style and architecture consistency across monitor subprojects.
- Current focused verification on this branch: build succeeded with 0 warnings; non-DB MyATM unit tests passed 55/55; PostgreSQL `TestDBClient` integration tests passed 26/26 using an ephemeral environment connection (no credential persisted). `git diff --check` passed.

## MyATM Monitor Review - 2026-07-14

- Native macOS clone was synchronized with `origin/main`; it was already current and the tracked worktree was clean before and after the review.
- Current MyATM layout: `MyAtmMonitor/` contains the shared-host bootstrap, minimal liveness API, Quartz/one-shot dispatcher, `api/UseCases/` handlers, vendor HTTP gateway, EF Core-backed `DBClient`, app-local Mapperly mapper, and DTO/JSON models. `MyAtmMonitorTests/` contains unit, architecture, mapper, endpoint, and PostgreSQL fixture tests.
- Focused verification: `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo` passed 78/78 on 2026-07-14. Build emitted only pre-existing Sonar analysis-target-not-found notices.
- Review findings to prioritize before new MyATM functionality:
  1. Normal dust ingestion mutates alert-rule state in memory but does not persist it, allowing repeated alert notifications on later polls; `HasOpenNotification` is implemented but unused.
  2. Catalogue paging increments `$skip` by 100 and continues only for a 100-item response, while the HTTP client’s own vendor API note says the default page size is 50 and the request sends no `$top`; customers with more than one page can be partially imported.
  3. `StoreAccessoryInfo` is exposed on the service and documented as daily work, but it is not registered in `MonitorJobRunner`, `SupportedJobNames`, or scheduler configuration, so it cannot run in the supported modes.
  4. Measurement/accessory incremental processing assumes strictly newest-first vendor arrays, breaks on the first old item, and records element zero as the watermark; an unordered or mixed response can silently omit newer records.
  5. Eight-hour rule processing does not advance `Accessed` where an eligible period has no aggregate result, so a single gap can block all later periods for that rule.
- Suggested follow-up scope: first add regression tests and fix the first three functional findings; then make vendor calls async/cancellable with explicit failure semantics, batch DB operations, inject MyATM operational configuration (customer/page size/portal URL), and refresh the stale Azure Functions deployment README.

## Omnidots Veff and VDV Scheduling - 2026-07-14

- `StoreVeffRecords` and `StoreVdvRecords` are now supported one-shot jobs via `--job` or `RVT__MONITOR_JOB`.
- Both jobs use the existing handler implementations through new `OmnidotsService` forwarding methods and pass a 120-minute fetch window.
- Quartz schedules are declared in UTC: Veff at `00:00` every two hours (`0 0 0/2 * * ?`) and VDV fifteen minutes later (`0 15 0/2 * * ?`).
- Focused dispatch/schedule tests passed (3); the complete Omnidots suite passed (90); `dotnet build omnidotsmonitor/omnidotsmonitor.sln --no-restore` and `git diff --check` passed.

## AirQ Import API Ingress Contract - 2026-07-14

- AirQ's import API is POST-only at `/store-noise-levels-for-date` and requires the `X-Api-Key` header with JSON `{ "date": "yyyy-MM-dd" }`.
- Base Docker Compose does not publish an AirQ host port; other Compose services reach it at `http://airqmonitor-api:8080`.
- The required secret name is `RVT__MONITOR_API_KEY`. No secret value is stored in the repository.

## Local PostgreSQL Integration Fixtures - 2026-07-14

- PostgreSQL integration fixtures now resolve their connection string in this order:
  1. `RVT__POSTGRES_INTEGRATION_CONNECTION` environment variable (required for CI and takes precedence).
  2. Ignored `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/appsettings.Development.json`, copied to each test output as `rvt-integration.appsettings.Development.json` by root `Directory.Build.targets`.
- The ignored development settings file uses the flat key `RVT__POSTGRES_INTEGRATION_CONNECTION`. It must point to the local Timescale/PostgreSQL instance and is never committed. Monitor app development settings use the flat `ConnectionStrings__DefaultConnection` key.
- Fixtures continue to create a unique `rvt_integration_{guid}` schema, set it as `SearchPath`, execute each project's PostgreSQL setup/reset SQL in that schema, and drop only that generated schema at cleanup. The local database role therefore needs database `CREATE` permission; no live-schema test connection is shared with the fixture lifecycle.
- `Directory.Build.targets` copies the ignored fixture settings only for test projects and only when that local file exists, so CI remains explicit and fails closed without its environment variable.
- PostgreSQL test corrections discovered during the local run:
  - MyAtm notification test now uses `ReadMonitor(serialId)` instead of the fleet-filtered monitor list.
  - AirQ's PostgreSQL eight-hour average test expects the intentional late-sample upsert (`30`, three samples).
  - Omnidots contact setup uses its written monitor IDs; trace persistence compares sample tuples as an unordered set because `omnidots_trace` has no sequence column.
- Verification completed sequentially against local Timescale: shared fixture 6, MyAtm 78, AirQ 104, Omnidots 87, Svantek 75 tests passed. `dotnet build rvt-monitors.sln --no-restore` passed with 0 warnings and 0 errors.

## Generic Blob Storage Port - 2026-07-13

- Active branch: `main`.
- The generic blob-storage work was merged into `main` on 2026-07-13 through merge commit `d53d5d8` (`merge: add generic blob storage adapters`) and pushed to `origin/main` at `2d00c16`. Generated `.superpowers` task reports were removed immediately afterward in `b6b8473`; merged feature branches were deleted locally and from `origin`.
- `rvt-monitor-common` now exposes `IBlobStorageService` with local-file, Azure Blob, and S3 adapters. Local storage is the default and writes under `/data/rvt/blobs/audiofiles` unless configured otherwise.
- `BlobObjectName` rejects traversal, rooted, Windows drive-rooted, and separator-escape names. The local adapter additionally validates configured container/prefix paths and rejects existing reparse points below `LocalRoot`.
- Azure and S3 remain opt-in. Secrets are not tracked; Azure supports connection string or managed-identity service URI, and S3 uses the SDK credential chain.
- Svantek recording import now injects `IBlobStorageService`, uploads WAVs as `{notificationId}.wav` with `audio/wav`, and writes that same object key to `RecordingLink`.
- Docker Compose creates and mounts `svantek-audiofiles` at `/data/rvt/blobs` for the Svantek container. `README.md` and `docs/container-builds.md` document Local, Azure Blob, and S3 settings.
- Focused verification passed: 34 BlobStorage tests and 2 Svantek sound-recording tests. `docker compose config` passed. The full Svantek suite remains blocked by an existing SQL Server Testcontainers readiness failure in `TestDBClient` (31 fixture-dependent tests); 38 non-DB tests passed.

## Common Runtime Namespace Migration - 2026-07-13

- Removed the misleading `rvt-monitor-common/Rvt.Monitor.Common/LegacyRvtApi/` folder and moved its active runtime code into the common library's domain folders:
  - `Configuration`: `RvtConfig`, `IMonitorRuntimeDefaultsResolver`, and runtime defaults.
  - `Diagnostics`: `AdapterException` and `RvtLogger`.
  - `Communications`: email, SMS, and notification message services.
  - `Mqtt`: MQTT client, message, and event publisher contracts.
  - `Notifications`, `Rules`, `Utilities`, and `Storage`: shared DTOs, rule compatibility types, date helpers, and the obsolete Azure Blob facade.
- Renamed all production and test imports from `Rvt.Api`, `Rvt.Api.Comms`, `Rvt.Api.Mqtt`, `Rvt.Model.Mqtt`, `Rvt.Notification`, `Rvt.Rules`, and `Rvt.Util` to the corresponding `Rvt.Monitor.Common.*` namespaces. No forwarding aliases remain.
- Added `SharedRuntimeNamespaceTests`, which prevents old runtime namespaces from reappearing in the common assembly. Renamed the previous legacy-namespace compatibility tests to `SharedRuntimeCompatibilityTests`.
- Verification: `dotnet build rvt-monitors.sln --no-restore` passed with 0 warnings and 0 errors. Tests passed: Common 111, MyAtm non-database 52, AirQ non-database 71, Omnidots non-database 72, Svantek non-database 41. The PostgreSQL fixture tests require `RVT__POSTGRES_INTEGRATION_CONNECTION`, which was intentionally absent during this run.

## Session Instruction

Start a new session with: "Read project_state.md to get up to speed".

Use the native macOS clone as the active workspace: `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`.

The old Parallels Windows share workspaces at `/Volumes/[C] Windows 11/...` and `/private/tmp/win11c/...` are retired fallbacks only. Do not remount or edit them for normal project work; sync through GitHub instead.

## Current Status

The active project has moved to the native macOS clone at `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`.

Local `main` has been synced from GitHub through `origin/main` at `97819cd` (`feat: add testlocal monitor filters`). The EF Core database-first monitor data access migration and subsequent testlocal/PostgreSQL hardening work are on `main`.

## Client/Audit Release Process - 2026-07-07

- Added a repeatable curated release process for publishing RVT Monitors to `RVT-Group-LTD/rvt-monitors`.
- Root `README.md` now describes the package contents, monitor apps, shared infrastructure, configuration, build/test commands, Docker usage, testlocal runs, observability, and release-package exclusions.
- Release policy and runbook:
  - `docs/release/client-release-exclusions.txt`
  - `docs/release/client-release-runbook.md`
  - `docs/superpowers/plans/2026-07-07-rvt-monitors-client-release.md`
- Release scripts:
  - `scripts/export-client-release.sh` builds a curated package from Git-tracked files only, applies explicit exclusions, writes `RELEASE_MANIFEST.txt`, and fails if blocked files remain.
  - `scripts/publish-client-release.sh` regenerates the export, clones the client repository, replaces the requested release branch with the curated payload, commits, and pushes.
- Client payload excludes internal agent/session state and release mechanics including `AGENTS.md`, `project_state.md`, `docs/superpowers/**`, `docs/monitor-data-access-migration.md`, `docs/release/**`, `.codegraph/**`, local secrets, private key files, and generated output.
- Local export verification passed: `/private/tmp/rvt-monitors-client-release` copied 421 tracked files and generated a 422-line manifest including `RELEASE_MANIFEST.txt`; blocked-path scan returned no files.
- Source verification before publishing:
  - `git diff --check` passed.
  - `dotnet test rvt-monitors.sln --no-build` passed: 403 tests.
- Source process commits pushed to `origin/main`:
  - `d4e7ec8` `chore: add client release export process`
  - `12143ff` `fix: support empty client release repositories`
- Published curated package to `https://github.com/RVT-Group-LTD/rvt-monitors.git` branch `release-candidate`.
- Target repository was empty on the first publish, so the publisher created `release-candidate` as an orphan release branch. The first RC commit `76cbedb` was superseded because it still included `docs/monitor-data-access-migration.md`, which contains internal Plane/work-item planning details.
- The publisher now creates fresh orphan release history by default so excluded files are not retained in release-branch history.
- Final target commit: `35fa748` `Deploy RVT monitors release candidate`.
- Fresh final verification clone: `/private/tmp/rvt-monitors-client-verify-final`.
- Target verification passed:
  - blocked-path scan found no `AGENTS.md`, `project_state.md`, `docs/superpowers/**`, `docs/monitor-data-access-migration.md`, `docs/release/**`, `.codegraph/**`, development appsettings, local settings, `.env`, or private key/certificate files.
  - required files exist: `README.md`, `rvt-monitors.sln`, `docker-compose.yml`, `observability/README.md`.
  - `docs/` contains only `container-builds.md`, `monitor-timer-triggers.md`, `quartz-monitor-scheduling.md`, and `sonarqube.md`.
  - remote `RELEASE_MANIFEST.txt` has 421 lines.
  - release branch history contains one commit: `35fa748`.

Current local `main` includes:

- Azure Functions removal and ASP.NET Core minimal API container support.
- Quartz monitor scheduling support.
- EF Core shared monitor data model and provider-aware mapping infrastructure.
- EF Core-backed data access for MyAtm, AirQ, Omnidots, and Svantek monitor clients.
- Mapperly-backed DTO/entity mapping inside each monitor app, with narrow CQRS-lite query/command interfaces layered over the `IDBClient` compatibility facade.
- SQL identifier whitelisting for the remaining raw SQL paths that still need to stay temporarily.

Git integration notes:

- Feature branch `codex/ef-core-monitor-data-access` was pushed to `origin`.
- Local `main` has been fast-forwarded to `origin/main`.
- Continue to fetch/pull before work and commit/push completed changes from the native macOS clone.

## Workspace

- Active path: `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`
- Retired Windows-share paths:
  - `/Volumes/[C] Windows 11/Users/oldgeorge/source/repos/chris-oldgeorge/rvtmonitors-new`
  - `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtmonitors-new`
- Shell: `zsh`
- Timezone: `Europe/Athens`
- Current date: `2026-07-02`

## Repository Structure

- `airqmonitor/`
  - `AirQMonitor/` AirQ monitor app with one-shot, Quartz, and minimal API modes
  - `AirQMonitorTests/` test project and SQL Server fixture data
- `myatmmonitor/`
  - `MyAtmMonitor/` MyAtm monitor app with one-shot, Quartz, and minimal API modes
  - `MyAtmMonitorTests/` test project and SQL Server fixture data
- `omnidotsmonitor/`
  - `OmnidotsMonitor/` Omnidots monitor app with one-shot, Quartz, webhook, and minimal API modes
  - `OmnidotsMonitorTests/` test project, test data, and manual test scripts
- `svantekmonitor/`
  - `SvantekMonitor/` Svantek monitor app with one-shot, Quartz, and minimal API modes
  - `SvantekMonitorTests/` test project and SQL Server fixture data
- `rvt-monitor-common/`
  - `Rvt.Monitor.Common/` shared monitor infrastructure, scheduling, provider-aware DB helpers, and EF model infrastructure
  - `Rvt.Monitor.CommonTests/` shared library tests
- `docs/`
  - Container, Quartz, timer inventory, EF design, EF implementation plan, and schema inventory docs

## EF Core Migration State

Completed:

- Added common EF Core provider packages and shared infrastructure:
  - `MonitorDbContextBase`
  - `MonitorDbContextOptionsFactory`
  - `MonitorModelBuilderExtensions`
  - `MonitorModelCacheKeyFactory`
  - shared entity classes under `Rvt.Monitor.Common/Data/Entities`
  - aggregate query metadata under `Rvt.Monitor.Common/Data/Queries`
- Added provider-aware EF contexts, entities, and aggregate field metadata for all four monitor apps.
- Migrated `DBClient` implementations for MyAtm, AirQ, Omnidots, and Svantek toward EF-backed read/write paths while preserving existing `IDBClient` contracts.
- Kept SQL Server and PostgreSQL/Timescale mapping support through provider-aware table and column mappings.
- Added EF metadata/mapping tests for common and monitor-specific models.
- Hardened remaining raw SQL identifier usage with `MonitorDb.RequireMappedSqlIdentifier` and `MonitorDb.RequireSafeSqlIdentifier`.

Still outstanding:

- Add live PostgreSQL/Timescale smoke or integration tests once a safe schema-read connection string is available.
- Continue removing temporary `DBUtil`/ADO.NET paths after parity is verified.
- Review and reduce existing nullability/MSTest analyzer warnings in monitor projects.
- Update deployment/runbook docs after production configuration decisions for EF provider selection and connection strings are final.

## Documentation State

- EF design: `docs/superpowers/specs/2026-06-20-ef-core-database-first-design.md`
- EF implementation plan: `docs/superpowers/plans/2026-06-20-ef-core-database-first-monitor-data-access.md`
- SQL Server schema inventory: `docs/superpowers/schema/2026-06-20-sqlserver-table-inventory.md`
- Timescale schema inventory placeholder: `docs/superpowers/schema/2026-06-20-timescale-table-inventory.md`
- Project-facing migration summary and Plane cycle payload: `docs/monitor-data-access-migration.md`

## Plane Cycle Status

Requested cycle: `Monitor EF Core Migration Hardening`.

Created in Plane on 2026-06-22:

- Project: `RVT Group Foundation` (`RVTGR`)
- Workspace: `rvt-group`
- Project ID: `1eff77df-acf1-4f43-a8b7-ce257cc2a10a`
- Cycle ID: `b3babc54-fe25-439d-8620-b67a70418950`
- Work items created and linked:
  - `RVTGR-393` `[MMH.1] Add live Timescale schema smoke tests`
  - `RVTGR-394` `[MMH.2] Verify EF mappings against Timescale`
  - `RVTGR-395` `[MMH.3] Remove remaining temporary ADO.NET data access paths`
  - `RVTGR-396` `[MMH.4] Close dynamic SQL and identifier safety gaps`
  - `RVTGR-397` `[MMH.5] Add provider selection and connection-string runbook`
  - `RVTGR-398` `[MMH.6] Run full solution and container verification`
  - `RVTGR-399` `[MMH.7] Reduce existing analyzer and nullability warnings`
  - `RVTGR-400` `[MMH.8] Finalize migration cleanup documentation`

## Verification Commands Run

Fresh verification after the final whitespace cleanup and before commit:

- `git diff --check` passed.
- `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj` passed: 44 tests.
- `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj` passed: 66 tests, with existing warnings.
- `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj` passed: 99 tests, with existing warnings.
- `dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj` passed: 79 tests.
- `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj` passed: 54 tests.

## Notes For Next Session

- Use `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors` as the working directory.
- Prefer GitHub sync over filesystem copy or SMB remounts when moving work between environments.
- Do not inspect Docker container environment variables for Timescale credentials. Use existing app configuration, an explicitly provided temporary connection string, or a secure secret flow.
- The native macOS clone avoids the SMB AppleDouble `._*` Docker build-context failures that occurred on the mounted Windows share.
- Treat EF Core plus app-local Mapperly mappers plus CQRS-lite query/command interfaces as the preferred design direction for monitor data access changes.

## Svantek Local Demo Secret State - 2026-06-30
- SvantekMonitor.csproj now has a UserSecretsId for project-level .NET user-secrets; no secret value is committed.
- Development secret RVT__SVANTEK_API_KEY is present for the Svantek project.
- Local container runs use /private/tmp/svantek_testlocal.env, chmod 600, with RVT__SVANTEK_API_KEY plus testlocal=true.
- Docker swarm secrets are unavailable because this daemon is not a swarm manager; standalone docker run should use --env-file.
- Relevant variables: RVT__SVANTEK_API_KEY, testlocal, RVT__MONITOR_JOB, RVT__DATABASE_PROVIDER, ConnectionStrings__DefaultConnection.
- Next session: read project_state.md to get up to speed.

- Added svantekmonitor/SvantekMonitor/postgres/2026-06-30-add-status-telemetry-columns.sql for missing svantek_monitor_status telemetry columns and svantek_error_message.
- Applied the same patch to the local rvt Timescale/PostgreSQL container.
- Svantek deployment mapping now ignores PostgreSQL What2words and maps What3Words to deployment.what_3_words.

## Svantek Postgres Schema Patch - 2026-06-30
- Project patch file: svantekmonitor/SvantekMonitor/postgres/2026-06-30-add-status-telemetry-columns.sql.
- Local Postgres now has the expected svantek_monitor_status telemetry columns and svantek_error_message table.
- Svantek maps deployment.what_3_words for PostgreSQL and ignores absent What2words.
- Svantek Docker runtime image installs libgssapi-krb5-2.
- Demo reset helper svantekmonitor/SvantekMonitor/postgres/2026-06-30-reset-demo-monitor-157206.sql rewinds Noise - E125V - 157206 to 2026-03-15 20:00:00, a Svantek API window confirmed to return samples.

## Verification Update - 2026-07-01
- Fixed svantekmonitor/SvantekMonitor/postgres/2026-06-30-add-status-telemetry-columns.sql so ix_svantek_error_message_error_time is a complete PostgreSQL index on public.svantek_error_message(error_time).
- Strengthened SvantekPostgreSqlSchemaPatchTests to assert the full index target clause, after first observing the test fail against the truncated SQL.
- Normalized touched files back to LF line endings; `git diff --check` passes.
- Historical Windows-share note: removed generated macOS SMB AppleDouble `._*` sidecar files after Docker build context packaging failed on svantekmonitor/SvantekMonitor/._Dockerfile. This should not be needed in the native macOS clone.
- Docker Desktop was started locally; full Testcontainers-backed monitor test suites then passed sequentially:
  - `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj`: 44 passed.
  - `dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`: 66 passed.
  - `dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj`: 99 passed.
  - `dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`: 79 passed.
  - `dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`: 61 passed.
- `docker build -f svantekmonitor/SvantekMonitor/Dockerfile -t rvt/svantekmonitor:local .` passed.

## Provider Default Decision - 2026-07-01
- PostgreSQL/Timescale is the intended default provider for all monitor apps.
- Shared provider resolution defaults to PostgreSQL when `RVT__DATABASE_PROVIDER` and `DatabaseProvider` are both unset.
- AirQ, MyAtm, Omnidots, and Svantek `RvtConfig.DATABASE_PROVIDER` defaults are `PostgreSql`.
- `docker-compose.yml` sets `RVT__DATABASE_PROVIDER=PostgreSql` explicitly for each monitor API service.
- SQL Server remains supported and is still selected explicitly in SQL Server-backed Testcontainers fixtures with `RVT__DATABASE_PROVIDER=SqlServer`.

## Omnidots Vibration Testlocal - 2026-07-01
- Implemented Svantek-style `testlocal=true` support for the Omnidots vibration monitor.
- Target demo monitor: `Vibration - R17222V-QUCILO - 14768`.
- `OmnidotsTestLocalMonitorFilter` filters catalog writes by serial `14768` and database monitor reads by serial `14768` plus fleet `R17222V-QUCILO`.
- `OmnidotsApi` accepts a test-local constructor flag and defaults it from `RvtConfig.TESTLOCAL`.
- Omnidots StorePeak, StoreVeff, StoreVdv, battery, traces, monitoring, and offline paths now route monitor reads through the filtered `ReadMonitors` helper.
- Operational doc: `docs/container-builds.md` has the Omnidots vibration local demo warning and implementation notes.
- Container verification on the retired Windows-share workspace: `docker build -f omnidotsmonitor/OmnidotsMonitor/Dockerfile -t rvt/omnidotsmonitor:testlocal .` passed after removing generated SMB `._*` sidecars.
- Container API smoke: `docker run ... -e testlocal=true ... rvt/omnidotsmonitor:testlocal` passed `/liveness` on port 18083, then the temporary container was stopped.
- Container one-shot `--job StoreMonitors` with `testlocal=true` reached Omnidots auth and failed with `Token invalid`; no local Omnidots env file or Omnidots user-secrets are currently configured, so a full authenticated StoreMonitors write needs a valid untracked env file containing Omnidots auth and the Postgres connection string.

## MyAtm Dust Testlocal - 2026-07-01
- Implemented Svantek-style `testlocal=true` support for the MyAtm dust monitor.
- Target demo monitor: `Dust - R6025V - 21972`.
- `MyAtmTestLocalMonitorFilter` filters catalog writes by serial `21972` and database monitor reads by serial `21972` plus fleet `R6025V`.
- `MyAtmApi` accepts a test-local constructor flag and defaults it from `RvtConfig.TESTLOCAL`.
- MyAtm StoreDust, StoreAccessoryInfo, offline, clear-offline, and serial-specific ProcessDustLevels paths now constrain to the target when `testlocal=true`.
- Operational doc: `docs/container-builds.md` has the MyAtm dust local demo warning and implementation notes.
- Container verification on the retired Windows-share workspace: `tar --exclude='._*' ... | docker build -f myatmmonitor/MyAtmMonitor/Dockerfile -t rvt/myatmmonitor:testlocal -` passed because direct Docker context packaging hit SMB AppleDouble xattr errors there. In the native macOS clone, use direct `docker compose build`.
- Container API smoke: `docker run ... -e testlocal=true ... rvt/myatmmonitor:testlocal` passed `/liveness` on port 18082, then the temporary container was stopped.
- Container one-shot `--job StoreMonitors` was not run because no local MyAtm env file was present in `/private/tmp`; a full authenticated StoreMonitors write needs a valid untracked env file containing `RVT__MYATM_TOKEN`, `testlocal=true`, `RVT__DATABASE_PROVIDER=PostgreSql`, and `ConnectionStrings__DefaultConnection`.

## MyAtm Dust Secret and Container Run - 2026-07-01
- MyAtm API key for `RVT Test` is saved locally in ignored `myatmmonitor/MyAtmMonitor/appsettings.Development.json`; the key value is intentionally not recorded in project_state.md.
- `.gitignore` and `.dockerignore` now exclude `appsettings.Development.json` / `appsettings.development.json` files so local secrets are not staged or baked into Docker images.
- `RvtConfig` now falls back to `appsettings.{environment}.json` after environment variables, so container/app secrets still take precedence.
- MyAtm Docker runtime image now installs `libgssapi-krb5-2`, matching the Svantek fix required by Npgsql in Debian-based runtime images.
- Temporary container env file: `/private/tmp/myatm_testlocal.env`, chmod 600, with `RVT__MYATM_TOKEN`, `RVT__MONITOR_JOB=StoreMonitors`, `RVT__DATABASE_PROVIDER=PostgreSql`, local loopback Postgres connection string, and `testlocal=true`.
- Rebuilt `rvt/myatmmonitor:testlocal` from a filtered tar context excluding the development appsettings secret.
- Authenticated container one-shot passed: `docker run --rm --network container:rvt-timescaledb --env-file /private/tmp/myatm_testlocal.env rvt/myatmmonitor:testlocal` exited 0.
- Local Timescale verification: `public.monitor` contains the target dust monitor row `serial_id=21972`, `fleet_row_count=R6025V`, `type_of_monitor=0`.

## Native macOS Workspace Cutover - 2026-07-02
- Fast-forwarded the native macOS clone from GitHub `origin/main`, moving local `main` from `b6761bf` to `97819cd`.
- The old `/private/tmp/win11c/.../rvtmonitors-new` path no longer contained the previous git working copy during this check, so future work should not depend on that mount.
- Project instructions now make `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors` the canonical active project.

## Generic Blob Storage Port Task 3 - 2026-07-13
- Active branch: `codex/generic-blob-storage-port`.
- `LocalFileBlobStorageService` now preserves lexical `LocalRoot` containment and rejects existing reparse points in all target-path components below `LocalRoot`; it checks before target directory creation, again afterward, and before delete.
- `LocalFileBlobStorageServiceTests` has a physical temporary-directory symlink regression test for both an intermediate directory symlink and an existing target-file symlink. It uses MSTest inconclusive only when Windows cannot create symlinks without privilege.
- Focused verification command: `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter LocalFileBlobStorageServiceTests`; passed 14 tests, 0 failed, 0 skipped on this macOS workspace.
- Docker compose uses relative build contexts, so running `docker compose build` from the native macOS clone builds the local images without SMB sidecar workarounds.
- Verified `docker compose config` resolves all monitor build contexts to `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors`.
- Verified `docker compose build` succeeds from the native macOS clone and rebuilt:
  - `rvt/airqmonitor:local` `cd6924a2d394`
  - `rvt/myatmmonitor:local` `004e60d4f438`
  - `rvt/omnidotsmonitor:local` `38723cb2467b`
  - `rvt/svantekmonitor:local` `f954cacf28c2`

## Observability Stack Recovery - 2026-07-02
- Recovered the local OpenTelemetry container stack from the Windows VM repo at `/private/tmp/win11c/Users/oldgeorge/source/repos/chris-oldgeorge/rvtmonitors-new`.
- Added it to the macOS project as the new `observability/` subfolder, including:
  - `observability/docker-compose.observability.yml`
  - `observability/docker-compose.monitors-observed.yml`
  - `observability/otel-collector-config.yml`
  - Grafana datasource/dashboard provisioning
  - Prometheus, Tempo, and Loki config
  - `observability/README.md`
- Operational verification from the macOS clone passed with:
  - `docker compose --project-directory . -f observability/docker-compose.observability.yml config`
  - `docker compose --project-directory . -f docker-compose.yml -f observability/docker-compose.observability.yml -f observability/docker-compose.monitors-observed.yml config`
  - `docker compose --project-directory . -f observability/docker-compose.observability.yml up -d`
- Verified endpoints:
  - Grafana health `http://localhost:3000/api/health` returned 200.
  - Prometheus readiness `http://localhost:9090/-/ready` returned 200.
  - Tempo readiness `http://localhost:3200/ready` returned 200.
  - Loki readiness `http://localhost:3100/ready` returned 200 after its initial ingester startup delay.
  - Collector metrics `http://localhost:9464/metrics` returned 200.
  - Grafana provisioned Prometheus, Loki, and Tempo datasources plus the three RVT dashboards.
- Note: the recovered subfolder is the container stack. The current macOS `main` branch still needs the monitor app-side OpenTelemetry instrumentation from the old `codex/open-telemetry-monitors` work or equivalent before monitor containers emit the custom job telemetry into the stack.

## Testlocal Suite Scope - 2026-07-03
- Added `scripts/run-testlocal-suite.sh` as the repo-local operational runner for already-running monitor API containers.
- AirQ is intentionally excluded from the testlocal suite for now because it does not yet have a single-monitor `testlocal=true` filter. Running AirQ `StoreNoiseLevels` currently loops across all active Turnkey noise monitors and records broad `Invalid request!` rows.
- Current included jobs:
  - MyAtm: `StoreMonitors`, `StoreDustLevels`
  - Omnidots: `StoreMonitors`, `StorePeakRecordsLastDataTime`, `StoreTraces`
  - Svantek: `StoreMonitors`, `StoreNoiseLevels`

## Infrastructure Mode Setting - 2026-07-03
- Added shared `Infrastructure` config support with allowed values `local` and `azure`.
- `Infrastructure=local` is the default and means an always-on container may initialize Quartz when `MonitorScheduler__Enabled=true`.
- `Infrastructure=azure` means the process is an Azure Container Apps Job; one-shot jobs still run through `--job` or `RVT__MONITOR_JOB`, but Quartz scheduler registration is suppressed even if `MonitorScheduler__Enabled=true`.
- AirQ, MyAtm, Omnidots, and Svantek `Program.cs` now use the shared effective scheduler helper instead of reading `MonitorScheduler:Enabled` directly.
- Root and observability Docker Compose files set `Infrastructure=local` for the local API containers.
- Runbook updates are in `docs/quartz-monitor-scheduling.md` and `docs/container-builds.md`.

## SonarQube Setup - 2026-07-03
- Added root solution `rvt-monitors.sln` for whole-repository SonarQube analysis.
- The solution includes all monitor apps, all monitor test projects, and `Rvt.Monitor.Common` plus tests.
- Added `scripts/create-sonarqube-project.sh` to create/check the SonarQube project by API with:
  - `SONAR_HOST_URL`, default `http://localhost:9000`
  - `SONAR_PROJECT_KEY`, default `rvt-monitors`
  - `SONAR_PROJECT_NAME`, default `RVT Monitors`
  - `SONAR_TOKEN`, required and not committed
- Added `scripts/run-sonarqube-analysis.sh` to run `dotnet sonarscanner` over `rvt-monitors.sln`; it supports optional `SONAR_ORGANIZATION` for SonarCloud.
- Added `docs/sonarqube.md` with local SonarQube and SonarCloud run instructions.
- `.sonarqube/` is ignored as scanner-generated output.

## Preferred Monitor Design Direction - 2026-07-06
- `main` now uses the same data-access design direction across MyAtm, AirQ, Omnidots, and Svantek.
- Monitor apps keep EF Core-backed `DBClient` implementations and `IDBClient` as a compatibility facade while API code is routed through narrow CQRS-lite interfaces:
  - monitor queries/commands
  - measurement commands
  - rule and notification queries
  - operational commands for exception logging, notification writes, audit writes, and rule state updates
- Mapperly is the preferred tool for simple DTO/entity conversion inside monitor app projects only. It should stay analyzer-only with `PrivateAssets="all"` and `OutputItemType="Analyzer"`.
- `rvt-monitor-common` should remain free of Mapperly and monitor-specific mapping policy.
- Keep manual code for vendor API JSON parsing, notification/rule state machines, aggregate field selection, and other business logic that is not straightforward DTO/entity mapping.
- Maintain architecture tests that prevent broad API-layer `dbClient.` usage and protect Mapperly dependency boundaries.

## SonarCloud Security Remediation - 2026-07-03
- SonarCloud project `aileron-forward_rvt-monitors` was scanned successfully.
- Security findings pulled from SonarCloud included:
  - 4 Docker runtime-user vulnerabilities on monitor Dockerfiles.
  - 4 committed MQTT private-key vulnerabilities under AirQ/MyAtm cert folders.
  - 8 regex timeout security hotspots in `Rvt.Monitor.Common/Data/MonitorDb.cs`.
- Remediation completed and verified:
  - Docker runtime stages now use `USER $APP_UID`.
  - Committed MQTT cert/key files were removed and `**/certs/*.key`, `*.p12`, `*.pfx`, and `*.pem` are ignored.
  - MQTT clients now require `RVT__MQTT_CERTIFICATE_PATH` and `RVT__MQTT_PRIVATE_KEY_PATH` when `RVT__MQTT_ENABLED=true`.
  - MQTT client identity/hostname can be configured with `RVT__MQTT_HOSTNAME`, `RVT__MQTT_CLIENT_ID`, and `RVT__MQTT_USERNAME`.
  - Shared SQL rewriting regexes now use a one-second match timeout.
- Post-remediation validation:
  - `git grep` found no committed private keys, removed MQTT key filenames, or MQTT certificate-validation bypasses.
  - `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj` passed: 49 tests.
  - `dotnet build rvt-monitors.sln` passed with 0 warnings/errors outside Sonar analyzer warnings.
  - `docker compose build omnidotsmonitor-api svantekmonitor-api airqmonitor-api myatmmonitor-api` passed and rebuilt all four local monitor images.
  - SonarCloud analysis completed successfully for `aileron-forward_rvt-monitors`; API checks reported 0 open vulnerabilities and 0 open security hotspots.
  - SonarCloud quality gate remains red because new-code coverage is 0.0% and duplicated-lines density is 62.4%; security rating and hotspot review conditions are OK.

## SonarCloud Reliability Remediation - 2026-07-03
- Checkpoint commit before reliability fixes: `260c2fc` (`chore: add sonar and monitor hardening`).
- Initial SonarCloud reliability review reported 388 open bugs:
  - 365 empty structured logging placeholders (`csharpsquid:S6674`).
  - 9 invalid composite format strings (`csharpsquid:S2275`).
  - 14 single-iteration loop patterns (`csharpsquid:S1751`).
- Remediation completed across AirQ, MyAtm, Omnidots, and Svantek:
  - Replaced empty Serilog-style placeholders with unique named placeholders per log message.
  - Fixed invalid `string.Format` placeholders and malformed format strings.
  - Changed confirmed single-row reader loops from `while (reader.Read())` to `if (reader.Read())`, while preserving list-building loops.
- Local verification passed:
  - `git diff --check`
  - `dotnet build rvt-monitors.sln`
  - `dotnet test rvt-monitors.sln --no-build`
- SonarCloud analysis `28b2a27f-982a-4a56-8c6e-8725c271a6ae` completed successfully with:
  - `bugs=0`
  - `reliability_rating=1.0` (A)
  - `new_bugs=0`
  - `new_reliability_rating=1.0` (A)
- SonarCloud quality gate remains red only for new-code coverage (`0.0%`) and duplicated-lines density (`27.6%`) at this checkpoint.

## SonarCloud Maintainability Remediation - 2026-07-03
- Addressed the smaller high/medium maintainability findings before the larger cognitive-complexity refactor pass:
  - Replaced `StringAssert.Contains` with MSTest `Assert.Contains` in common and Svantek tests.
  - Replaced literal boolean assertions with `Assert.IsTrue` / `Assert.IsFalse`.
  - Aligned monitor interface and implementation parameter names for AirQ, MyAtm, Omnidots, and Svantek.
  - Converted monitor one-shot runner error output to await `Console.Error.WriteLineAsync`.
  - Changed `MonitorSchedulerOptions.EnabledJobs` from a collection-copying property into `GetEnabledJobs()`.
  - Expanded the Svantek text-boolean converter nested ternary into explicit control flow.
  - Fixed the AirQ HTTP header log-template placeholder names.
- Local verification passed:
  - `git diff --check`
  - `dotnet build rvt-monitors.sln`
  - `dotnet test rvt-monitors.sln --no-build`
- SonarCloud analysis `70f961ac-696d-4268-b6db-f8c65606be26` completed successfully with:
  - `code_smells=776`, down from 832.
  - `sqale_index=2630`, down from 2867.
  - High-impact maintainability findings reduced from 50 to 26.
  - `sqale_rating=1.0` (A) and `new_maintainability_rating=1.0` (A).
- Remaining high-impact maintainability work is almost entirely `csharpsquid:S3776` cognitive-complexity refactors across monitor processing methods, plus one Python duplicate-literal warning in `svantekmonitor/Documents/projectsGetData.py`.
- SonarCloud quality gate remains red because new-code coverage is `0.0%` and duplicated-lines density is `27.0%`; maintainability, reliability, and security conditions are OK.

## SonarCloud Coverage Baseline - 2026-07-03
- Updated `scripts/run-sonarqube-analysis.sh` so `RUN_TESTS=true` collects OpenCover via the existing `coverlet.collector` test project references and passes `sonar.cs.opencover.reportsPaths` to the scanner.
- Coverage reports are written under `TestResults/coverage/<timestamp>/`; the script now fails if coverage import is requested with a non-OpenCover format or no `coverage.opencover.xml` files are generated.
- Documented the coverage-enabled Sonar workflow in `docs/sonarqube.md`.
- Local coverage verification passed with:
  - `dotnet test rvt-monitors.sln --no-build --collect "XPlat Code Coverage;Format=opencover" --results-directory TestResults/coverage/local-check`
  - 362 tests passed across all five test projects.
  - 5 OpenCover reports were generated.
- SonarCloud analysis completed successfully:
  - CE task `AZ8n0BBDqUs7gWbHIG-n`
  - Analysis `71801e9e-891d-44eb-9b66-0f6bf52b27ff`
  - Scanner imported all 5 OpenCover reports and reported coverage data for 225 main files.
  - Overall coverage is `40.9%`; line coverage is `43.4%`; branch coverage is `28.3%`.
  - New-code coverage improved from `0.0%` to `32.7%`.
- SonarCloud quality gate remains red because:
  - new-code coverage is `32.7%`, below the `80%` threshold.
  - new duplicated-lines density is `26.1%`, above the `3%` threshold.
- Security, reliability, and maintainability new-code ratings are all A at this checkpoint.

## SonarCloud Duplication Reduction Baseline - 2026-07-03
- Branch for implementation: `codex-sonar-duplication-reduction`.
- Starting duplication metrics from SonarCloud:
  - overall duplicated-lines density `24.2%`
  - new-code duplicated-lines density `26.1%`
  - duplicated lines `6173`
  - duplicated blocks `307`
  - duplicated files `88`
- Highest-impact duplicated families are monitor rule processing, copied `api/rvt-common` runtime helpers, legacy `DBUtil`, monitor DB clients, and small DTO/model clones.
- Implementation plan: `docs/superpowers/plans/2026-07-03-sonar-duplication-reduction.md`.

## SonarCloud Duplication Reduction Implementation - 2026-07-03
- Branch in progress: `codex-sonar-duplication-reduction`.
- `rvt-monitor-common` now owns the shared legacy `api/rvt-common` runtime helpers, shared monitor host bootstrap, notification dispatch, and shared noise rule evaluation state machine used by AirQ and Svantek.
- Duplicated monitor-local `api/rvt-common` helper trees and unused legacy `DBUtil` code were removed in earlier commits on this branch.
- AirQ and Svantek rule processing now route alert/caution state transitions, notification writes, contact dispatch, MQTT alert publication, inactive resets, and deleted-rule handling through `Rvt.Monitor.Common.Rules.NoiseRuleEvaluator` and `RuleAlertNotificationDispatcher`.
- `scripts/run-sonarqube-analysis.sh` supports `.NET`-focused local scans with:
  - `SONAR_SCANNER_SCAN_ALL=false` by default.
  - `SONAR_DISABLE_WEB_ANALYSIS=true` to disable JavaScript, TypeScript, YAML-JS, and CSS suffix analysis when the local JS bridge stalls.
  - `dotnet build --no-incremental` so Sonar coverage imports do not reuse stale line mappings after large refactors.
- Latest successful SonarCloud duplication-only scan on this branch reported:
  - overall duplicated-lines density `10.8%`
  - new-code duplicated-lines density `2.84%`
  - duplicated lines `1928`
  - duplicated blocks `84`
  - duplicated files `32`
- Added a common regression test for the shared evaluator so an already-active caution cannot downgrade a previous alert in the same rule batch.
- Final local verification before commit:
  - `git diff --check` passed.
  - `dotnet build rvt-monitors.sln` passed with `0 Error(s)`; local Sonar analyzer warnings remain for pre-existing maintainability work.
  - `dotnet test rvt-monitors.sln --no-build` passed with 381 tests:
  - Common 68
  - MyAtm 70
  - Omnidots 83
  - Svantek 61
  - AirQ 99
- A coverage-bearing Sonar pass after the rule extraction was attempted but canceled because the `dotnet test` coverage collectors stalled. Use normal local build/test verification for this checkpoint, or rerun coverage one project at a time before another coverage import.

## SonarCloud Reliability Quality Cleanup - 2026-07-03
- Fresh SonarCloud analysis `92bd650a-9858-4822-bc4b-8139cdf880d8` reported:
  - `bugs=0`
  - `new_bugs=0`
  - reliability rating `A`
  - 11 open issues under the newer `RELIABILITY` software-quality impact filter.
- Fixed all 11 reliability-impact issues:
  - 9 logging calls now pass exceptions via the `ILogger` exception overload instead of as template values.
  - 2 Azure Blob container calls now await `ExistsAsync` and `CreateAsync`.
- Local verification before commit:
  - `git diff --check` passed.
  - `dotnet build rvt-monitors.sln` passed with `0 Warning(s), 0 Error(s)`.
  - `dotnet test rvt-monitors.sln --no-build` passed with 381 tests:
    - Common 68
    - MyAtm 70
    - Omnidots 83
    - Svantek 61
    - AirQ 99

## DI Composition Root And Lazy MQTT Connect - 2026-07-12
- Completed the "last mile" of the ports-and-adapters migration: monitor services are now composed through the host DI container instead of manual `new` wiring inside service constructors.
- `MonitorHost.RunAsync` gained an optional `configureServices` hook (applied in one-shot, API, and Quartz host paths) and now passes `IServiceProvider` (instead of `ILoggerFactory`) to the one-shot job runner.
- Each monitor app has a composition root (`AirQMonitorServices`, `MyAtmMonitorServices`, `OmnidotsMonitorServices`, `SvantekMonitorServices`) registering `IHttpClient`, `IDBClient`, `IMqttClient`, `IMessageService`, the vendor API class, and the service class as singletons. The startup-failure DB error write and `RvtLogger.CreateLogger` ordering are preserved inside the service factory.
- `XxxService` constructors now take the injected `XxxApi`; `MonitorJobRunner.RunAsync` takes the service; Quartz dispatchers take the service via DI (parameterless ctor retained only for schedule validation).
- Removed the sync-over-async `ConnectAsync().Result` from all four vendor API constructors. `RvtMqttClient` now connects lazily (semaphore-guarded, `IsConnected` double-check) on first publish; `MQTT_ENABLED=false` behavior unchanged.
- AirQ `/store-noise-levels-for-date` and Omnidots `/configure-measuring-point` + `/webhook` endpoints resolve the container-managed service via `[FromServices]` instead of constructing one per request.
- Fixed Omnidots logger category copy-paste bug ("AirQService" -> "OmnidotsService"); not referenced by observability dashboards.
- Removed empty local artifact directories `Comms 2`, `Mqtt 2`, `Notification 2`, `Rules 2`, `Storage 2`, `Util 2` under `LegacyRvtApi/`.
- Tests: removed constructor `ConnectAsync` mock setups/verifies (behavior moved into the MQTT adapter); `MonitorHostTests` updated to the new job-runner signature and now covers the `configureServices` hook.
- Local verification:
  - `dotnet build rvt-monitors.sln` passed with `0 Warning(s), 0 Error(s)`.
  - Per-project `dotnet test` passed with 403 tests: Common 72, MyAtm 75, Omnidots 86, Svantek 67, AirQ 103.
  - Solution-level parallel `dotnet test rvt-monitors.sln` currently fails on this machine due to Docker Testcontainers contention (MsSql fixture container conflicts); verified the same failures occur on unmodified `main`, so this is environmental, not a regression. Run test projects sequentially for local verification.

## God-Class Split Into Use-Case Handlers - 2026-07-12
- Split all four vendor API god classes (`SvantekApi`, `AirQApi`, `MyAtmApi`, `OmnidotsApi`, ~4,600 lines of partials) into per-use-case handler classes under each app's `api/UseCases/`, each ctor-injected with only the narrow ports it needs.
- Per monitor, extracted: an `<X>HttpGateway` (`api/http/`) owning vendor HTTP calls and response parsing; an `<X>RuleProcessor` owning rule evaluation/alert dispatch (dispatch-only for Omnidots, whose thresholds evaluate on-device); an `<X>MonitorReader` for the shared (testlocal-filtered where applicable) monitor-list read.
- Each `<X>Api` class is now a thin non-partial facade with the historical public surface (ctors, public methods, `JAN1_1970`, `BatteryAlertType` enums, `ReadRequestBody`, etc.), so `<X>Service`, DI wiring, endpoints, and all tests needed no changes. The facade ctor composes the handler graph.
- Code was moved verbatim; only grep-verified dead code was dropped: Svantek `ProcessRulesV2`/`TruncateByLatestMills`; AirQ `StoreNoiseLevelsOld`/`ProcessRulesOld`/`set8avg`/`getTimePeriodEnd`; MyAtm `StoreMonitors_old`; Omnidots `HasPreviousOfflineNotification`/`ProcessAlarmOld`.
- Local verification: `dotnet build rvt-monitors.sln` 0 warnings/0 errors; per-project `dotnet test` all green (Common 72, Svantek 67, AirQ 103, MyAtm 75, Omnidots 86 = 403). Solution-level parallel test runs remain environmentally flaky (see 2026-07-12 DI checkpoint).

## TimerInfo Removal - 2026-07-12
- Deleted the Azure Functions-era `TimerInfo`/`MyScheduleStatus` classes and parameters from all four monitor services and job runners; only one call site ever used them (`OmnidotsService.StoreTraces`), which now takes the window start (`DateTime since`) directly — the Omnidots job runner passes `DateTime.UtcNow.AddMinutes(-5)`, matching the old `CreateTimerInfo` value.
- Verified: solution build 0 warnings/0 errors; monitor test projects all green (67/103/75/86).

## MQTT Publish Centralization - 2026-07-12
- Added `Rvt.Monitor.Common.Mqtt.IMonitorEventPublisher`/`MonitorEventPublisher`: one shared class that builds `RvtMqttMessage` payloads (timestamp, serialId, optional customerId metadata) and fire-and-forget publishes to `RVT__INSERT_TOPIC` (`PublishDataInserted`) or `RVT__ALERT_TOPIC` (`PublishAlert`), with faulted publishes now logged instead of unobserved.
- Removed all inline `IMqttClient.PublishAsync` + payload-serialization code from the monitors: AirQ/MyAtm/Omnidots insert-publish handlers and the Svantek/AirQ/MyAtm rule processors now depend on `IMonitorEventPublisher`; `NoiseRuleEvaluator` takes the publisher instead of a topic/payload delegate. MyAtm keeps its customerId metadata via the optional parameter.
- Facades construct one `MonitorEventPublisher` per monitor from the injected `IMqttClient`; public surfaces unchanged, and mocked-`IMqttClient` topic/count test assertions still hold since the publisher publishes to the same topics.
- Verified: solution build 0 warnings/0 errors; all test projects green (Common 72, Svantek 67, AirQ 103, MyAtm 75, Omnidots 86 = 403).

## RvtConfig Overhaul And Dead-Code Sweep - 2026-07-12
- Phase 1: moved non-config constants out of `RvtConfig` — `SENT_OK` -> `Rvt.Notification.NotificationConstants`, `OFFLINE_RULE` -> `Rvt.Rules.RuleConstants`, Omnidots protocol strings -> `Omnidots.Model.Dto.OmnidotsProtocol`, Svantek `API_URL_*` -> private consts in `SvantekHttpGateway`; deleted unreferenced `APP_ID`.
- Phase 2: configuration injected into the new classes — `MonitorEventPublisher` takes topics, `OmnidotsHttpGateway` takes Honeycomb credentials, webhook/configure handlers take `OmnidotsWebhookOptions`, MyAtm/Omnidots rule processors take the portal base URL. `RvtConfig` reads now live only in facades/composition roots and LegacyRvtApi internals.
- Phase 3: `MonitorHost.RunAsync` declares the monitor kind via `RvtConfig.ConfigureMonitorKind(monitorName)`; `RVT__MONITOR_KIND` still overrides, assembly-name sniffing remains only as a test fallback. Kind-dependent settings became lazily resolved properties instead of type-load-latched statics.
- Dead-code and obsolete-comment sweep across all projects: deleted commented-out code blocks everywhere (incl. MyAtm's ~65-line legacy rule block, Omnidots' ProcessWebhookOld), dead files (`AirQQueryProcessor`, `NoiseAveragesDto`, `AlertRequest`, Svantek `SmsSender.cs` stale copy, `DataType.cs`, `SvantekResponse.cs`, `MonitorRequest.cs`, Svantek `common/DateTimeUtil.cs`), dead members (`SvantekApi.Process()` + orphaned gateway methods, `Liveness()` on AirQ/MyAtm services, `DELAYED_DATA_PERIOD_MINUTES`, `GetErrorMessages` copies, `MAX_ERROR_COUNT`), and fixed comments contradicting code.
- SECURITY: removed a real MyAtmosphere vendor private API key from a comment in `MyAtmApi.cs`. The key remains in git history and should be rotated with the vendor.
- Known pre-existing bug spotted (not fixed here): MyAtm `CheckForOfflineMonitorsHandler` sets the in-memory `monitor.Offline = true` in the marking-online branch (DB write passes `false` correctly).
- Verification: solution build 0 warnings/0 errors; all test projects green sequentially (Common 72, Svantek 67, MyAtm 75, AirQ 103, Omnidots 86 = 403; Testcontainers fixtures needed the usual rerun under Docker contention).
