# Client Release Sonar Exclusion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep SonarCloud automation internal to `rvt-mono` while ensuring every reviewable `RVT-Group-LTD/rvt-monitors` payload excludes the Sonar workflow, runner stack, dedicated checks, and operator documentation.

**Architecture:** Extend the existing declarative client-release denylist for known Sonar paths, then add a narrow semantic verifier over published GitHub workflow YAML so renamed Sonar automation cannot leak. Preserve the ordinary client test and engineering workflows, relocate the live globalization-suppression rationale to a vendor-neutral documentation path, and publish the resulting payload through a review branch and client pull request.

**Tech Stack:** Bash, Git/GitHub CLI, GitHub Actions YAML, Markdown, .NET 10, existing shell contract tests.

## Global Constraints

- Sonar remains enabled and validated in the internal source repository.
- No export-time text templating, conditional overlay, or second client source tree.
- The client keeps `.github/workflows/tests.yml` and `.github/workflows/engineering-standards.yml`.
- The client verifier reports only a relative path and stable rule name for Sonar workflow findings; it never prints matching contents.
- Workflow-signature scanning is limited to `.github/workflows/*.yml` and `.github/workflows/*.yaml`.
- Existing secret, generated-output, local-state, internal-documentation, manifest, and symlink guards remain intact.
- Source publication uses a pull request with required checks.
- Client publication uses a review branch and pull request; do not overwrite `release-candidate` directly.

---

### Task 1: Exclude Known Sonar Operational Paths

**Files:**
- Modify: `tests/export-client-release.test.sh:30-135`
- Modify: `docs/release/client-release-exclusions.txt:17-35`

**Interfaces:**
- Consumes: the existing shell-pattern matching behavior in `scripts/export-client-release.sh`.
- Produces: a client policy that removes every known Sonar operational path before manifest generation.

- [ ] **Step 1: Add Sonar paths to the export fixture**

Extend `create_source_repo` so its `mkdir -p` list includes:

```bash
"${fixture}/.github/runner" \
"${fixture}/docs/development/portal/sonar" \
"${fixture}/docs/operations/github-actions" \
```

Add representative internal files before the fixture commit:

```bash
printf 'name: SonarQube\n' \
  > "${fixture}/.github/workflows/sonarqube.yml"
printf 'FROM example.invalid/runner\n' \
  > "${fixture}/.github/runner/Dockerfile"
printf '#!/usr/bin/env bash\n' \
  > "${fixture}/.github/runner/entrypoint.sh"
printf 'services: {}\n' \
  > "${fixture}/.github/runner/docker-compose.yml"
printf '# Sonar SQL policy\n' \
  > "${fixture}/docs/development/portal/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md"
printf '# Sonar runner\n' \
  > "${fixture}/docs/operations/github-actions/self-hosted-sonar-runner.md"
printf 'test\n' \
  > "${fixture}/tests/verify-manual-sonarqube-workflow.test.sh"
printf 'test\n' \
  > "${fixture}/tests/verify-sonar-runner-stack.test.sh"
printf 'test\n' \
  > "${fixture}/tests/verify-engineering-standards-integration.test.sh"
```

Add these paths to `blocked_paths`:

```bash
.github/workflows/sonarqube.yml
.github/runner/Dockerfile
.github/runner/entrypoint.sh
.github/runner/docker-compose.yml
docs/development/portal/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md
docs/operations/github-actions/self-hosted-sonar-runner.md
tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-sonar-runner-stack.test.sh
tests/verify-engineering-standards-integration.test.sh
```

- [ ] **Step 2: Run the export contract and verify the red phase**

Run:

```bash
tests/export-client-release.test.sh
```

Expected: FAIL with `excluded path was exported` for
`.github/workflows/sonarqube.yml` or the first newly blocked Sonar path.

- [ ] **Step 3: Add the declarative Sonar boundary**

Insert a section after the internal release-mechanics exclusions:

```text
# Internal Sonar analysis, runner, checks, and operator documentation
.github/workflows/sonarqube.yml
.github/runner/**
docs/operations/github-actions/self-hosted-sonar-runner.md
docs/development/portal/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md
tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-sonar-runner-stack.test.sh
tests/verify-engineering-standards-integration.test.sh
```

- [ ] **Step 4: Run the export contract and verify the green phase**

Run:

```bash
tests/export-client-release.test.sh
```

Expected: PASS with `Revision-pinned client release export fixtures verified.`

- [ ] **Step 5: Commit the known-path boundary**

