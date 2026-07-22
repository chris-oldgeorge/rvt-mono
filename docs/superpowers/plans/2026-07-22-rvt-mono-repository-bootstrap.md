# RVT Mono-Repository Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import the four approved RVT repositories into a fresh mono-repository with a single aggregate solution and provable source provenance.

**Architecture:** Source contents remain isolated under four module directories. A source manifest pins every import, shell tests guard structure and solution membership, and `Rvt.Mono.slnx` contains every imported C# project without modifying individual project files.

**Tech Stack:** Git, Bash, .NET SDK, `dotnet` CLI, `.slnx`.

## Global Constraints

- Exact commits: monitors `5935f40614073afa6c4ef954db1308a72a5f8f2b`; portal `8355070f094a591297c9f8468057f44a6c876986`; common `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`; reporting `e602e8317e35bd94a1eb4dd017759b91713ea111`.
- Retain one fresh history only; no copied `.git` directories, remotes, submodules, or source history.
- Preserve source files below `apps/monitors`, `apps/portal`, `libs/rvt-monitor-common`, and `services/reporting`.
- Do not convert package references, modify versions, merge schemas/CI/deployments, or store credentials.

---

### Task 1: Source manifest and import-boundary guard

**Files:** Create `.gitignore`, `docs/imports/source-manifest.md`, `scripts/verify-mono-layout.sh`, and `tests/verify-mono-layout.test.sh`. Modify `project_state.md`.

**Interfaces:** The test runs `scripts/verify-mono-layout.sh`; the verifier exits zero only when all four module roots, the manifest, and `Rvt.Mono.slnx` exist, and no module contains `.git` metadata.

- [ ] **Step 1: Write the failing test**

Create the executable test with exactly `#!/usr/bin/env bash`, `set -euo pipefail`, root directory resolution from `BASH_SOURCE[0]`, and an invocation of `scripts/verify-mono-layout.sh`. Run `tests/verify-mono-layout.test.sh`; expect failure because the verifier does not exist.

- [ ] **Step 2: Write the minimum guard**

Create `.gitignore` for `.DS_Store`, `**/bin/`, `**/obj/`, `**/TestResults/`, `**/.vs/`, `**/.env`, `**/.env.*`, and `artifacts/`. Create the manifest with this exact mapping: `apps/monitors` -> `https://github.com/chris-oldgeorge/rvt-monitors.git` / `main` / `5935f40614073afa6c4ef954db1308a72a5f8f2b`; `apps/portal` -> `https://github.com/chris-oldgeorge/rvtportal-spa-alpha.git` / `master` / `8355070f094a591297c9f8468057f44a6c876986`; `libs/rvt-monitor-common` -> `https://github.com/RVT-Group-LTD/rvt-reporting.git` / `main` / `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`; `services/reporting` -> `https://github.com/chris-oldgeorge/rvt-reporting-new.git` / `main` / `e602e8317e35bd94a1eb4dd017759b91713ea111`.

Implement the verifier to iterate required paths, reject a `find apps libs services -type d -name .git` result, and require every pinned revision to occur in the manifest. Make it executable.

- [ ] **Step 3: Verify RED and commit**

Run `tests/verify-mono-layout.test.sh`; expect `Missing required mono-repository path: apps/monitors`. Commit with `git add .gitignore docs/imports/source-manifest.md scripts/verify-mono-layout.sh tests/verify-mono-layout.test.sh project_state.md && git commit -m "chore: add mono-repository import guard"`.

### Task 2: Source-only snapshot import

**Files:** Create `apps/monitors/**`, `apps/portal/**`, `libs/rvt-monitor-common/**`, and `services/reporting/**`. Modify `project_state.md`.

**Interfaces:** Consumes manifest URLs and revisions. Produces module roots that satisfy the layout guard once Task 3 creates the root solution.

- [ ] **Step 1: Verify pre-import RED**

Run `tests/verify-mono-layout.test.sh`; expect the `apps/monitors` missing-path failure.

- [ ] **Step 2: Check out exact staged source**

Use `staging_dir="$(mktemp -d /private/tmp/rvt-mono-import.XXXXXX)"`. Clone each manifest URL using `git clone --no-checkout`, then use `git -C "$staging_dir/<module>" checkout --detach <revision>`. Run `git -C` `rev-parse HEAD` for all four staging clones and require exact manifest revisions.

- [ ] **Step 3: Copy only source content**

Create `apps`, `libs`, and `services`. Run `rsync -a --exclude='.git'` from each staged clone into its defined module path. Update `project_state.md` with module paths, revisions, and verification state. Keep the explicit staging directory until final verification succeeds.

- [ ] **Step 4: Verify expected intermediate failure and commit**

Run `tests/verify-mono-layout.test.sh`; expect `Missing required mono-repository path: Rvt.Mono.slnx`. Commit with `git add apps libs services project_state.md && git commit -m "chore: import RVT source snapshots"`.

### Task 3: Aggregate solution and solution-membership guard

**Files:** Create `Rvt.Mono.slnx`, `scripts/verify-mono-solution.sh`, and `tests/verify-mono-solution.test.sh`. Modify `README.md` and `project_state.md`.

**Interfaces:** The solution guard consumes all `*.csproj` files under the four module roots and compares their count to `dotnet sln Rvt.Mono.slnx list`. It exits zero only when every module contributes at least one listed project and the counts match.

- [ ] **Step 1: Write the failing solution test**

Create an executable `tests/verify-mono-solution.test.sh` using the same test wrapper pattern as Task 1, calling `scripts/verify-mono-solution.sh`. Run it; expect failure because the verifier is absent.

- [ ] **Step 2: Generate the solution and implement the guard**

Run `dotnet new sln --format slnx --name Rvt.Mono`. Run `find apps/monitors apps/portal libs/rvt-monitor-common services/reporting -name '*.csproj' -print0 | xargs -0 dotnet sln Rvt.Mono.slnx add`. Implement `scripts/verify-mono-solution.sh` to use `dotnet sln "$root_dir/Rvt.Mono.slnx" list`, count listed `.csproj` lines, compare that count with `find` output, and assert a listed path beginning with each module directory. Make it executable.

- [ ] **Step 3: Verify GREEN**

Run `tests/verify-mono-solution.test.sh && tests/verify-mono-layout.test.sh`; both must exit zero. Then run `dotnet sln Rvt.Mono.slnx list`, `dotnet restore Rvt.Mono.slnx`, and `dotnet build Rvt.Mono.slnx --no-restore --nologo`. If private package access is the only restore failure, record the non-secret error in `project_state.md`; do not alter feeds or dependencies.

- [ ] **Step 4: Write onboarding documentation and commit**

Create `README.md` titled `RVT Mono-Repository`, list the four module paths and the two test commands, then show `dotnet restore Rvt.Mono.slnx` and `dotnet build Rvt.Mono.slnx --no-restore --nologo`. State that the nearest imported `AGENTS.md` governs work and that portal work requires reading `apps/portal/docs/development/development-guidelines.md` first. Update `project_state.md` with project count, source/import commits, and non-secret verification results. Run `git diff --check`, both test scripts, `git status --short`, then commit with `git add Rvt.Mono.slnx README.md scripts/verify-mono-solution.sh tests/verify-mono-solution.test.sh project_state.md && git commit -m "build: add RVT aggregate solution"`.

- [ ] **Step 5: Final evidence**

Run `git log --oneline --max-count=4`, `git status --short`, and `dotnet sln Rvt.Mono.slnx list`. Expected: design plus three implementation commits, a clean worktree, and a complete aggregate project list.
