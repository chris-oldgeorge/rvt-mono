# RVT Common Source-Reference Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make active applications build against in-repository RVT common source projects, while retaining package-based validation from locally packed artifacts.

**Architecture:** Direct application consumers use `ProjectReference` paths to the three shared projects. Package-validation remains a separate package-consumer boundary; the root build script packs the shared projects into `artifacts/packages` before it restores/builds/tests the aggregate solution with the local feed.

**Tech Stack:** .NET 10, MSBuild `ProjectReference`, NuGet package source mapping, Bash, `.slnx`.

## Global Constraints

- Keep package consumers only in `libs/rvt-monitor-common/package-validation`.
- Use source projects `src/Rvt.Monitor.Common`, `src/Rvt.Monitor.Common.Infrastructure`, and `testing/Rvt.Monitor.IntegrationTesting` from `libs/rvt-monitor-common`.
- Preserve the local package-validation version `0.2.0-rc.1`.
- Do not change non-RVT package versions, source code, database assets, deployment configuration, or credentials.
- Remove active monitor/portal dependencies on GitHub Packages; do not remove `nuget.org`.
- The decision is explicitly reviewable: application consumers are source-bound, while package-validation remains package-bound.

---

### Task 1: Add a source-boundary regression guard

**Files:**
- Create: `scripts/verify-rvt-common-source-boundary.sh`
- Create: `tests/verify-rvt-common-source-boundary.test.sh`
- Modify: `project_state.md`

**Interfaces:** The test invokes the guard. The guard must fail if an active app/portal project has a `PackageReference` to an RVT common package, lacks the corresponding `ProjectReference`, or if either package-validation consumer stops being package-based.

- [ ] **Step 1: Write the failing test**

Create an executable test wrapper using strict Bash mode and repository-root resolution, then invoke `scripts/verify-rvt-common-source-boundary.sh`. Run it and verify failure because the guard does not exist.

- [ ] **Step 2: Implement the minimum guard**

Implement an executable guard that declares these exact project paths: Common `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`; Infrastructure `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`; IntegrationTesting `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`.

Require Common and Infrastructure source references in AirQ, MyATM, Omnidots, Svantek, and ReportingMonitor application projects; Common references in ReportingMonitor's Messaging and Storage projects; Common and IntegrationTesting references in ReportingMonitorTests; IntegrationTesting references in the four monitor test projects; and Infrastructure in `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`. Reject all `PackageReference Include="Rvt.Monitor.Common"`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` entries below `apps/monitors` and `apps/portal`.

Require package references to Common/Infrastructure in `package-validation/RuntimeConsumer/RuntimeConsumer.csproj` and IntegrationTesting in `package-validation/TestConsumer/TestConsumer.csproj`; reject source project references in those two projects.

- [ ] **Step 3: Verify RED and commit**

Run the test; expect failure because current active consumers use package references. Commit the guard/test/state update with `git commit -m "test: define RVT common source boundary"`.

### Task 2: Convert active consumers to source project references

**Files:**
- Modify: `apps/monitors/Directory.Packages.props`
- Modify: `apps/monitors/NuGet.config`
- Modify: `apps/portal/NuGet.config`
- Modify: `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj`

**Interfaces:** Every active application reference resolves directly to the shared source project. MSBuild determines normal build order from those references. Package-validation files remain untouched in this task.

- [ ] **Step 1: Confirm the guard is RED before conversion**

Run `tests/verify-rvt-common-source-boundary.test.sh`; expect the first active package reference to be reported.

- [ ] **Step 2: Replace active package references**

Replace each active RVT `PackageReference` with the matching relative `ProjectReference`. For monitor projects located at `apps/monitors/<monitor>/<project>`, use `../../../../libs/rvt-monitor-common/...`; for the portal host, use `../../libs/rvt-monitor-common/...`. Keep direct Common and Infrastructure references where both packages were direct dependencies; retain Common-only and Infrastructure-only edges where that was the existing package dependency. Add IntegrationTesting source references only to the five current test consumers.

