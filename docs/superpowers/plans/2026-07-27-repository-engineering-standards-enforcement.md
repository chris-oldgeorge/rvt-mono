# Repository Engineering Standards Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce the approved RVT Engineering Standards with a shared
configuration hierarchy, machine-readable ratchet baselines and exceptions,
changed-scope .NET/TypeScript verification, and local/CI gates that reject new
violations without making untouched legacy debt an unrelated blocker.

**Architecture:** A small Node.js verifier owns deterministic path/range
resolution, diagnostic normalization, exception validation, and baseline
comparison. New files must have zero diagnostics; diagnostics on added or
modified lines fail even when the file total is unchanged; and whole-path
diagnostic counts may only decrease. Existing SDK tools (`dotnet format`, ESLint,
and pinned Prettier) remain the diagnostic engines. Root configuration supplies
common policy; module configuration may strengthen it. Shell wrappers preserve
the repository's existing guard style and make the verifier callable from
aggregate builds and GitHub Actions.

**Tech Stack:** .NET 10 SDK, MSBuild, EditorConfig, Node.js 24, built-in
`node:test`, ESLint 9, TypeScript ESLint 8, Prettier 3.9.6, Bash, GitHub Actions.

## Global Constraints

- The normative rules are
  `docs/development/engineering-standards.md`; every task maps to its rule IDs.
- Enforcement mode is exactly `ratcheted`: new files and modified logical units
  comply; untouched legacy violations are baselined and may not increase.
- Automated changed-range checks supplement, but do not replace, review of the
  complete changed logical unit. Every implementation review records that check.
- Root policy may be strengthened by module configuration but not silently
  weakened.
- Baseline keys are tool, stable rule ID, and repository-relative POSIX path;
  counts are non-negative integers.
- Exceptions require ID, rule ID, owner, exact path or symbol scope,
  justification, introduced date, review date, removal condition, and validation
  text. Generic diagnostic exceptions use exact paths; symbol-scoped exceptions
  require a rule-specific validator.
- Public APIs, serialized names, configuration keys, routes, database names,
  and persisted values remain compatibility contracts.
- Generated, vendored, migration, dependency-cache, test-result, artifact, and
  code-index paths are excluded only through explicit repository policy.
- No task may introduce a compiler, analyzer, formatter, lint, architecture, or
  test baseline increase.
- Each behavioral implementation follows focused RED, minimal GREEN, relevant
  regression tests, and a dedicated commit.
- Do not change R1–R8 or R10–R11 production behavior in this plan.

## Scope decomposition

This plan implements the standards foundation and R9 enforcement machinery.
The remaining remediation areas are independent and retain separate plans:

1. R1 architecture-path guards;
2. R2 Help Admin release exclusion;
3. R3 reporting-lineage decision and migration;
4. R4 Portal storage/utilities retirement;
5. R5 Portal vertical-slice extraction;
6. R6 monitor narrow-port migration;
7. R7 synchronous compatibility removal;
8. R8 selectable infrastructure extraction;
9. R10 Portal client/host decomposition; and
10. R11 ambient configuration disposition verification.

Every later plan must cite applicable standard rule IDs and run the ratchet gate
created here.

---

### Task 1: Establish the shared configuration hierarchy

**Rules:** GOV-001, GOV-002, NAM-002, CSH-001, CSH-002, CSH-004,
BLD-001, BLD-002, BLD-005

**Files:**

- Create: `.editorconfig`
- Create: `Directory.Build.props`
- Modify: `apps/monitors/.editorconfig`
- Modify: `apps/portal/.editorconfig`
- Modify: `services/reporting/.editorconfig`
- Modify: `apps/monitors/Directory.Build.props`
- Modify: `apps/portal/Directory.Build.props`
- Modify: `libs/rvt-monitor-common/Directory.Build.props`
- Modify: `services/reporting/Directory.Build.props`
- Create: `tests/verify-engineering-configuration.test.sh`

**Interfaces:**

- Consumes: existing nearest-file EditorConfig/MSBuild behavior.
- Produces: one root EditorConfig policy and one root MSBuild policy imported by
  all four module build roots.

- [ ] **Step 1: Write the failing evaluated-configuration test**

Create `tests/verify-engineering-configuration.test.sh`. It MUST exercise the
configuration through the tools that consume it; it MUST NOT pass merely because
an expected XML or EditorConfig line exists.