```bash
git add \
  docs/release/client-release-exclusions.txt \
  tests/export-client-release.test.sh
git commit -m "feat: exclude Sonar operations from client exports"
```

---

### Task 2: Reject Renamed Sonar Workflows

**Files:**
- Modify: `tests/verify-client-release.test.sh:15-120`
- Modify: `scripts/verify-client-release.sh:76-196`
- Modify: `.github/workflows/tests.yml:84-87`

**Interfaces:**
- Consumes: a prepared payload directory and the existing `report_finding RULE PATH` diagnostic.
- Produces: `report_workflow_pattern_matches RULE REGEX`, limited to workflow YAML, with the stable rule `Sonar client automation`.

- [ ] **Step 1: Write the renamed-workflow rejection fixture**

Add this helper after `assert_rejected_path`:

```bash
assert_rejected_workflow() {
  local name="$1"
  local workflow_content="$2"
  local fixture="${temp_dir}/${name}"
  local relative_path=".github/workflows/quality-analysis.yml"
  local output

  create_payload "${fixture}"
  printf '%s\n' "${workflow_content}" > "${fixture}/${relative_path}"

  if output="$("${verifier}" --payload-dir "${fixture}" 2>&1)"; then
    printf 'FAIL: %s Sonar workflow fixture must be rejected.\n' "${name}" >&2
    exit 1
  fi

  if [[ "${output}" != *"${relative_path}"* \
    || "${output}" != *"[rule: Sonar client automation]"* ]]; then
    printf 'FAIL: %s must report only its path and Sonar rule, got:\n%s\n' \
      "${name}" "${output}" >&2
    exit 1
  fi

  if [[ "${output}" == *"${workflow_content}"* ]]; then
    printf 'FAIL: %s diagnostic exposed workflow contents.\n' "${name}" >&2
    exit 1
  fi
}
```

Call it once for each supported signature:

```bash
assert_rejected_workflow sonar-token 'env: { SONAR_TOKEN: placeholder }'
assert_rejected_workflow sonar-cloud 'run: curl https://sonarcloud.io'
assert_rejected_workflow dotnet-sonar-scanner 'run: dotnet-sonarscanner begin'
assert_rejected_workflow generic-sonar-scanner 'run: sonar-scanner'
assert_rejected_workflow sonar-runner-label 'runs-on: rvt-sonar'
assert_rejected_workflow sonarqube-name 'name: SonarQube'
```

- [ ] **Step 2: Run the verifier contract and verify the red phase**

Run:

```bash
tests/verify-client-release.test.sh
```

Expected: FAIL with
`quality-analysis.yml Sonar workflow fixture must be rejected`.

- [ ] **Step 3: Add the workflow-only semantic scanner**

Add this function after `report_pattern_matches`:

```bash
report_workflow_pattern_matches() {
  local rule="$1"
  local pattern="$2"
  local workflow_root="${payload_dir}/.github/workflows"
  local path
  local relative_path

  if [[ ! -d "${workflow_root}" ]]; then
    return 0
  fi

  while IFS= read -r path; do
    relative_path="${path#"${payload_dir}/"}"
    report_finding "${rule}" "${relative_path}"
  done < <(find "${workflow_root}" -type f \
    \( -name '*.yml' -o -name '*.yaml' \) \
    -exec grep -IlEi -- "${pattern}" {} + 2>/dev/null)
}
```

Invoke it after the excluded-path traversal and before the general secret
signatures:

```bash
report_workflow_pattern_matches \
  "Sonar client automation" \
  'sonar_token|sonarcloud\.io|dotnet-sonarscanner|sonar-scanner|rvt-sonar|sonarqube'
```

- [ ] **Step 4: Remove the benign shared-workflow false positive**

In `.github/workflows/tests.yml`, replace:

```yaml
# as in the SonarQube workflow.
```

with:

```yaml
# as in the trusted full-analysis workflow.
```

- [ ] **Step 5: Run the verifier contract and verify the green phase**

Run:

```bash
tests/verify-client-release.test.sh
```

Expected: PASS with `Client release path-boundary fixtures verified.`

- [ ] **Step 6: Run shell guardrails**

Run:

```bash
tests/verify-shell-conditionals.test.sh
scripts/verify-shell-conditionals.sh .
```

Expected: both commands exit 0 with no new shell-policy diagnostics.

- [ ] **Step 7: Commit the renamed-workflow guard**

```bash
git add \
  .github/workflows/tests.yml \
  scripts/verify-client-release.sh \
  tests/verify-client-release.test.sh
git commit -m "feat: reject Sonar automation in client workflows"
```

---

