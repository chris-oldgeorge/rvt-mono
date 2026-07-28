# Project Architecture and Code Quality Review

**Date:** 2026-07-27

**Baseline branch:** `codex/direct-project-references`

**Scope:** Coding practices, hexagonal architecture conformity, style consistency,
remaining work, and dead or unreachable code.

**Explicit exclusion:** Security analysis.

This document is the authoritative reference for the remaining architecture and
code-quality remediation work identified during the post-adapter-split review.
Update the status checklist as each remediation phase is completed.

All remediation and subsequent code analysis MUST follow the
[RVT Engineering Standards](../development/engineering-standards.md). New and
modified logical units comply immediately; untouched legacy violations are
reduced through the approved ratcheted baseline. Each phase maps its scope to
applicable standard rule IDs and records its baseline delta.

## Executive assessment

The communication and storage provider split is structurally sound. Provider
contracts point inward to dependency-free abstraction projects, provider SDKs
remain in outbound adapter projects, and executable hosts act as composition
roots.

The principal remaining architectural debt is not in the new adapters. It is
concentrated in:

1. legacy monitor facades that aggregate too many narrow ports;
2. the Portal's incomplete transition from a layered application to a
   ports-and-adapters architecture;
3. two divergent reporting implementations with overlapping project and
   namespace ownership;
4. a broad shared monitor package containing application contracts and several
   unrelated infrastructure technologies;
5. architecture tests that still assume the repository layout that existed
   before the monorepo migration; and
6. inconsistent formatting, analyzer, package-management, and test conventions.

The recommended direction is incremental extraction around real infrastructure
boundaries. Small cohesive domain components should not be split into projects
merely to increase the project count.

## Material findings

### 1. Help Admin release decision and runtime behavior disagree — implementation resolved; release gate pending

The Portal release matrix says Help Admin is excluded, but the client still
imports `HelpAdminPanel`, exposes `/admin/help` in navigation, resolves the
route, and renders the panel for administrators.

**Impact:** The documented release surface is not the actual compiled release
surface.

**Resolution:** Shipment was explicitly approved. The complete Help slice now
lives in BCL-only `RvtPortal.Application.Help`, depends on inward-owned
read/write ports, and is implemented by EF adapters under
`RvtPortal.Spa.Adapters.Help`. The controller is an HTTP-only adapter, the
canonical create route is `POST /api/help/admin/articles`, and application plus
HTTP authorization independently protect admin operations.

Assets remain URL metadata only. Persisted rows must pass the HTTPS or
`/help-assets/` policy. The retired SQL artifact was removed. The shared
BCL-only `HelpAssetUrlPolicy` and `RVT.ReleaseAudit help-asset-urls` are the
sole policy/audit authority. Help Admin remains conditional until zero-finding
receipts from that audit exist for every release database; no release-database
receipts were produced during implementation. Each receipt must identify the
environment/database, UTC execution time, application revision, and returned
finding count; exit `10`, `2`, `3`, a missing receipt, or any finding blocks
release. The design is recorded in
`docs/superpowers/specs/2026-07-28-help-asset-url-release-audit-design.md`.
Stable persisted asset IDs and client-only row keys are covered by focused
regressions, and the browser journey covers create, publish, preview, edit,
delete, and Company User denial. Rollback may disable the admin route/endpoints
without disabling published `/help`.

### 2. Reporting has two divergent implementations

Both of these lineages are present in the root solution:

- `apps/monitors/reportingmonitor/Rvt.Reporting.*`
- `services/reporting/src/Rvt.Reporting.*`

They define overlapping Core, Messaging, PDF, and Storage responsibilities but
have already diverged. The monitor lineage uses the new provider-neutral storage
abstraction; the service lineage still owns an Azure-specific storage adapter
and independent messaging/PDF implementations.

**Impact:** Fixes and behavioral changes can be applied to one lineage without
reaching the other. Identically named concepts obscure which implementation is
authoritative.