1. For one representative project in each of the four module roots, run
   `dotnet msbuild -getProperty` and assert the evaluated values of `Nullable`,
   `ImplicitUsings`, `AnalysisLevel`, `EnforceCodeStyleInBuild`, and
   `Deterministic` match the root policy.
2. Build a temporary minimal SDK project beneath copies of each of the three real
   nested EditorConfig files. Add a probe-only naming rule to the copied root
   EditorConfig, run `dotnet format style --verify-no-changes --severity info`,
   and assert the root-only diagnostic is observed beneath every nested config.
3. Keep all probe files and NuGet artifacts inside the temporary directory. The
   test must be deterministic, offline after the repository restore, and must
   clean up through a trap.

The production change that makes this test fail is a missing root MSBuild import
or a nested EditorConfig that terminates root policy inheritance.

- [ ] **Step 2: Run the hierarchy test and verify RED**

Run:

```bash
tests/verify-engineering-configuration.test.sh
```

Expected: FAIL because the root configuration does not exist and the three nested
EditorConfig files terminate inheritance, so evaluated root properties/rules are absent.

- [ ] **Step 3: Add the root EditorConfig policy**

Create `.editorconfig`. Use suggestion-level style diagnostics initially; the
changed-scope verifier in Task 3 owns the ratchet.

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,jsonc,yml,yaml,xml,props,targets,csproj,ts,tsx,js,mjs,cjs}]
indent_size = 2

[*.cs]
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_prefer_braces = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_elsewhere = false:suggestion
dotnet_style_readonly_field = true:suggestion

dotnet_naming_symbols.private_instance_fields.applicable_kinds = field
dotnet_naming_symbols.private_instance_fields.applicable_accessibilities = private
dotnet_naming_style.private_instance_field_style.required_prefix = _
dotnet_naming_style.private_instance_field_style.capitalization = camel_case
dotnet_naming_rule.private_instance_fields_use_underscore.symbols = private_instance_fields
dotnet_naming_rule.private_instance_fields_use_underscore.style = private_instance_field_style
dotnet_naming_rule.private_instance_fields_use_underscore.severity = suggestion

[**/Migrations/**/*.cs]
generated_code = true

[**/Generated/**/*.cs]
generated_code = true
```

- [ ] **Step 4: Add root MSBuild policy and module imports**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

Insert this immediately after `<Project>` in each module build file:

```xml
<Import Project="../../Directory.Build.props" />
```

Remove `root = true` from the three nested EditorConfig files. Preserve their
module-specific rules.

- [ ] **Step 5: Run hierarchy and repository guards for GREEN**

```bash
tests/verify-engineering-configuration.test.sh
for test_script in $(find tests -maxdepth 1 -type f -name '*.test.sh' | sort); do
  "$test_script"
done
```

Expected: hierarchy verification and every existing root guard pass.

- [ ] **Step 6: Add a nested-root mutation case**

In the temporary evaluation tree, append `root = true` to each copied nested
EditorConfig in turn and prove the root-only `dotnet format` diagnostic disappears.
Also remove one copied module MSBuild import and prove at least one evaluated root
property disappears. Restore each mutant before the next case. The guard passes only
after every representative mutation is observed failing for the intended reason.

- [ ] **Step 7: Build the root solution**

```bash
dotnet restore Rvt.Mono.slnx --disable-parallel
dotnet build Rvt.Mono.slnx --no-restore --nologo -m:1
```

Expected: zero errors and no increase over the recorded warning count.

- [ ] **Step 8: Commit configuration hierarchy**

```bash
git add .editorconfig Directory.Build.props \
  apps/monitors/.editorconfig apps/portal/.editorconfig \
  services/reporting/.editorconfig \
  apps/monitors/Directory.Build.props apps/portal/Directory.Build.props \
  libs/rvt-monitor-common/Directory.Build.props \
  services/reporting/Directory.Build.props \
  tests/verify-engineering-configuration.test.sh
