# Client Release Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repeatable exported-folder release process that packages client milestone source without internal AI/process artifacts.

**Architecture:** A PowerShell script in `docs/release` reads a committed exclusion list, copies Git-tracked files to a clean output folder, removes blocked paths, verifies required source assets are present, and fails if forbidden artifacts remain. The script does not push to Git or modify the client repository.

**Tech Stack:** PowerShell 5+/7, Git CLI, repo-local text exclusion config.

---

### Task 1: Release Exclusion Config

**Files:**
- Create: `docs/release/client-release-exclusions.txt`

- [ ] **Step 1: Add a committed exclusion list**

Create `docs/release/client-release-exclusions.txt` with one path or glob-like pattern per line:

```text
AGENTS.md
docs/superpowers/
.codegraph/
.worktrees/
**/._*
**/.DS_Store
**/~$*
RvtPortal.Client/node_modules/
**/bin/
**/obj/
RvtPortal.Spa/wwwroot/
```

- [ ] **Step 2: Verify the config is tracked**

Run: `git status --short docs/release/client-release-exclusions.txt`

Expected: the file appears as untracked before commit.

### Task 2: Export Script

**Files:**
- Create: `docs/release/export-client-release.ps1`

- [ ] **Step 1: Add a PowerShell exporter**

Create `docs/release/export-client-release.ps1` with parameters for milestone name, output root, dry run, and zip creation. The script should use `git ls-files` as its source list, skip paths matching the exclusion config, copy files into a clean export folder, verify blocked paths are absent, and verify source asset directories are preserved when present.

- [ ] **Step 2: Run dry-run verification**

Run: `pwsh ./docs/release/export-client-release.ps1 -Milestone milestone-test -DryRun`

Expected: the command prints the target folder, copied/excluded counts, and no files are written.

- [ ] **Step 3: Run real export verification**

Run: `pwsh ./docs/release/export-client-release.ps1 -Milestone milestone-test -OutputRoot <temp-folder>`

Expected: the command creates a folder with tracked source files, excludes `AGENTS.md` and `docs/superpowers`, includes frontend source assets if present, and prints a success summary.

### Task 3: State Update

**Files:**
- Modify: workspace `project_state.md`

- [ ] **Step 1: Record the release process**

Add a dated section that records the export script path, exclusion config path, default behavior, and verification command results.

- [ ] **Step 2: Verify no blocked files appear in a generated export**

Run the script against a temporary output folder and confirm no blocked paths remain.