Remove only the three RVT `PackageVersion` entries and obsolete RVT version properties from `apps/monitors/Directory.Packages.props`. Remove the private RVT source/mapping from `apps/monitors/NuGet.config`; remove the RVT source, credentials, and matching mapping from `apps/portal/NuGet.config`. Preserve `nuget.org`, all unrelated package versions, and all existing package references.

- [ ] **Step 3: Verify GREEN and the active dependency graphs**

Run `tests/verify-rvt-common-source-boundary.test.sh`, `tests/verify-mono-layout.test.sh`, `tests/verify-mono-solution.test.sh`, and `dotnet sln Rvt.Mono.slnx list`; all must pass. Run `dotnet restore apps/monitors/rvt-monitors.sln -p:RestoreIgnoreFailedSources=true` and `dotnet restore apps/portal/RvtPortal.Spa.sln -p:RestoreIgnoreFailedSources=true`; verify that their active application graphs reach the shared source projects without contacting GitHub Packages. Do not restore the aggregate solution until Task 3 has packed and configured the local package-validation feed.

- [ ] **Step 4: Commit**

Commit the conversion with `git commit -m "build: reference RVT common source"`.

### Task 3: Build locally packed artifacts for package validation

**Files:**
- Create: `scripts/build-mono.sh`
- Modify: `libs/rvt-monitor-common/NuGet.config`
- Modify: `libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json`
- Modify: `libs/rvt-monitor-common/package-validation/TestConsumer/packages.lock.json`
- Modify: `README.md`
- Modify: `project_state.md`

**Interfaces:** `scripts/build-mono.sh` accepts no credentials. It packs the three common projects at `0.2.0-rc.1` to `artifacts/packages`, restores the root solution from the local package feed, then builds and tests it. Package-validation references stay package references and resolve only from the local feed.

- [ ] **Step 1: Write a failing package-sequence assertion**

Extend `tests/verify-rvt-common-source-boundary.test.sh` or add a focused companion test that invokes the root build script with a temporary package-feed path that does not contain the three required `.nupkg` files. It must fail before a package-validation restore is attempted, with a diagnostic naming the missing package artifact.

- [ ] **Step 2: Configure the local validation feed and script**

Update `libs/rvt-monitor-common/NuGet.config` so `nuget.org` remains available and `Rvt.*` maps to the relative root feed `../../artifacts/packages`; remove the GitHub Packages source. Keep package-validation project references and `RvtPackageVersion` unchanged.

Implement `scripts/build-mono.sh` with strict Bash mode, root resolution, and these ordered commands: create `artifacts/packages`; restore the three shared source projects; pack Common, Infrastructure, and IntegrationTesting to the local feed using `-p:PackageVersion=0.2.0-rc.1`; verify the three named `.nupkg` artifacts exist; restore `Rvt.Mono.slnx` with force evaluation; build with `--no-restore`; and test with `--no-build`. The script must stop on the first failure and must not read or emit credentials.

Regenerate the two package-validation lock files through this local-feed sequence only. Confirm their RVT package versions remain `0.2.0-rc.1` and their content hashes correspond to the locally packed artifacts.

- [ ] **Step 3: Verify RED then GREEN**

Run the missing-artifact assertion and record its expected failure. Run `scripts/build-mono.sh`; expected result is source restore/build/test and package-validation restore use the local artifacts with no GitHub Packages authentication attempt. If an unrelated third-party restore or runtime integration dependency blocks a test, preserve the exact non-secret diagnostic in `project_state.md` and do not change package versions or test behavior to mask it.

- [ ] **Step 4: Final documentation, structural verification, and commit**

Update the README with the root build command and explain that package-validation intentionally remains package-based against local artifacts. Update `project_state.md` with the reviewable decision, build result, artifact location, and any non-secret limitations.

Run `tests/verify-rvt-common-source-boundary.test.sh`, `tests/verify-mono-layout.test.sh`, `tests/verify-mono-solution.test.sh`, `git diff --check`, and `git status --short`. Commit with `git commit -m "build: validate RVT common packages locally"`.