git commit -m "build: establish shared engineering configuration"
```

---

### Task 2: Implement the diagnostic and exception model

**Rules:** GOV-002, GOV-003, REV-001, REV-002, BLD-002

**Files:**

- Create: `scripts/engineering-standards/model.mjs`
- Create: `tests/engineering-standards-model.test.mjs`
- Create: `tests/fixtures/engineering-standards/dotnet-format-report.json`
- Create: `tests/fixtures/engineering-standards/eslint-report.json`

**Interfaces:**

- `normalizeRepositoryPath(repoRoot, candidate): string`
- `parseDotnetFormatReport(report): Diagnostic[]`
- `parseEslintReport(report): Diagnostic[]`
- `countDiagnostics(diagnostics): Map<string, number>`
- `validateBaseline(document): void`
- `validateExceptions(document, now): void`
- `compareRatchet({ diagnostics, baseline, newPaths, changedRanges, exceptions }): Result`
- `Diagnostic` is
  `{ tool: string, ruleId: string, path: string, line: number, message: string }`.
- `LineRange` is `{ startLine: number, endLine: number }`, inclusive.
- `Result` is
  `{ changedSurfaceViolations: Diagnostic[], increases: Delta[], decreases: Delta[], unchanged: Delta[] }`.

- [ ] **Step 1: Add realistic report fixtures**

The .NET fixture uses:

```json
[
  {
    "FilePath": "/repo/src/Clock.cs",
    "FileChanges": [
      {
        "LineNumber": 7,
        "CharNumber": 5,
        "DiagnosticId": "IDE0055",
        "FormatDescription": "Fix formatting"
      }
    ]
  }
]
```

The ESLint fixture uses:

```json
[
  {
    "filePath": "/repo/src/calendarDate.ts",
    "messages": [
      {
        "ruleId": "@typescript-eslint/no-unused-vars",
        "severity": 2,
        "message": "'unused' is assigned a value but never used.",
        "line": 4,
        "column": 7
      }
    ]
  }
]
```

- [ ] **Step 2: Write failing pure-model tests**

Use `node:test` and `node:assert/strict`. Include path normalization, outside-root
rejection, duplicate/negative/fractional baseline rejection, expired exceptions,
exact exception matching, baseline increase/decrease, a stable-count diagnostic on
a changed line, a pre-existing diagnostic outside changed ranges, and a diagnostic
in a new file.

```javascript
test('reports a changed-scope baseline increase', () => {
  const result = compareRatchet({
    diagnostics: [
      { tool: 'dotnet-format-style', ruleId: 'IDE0055', path: 'src/Clock.cs', line: 7, message: 'Fix formatting' },
      { tool: 'dotnet-format-style', ruleId: 'IDE0055', path: 'src/Clock.cs', line: 11, message: 'Fix formatting' }
    ],
    baseline: new Map([['dotnet-format-style\tIDE0055\tsrc/Clock.cs', 1]]),
    newPaths: new Set(),
    changedRanges: new Map([['src/Clock.cs', [{ startLine: 7, endLine: 7 }]]]),
    exceptions: []
  });
  assert.equal(result.increases[0].observed, 2);
  assert.equal(result.increases[0].baseline, 1);
});
```

- [ ] **Step 3: Run model tests for RED**

```bash
node --test tests/engineering-standards-model.test.mjs
```

Expected: FAIL because `model.mjs` does not exist.

- [ ] **Step 4: Implement the pure model**

Baseline keys MUST be constructed only through:

```javascript
export function diagnosticKey({ tool, ruleId, path }) {
  return `${tool}\t${ruleId}\t${path}`;
}
```

Normal comparison rejects every non-excepted diagnostic in a new file or changed
line range, then compares whole-path counts with missing entries treated as zero.
It never lets an exception match another rule or broader path. A reviewer still
checks the complete changed logical unit because generic .NET/ESLint reports do
not provide a shared cross-language syntax-tree boundary model.

- [ ] **Step 5: Run model tests for GREEN**

```bash
node --test tests/engineering-standards-model.test.mjs
```

Expected: all tests pass.

- [ ] **Step 6: Commit the model**

```bash
git add scripts/engineering-standards/model.mjs \
  tests/engineering-standards-model.test.mjs \
  tests/fixtures/engineering-standards/dotnet-format-report.json \
  tests/fixtures/engineering-standards/eslint-report.json
