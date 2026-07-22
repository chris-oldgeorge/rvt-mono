# Documentation Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move all module Markdown documentation into the root `docs/` hierarchy, retaining concise README/AGENTS entry points and valid navigation.

**Architecture:** Preserve document content and provenance by moving files into module/current/history root sections. Generate an explicit move manifest and link verifier before moving files; rewrite only documentation references and entry-point indexes.

**Tech Stack:** Git, Bash, Markdown, `rg`.

## Global Constraints

- Move all non-entry Markdown under `apps/`, `libs/`, and `services/` into `docs/`.
- Retain root and module-entry `README.md` and all `AGENTS.md` files in place.
- Preserve historical plans, specs, evidence, and schema records under `docs/history`.
- Do not delete documentation content or modify production code/configuration.
- Preserve the existing untracked `CommonPackageBoundaryTests 2.cs` file.

---

### Task 1: Define the move manifest and documentation guard

**Files:** Create `docs/documentation-move-manifest.md`, `scripts/verify-documentation-layout.sh`, and `tests/verify-documentation-layout.test.sh`. Modify `project_state.md`.

- [ ] **Step 1: Write the failing wrapper**

Create an executable strict-mode wrapper for `scripts/verify-documentation-layout.sh`; run it and expect failure because the guard is absent.

- [ ] **Step 2: Implement the manifest and guard**

List every move source/destination in the manifest. The guard must reject non-entry Markdown below module roots, require every manifest destination, verify retained README/AGENTS paths, and detect stale old-document links.

- [ ] **Step 3: Verify RED and commit**

Run the wrapper and expect old module Markdown violations. Commit as `test: define documentation consolidation guard`.

### Task 2: Move documentation into the root hierarchy

**Files:** Move all manifest-listed Markdown files into `docs/architecture`, `docs/database`, `docs/development`, `docs/operations`, `docs/release`, `docs/modules/<module>`, and `docs/history/<module>`.

- [ ] **Step 1: Confirm the guard remains RED**

Run the documentation guard and record the expected module-document violations.

- [ ] **Step 2: Perform Git-aware moves**

Use `git mv` for all tracked manifest files. Preserve document names and module origin; move historical `superpowers` plans/specs/evidence/schema content below the corresponding `docs/history/<module>` paths.

- [ ] **Step 3: Verify expected intermediate state**

Run the guard; expect only stale links/entry-point failures until Task 3 updates navigation.

- [ ] **Step 4: Commit**

Commit as `docs: centralize module documentation`.

### Task 3: Repair navigation and verify the root documentation hub

**Files:** Create `docs/index.md`. Modify root/module READMEs, affected AGENTS/project-state references, and moved Markdown links.

- [ ] **Step 1: Create a failing navigation assertion**

Extend the documentation guard to require `docs/index.md` and verify its links to architecture, development, operations, release, database, module, history, and import documentation.

- [ ] **Step 2: Write the documentation index and repair links**

Create concise sectioned navigation in `docs/index.md`; update entry README files to their module documentation landing pages; rewrite old relative Markdown paths in moved/current documents.

- [ ] **Step 3: Verify GREEN**

Run the documentation guard, `rg` for stale module-doc links, `git diff --check`, and `git status --short`. Confirm the untracked duplicate test file is still present and unstaged.

- [ ] **Step 4: Commit**

Update `project_state.md` with the consolidation result and commit as `docs: add root documentation index`.
