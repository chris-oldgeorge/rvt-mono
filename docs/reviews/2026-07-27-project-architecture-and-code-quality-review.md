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

### 1. Help Admin release decision and runtime behavior disagree

The Portal release matrix says Help Admin is excluded, but the client still
imports `HelpAdminPanel`, exposes `/admin/help` in navigation, resolves the
route, and renders the panel for administrators.

**Impact:** The documented release surface is not the actual compiled release
surface.

**Required decision:** Either remove or production-disable the route and import,
or reverse the exclusion decision and complete the missing release validation.
The existing documented decision favors exclusion.

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

### 4. `RVT.Utilities` is a retirement candidate

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

**Recommendation:** Confirm there are no reflection or external binary
consumers, migrate any remaining behavior to `IObjectStorageClientFactory`, then
remove the project and its references.

### 5. `RVT.BusinessLogic` contains stale dependency declarations

The project references Azure Blobs, configuration binding, HTTP, logging, and
`RVT.Utilities`, but production source does not use those dependencies. Its
Options use is currently satisfied through a transitive dependency instead of a
direct declaration.

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

### 9. Architecture tests contain stale monorepo paths

MyAtm and Svantek architecture tests construct paths such as
`myatmmonitor/MyAtmMonitor` and `svantekmonitor/SvantekMonitor`, rather than the
current `apps/monitors/...` paths. One Mapperly rule also assumes a fixed
three-segment path.

**Impact:** Several tests fail before evaluating the intended policy and
therefore provide incomplete architectural protection.

**Recommendation:** Introduce one shared repository-layout test helper and make
all boundary tests resolve projects through it.

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
| `RVT.Utilities` | Retirement candidate | Replace and remove |
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
- `RVT.Utilities/AzureBlobService` and unused project references;
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
- [ ] **R1 — Repair architecture guards.** Replace stale repository paths and
      prove the boundary tests fail for real violations.
- [ ] **R2 — Align Help Admin with the release decision.** Exclude it from the
      production route/import surface unless shipment is explicitly approved.
- [ ] **R3 — Select the authoritative reporting lineage.** Inventory unique
      behavior, migrate it, and merge/remove or rename duplicate projects.
- [ ] **R4 — Retire dead Portal infrastructure.** Complete shared storage
      adoption, remove `RVT.Utilities`, and clean `RVT.BusinessLogic`
      dependencies.
- [ ] **R5 — Continue Portal vertical extraction.** Move use cases into
      `RvtPortal.Application` with inward-owned ports.
- [ ] **R6 — Finish monitor narrow-port migration.** Prioritize AirQ and
      Svantek, followed by Omnidots and MyAtm compatibility removal.
- [ ] **R7 — Remove synchronous compatibility paths.** Complete async HTTP,
      MQTT, messaging, and rule-processing call chains.
- [ ] **R8 — Split selectable infrastructure from Common.** Do this only where
      independent host composition is required.
- [ ] **R9 — Implement repository standards enforcement.** Introduce the root
      baseline, ratchet existing violations, and normalize naming, namespaces,
      analyzers, package management, and test conventions.
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
- MyAtm and Svantek architecture failures were traced to stale layout
  assumptions rather than demonstrated production boundary violations.

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