git commit -m "test: define engineering standards ratchet model"
```

---

### Task 3: Build the changed-scope verifier

**Rules:** GOV-001, GOV-002, GOV-003, CSH-002, WEB-001, BLD-002,
BLD-006, REV-001

**Files:**

- Create: `scripts/engineering-standards/verify.mjs`
- Create: `scripts/verify-engineering-standards.sh`
- Create: `tests/verify-engineering-standards.test.sh`
- Create: `tests/fixtures/engineering-standards/baseline.json`
- Create: `tests/fixtures/engineering-standards/exceptions.json`

**Interfaces:**

- `--working-tree`: staged, unstaged, and untracked paths relative to `HEAD`.
- `--base REV --head REV`: committed comparison range.
- `--base auto --head HEAD`: feature merge-base against `origin/main`, or
  `HEAD^` when `HEAD == origin/main`.
- `--all`: all tracked source paths.
- `--initialize-baseline`: create the baseline exactly once; refuse when it already
  exists or contains entries.
- `--update-baseline`: write only equal-or-lower observed counts.
- Test-only command overrides are `RVT_STANDARDS_DOTNET_COMMAND`,
  `RVT_STANDARDS_ESLINT_COMMAND`, `RVT_STANDARDS_PRETTIER_COMMAND`,
  `RVT_STANDARDS_BASELINE_PATH`, and `RVT_STANDARDS_EXCEPTIONS_PATH`.
- Exit codes: `0` compliant, `1` policy violation, `2` invocation/tool failure.

- [ ] **Step 1: Write the end-to-end shell test and fake tools**

The test creates a temporary Git repository and fake .NET, ESLint, and Prettier
commands. Required scenarios are: clean tree skips source tools; changed C#
invokes whitespace/style/analyzers; changed TypeScript invokes ESLint/Prettier;
new-file and changed-line diagnostics fail even at a stable total; an unchanged-line
legacy diagnostic is allowed only at or below baseline; an increase fails with
counts; a decrease is reported; an expired exception fails before tools run;
baseline initialization succeeds once and is then refused; baseline update refuses
increases and writes decreases atomically; outside/generated/cache paths are
rejected or excluded.

- [ ] **Step 2: Run the verifier test for RED**

```bash
tests/verify-engineering-standards.test.sh
```

Expected: FAIL because the verifier and wrapper do not exist.

- [ ] **Step 3: Implement range and path resolution**

Run Git with argument arrays. Working-tree mode uses:

```javascript
const tracked = gitLines(['diff', '--name-only', '--diff-filter=ACMR', 'HEAD']);
const untracked = gitLines(['ls-files', '--others', '--exclude-standard']);
const changedPaths = new Set([...tracked, ...untracked].map(toPosixPath));
const patch = gitText(['diff', '--unified=0', '--no-ext-diff', 'HEAD', '--', ...tracked]);
const changedRanges = parseUnifiedDiffRanges(patch);
const newPaths = new Set([
  ...gitLines(['diff', '--name-only', '--diff-filter=A', 'HEAD']),
  ...untracked
]);
```

Committed-range mode parses the same zero-context patch between the resolved
base and head revisions. Every existing changed path must have at least one parsed
new-side hunk; every untracked file is new and every source line is changed. Reject
binary source inputs and malformed or path-escaping patches with exit code `2`.

Use exact ignored prefixes plus nested-segment matching:

```javascript
const ignoredPrefixes = [
  '.git/', '.worktrees/', '.codegraph/', 'artifacts/',
  'node_modules/', 'bin/', 'obj/', 'dist/', 'coverage/',
  'TestResults/', 'playwright-report/', 'test-results/'
];
```

- [ ] **Step 4: Implement .NET and frontend execution**

For changed C# files run:

```text
dotnet format Rvt.Mono.slnx whitespace --verify-no-changes --no-restore --include PATHS --report REPORT
dotnet format Rvt.Mono.slnx style --verify-no-changes --no-restore --severity info --include PATHS --report REPORT
dotnet format Rvt.Mono.slnx analyzers --verify-no-changes --no-restore --severity warn --include PATHS --report REPORT
```

In working-tree/range modes, whitespace failure is immediate for every changed
C# file. In inventory mode, normalize its report like the style/analyzer reports.
Style/analyzer nonzero exits are accepted only with a readable report, no
non-excepted diagnostic on a new file or changed line, and no normalized whole-path
count increase.

For changed Portal client files run:

```text
node_modules/.bin/prettier --list-different PATHS
node_modules/.bin/eslint --format json TYPESCRIPT_PATHS
```

Prettier is file-scoped: any changed Portal file that fails formatting is an
immediate violation. ESLint uses the same new-file, changed-range, and whole-path
ratchet rules as .NET diagnostics. `--all` is inventory mode: it records legacy
Prettier/ESLint diagnostics without treating every tracked line as changed; changed
surface rules apply only to working-tree and committed-range modes. Missing
executables exit `2` with
`run npm ci in apps/portal/RvtPortal.Client`.

- [ ] **Step 5: Add the shell wrapper**

```bash
#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec node "${repo_root}/scripts/engineering-standards/verify.mjs" "$@"
```

- [ ] **Step 6: Run verifier and model tests for GREEN**

```bash
node --test tests/engineering-standards-model.test.mjs
tests/verify-engineering-standards.test.sh
```

Expected: all Node and shell scenarios pass.

- [ ] **Step 7: Commit the verifier**

```bash
git add scripts/engineering-standards/verify.mjs \
  scripts/verify-engineering-standards.sh \
  tests/verify-engineering-standards.test.sh \
  tests/fixtures/engineering-standards/baseline.json \
  tests/fixtures/engineering-standards/exceptions.json
