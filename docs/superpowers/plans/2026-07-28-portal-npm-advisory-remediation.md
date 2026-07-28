# Portal npm Advisory Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all six high-severity Portal npm advisories without changing application behavior or upgrading Vite beyond major 6.

**Architecture:** Upgrade the ESLint toolchain as one peer-compatible unit so ESLint no longer pulls the vulnerable legacy `minimatch` graph. Regenerate the Node 24 lockfile so Vite resolves patched PostCSS 8.5.18 or newer, then use the audit, lint, unit-test, and production-build commands as the behavioral and security gates.

**Tech Stack:** Node.js 24, npm, ESLint 10, TypeScript-ESLint 8, Vite 6, Vitest 4, React 19.

## Global Constraints

- Keep `apps/portal/RvtPortal.Client` on Node `>=24 <25`.
- Keep Vite on major 6; a Vite major upgrade is outside this advisory scope.
- Do not use `npm audit fix --force`.
- Do not add broad transitive overrides unless the peer-compatible direct upgrade still leaves a reportable advisory.
- Preserve the two existing `react-refresh/only-export-components` lint warnings; introduce no lint errors or new warnings.
- Finish with zero npm vulnerabilities at every severity.

---

### Task 1: Replace the vulnerable ESLint dependency graph

**Files:**
- Modify: `apps/portal/RvtPortal.Client/package.json`
- Modify: `apps/portal/RvtPortal.Client/package-lock.json`

**Interfaces:**
- Consumes: the existing flat config in `apps/portal/RvtPortal.Client/eslint.config.js`
- Produces: an npm dependency graph compatible with Node 24 and ESLint 10

- [ ] **Step 1: Preserve the failing security test**

Run:

```bash
docker run --rm \
  -v "$PWD/apps/portal/RvtPortal.Client:/workspace:ro" \
  -w /workspace node:24-alpine \
  npm audit --package-lock-only
```

Expected: exit `1`, reporting six high-severity vulnerabilities through ESLint/minimatch/brace-expansion and PostCSS.

- [ ] **Step 2: Upgrade the peer-compatible lint toolchain**

Set these development dependency ranges in `package.json`:

```json
"@eslint/js": "^10.0.1",
"eslint": "^10.8.0",
"eslint-plugin-react-hooks": "^7.1.1",
"eslint-plugin-react-refresh": "^0.5.3",
"typescript-eslint": "^8.65.0"
```

Remove the obsolete `overrides` block after the upgraded graph no longer contains `@eslint/eslintrc` or legacy `minimatch` releases.

- [ ] **Step 3: Regenerate the lockfile with Node 24**

Run in a writable Node 24 container copy:

```bash
docker run --rm \
  -v "$PWD/apps/portal/RvtPortal.Client:/source:ro" \
  -v "$PWD/apps/portal/RvtPortal.Client:/output" \
  node:24-alpine sh -lc \
  'cp -R /source /tmp/client &&
   cd /tmp/client &&
   npm install --package-lock-only --ignore-scripts &&
   cp package-lock.json /output/package-lock.json'
```

Expected: the lock resolves ESLint 10.8.0, `brace-expansion` 5.0.8 or newer, and PostCSS 8.5.18 or newer.

- [ ] **Step 4: Verify the security test turns green**

Run:

```bash
docker run --rm \
  -v "$PWD/apps/portal/RvtPortal.Client:/workspace:ro" \
  -w /workspace node:24-alpine \
  npm audit --package-lock-only
```

Expected: exit `0` and `found 0 vulnerabilities`.

- [ ] **Step 5: Commit the dependency graph**

```bash
git add apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json
git commit -m "build: remediate Portal npm advisories"
```

### Task 2: Prove ESLint 10 and the patched build graph preserve behavior

**Files:**
- Verify: `apps/portal/RvtPortal.Client/eslint.config.js`
- Verify: `apps/portal/RvtPortal.Client/src/**/*.ts`
- Verify: `apps/portal/RvtPortal.Client/src/**/*.tsx`

**Interfaces:**
- Consumes: the regenerated package lock from Task 1
- Produces: lint, test, and production-build evidence under Node 24

- [ ] **Step 1: Install and run the complete Portal verification gate**

Run:

```bash
docker run --rm \
  -v "$PWD/apps/portal/RvtPortal.Client:/workspace:ro" \
  -w /tmp node:24-alpine sh -lc \
  'cp -R /workspace /tmp/client &&
   cd /tmp/client &&
   npm ci --ignore-scripts &&
   npm run lint &&
   npm run test:run &&
   npm run build &&
   npm audit'
```

Expected: npm install succeeds without peer conflicts; lint has zero errors and only the two existing Fast Refresh warnings; all 78 Vitest tests pass; Vite production build succeeds; audit reports zero vulnerabilities.

- [ ] **Step 2: Inspect the resolved security floors**

Run:

```bash
node -e '
const lock = require("./apps/portal/RvtPortal.Client/package-lock.json");
for (const name of ["eslint", "brace-expansion", "postcss"]) {
  const versions = Object.entries(lock.packages)
    .filter(([path]) => path === `node_modules/${name}` || path.endsWith(`/node_modules/${name}`))
    .map(([, value]) => value.version);
  console.log(name, [...new Set(versions)].sort());
}'
```

Expected: no ESLint 9, no `brace-expansion` version below 5.0.8, and no PostCSS version below 8.5.18.

- [ ] **Step 3: Run repository policy verification**

Run:

```bash
scripts/verify-engineering-standards.sh --base origin/main --head HEAD
tests/verify-engineering-standards-workflow.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
git diff --check
```

Expected: every command exits `0`.

### Task 3: Record and publish the completed remediation

**Files:**
- Modify: `project_state.md`

**Interfaces:**
- Consumes: the verified package graph and command evidence from Tasks 1-2
- Produces: the durable resume checkpoint and reviewable GitHub branch

- [ ] **Step 1: Update the authoritative checkpoint**

Record the dedicated branch, old and new advisory counts, exact direct package versions, resolved `brace-expansion` and PostCSS security floors, test totals, lint warning count, build result, and repository policy results. Preserve Help Admin/R2 as separate conditional operator work.

- [ ] **Step 2: Verify and commit the checkpoint**

Run:

```bash
git diff --check
scripts/verify-engineering-standards.sh --base origin/main --head HEAD
git add project_state.md
git commit -m "docs: record Portal npm remediation"
```

Expected: verification exits `0` and the commit contains only `project_state.md`.

- [ ] **Step 3: Push the dedicated branch and open a draft PR**

Run:

```bash
git push -u origin codex/portal-npm-advisories
gh pr create \
  --base main \
  --head codex/portal-npm-advisories \
  --draft \
  --title "Remediate Portal npm advisories" \
  --body-file /private/tmp/rvt-portal-npm-advisories-pr.md
```

Expected: GitHub returns a draft pull-request URL and the Engineering standards check starts for the branch.