**Recommendation:** Treat ReportingMonitor's provider-neutral split as the
preferred target. Inventory unique behavior in `services/reporting`, migrate
required behavior, and retire that duplicate lineage. If both are independent
products, rename their assemblies/namespaces and extract only genuinely shared
code.

### 3. Portal has a dual application architecture

`RvtPortal.Application` is a clean, framework-independent application core, but
currently covers mainly the Sites slice. Many classes under
`RvtPortal.Spa/Application` directly depend on EF Core contexts, persistence
entities, ASP.NET Core services, or HTTP factories.

**Impact:** The `Application` folder name suggests a dependency boundary that
does not exist for most Portal use cases.

**Recommendation:** Move one vertical use-case slice at a time into
`RvtPortal.Application`. Define inward-owned ports there and implement those
ports under `RvtPortal.Spa/Adapters`. Keep `RvtPortal.Spa` as the host,
composition root, transport layer, and adapter container during the migration.

### 4. `RVT.Utilities` was retired

Production source analysis found no consumers of `AzureBlobService`. The class:

- reads `appsettings.json` from the current working directory;
- constructs its own Azure clients;
- mixes synchronous and asynchronous SDK calls;
- closes a caller-owned stream;
- catches and rethrows unchanged exceptions;
- contains the misspelled APIs `GetPubliciUri` and
  `GetPubliciArchiveUri`; and
- duplicates responsibilities now represented by the shared object-storage
  ports and adapters.

**Resolution (2026-07-28):** Repository-wide source and project-graph analysis
found no production, reflection, or test consumer that required the
implementation. The project, both project references, both solution entries,
its dedicated test coupling, and deleted-file standards metadata were removed.
External binaries outside this repository remain outside the scope of that
evidence.

### 5. `RVT.BusinessLogic` contains stale dependency declarations

The project references Azure Blobs, configuration binding, HTTP, and logging,
but production source does not use those dependencies. Its Options use is
currently satisfied through a transitive dependency instead of a direct
declaration. The stale `RVT.Utilities` project reference was removed on
2026-07-28.

**Recommendation:** Remove unused packages and project references under build
and test protection. Add the actual direct dependency if the affected time
provider remains in this project, or move the abstraction to the application
core.

### 6. `Rvt.Monitor.Common` remains an infrastructure umbrella

The project combines shared contracts and runtime behavior with ASP.NET Core,
EF Core, Npgsql, MQTT, Quartz, and OpenTelemetry dependencies.

**Recommendation:** Extract infrastructure only when a host needs to select it
independently. Candidate future projects are:

- `Rvt.Database.Postgres`
- `Rvt.Messaging.Mqtt`
- `Rvt.Scheduling.Quartz`
- `Rvt.Observability.OpenTelemetry`

Keep small cohesive domain rules and contracts together.

### 7. Monitor compatibility facades remain broad

`IDBClient` inherits between five and thirteen narrower ports depending on the
monitor. The concrete DB clients range from approximately 560 to 1,250 lines.
MyAtm is furthest through the narrow-port migration. AirQ and Svantek remain the
most dependent on their broad legacy facades.

**Recommendation:** Split implementations along the already defined port
boundaries, retain `IDBClient` only as a temporary compatibility facade, and
remove it once all callers use focused ports.

### 8. Synchronous compatibility and HTTP behavior remain

Active synchronous-over-asynchronous paths remain in the communication bridge,
MQTT wrappers, monitor APIs, and rule processors. AirQ, Omnidots, and Svantek
also retain manually constructed `HttpClient` paths.

**Recommendation:** Migrate callers to asynchronous methods end to end. Use
factory-created or typed clients, propagate cancellation, and remove obsolete
synchronous signatures only after the compatibility allowlist reaches zero.

### 9. Architecture guards now use portable monorepo paths