git commit -m "build: enforce changed-scope standards ratchet"
```

---

### Task 4: Add deterministic frontend formatting and naming policy

**Rules:** NAM-005, WEB-001, WEB-002, WEB-003, WEB-004, CSH-002,
BLD-001, BLD-006

**Files:**

- Create: `apps/portal/RvtPortal.Client/.prettierrc.json`
- Create: `apps/portal/RvtPortal.Client/.prettierignore`
- Modify: `apps/portal/RvtPortal.Client/package.json`
- Modify: `apps/portal/RvtPortal.Client/package-lock.json`
- Modify: `apps/portal/RvtPortal.Client/eslint.config.js`
- Create then delete: `apps/portal/RvtPortal.Client/src/test/engineeringStandardsFixture.ts`
- Create then delete: `apps/portal/RvtPortal.Client/src/test/engineeringStandardsFixture.test.ts`

**Interfaces:**

- Adds local `prettier` version exactly `3.9.6`.
- Adds TypeScript ESLint naming policy; legacy counts are captured in Task 5.

- [ ] **Step 1: Add a deliberately non-compliant frontend fixture**

```typescript
export function formatMonitorId(monitor_id: number): string {
  return `monitor-${monitor_id}`;
}
```

```typescript
import { describe, expect, it } from 'vitest';
import { formatMonitorId } from './engineeringStandardsFixture';

describe('formatMonitorId', () => {
  it('formats the stable monitor identifier', () => {
    expect(formatMonitorId(42)).toBe('monitor-42');
  });
});
```

- [ ] **Step 2: Prove the formatter is not yet repository-managed**

```bash
cd apps/portal/RvtPortal.Client
test ! -x node_modules/.bin/prettier
```

Expected: pass, proving the repository has not yet installed its pinned
formatter. Do not use `npx prettier`, because it may download an unpinned tool.

- [ ] **Step 3: Install pinned Prettier and configuration**

```bash
npm install --save-dev --save-exact prettier@3.9.6
```


Create `.prettierrc.json`:

```json
{
  "endOfLine": "lf",
  "printWidth": 120,
  "semi": true,
  "singleQuote": true,
  "tabWidth": 2,
  "trailingComma": "all"
}
```

Create `.prettierignore`:

```text
coverage/
dist/
node_modules/
playwright-report/
test-results/
src/api/schema.d.ts
```

- [ ] **Step 4: Add ESLint naming convention**

```javascript
'@typescript-eslint/naming-convention': [
  'warn',
  { selector: 'variableLike', format: ['camelCase', 'PascalCase', 'UPPER_CASE'] },
  { selector: 'typeLike', format: ['PascalCase'] },
  { selector: 'parameter', format: ['camelCase'], leadingUnderscore: 'allow' }
]
```

Do not apply the rule to generated `src/api/schema.d.ts`. Make the generated
file exclusion structural in ESLint configuration, not an inline suppression.

- [ ] **Step 5: Run the local tools against the non-compliant fixture for RED**

```bash
node_modules/.bin/eslint --max-warnings 0 src/test/engineeringStandardsFixture.ts
node_modules/.bin/prettier --check src/test/engineeringStandardsFixture.ts
```

Expected: both commands fail. ESLint names `monitor_id`; Prettier reports the
fixture as unformatted. Fix the policy or fixture if either command passes.

- [ ] **Step 6: Correct fixture and verify GREEN**

Rename `monitor_id` to `monitorId`, run Prettier, then run:

```bash
node_modules/.bin/eslint --max-warnings 0 src/test/engineeringStandardsFixture.ts \
  src/test/engineeringStandardsFixture.test.ts
node_modules/.bin/prettier --check src/test/engineeringStandardsFixture.ts \
  src/test/engineeringStandardsFixture.test.ts