### Task 3: Preserve Suppression Rationale Without a Sonar Documentation Path

**Files:**
- Move: `docs/development/portal/sonar/globalization-suppressions.md`
  → `docs/development/portal/globalization-suppressions.md`
- Modify: `README.md:123-126`
- Modify: `docs/index.md:23-27`
- Modify: `docs/development/portal/development-guidelines.md:230-235`
- Modify: `.github/workflows/tests.yml:86` if Task 2 has not already changed it
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteReadAdapter.cs:215-217`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Companies/CompanyCommands.cs:163-165`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Dashboard/DashboardBreachApplicationService.cs:66-68`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Lookups/LookupService.cs:166-168`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Monitors/MonitorListReader.cs:276-300`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Reports/ReportApplicationService.cs:122-124`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/ReportRules/ReportRuleApplicationService.cs:645-647`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/ReportRules/ReportRuleRecipientReader.cs:247-249`
- Modify: `apps/portal/RvtPortal.Spa/UseCases/Users/UserListApplicationService.cs:165-167`

**Interfaces:**
- Consumes: live `[SuppressMessage]` justifications that point readers to the rationale.
- Produces: the same rationale at
  `docs/development/portal/globalization-suppressions.md`, with no live code
  reference to the old Sonar directory.

- [ ] **Step 1: Record the pre-move reference set**

Run:

```bash
rg -n --fixed-strings \
  'docs/development/portal/sonar/globalization-suppressions.md' \
  apps/portal/RvtPortal.Spa
```

Expected: the live suppression references listed in the Files section above.

- [ ] **Step 2: Move the rationale document**

Run:

```bash
git mv \
  docs/development/portal/sonar/globalization-suppressions.md \
  docs/development/portal/globalization-suppressions.md
```

- [ ] **Step 3: Update every live suppression reference**

In the nine C# files above, replace:

```text
docs/development/portal/sonar/globalization-suppressions.md
```

with:

```text
docs/development/portal/globalization-suppressions.md
```

Update the example in
`docs/development/portal/development-guidelines.md` to describe the
vendor-neutral path as documentation that must continue shipping.

- [ ] **Step 4: Remove client-visible links to excluded operator material**

Delete the two-line manual SonarQube paragraph from `README.md`.

Delete this entry from `docs/index.md`:

```markdown
- [Self-hosted SonarQube runner](operations/github-actions/self-hosted-sonar-runner.md)
```

Do not delete the source-only operator guide itself; Task 1 excludes it only
from client exports.

- [ ] **Step 5: Verify no live reference still targets the old path**

Run:

```bash
rg -n --fixed-strings \
  'docs/development/portal/sonar/globalization-suppressions.md' \
  --glob '!docs/documentation-move-manifest.md' \
  --glob '!apps/portal/docs/release/client-release-exclusions.txt' \
  .
```

Expected: no output and exit status 1.

Run:

```bash
rg -n --fixed-strings \
  'docs/development/portal/globalization-suppressions.md' \
  apps/portal/RvtPortal.Spa
```

Expected: every live suppression reference now points at the new path.

- [ ] **Step 6: Verify documentation and compilation**

Run:

```bash
tests/verify-documentation-layout.test.sh
scripts/verify-documentation-layout.sh
dotnet restore Rvt.Mono.slnx --locked-mode --disable-parallel
dotnet build Rvt.Mono.slnx -c Release --no-restore
```

Expected: documentation guards pass; Release build exits 0 with zero warnings
and zero errors.

- [ ] **Step 7: Commit the vendor-neutral documentation move**

```bash
git add \
  README.md \
  docs/index.md \
  docs/development/portal/development-guidelines.md \
  docs/development/portal/globalization-suppressions.md \
  apps/portal/RvtPortal.Spa
git add -u docs/development/portal/sonar/globalization-suppressions.md
git commit -m "docs: decouple suppression rationale from Sonar"
```

---

### Task 4: Verify the Complete Source and Prepared Client Payload

**Files:**
- Verify: all files changed in Tasks 1-3
- Verify: `scripts/publish-client-release.sh`
- Verify: `RELEASE_MANIFEST.txt` generated in a temporary export

**Interfaces:**
- Consumes: committed source `HEAD`.
- Produces: evidence that the source keeps internal Sonar automation and the
  prepared client payload removes it without losing ordinary CI.

- [ ] **Step 1: Run focused release contracts**

Run:

```bash
tests/export-client-release.test.sh
tests/verify-client-release.test.sh
tests/publish-client-release.test.sh
tests/verify-documentation-layout.test.sh
tests/verify-engineering-standards-workflow.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-sonar-runner-stack.test.sh
```