The MyATM and Svantek R1 repository-reading tests now resolve the checkout
through the shared `Rvt.Monitor.IntegrationTesting.RepositoryLayout` helper.
The helper independently evaluates the test output, `[CallerFilePath]` source,
process current directory, and optional `RVT_MONOREPO_ROOT` candidates. Every
root must contain `Rvt.Mono.slnx` plus either a normal `.git` directory or a
worktree `.git` file. An explicitly configured root is validated without
fallback; physical aliases of one checkout are collapsed, while distinct
checkout candidates are rejected as ambiguous. Each `GetPath` argument must be
one non-empty, non-rooted name with no separator or traversal, and the
normalized result must remain below the canonical root.

**Resolution (2026-07-28):** The normal and deterministic-CI external-artifact
suites both pass: 19/19 helper tests, 38/38 MyATM tests, and 5/5 Svantek tests.
The CI proof sets a validated `RVT_MONOREPO_ROOT`. Its expected bounded
no-environment RED demonstrates fail-closed behavior: compiler `PathMap`
produces a `/_/...` caller-source path and VSTest relocates the current
directory to external artifacts, so discovery fails with actionable
`RVT_MONOREPO_ROOT` guidance instead of inspecting an inferred checkout. The
disposable `tests/verify-r1-architecture-guards.test.sh` worktree harness also
proves that a Mapperly reference in `MyAtmMonitorTests.csproj` and a forbidden
`Rvt.Monitor.Common` package dependency in `MyAtmMonitor.csproj` are rejected
by the intended architecture policies before the baseline is restored.

### 10. Repository style and tooling are inconsistent

Observed inconsistencies include:

- block-scoped and file-scoped namespaces within the same project;
- private fields using `_camelCase`, `camelCase`, and PascalCase;
- historical file headers and dated `pending/current` comments in legacy Portal
  code but not in newer modules;
- MSTest and xUnit without an explicitly documented project boundary;
- central package management in monitor/shared projects and inline versions in
  Portal/reporting projects;
- different analyzer severities by subtree and no common root baseline; and
- very large host and client composition files.

The Portal host `Program.cs` is approximately 630 lines. Several React panels
exceed 1,000 lines, and `App.tsx` is approximately 1,500 lines.

## Project-by-project assessment

| Project | Conformity | Required direction |
|---|---|---|
| `Rvt.Communication.Abstractions` | Strong | Keep dependency-free |
| `Rvt.Communication` | Transitional | Remove synchronous compatibility API after callers migrate |
| `Rvt.Communication.SendGridMail` | Strong adapter | Retain |
| `Rvt.Communication.MicrosoftGraphMail` | Strong adapter | Decompose internals only if responsibilities grow |
| `Rvt.Communication.TransmitSms` | Strong adapter | Retain |
| `Rvt.Storage.Abstractions` | Strong | Make it the canonical object-storage contract |
| `Rvt.Storage.Local` | Strong adapter | Retain |
| `Rvt.Storage.AzureBlob` | Strong adapter | Retain |
| `Rvt.Storage.S3` | Strong adapter | Retain |
| `Rvt.Monitor.Common` | Weak project isolation | Extract selectable infrastructure incrementally |
| `Rvt.Monitor.IntegrationTesting` | Appropriate test support | Keep out of production dependency graphs |
| `AirQMonitor` | Low/transitional | Replace broad facade and synchronous HTTP path |
| `MyAtmMonitor` | Medium-high/transitional | Finish narrow-port migration |
| `OmnidotsMonitor` | Medium/transitional | Replace sync HTTP and clarify transaction boundaries |
| `SvantekMonitor` | Low-medium/transitional | Replace broad API/DB facade and generic HTTP errors |
| `ReportingMonitor` | Strong composition | Use as the monitor reference architecture |
| Monitor `Rvt.Reporting.Core` | Strong | Preferred canonical reporting core |
| Monitor `Rvt.Reporting.Pdf` | Good adapter | Consolidate duplicate lineage |
| Monitor `Rvt.Reporting.Storage` | Strong adapter | Prefer provider-neutral implementation |
| Monitor `Rvt.Reporting.Messaging` | Strong adapter | Consolidate duplicate lineage |
| `RVT.Entities` | Transitional | Delete dead file and reduce serialization coupling |
| `RVT.DataAccess` | Good adapter | Preserve persistence and commit ownership |
| `RVT.BusinessLogic` | Weak dependency hygiene | Remove stale dependencies and migrate slices |
| `RVT.Utilities` | Retired | Removed on 2026-07-28 |
| `RvtPortal.Application` | Strong but incomplete | Expand by vertical use-case slices |
| `RvtPortal.Spa` | Transitional host/application mix | Reduce toward host, API, composition, and adapters |
| `RVT.SchemaDeploy` | Appropriate infrastructure tool | Keep isolated |
| `RvtPortal.Client` | Functional with structural debt | Split route features and resolve Help Admin |
| Service `Rvt.Reporting.Core` | Good but duplicated | Merge or rename |
| Service `Rvt.Reporting.Data` | Correct adapter, oversized | Split repository by aggregate/use case |
| Service `Rvt.Reporting.Messaging` | Good but duplicated | Consolidate |
| Service `Rvt.Reporting.Pdf` | Good but duplicated | Consolidate |
| Service `Rvt.Reporting.Storage` | Azure-specific under generic name | Replace with shared adapter or rename |
| `Rvt.Reporting.Service` | Valid composition, duplicated product | Decide whether it remains authoritative |