npm run test:run -- src/test/engineeringStandardsFixture.test.ts
```

Expected: lint, format, and the focused Vitest test pass.

- [ ] **Step 7: Remove the policy-only fixture**

Delete both fixture files. The verifier shell test is the durable regression.

- [ ] **Step 8: Run existing frontend gates**

```bash
npm run lint
npm run test:run
npm run build
```

Expected: zero lint errors, all tests pass, and production build succeeds.
Record warning counts by rule for Task 5; existing Fast Refresh and newly exposed
legacy naming warnings are ratchet inputs, not reasons for blanket suppression.

- [ ] **Step 9: Commit frontend enforcement**

```bash
git add apps/portal/RvtPortal.Client/.prettierrc.json \
  apps/portal/RvtPortal.Client/.prettierignore \
  apps/portal/RvtPortal.Client/package.json \
  apps/portal/RvtPortal.Client/package-lock.json \
  apps/portal/RvtPortal.Client/eslint.config.js
git commit -m "build: add frontend standards formatting"
```

---

### Task 5: Capture the legacy baseline and module policy

**Rules:** GOV-002, GOV-003, BLD-002, BLD-003, BLD-004, TST-005,
REV-001

**Files:**

- Create: `eng/standards/baseline.json`
- Create: `eng/standards/exceptions.json`
- Create: `eng/standards/module-policy.json`
- Create: `tests/verify-engineering-standards-policy.test.mjs`

**Interfaces:**

`baseline.json` uses:

```json
{
  "version": 1,
  "generatedAt": "2026-07-27",
  "entries": [
    {
      "tool": "dotnet-format-style",
      "ruleId": "IDE0055",
      "path": "relative/path.cs",
      "count": 1
    }
  ]
}
```

`exceptions.json` starts exactly as:

```json
{
  "version": 1,
  "exceptions": []
}
```

`module-policy.json` records current supported boundaries:

```json
{
  "version": 1,
  "modules": [
    {
      "path": "apps/monitors",
      "testFramework": "MSTest",
      "packageVersionPolicy": "module-central",
      "testFrameworkOverrides": [
        {
          "path": "apps/monitors/reportingmonitor",
          "testFramework": "xUnit"
        }
      ]
    },
    {
      "path": "apps/portal",
      "testFramework": "xUnit",
      "packageVersionPolicy": "project-inline-legacy"
    },
    {
      "path": "libs/rvt-monitor-common",
      "testFramework": "MSTest",
      "packageVersionPolicy": "module-central-locked"
    },
    {
      "path": "services/reporting",
      "testFramework": "xUnit",
      "packageVersionPolicy": "project-inline-legacy"
    }
  ]
}
```

- [ ] **Step 1: Write failing policy tests**

Test that baseline entries are deterministically sorted and unique; counts are
non-negative integers; generated/cache paths are absent; exceptions validate at
the fixed date `2026-07-27`; each test project matches the longest exact module
or override prefix; package version style matches module policy; and a fixture
adding xUnit to an MSTest project outside the reporting-monitor override fails.
Also mutate the override path and prove the existing ReportingMonitor xUnit
project then fails. This preserves the approved no-framework-migration non-goal
without allowing an accidental framework change elsewhere.

```bash
node --test tests/verify-engineering-standards-policy.test.mjs
```

Expected: FAIL because the policy files do not exist.

- [ ] **Step 2: Create exceptions and module policy**

Create the exact JSON shapes above. Do not create exceptions merely to make
diagnostic capture pass; legacy diagnostics belong in `baseline.json`.

- [ ] **Step 3: Capture actual diagnostics**

```bash
dotnet restore Rvt.Mono.slnx --disable-parallel
npm --prefix apps/portal/RvtPortal.Client ci
scripts/verify-engineering-standards.sh --all --initialize-baseline
```

Expected: baseline JSON is written atomically with deterministic ordering.
Re-running initialization is refused. The ordinary `--all --update-baseline`
command produces no diff and can only reduce entries.

- [ ] **Step 4: Review baseline exclusions**

```bash
git diff -- eng/standards/baseline.json
rg -n '(^|/)(bin|obj|node_modules|dist|coverage|TestResults)/' \
  eng/standards/baseline.json
```

Expected: the first command shows normalized diagnostics; the second has no
output. Do not manually delete legitimate diagnostics.

- [ ] **Step 5: Run policy and ratchet tests for GREEN**

```bash
node --test tests/engineering-standards-model.test.mjs \
  tests/verify-engineering-standards-policy.test.mjs
