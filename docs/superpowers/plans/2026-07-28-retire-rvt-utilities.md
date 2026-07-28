# Retire RVT.Utilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the production-dead `RVT.Utilities` project, its dedicated test coupling, and all current build and documentation references without disturbing unrelated portal work.

**Architecture:** Treat the existing exact solution-membership verifier as the regression boundary: deleting the project must make the stale solution graph fail, and removing its graph entries must restore the verifier. Preserve the independent `RVT.BusinessLogic` SendGrid boundary test while removing its dependency on the retired assembly.

**Tech Stack:** .NET 10, MSBuild project references, `.slnx` and legacy `.sln` solutions, xUnit, Bash repository verifiers, JSON engineering baseline.

## Global Constraints

- Preserve unrelated modifications in the existing working tree.
- Do not inspect the two Omnidots test-data files previously flagged by Sonar secrets scanning.
- Do not add a replacement project or compatibility shim for `RVT.Utilities`.
- Do not add or change runtime environment-variable definitions.
- Keep historical design and task records intact; update only current operational documentation and the dated architecture review's resolution status.
- Use the existing general solution verifier instead of adding a permanent test dedicated to the retired project.
- Do not commit or push as part of this implementation.

---

### Task 1: Establish the solution-graph regression

**Files:**
- Delete: `apps/portal/RVT.Utilities/AzureBlobService.cs`
- Delete: `apps/portal/RVT.Utilities/RVT.Utilities.csproj`
- Test: `tests/verify-mono-solution.test.sh`

**Interfaces:**
- Consumes: `scripts/verify-mono-solution.sh`, which compares discovered `.csproj` files with `Rvt.Mono.slnx`.
- Produces: A failing verifier that identifies the stale `RVT.Utilities` solution entry after the project is deleted.

- [x] **Step 1: Delete the production-dead project files**

Remove both tracked files under `apps/portal/RVT.Utilities`.

- [x] **Step 2: Run the solution verifier to verify the stale graph fails**

Run: `bash tests/verify-mono-solution.test.sh`

Expected: FAIL because `Rvt.Mono.slnx` still lists `apps/portal/RVT.Utilities/RVT.Utilities.csproj` while the project is no longer discoverable.

### Task 2: Remove build and test coupling

**Files:**
- Modify: `Rvt.Mono.slnx`
- Modify: `apps/portal/RvtPortal.Spa.sln`
- Modify: `apps/portal/RVT.BusinessLogic/RVT.BusinessLogic.csproj`
- Modify: `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `apps/portal/.gitignore`
- Test: `tests/verify-mono-solution.test.sh`
- Test: `apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`

**Interfaces:**
- Consumes: The remaining portal project graph and the `RVT.BusinessLogic` assembly boundary.
- Produces: Solutions and project references with no `RVT.Utilities` edge, plus a business-only SendGrid boundary test.

- [x] **Step 1: Remove solution and project references**

Delete the `RVT.Utilities` entry from `Rvt.Mono.slnx`, its project and configuration blocks from `RvtPortal.Spa.sln`, and both `<ProjectReference>` elements from the BusinessLogic and SPA projects.

- [x] **Step 2: Remove the dedicated utility test coupling**

Rename `BusinessAndUtilityAssemblies_DoNotReferenceSendGrid` to `BusinessLogicAssembly_DoesNotReferenceSendGrid` and inspect only `typeof(IRvtDateTimeProvider).Assembly`.

- [x] **Step 3: Remove stale project-specific ignore rules**

Delete the `RVT.Utilities/obj/` and `RVT.Utilities/bin/` entries; the repository-wide `.NET` output rules remain.

- [x] **Step 4: Run focused regression checks**

Run: `bash tests/verify-mono-solution.test.sh`

Expected: PASS.

Run: `dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter FullyQualifiedName~BusinessLogicAssembly_DoesNotReferenceSendGrid`

Expected: PASS after restore assets are refreshed if needed.

### Task 3: Remove stale current metadata

**Files:**
- Modify: `eng/standards/baseline.json`
- Modify: `apps/portal/README.md`
- Modify: `docs/development/portal/onboarding/REACT_PORT_ONBOARDING.md`
- Modify: `docs/development/portal/testing/testability-rc-grade-update.md`
- Modify: `docs/release/portal/CUTOVER_RUNBOOK.md`
- Modify: `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: The retired project graph from Task 2.
- Produces: Current documentation, standards metadata, and session state that accurately describe the repository.

- [x] **Step 1: Remove deleted-file baseline entries**

Delete every engineering-baseline object whose path is `apps/portal/RVT.Utilities/AzureBlobService.cs`, preserving valid JSON and all unrelated entries.

- [x] **Step 2: Update current portal documentation**

Remove `RVT.Utilities` from the portal project inventory, onboarding guide, testing priorities, and cutover dependency list.

- [x] **Step 3: Mark the architecture-review action resolved**

Record that the project, references, and dedicated test coupling were retired on 2026-07-28; mark recommendation R4 complete without rewriting historical evidence.

- [x] **Step 4: Save the new authoritative repository state**

Add a top checkpoint to `project_state.md` listing the retired files, remaining portal project structure, verification results, and the fact that no environment-variable definitions changed.

### Task 4: Verify the retirement

**Files:**
- Verify: `Rvt.Mono.slnx`
- Verify: `apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`
- Verify: repository working tree

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: Evidence that the repository builds, tests, and contains no live `RVT.Utilities` reference.

- [x] **Step 1: Restore the updated graph**

Run: `dotnet restore Rvt.Mono.slnx`

Expected: PASS.

- [x] **Step 2: Build and test**

Run: `dotnet build Rvt.Mono.slnx -c Release --no-restore`

Expected: PASS.

Run: `dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj -c Release --no-restore`

Expected: PASS.

- [x] **Step 3: Run repository verifiers**

Run: `bash tests/verify-mono-layout.test.sh`

Run: `bash tests/verify-mono-solution.test.sh`

Run: `node tests/verify-engineering-standards-policy.test.mjs`

Run: `bash tests/verify-engineering-configuration.test.sh`

Expected: all PASS.

- [x] **Step 4: Check references and patch hygiene**

Run: `git grep -n -E 'RVT\\.Utilities|Rvt\\.Utilities' -- ':!docs/superpowers/**' ':!docs/reviews/**' ':!project_state.md'`

Expected: no live build, source, test, current operational-documentation, or baseline references.

Run: `git diff --check`

Expected: PASS.