## Dead-code and remaining-work inventory

High-confidence removal candidates:

- `apps/portal/RVT.Entities/CreateDB.cs`, which is entirely commented out;
- unused package references in `RVT.BusinessLogic`; and
- the unreferenced nested `MessageService.MessageContent` DTO.

An explicit source TODO remains in
`RvtPortal.Spa/Application/Monitors/MonitorService.cs`: determine the ordering
contract for vibration traces.

Future work already approved as pending:

- unify Portal blob client/service usage through
  `IObjectStorageClientFactory`;
- decide adoption for customer-logo and reporting storage;
- remove legacy synchronous `IMessageService`;
- decide whether provider selection must become dynamically loadable; and
- evaluate separate database, MQTT, scheduling, and observability adapters.

No compiler-detected unreachable statements, `#if false` regions, or
constant-false blocks were found. Static source analysis cannot by itself rule
out reflection or external consumers, so dead-code deletion must remain
build/test guarded.

## Ordered remediation checklist

- [x] **Standards foundation.** Approve and publish the repository-wide
      engineering standard and ratcheted governance model. Automated root
      tooling and machine-readable baselines remain part of R9.
- [x] **R1 — Repair architecture guards.** Replace stale repository paths and
      prove the boundary tests fail for real violations. Completed 2026-07-28
      with fail-closed normal/deterministic-CI external-artifact discovery,
      explicit validated-root guidance, and two disposable mutation proofs.
- [ ] **R2 — Align Help Admin with the release decision.** Shipment was
      explicitly approved and the application-boundary, role,
      stable-identity/focus, HTTP, and browser work is complete. R2 remains
      conditional because no release-database receipts were produced during
      implementation. The shared BCL-only URL policy and read-only .NET release
      audit are implemented; record complete zero-finding audit receipts for
      every release database before marking R2 complete.
- [ ] **R3 — Select the authoritative reporting lineage.** Inventory unique
      behavior, migrate it, and merge/remove or rename duplicate projects.
- [ ] **R4 — Retire dead Portal infrastructure.** `RVT.Utilities` was removed
      on 2026-07-28; complete the remaining `RVT.BusinessLogic` dependency
      cleanup.
- [ ] **R5 — Continue Portal vertical extraction.** Move use cases into
      `RvtPortal.Application` with inward-owned ports.
- [ ] **R6 — Finish monitor narrow-port migration.** Prioritize AirQ and
      Svantek, followed by Omnidots and MyAtm compatibility removal.