tests/verify-engineering-standards.test.sh
scripts/verify-engineering-standards.sh --working-tree
```

Expected: all tests pass with no baseline increase.

- [ ] **Step 6: Commit policy and baseline**

```bash
git add eng/standards/baseline.json eng/standards/exceptions.json \
  eng/standards/module-policy.json \
  tests/verify-engineering-standards-policy.test.mjs
git commit -m "build: baseline legacy standards diagnostics"
```

---

### Task 6: Wire the ratchet into aggregate build and CI

**Rules:** GOV-002, BLD-001, BLD-002, BLD-006, REV-005

**Files:**

- Modify: `scripts/build-mono.sh`
- Modify: `tests/verify-rvt-common-source-boundary.test.sh`
- Modify: `.github/workflows/sonarqube.yml`
- Create: `tests/verify-engineering-standards-integration.test.sh`

**Interfaces:**

- Local build calls `scripts/verify-engineering-standards.sh --working-tree`
  after restore and before compile.
- GitHub Actions calls
  `scripts/verify-engineering-standards.sh --base auto --head HEAD` after .NET
  restore and `npm ci`, before build.

- [ ] **Step 1: Write the failing integration-order test**

Copy the build script and workflow to a temporary root and assert:

1. PostgreSQL boundary verification remains first;
2. restore occurs before standards verification;
3. standards verification occurs before build/test;
4. workflow `npm ci` and .NET restore occur before standards verification;
5. standards verification occurs before the Release build; and
6. Portal coverage does not run a second `npm ci`.

Mutate a copy by removing the standards command and prove the test fails.

- [ ] **Step 2: Run integration test for RED**

```bash
tests/verify-engineering-standards-integration.test.sh
```

Expected: FAIL because aggregate build and workflow do not invoke the verifier.

- [ ] **Step 3: Update aggregate build order**

The resulting sequence in `scripts/build-mono.sh` is:

```bash
bash scripts/verify-postgresql-only.sh .
dotnet restore "${solution}" --disable-parallel
"${repo_root}/scripts/verify-engineering-standards.sh" --working-tree
dotnet build "${solution}" --no-restore --nologo -m:1
dotnet test "${solution}" --no-build --nologo
```

Update `tests/verify-rvt-common-source-boundary.test.sh` so its fake sequence
still proves no pack/package-validation calls and recognizes the standards
boundary without bypassing it.

- [ ] **Step 4: Add the workflow gate**

In `.github/workflows/sonarqube.yml`:

1. add this immediately after Node setup:

```yaml
- name: Install Portal client dependencies
  working-directory: apps/portal/RvtPortal.Client
  run: npm ci
```

2. split “Restore and build monorepo” into restore, standards verification,
   and Release build steps;
3. run:

```bash
scripts/verify-engineering-standards.sh --base auto --head HEAD
```

4. remove the duplicate `npm ci` from Portal coverage.

All action references remain commit-SHA pinned.

- [ ] **Step 5: Run integration and workflow guards for GREEN**

```bash
tests/verify-engineering-standards-integration.test.sh
tests/verify-manual-sonarqube-workflow.test.sh
tests/verify-rvt-common-source-boundary.test.sh
```

Expected: all pass, including the mutation case.

- [ ] **Step 6: Run aggregate build sequence**

```bash
scripts/build-mono.sh
```

Expected: standards verification, root build, and tests complete with zero new
violations. Classify environment-dependent failures; do not weaken the gate.

- [ ] **Step 7: Commit build and CI integration**

```bash
git add scripts/build-mono.sh tests/verify-rvt-common-source-boundary.test.sh \
  .github/workflows/sonarqube.yml \
  tests/verify-engineering-standards-integration.test.sh
git commit -m "ci: enforce engineering standards ratchet"
```

---

### Task 7: Complete R9 documentation and end-to-end verification

**Rules:** DOC-004, DOC-005, REV-001, REV-003, REV-005

**Files:**

- Modify: `docs/development/engineering-standards.md`
- Modify: `docs/superpowers/specs/2026-07-27-repository-engineering-standards-design.md`
- Modify: `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
- Create: `docs/development/engineering-standards-enforcement.md`
- Create: `docs/reviews/2026-07-27-engineering-standards-enforcement-report.md`
- Modify: `docs/index.md`
- Modify: `project_state.md`

**Interfaces:**

- Developer guide documents verifier modes, baseline reduction, exceptions,
  local remediation, and CI behavior.
- Enforcement report maps every approved requirement to code, tests, and
  command evidence.

