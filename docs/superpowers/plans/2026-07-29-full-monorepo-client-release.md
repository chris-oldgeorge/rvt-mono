# Full Monorepo Client Release Implementation Plan

> **For Codex:** Execute the tasks in order. Use test-driven development for
> every script behavior, preserve unrelated work, and do not publish until all
> local and exported-snapshot gates have been reviewed.

**Goal:** Replace the obsolete monitor-only client release process with a
revision-pinned full-monorepo export that excludes internal development
material and saved secrets, then publish and independently verify the result in
`RVT-Group-LTD/rvt-monitors` on `release-candidate`.

**Architecture:** Release tooling remains in the private source repository and
acts as a control plane. The exporter reads an exact Git tree, applies a
deny-list policy, emits auditable metadata, and invokes a standalone payload
verifier. The publisher stages the payload in an orphan commit and uses an
explicit force-with-lease against the remote SHA it observed before preparing
the release.

**Tools:** Bash, Git, standard Unix tools, .NET SDK 10.0.302, Node.js/npm,
Docker Compose, GitHub CLI.

---

## Task 1: Establish the release-policy contract

**Files:**

- Create: `docs/release/client-release-exclusions.txt`
- Create: `tests/verify-client-release.test.sh`
- Create: `scripts/verify-client-release.sh`

### Step 1: Write failing filename-boundary tests

Create fixtures in temporary directories and assert that verification rejects:

- `AGENTS.md` at the root and below an application
- `project_state.md`
- `.agents/`, `.codex/`, and `.codegraph/`
- `docs/superpowers/`, `docs/history/`, and `docs/reviews/`
- exact `.env` and local settings files
- private-key and certificate file extensions

Also assert that normal source, `.github` workflows, tests, operational docs,
and placeholder-only sample configuration pass.

Run:

```bash
bash tests/verify-client-release.test.sh
```

Expected: FAIL because the policy and verifier do not exist.

### Step 2: Add the explicit exclusion policy

Define repository-relative shell globs. Keep product paths included by default
and document why each excluded family is internal, generated, or secret-bearing.

### Step 3: Implement filename and required-file verification

Implement `scripts/verify-client-release.sh --payload-dir DIR` with:

- canonical absolute-directory safety checks
- policy loading from the source repository
- blocked-path traversal without following symlinks
- required root-file checks
- rejection of special files and unsafe symlinks
- findings that print only rule identifiers and relative paths

Run the test again.

Expected: filename-boundary tests PASS.

### Step 4: Commit the filename boundary

```bash
git add docs/release/client-release-exclusions.txt \
  scripts/verify-client-release.sh \
  tests/verify-client-release.test.sh
git commit -m "test: define client release boundary"
```

## Task 2: Add fail-closed secret-content verification

**Files:**

- Modify: `tests/verify-client-release.test.sh`
- Modify: `scripts/verify-client-release.sh`

### Step 1: Add failing representative-secret tests

Test high-confidence fixtures for:

- PEM private-key headers
- GitHub, AWS, Slack, and common service-token shapes
- passwords embedded in database connection strings
- credentials embedded in URLs
- non-placeholder assignments to recognized secret names

For every rejection, assert that the output includes the path and rule but not
the secret value. Add passing fixtures for:

- empty values
- `${VARIABLE}` references
- `<placeholder>`, `changeme`, and documented example values
- environment-variable names in Markdown and Compose files
- GitHub Actions `${{ secrets.NAME }}` references

Run:

```bash
bash tests/verify-client-release.test.sh
```

Expected: new content-scan tests FAIL.

### Step 2: Implement redacted content scanning

Scan regular text files in the payload. Skip known binary files by detecting
NUL bytes. Use conservative, named rules and stop publication on a finding.
Never include matching content in diagnostics.

Run:

```bash
bash tests/verify-client-release.test.sh
```

Expected: all verifier tests PASS.

### Step 3: Commit secret verification

```bash
git add scripts/verify-client-release.sh tests/verify-client-release.test.sh
git commit -m "feat: reject saved secrets from client releases"
```

## Task 3: Build a revision-pinned exporter

**Files:**

- Create: `tests/export-client-release.test.sh`
- Create: `scripts/export-client-release.sh`
- Remove: `apps/monitors/scripts/export-client-release.sh`
- Remove: `apps/monitors/docs/release/client-release-exclusions.txt`

### Step 1: Write failing exporter contract tests

Create temporary Git repositories and assert:

- a requested commit, not the working tree, supplies file contents
- dirty tracked edits and untracked files do not appear
- root files plus `.github`, `apps`, `libs`, `eng`, `scripts`, `tests`, and
  ordinary `docs` content are preserved
- excluded internal path families are absent
- unsafe or repository-contained output directories are refused
- the manifest is sorted, complete, and excludes itself
- metadata contains the resolved full commit and commit timestamp
- two exports of the same commit have identical file content and metadata

Run:

```bash
bash tests/export-client-release.test.sh
```

Expected: FAIL because the root exporter does not exist.

### Step 2: Implement Git-object export

Implement:

```text
scripts/export-client-release.sh \
  --source-ref REF \
  --export-dir DIR
```

Resolve `REF` once with `git rev-parse REF^{commit}`. Use `git archive` for the
resolved commit, apply the exclusion policy to the extracted tree, generate
`RELEASE_SOURCE.json` and `RELEASE_MANIFEST.txt`, and invoke the payload
verifier. Do not copy content from the working tree.

### Step 3: Run exporter tests

```bash
bash tests/export-client-release.test.sh
```

Expected: PASS.

### Step 4: Commit the exporter

```bash
git add scripts/export-client-release.sh \
  scripts/verify-client-release.sh \
  tests/export-client-release.test.sh \
  apps/monitors/scripts/export-client-release.sh \
  apps/monitors/docs/release/client-release-exclusions.txt
git commit -m "feat: export reviewable monorepo snapshots"
```

## Task 4: Make publication lease-protected and testable

**Files:**

- Create: `tests/publish-client-release.test.sh`
- Create: `scripts/publish-client-release.sh`
- Remove: `apps/monitors/scripts/publish-client-release.sh`

### Step 1: Write failing publisher tests

Use local bare Git repositories to verify:

- default target and branch values
- explicit source-ref forwarding
- fresh orphan release history
- no-change behavior
- exact `--force-with-lease=<branch>:<observed-sha>` protection
- refusal when the remote branch changes after observation
- post-push verification of the published tree
- `--prepare-only` support for inspecting without pushing

Run:

```bash
bash tests/publish-client-release.test.sh
```

Expected: FAIL because the root publisher does not exist.

### Step 2: Implement publisher and review summary

Clone the target to an explicit temporary directory, record the remote branch
SHA, prepare an orphan commit from the export, and print source SHA, target SHA,
file count, changed-path summary, and commit SHA. Push only when not in
`--prepare-only` mode. Use the observed remote SHA in the lease.

After pushing, fetch the published branch into a fresh verification directory,
run the payload verifier, and compare the committed manifest and source
metadata.

### Step 3: Run publisher tests and commit

```bash
bash tests/publish-client-release.test.sh
git add scripts/publish-client-release.sh \
  tests/publish-client-release.test.sh \
  apps/monitors/scripts/publish-client-release.sh
git commit -m "feat: publish lease-protected client releases"
```

## Task 5: Update the private runbook and project state

**Files:**

- Modify: `docs/release/monitors/client-release-runbook.md`