- [ ] **R7 — Remove synchronous compatibility paths.** Complete async HTTP,
      MQTT, messaging, and rule-processing call chains.
- [ ] **R8 — Split selectable infrastructure from Common.** Do this only where
      independent host composition is required.
- [x] **R9 — Implement repository standards enforcement.** The root
      configuration, exact diagnostic baseline, exception and module policy,
      changed-surface ratchet, frontend policy, local aggregate gate, CI gate,
      mutation guards, operator guide, and evidence report are implemented.
      This closes the shared enforcement foundation, not the legacy diagnostic
      backlog. The final backend aggregate recorded 186 tests requiring a
      dedicated PostgreSQL integration connection and 17 stale-layout
      architecture failures already owned by R1; no production database was
      used and no gate was weakened.
- [ ] **R10 — Reduce Portal client/host structural size.** Extract routes,
      feature panels, date helpers, and composition extensions.
- [ ] **R11 — Dispose of ambient untracked configuration.** Decide whether the
      reporting `Directory.Packages.props` and storage lockfiles are intentional.

## Verification baseline

At review time:

- the root backend solution built with zero errors;
- Portal client build passed;
- Portal client tests passed 68/68;
- Portal client lint reported zero errors and two module-export warnings;
- Portal architecture tests passed 44/44;
- shared communication, storage, Common, ReportingMonitor, and relevant
  Omnidots architecture suites passed; and
- R1 completion verification passed 19/19 shared repository-layout tests,
  38/38 focused MyATM tests, and 5/5 focused Svantek tests in both normal and
  deterministic-CI external-artifact layouts with a validated
  `RVT_MONOREPO_ROOT`; the bounded no-environment CI case failed closed with
  actionable root guidance, and the Mapperly project-shape and forbidden
  internal-package mutations were both rejected in a disposable worktree.

## Suffixed sidecar-file investigation

The repository is under `/Users/oldgeorge/Documents`. That directory carries
the extended attribute:

```text
com.apple.file-provider-domain-id =
com.apple.CloudDocs.iCloudDriveFileProvider/E2494D5B-200D-4B93-8033-4F36D6975AE8
```

This identifies the directory as an iCloud Drive File Provider domain. Neither
the searched MSBuild projects/targets nor the Vite, package, or shell
configuration contains a rule that produces names ending in ` 2`.

MSBuild and Vite rapidly delete, replace, rename, and recreate files under
`bin`, `obj`, and `dist`. When iCloud reconciles a local replacement with a
version it still tracks or materializes, its conflict-preservation behavior
keeps both versions and creates Finder-style names such as `file 2.dll`,
`index 2.html`, or `asset 2.js`. The presence of the same pattern in .NET
outputs, Vite outputs, copied test data, dependency trees, and previously in
source documents is consistent with filesystem synchronization, not with a
single build target.

The durable remedy is to keep Git worktrees and generated build/dependency
directories outside iCloud-synchronized Desktop/Documents folders. Cleaning
the copies treats the symptom; subsequent builds can recreate them while the
worktree remains inside the File Provider domain.

The repository migration was completed on 2026-07-27. The current repository
root is `/Users/oldgeorge/Developer/rvt-mono`, the linked review worktree is
under that root at `.worktrees/release-platform-hardening`, and the former
`/Users/oldgeorge/Documents/rvt-mono` path no longer exists. The new root does
not carry `com.apple.file-provider-domain-id`. All existing Finder-style
numbered build conflicts were removed before post-migration validation.

Post-migration validation regenerated the absolute NuGet restore metadata,
built the 50-project root solution with zero errors, passed all nine repository
guard scripts, passed all 68 Portal client tests, and produced a successful
Vite production build. Client lint retained the two previously recorded
Fast Refresh warnings and no errors. A final repository-wide scan after those
builds and tests found zero Finder-style numbered conflict files and zero
numbered conflict directories.