- [ ] **Step 1: Write the enforcement guide**

Document exact workflows:

```bash
# Check current changes
scripts/verify-engineering-standards.sh --working-tree

# Compare a branch with main
scripts/verify-engineering-standards.sh --base auto --head HEAD

# Reduce baseline after cleanup; increases are refused
scripts/verify-engineering-standards.sh --all --update-baseline
```

Include a complete exception JSON example with every GOV-003 field. Exception
review dates use ISO `YYYY-MM-DD` UTC calendar dates.

- [ ] **Step 2: Update authoritative status documents**

- Mark R9 complete only after Task 1–6 gates pass.
- Change design status from `Approved for implementation` to `Implemented`.
- Link the enforcement guide/report from `docs/index.md`.
- Record branch, commits, baseline entry counts by tool, exact test commands,
  warnings, and remaining R1–R11 sequence in `project_state.md`.

- [ ] **Step 3: Run complete standards test matrix**

```bash
node --test tests/engineering-standards-model.test.mjs \
  tests/verify-engineering-standards-policy.test.mjs
tests/verify-engineering-configuration.test.sh
tests/verify-engineering-standards.test.sh
tests/verify-engineering-standards-integration.test.sh
scripts/verify-engineering-standards.sh --working-tree
```

Expected: all pass with zero baseline increase.

- [ ] **Step 4: Run every root guard**

```bash
for test_script in $(find tests -maxdepth 1 -type f -name '*.test.sh' | sort); do
  "$test_script"
done
```

Expected: every root guard passes.

- [ ] **Step 5: Run backend and frontend aggregate verification**

```bash
dotnet restore Rvt.Mono.slnx --locked-mode --disable-parallel
dotnet build Rvt.Mono.slnx --no-restore --nologo -m:1 \
  -p:UseSharedCompilation=false
dotnet test Rvt.Mono.slnx --no-build --nologo -m:1
npm --prefix apps/portal/RvtPortal.Client run lint
npm --prefix apps/portal/RvtPortal.Client run test:run
npm --prefix apps/portal/RvtPortal.Client run build
git diff --check
```

Expected: restore/build/frontend gates pass. Report exact test totals and
classify environment-dependent failures without suppressing them. No new
warning or baseline increase is accepted.

- [ ] **Step 6: Prove real ratchet increases fail**

In a temporary copy, add one formatting violation to a changed C# file and one
naming violation to a changed TypeScript file. Assert both tool/rule/path deltas
are reported. Delete the temporary copy; do not alter production source.

- [ ] **Step 7: Commit implementation evidence**

```bash
git add docs/development/engineering-standards.md \
  docs/development/engineering-standards-enforcement.md \
  docs/superpowers/specs/2026-07-27-repository-engineering-standards-design.md \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md \
  docs/reviews/2026-07-27-engineering-standards-enforcement-report.md \
  docs/index.md project_state.md
git commit -m "docs: record engineering standards enforcement"
```

## Completion audit

Before calling R9 complete, verify every approved requirement:

| Requirement | Required evidence |
| --- | --- |
| One authoritative standard | Root README/index links and normative document |
| Ratcheted changed-scope enforcement | New-file, changed-line, increase/decrease tests and real mutation proof |
| Logical-unit compliance | Required review record plus automated changed-range evidence |
| Root plus stricter module policy | Hierarchy guard and four MSBuild imports |
| Stable rule/evidence model | Model tests and deterministic baseline keys |
| Owned, expiring exceptions | Validation and expired-exception RED case |
| .NET formatting/analyzers | Changed-C# fake-tool test and real repository run |
| TypeScript lint/format | Pinned Prettier, ESLint naming policy, changed-file test |
| No package/test-policy drift | Module-policy mutation test |
| Local aggregate enforcement | `build-mono.sh` ordering test |
| CI enforcement | Workflow guard and Sonar workflow structure test |
| Guards can fail | Nested root, increase, removed gate, C#, and TS mutations |
| No regression | Root guards, backend/frontend gates, zero increase |

If evidence is absent, indirect, or pass-only, R9 remains incomplete.

## Next remediation sequence

After this plan is complete and merged, write and execute a separate R1 plan for
stale MyAtm/Svantek architecture paths and the shared repository-layout helper.
Apply the same spec, test-first plan, focused review, and aggregate verification
cycle to each remaining remediation area. Do not combine R2 product behavior or
R3 reporting-lineage decisions with R1.
