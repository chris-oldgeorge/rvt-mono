# RVT Monitors Client Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and execute a repeatable curated release process that publishes RVT Monitors to `RVT-Group-LTD/rvt-monitors` without internal development memory or planning files.

**Architecture:** The source repository owns the release policy, exporter, publisher, and runbook. The exporter builds from `git ls-files`, applies explicit exclusions, generates a manifest, and validates blocked paths before any publish. The publisher regenerates the export, replaces a target repository release branch with the curated payload, commits, and pushes.

**Tech Stack:** Bash, Git, GitHub, .NET solution verification, Docker/observability files as release payload.

## Global Constraints

- Use `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors` as the active source workspace.
- Publish to `https://github.com/RVT-Group-LTD/rvt-monitors.git`.
- Exclude `AGENTS.md`, `project_state.md`, `docs/superpowers/**`, `docs/monitor-data-access-migration.md`, `.codegraph/**`, release tooling, local secrets, and generated output from the client payload.
- Include a comprehensive root `README.md` in the curated payload.
- Keep the release process repeatable from documented commands.
- Do not copy untracked local files into the curated payload.

---

### Task 1: Release Policy And README

**Files:**
- Create: `README.md`
- Create: `docs/release/client-release-exclusions.txt`
- Create: `docs/release/client-release-runbook.md`

**Interfaces:**
- Produces: a client-facing root README and an exclusion policy consumed by `scripts/export-client-release.sh`.

- [x] **Step 1: Create root README**

Add a repository inventory describing monitor apps, common library, observability, docs, scripts, build/test commands, Docker commands, configuration keys, and release package exclusions.

- [x] **Step 2: Create release exclusion policy**

Add `docs/release/client-release-exclusions.txt` with explicit repository-relative patterns for internal memory/planning files, release tooling, secrets, and generated output.

- [x] **Step 3: Create repeatable runbook**

Add `docs/release/client-release-runbook.md` with source preparation, export, publish, and verification commands.

### Task 2: Export And Publish Scripts

**Files:**
- Create: `scripts/export-client-release.sh`
- Create: `scripts/publish-client-release.sh`

**Interfaces:**
- Consumes: `docs/release/client-release-exclusions.txt`.
- Produces: a curated export directory containing `RELEASE_MANIFEST.txt`.

- [x] **Step 1: Implement exporter**

Create a strict Bash script that copies `git ls-files` into an export directory unless a path matches the exclusion list.

- [x] **Step 2: Implement blocked-path validation**

Make the exporter fail if the export contains `AGENTS.md`, `project_state.md`, `docs/superpowers/**`, `docs/release/**`, `.codegraph/**`, development appsettings, local settings, env files, or private-key file extensions.

- [x] **Step 3: Implement publisher**

Create a strict Bash script that regenerates the export, clones the target repository, creates the requested branch as a fresh orphan release history by default, replaces branch contents with the export, commits, and pushes with `--force-with-lease`.

### Task 3: Execute And Verify

**Files:**
- Read: generated export under `/private/tmp/rvt-monitors-client-release`
- Read: target repository clone under `/private/tmp/rvt-monitors-client-publish`

**Interfaces:**
- Consumes: exporter and publisher scripts from Task 2.
- Produces: pushed release branch in `RVT-Group-LTD/rvt-monitors`.

- [x] **Step 1: Run source verification**

Run:

```bash
git status --short --branch
dotnet test rvt-monitors.sln --no-build
```

Expected: source branch is clean or contains only intended release-process changes; tests pass.

- [x] **Step 2: Run local export**

Run:

```bash
scripts/export-client-release.sh /private/tmp/rvt-monitors-client-release
```

Expected: export succeeds and prints a file count.

- [x] **Step 3: Inspect export**

Run:

```bash
find /private/tmp/rvt-monitors-client-release -type f \( -name AGENTS.md -o -name project_state.md -o -path '*/docs/superpowers/*' -o -path '*/docs/monitor-data-access-migration.md' -o -path '*/docs/release/*' -o -path '*/.codegraph/*' \) -print
test -f /private/tmp/rvt-monitors-client-release/README.md
test -f /private/tmp/rvt-monitors-client-release/rvt-monitors.sln
test -f /private/tmp/rvt-monitors-client-release/docker-compose.yml
```

Expected: blocked-path scan prints nothing; required files exist.

- [x] **Step 4: Commit source release tooling**

Run:

```bash
git add README.md docs/release docs/superpowers/plans/2026-07-07-rvt-monitors-client-release.md scripts/export-client-release.sh scripts/publish-client-release.sh
git commit -m "chore: add client release export process"
git push origin main
```

Expected: release process is versioned in the source repository.

- [x] **Step 5: Publish target branch**

Run:

```bash
scripts/publish-client-release.sh \
  --target-repo https://github.com/RVT-Group-LTD/rvt-monitors.git \
  --branch release-candidate \
  --export-dir /private/tmp/rvt-monitors-client-release
```

Expected: `release-candidate` is pushed to `RVT-Group-LTD/rvt-monitors`.

- [x] **Step 6: Verify remote branch**

Clone or fetch the target branch and repeat the blocked-path and required-file scans.

Expected: target branch contains the curated package only.