Expected: every command exits 0. The last two prove the internal source still
validates Sonar even though the client export removes those files.

- [ ] **Step 2: Run repository and engineering guards**

Run:

```bash
scripts/verify-postgresql-only.sh .
scripts/verify-mono-layout.sh
scripts/verify-mono-solution.sh
scripts/verify-rvt-common-source-boundary.sh
scripts/verify-documentation-layout.sh
tests/verify-engineering-configuration.test.sh
tests/verify-engineering-standards-workflow.test.sh
tests/verify-shell-conditionals.test.sh
scripts/verify-shell-conditionals.sh .
git diff --check origin/main...HEAD
```

Expected: every guard exits 0 and `git diff --check` prints nothing.

- [ ] **Step 3: Prepare a real export from committed HEAD**

Run:

```bash
rvt_sonar_free_export="$(mktemp -d /private/tmp/rvt-sonar-free-export.XXXXXX)"
scripts/export-client-release.sh \
  --source-ref HEAD \
  --export-dir "${rvt_sonar_free_export}/payload"
```

Expected: exporter prints the exact source commit, file count, and
`Client release payload boundary verified`.

- [ ] **Step 4: Assert ordinary CI remains and Sonar operations are absent**

Run:

```bash
test -f "${rvt_sonar_free_export}/payload/.github/workflows/tests.yml"
test -f "${rvt_sonar_free_export}/payload/.github/workflows/engineering-standards.yml"
test ! -e "${rvt_sonar_free_export}/payload/.github/workflows/sonarqube.yml"
test ! -e "${rvt_sonar_free_export}/payload/.github/runner"
test ! -e "${rvt_sonar_free_export}/payload/tests/verify-manual-sonarqube-workflow.test.sh"
test ! -e "${rvt_sonar_free_export}/payload/tests/verify-sonar-runner-stack.test.sh"
test ! -e "${rvt_sonar_free_export}/payload/tests/verify-engineering-standards-integration.test.sh"
! rg -i \
  'sonar_token|sonarcloud\.io|dotnet-sonarscanner|sonar-scanner|rvt-sonar|sonarqube' \
  "${rvt_sonar_free_export}/payload/.github/workflows"
scripts/verify-client-release.sh \
  --payload-dir "${rvt_sonar_free_export}/payload"
```

Expected: all assertions exit 0 and the verifier reports a valid payload.

- [ ] **Step 5: Scan every changed file for secrets**

Run `sonar analyze secrets` over each changed source, test, policy, workflow,
and documentation path. Include both the new suppression-document path and
every C# file whose justification changed.

Expected: no finding in any changed file.

- [ ] **Step 6: Record verification evidence**

Update `project_state.md` by replacing the stale client-publication paragraph
with the source commit under test, the explicit Sonar-free client boundary,
focused test results, build result, and prepared export result. Do not append
a second current-state narrative.

Scan `project_state.md`, run the documentation guard again, and commit:

```bash
git add project_state.md
git commit -m "docs: record Sonar-free client boundary"
```

---

### Task 5: Merge the Source Tooling Change

**Files:**
- Publish: the implementation branch containing the design, plan, code, tests,
  documentation move, and state update.

**Interfaces:**
- Consumes: the verified local branch.
- Produces: an exact merged source commit on `chris-oldgeorge/rvt-mono:main`.

- [ ] **Step 1: Rebase or merge newer source main if required**

Run:

```bash
git fetch origin main
git merge origin/main
```

Expected: clean merge. Resolve only genuine overlaps in favor of the newer
source structure while retaining the approved client boundary, then rerun
Task 4 if a merge commit was created.

- [ ] **Step 2: Push the source branch**

```bash
git push -u origin docs/client-release-sonar-exclusion-design
```

- [ ] **Step 3: Open the source pull request**

```bash
gh pr create \
  --repo chris-oldgeorge/rvt-mono \
  --base main \
  --head docs/client-release-sonar-exclusion-design \
  --title "feat: keep Sonar automation out of client releases" \
  --body-file docs/superpowers/specs/2026-07-31-client-release-sonar-exclusion-design.md
```

- [ ] **Step 4: Wait for and inspect every required check**

Run:

```bash
gh pr checks --repo chris-oldgeorge/rvt-mono --watch
```

Expected: all required source checks pass. Investigate any failure from its
logs; do not merge a red or pending PR.

- [ ] **Step 5: Merge and capture the exact source commit**

Run:

```bash
gh pr merge --repo chris-oldgeorge/rvt-mono --merge --delete-branch
git fetch origin main
git rev-parse origin/main
```

Store the printed source merge commit for Task 6.

---

### Task 6: Publish and Merge the Sonar-Free Client Review Build

**Files:**
- Generate: a temporary client export from the exact source merge commit.
- Publish: `RVT-Group-LTD/rvt-monitors:agent/sonar-free-reviewable-build`.
- Merge into: `RVT-Group-LTD/rvt-monitors:release-candidate`.

**Interfaces:**
- Consumes: the exact merged source commit from Task 5 and current client
  `release-candidate`.
- Produces: a reviewable client commit whose tree is the verified export and
  whose parent is the current client base.

- [ ] **Step 1: Export the exact merged source commit**

Run:

```bash
rvt_client_publish_root="$(mktemp -d /private/tmp/rvt-client-sonar-free.XXXXXX)"
scripts/export-client-release.sh \
  --source-ref origin/main \
  --export-dir "${rvt_client_publish_root}/payload"
```

Verify `RELEASE_SOURCE.json` contains the exact Task 5 merge commit.

- [ ] **Step 2: Create the reviewable client commit on the current base**

Run:

```bash
git clone \
  https://github.com/RVT-Group-LTD/rvt-monitors.git \
  "${rvt_client_publish_root}/repo"
git -C "${rvt_client_publish_root}/repo" \
  switch -c agent/sonar-free-reviewable-build origin/release-candidate
find "${rvt_client_publish_root}/repo" \
  -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -R \
  "${rvt_client_publish_root}/payload/." \
  "${rvt_client_publish_root}/repo/"
git -C "${rvt_client_publish_root}/repo" add -A
git -C "${rvt_client_publish_root}/repo" commit \
  -m "Publish Sonar-free reviewable build"
```

Expected: the new commit has exactly one parent,
`origin/release-candidate`, and its tree matches the prepared payload.

- [ ] **Step 3: Verify the review commit before pushing**

Run:

```bash
scripts/verify-client-release.sh \
  --payload-dir "${rvt_client_publish_root}/repo"
git -C "${rvt_client_publish_root}/repo" diff --check \
  origin/release-candidate...HEAD
git -C "${rvt_client_publish_root}/repo" rev-parse 'HEAD^{tree}'
```

Expected: the verifier passes and the diff check is clean.

- [ ] **Step 4: Push the client review branch and open its PR**

Run:

```bash
git -C "${rvt_client_publish_root}/repo" push \
  -u origin agent/sonar-free-reviewable-build
gh pr create \
  --repo RVT-Group-LTD/rvt-monitors \
  --base release-candidate \
  --head agent/sonar-free-reviewable-build \
  --title "Publish Sonar-free reviewable build" \
  --body "Removes internal Sonar workflow, runner, dedicated checks, and operator documentation from the curated client payload while retaining Tests and Engineering standards."
```

- [ ] **Step 5: Wait for client checks and inspect the file list**

Run:

```bash
gh pr checks --repo RVT-Group-LTD/rvt-monitors --watch
gh pr diff --repo RVT-Group-LTD/rvt-monitors --name-only
```

Expected:

- all required checks pass;
- `.github/workflows/sonarqube.yml`, `.github/runner/**`, the dedicated Sonar
  checks, and operator/SQL-analysis docs appear as deletions;
- `.github/workflows/tests.yml` and
  `.github/workflows/engineering-standards.yml` are not deleted.

- [ ] **Step 6: Merge the client PR and delete the review branch**

Run:

```bash
gh pr merge \
  --repo RVT-Group-LTD/rvt-monitors \
  --merge \
  --delete-branch
```

- [ ] **Step 7: Independently verify the merged client branch**

Run:

```bash
git clone \
  --branch release-candidate \
  --single-branch \
  https://github.com/RVT-Group-LTD/rvt-monitors.git \
  "${rvt_client_publish_root}/verify"
scripts/verify-client-release.sh \
  --payload-dir "${rvt_client_publish_root}/verify"
```

Confirm the merged tree equals the reviewed payload tree and
`RELEASE_SOURCE.json` still names the exact Task 5 source merge commit.

- [ ] **Step 8: Record the final state and clean up branches**

Replace the pending-publication details in `project_state.md` with the source
PR, client PR, exact merge commits, verified tree, retained client workflows,
and removed Sonar surfaces. Commit and merge that documentation-only source
update through the normal PR path.

Delete only merged local branches with `git branch -d`, leave the helper
worktree clean, and report both repository URLs and exact commit IDs.
