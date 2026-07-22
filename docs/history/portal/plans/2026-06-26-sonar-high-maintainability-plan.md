# Sonar High Maintainability Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce the current SonarCloud HIGH maintainability-impact issue set for `aileron-forward_rvtportal-spa-alpha` from 728 open issues to zero or to an explicitly documented/ignored legacy-script baseline.

**Architecture:** Treat application code and SQL migration artifacts separately. C# and PowerShell issues should be fixed in source with focused tests. SQL script issues are concentrated in generated or one-off canonical migration drafts and should either be remediated mechanically or excluded/ignored with an explicit documented rationale before analysis is refreshed.

**Tech Stack:** SonarCloud, C#/.NET 10, xUnit, SQL Server/Postgres migration scripts, PowerShell release scripts, GitHub Actions Sonar scanner.

---

## Live Sonar Snapshot

Observed on 2026-06-26 via authenticated SonarCloud API:

```text
Filter: softwareQualities=MAINTAINABILITY, impactSeverities=HIGH, statuses=OPEN,CONFIRMED,REOPENED
Total: 728 issues
Rules: 7
Files: 36
```

Rule breakdown:

| Rule | Count | Primary Fix |
| --- | ---: | --- |
| `plsql:S1192` duplicated literals | 577 | Decide script exclusion vs constants/mechanical generation cleanup |
| `plsql:LiteralsNonPrintableCharactersCheck` | 84 | Normalize dynamic SQL literals or exclude generated script drafts |
| `csharpsquid:S1006` default parameter mismatch | 36 | Add implementation default values matching interfaces |
| `csharpsquid:S927` parameter name mismatch | 22 | Rename implementation parameters to match interface declarations |
| `csharpsquid:S3776` cognitive complexity | 6 | Extract small helpers without behavior changes |
| `csharpsquid:S4487` unread private fields | 2 | Remove unused fields/dependencies |
| `powershelldre:S3776` cognitive complexity | 1 | Split release export function into helpers |

File breakdown by count:

| File | Count | Rules |
| --- | ---: | --- |
| `database/sqlserver/canonical_database_naming.sql` | 258 | `plsql:S1192` |
| `database/sqlserver/canonical_database_naming_rollback.sql` | 258 | `plsql:S1192` |
| `database/sqlserver/canonical_view_module_rewrite.sql` | 37 | `plsql:LiteralsNonPrintableCharactersCheck` |
| `database/sqlserver/canonical_view_module_rewrite_rollback.sql` | 37 | `plsql:LiteralsNonPrintableCharactersCheck` |
| `RVT.BusinessLogic/MonitorService.cs` | 32 | `csharpsquid:S927`, `csharpsquid:S1006` |
| `database/sqlserver/monitor_natural_key_changes_20260618.sql` | 19 | `plsql:S1192` |
| `database/sqlserver/canonical_constraint_index_naming.sql` | 12 | `plsql:S1192` |
| `database/sqlserver/canonical_constraint_index_naming_rollback.sql` | 12 | `plsql:S1192` |
| `RVT.DatabaseMigrator/post-load/03_views_and_routines.sql` | 7 | `plsql:S1192` |
| `RVT.BusinessLogic/NotificationService.cs` | 6 | `csharpsquid:S1006` |
| `RVT.Dataaccess/SiteUserRepository.cs` | 6 | `csharpsquid:S927` |
| `database/sqlserver/performance_indexes_20260609.sql` | 5 | `plsql:S1192` |
| `database/sqlserver/canonical_routine_rewrite.sql` | 5 | `plsql:LiteralsNonPrintableCharactersCheck` |
| `database/sqlserver/canonical_routine_rewrite_rollback.sql` | 5 | `plsql:LiteralsNonPrintableCharactersCheck` |
| `RVT.BusinessLogic/LookupService.cs` | 3 | `csharpsquid:S4487`, `csharpsquid:S927`, `csharpsquid:S1006` |
| `RVT.BusinessLogic/MonitorData.cs` | 3 | `csharpsquid:S3776` |
| 20 additional files | 35 | low-count C#, SQL, and PowerShell issues |

Important context:

- Historical note: the implementation branch was later pushed, merged to `master`, and Plane issue `#437` was marked Done with merged-master verification evidence.
- Existing workflow uses `/d:sonar.cpd.exclusions=...`; this excludes duplication scoring only. It does not suppress maintainability issue rules such as `plsql:S1192`.

---

### Task 1: Preserve The Sonar Inventory And Add Local Guardrails

**Files:**
- Modify: `docs/superpowers/plans/2026-06-26-sonar-high-maintainability-plan.md`
- Modify: `project_state.md`
- Optionally create: `docs/sonar/high-maintainability-2026-06-26.md`

- [x] **Step 1: Refresh the issue list before implementation**

Run from `/private/tmp/rvtportal-spa-alpha-transaction` with `SONAR_TOKEN` in the shell environment, not committed anywhere:

```bash
python3 scripts/dev/fetch_sonar_issues.py \
  --organization aileron-forward \
  --project aileron-forward_rvtportal-spa-alpha \
  --software-quality MAINTAINABILITY \
  --impact-severity HIGH \
  --output /private/tmp/rvt_sonar_high_maintainability_all.json
```

If the helper does not exist yet, use a one-off local script in `/private/tmp`; do not add credentials to the repo.

- [x] **Step 2: Confirm the C# issue set**

Expected C# issue count before fixes:

```text
csharpsquid:S1006 = 36
csharpsquid:S927 = 22
csharpsquid:S3776 = 6
csharpsquid:S4487 = 2
```

- [x] **Step 3: Commit only documentation if this task changes docs**

```bash
git add docs/superpowers/plans/2026-06-26-sonar-high-maintainability-plan.md
git commit -m "docs(sonar): plan high maintainability cleanup"
```

### Task 2: Fix Low-Risk C# Signature And Field Issues

**Files:**
- Modify: `RVT.BusinessLogic/MonitorService.cs`
- Modify: `RVT.BusinessLogic/NotificationService.cs`
- Modify: `RVT.BusinessLogic/LookupService.cs`
- Modify: `RVT.BusinessLogic/UserService.cs`
- Modify: `RVT.BusinessLogic/ContractService.cs`
- Modify: `RVT.BusinessLogic/SiteService.cs`
- Modify: `RVT.BusinessLogic/ReportService.cs`
- Modify: `RVT.Dataaccess/MonitorSearchRepository.cs`
- Modify: `RVT.Dataaccess/MonitorUserSearchRepository.cs`
- Modify: `RVT.Dataaccess/ReportRuleSearchRepository.cs`
- Modify: `RVT.Dataaccess/ReportRuleUserSearchRepository.cs`
- Modify: `RVT.Dataaccess/ReportSearchRepository.cs`
- Modify: `RVT.Dataaccess/ReportUserSearchRepository.cs`
- Modify: `RVT.Dataaccess/SiteSearchRepository.cs`
- Modify: `RVT.Dataaccess/SiteUserRepository.cs`
- Modify: `RVT.Dataaccess/SiteUserSearchRepository.cs`
- Test: `RvtPortal.Spa.Tests/CutoverReadinessTests.cs`

- [x] **Step 1: Write a source-shape guard for the known recurring patterns**

Add a focused test method to `CutoverReadinessTests` that reads the affected source files and asserts no known issue messages remain as literal patterns. Keep it conservative:

```csharp
[Fact]
// Function summary: Verifies Sonar high-maintainability signature and unused-field issues stay remediated.
public void SonarHighMaintainabilitySignatureIssues_AreRemediatedInSource()
{
    var root = FindRepositoryRoot();
    var reportService = File.ReadAllText(Path.Combine(root, "RVT.BusinessLogic", "ReportService.cs"));
    var lookupService = File.ReadAllText(Path.Combine(root, "RVT.BusinessLogic", "LookupService.cs"));

    Assert.DoesNotContain("ILookupService lookupService", reportService, StringComparison.Ordinal);
    Assert.DoesNotContain("companySearchRepository", lookupService, StringComparison.Ordinal);
}
```

This guard should fail before removing the unread fields. It should not try to parse every parameter signature; compilation and Sonar refresh are the authoritative checks for S1006/S927.

- [x] **Step 2: Run the guard to verify red**

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore \
  --filter "FullyQualifiedName~CutoverReadinessTests.SonarHighMaintainabilitySignatureIssues_AreRemediatedInSource" \
  --logger "console;verbosity=minimal"
```

Expected: fail on `lookupService` and/or `companySearchRepository`.

- [x] **Step 3: Fix `csharpsquid:S4487` unread fields**

Remove unread fields and constructor parameters only when there is no behavior dependency:

```text
RVT.BusinessLogic/LookupService.cs
- Remove unread private field companySearchRepository.
- Remove the matching constructor parameter if no constructor overload or method uses it.
- Update dependency injection/tests that instantiate LookupService directly.

RVT.BusinessLogic/ReportService.cs
- Remove unread private field lookupService.
- Remove the matching constructor parameter if no method uses it.
- Update dependency injection/tests that instantiate ReportService directly.
```

- [x] **Step 4: Fix `csharpsquid:S927` parameter names**

Rename implementation parameter names only; do not change method names or types. Preserve public behavior.

Use the Sonar list:

```text
RVT.BusinessLogic/MonitorService.cs
- line 231 DeploymentId -> MonitorId
- line 236 DeploymentId -> MonitorId
- line 670 Id -> AlertLevelId
- line 819 MonitorId -> monitorId

RVT.BusinessLogic/SiteService.cs
- line 143 SiteId -> MonitorId

RVT.BusinessLogic/ContractService.cs
- line 96 ContractNumber -> ContractName
- line 109 SerachText -> ContractName

RVT.BusinessLogic/LookupService.cs
- line 128 CompanyId -> companyId

RVT.Dataaccess/*SearchRepository.cs
- Id -> monitorId/reportRuleId/reportId/siteId as listed by the matching interface.

RVT.Dataaccess/SiteUserRepository.cs
- UserId -> userId
- SiteId -> siteId
```

- [x] **Step 5: Fix `csharpsquid:S1006` default parameter mismatches**

Compare each implementation signature to its interface. Add the same default values to the implementation signatures in:

```text
RVT.BusinessLogic/LookupService.cs line 358
RVT.BusinessLogic/MonitorService.cs lines 842, 875, 920, 945, 970, 998, 1060
RVT.BusinessLogic/NotificationService.cs lines 192, 259
RVT.BusinessLogic/UserService.cs line 91
```

Do not change runtime logic. This is a signature-only cleanup.

- [x] **Step 6: Verify and commit**

```bash
dotnet build RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore -v minimal
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
git diff --check
git add \
  RVT.BusinessLogic/MonitorService.cs \
  RVT.BusinessLogic/NotificationService.cs \
  RVT.BusinessLogic/LookupService.cs \
  RVT.BusinessLogic/UserService.cs \
  RVT.BusinessLogic/ContractService.cs \
  RVT.BusinessLogic/SiteService.cs \
  RVT.BusinessLogic/ReportService.cs \
  RVT.Dataaccess/MonitorSearchRepository.cs \
  RVT.Dataaccess/MonitorUserSearchRepository.cs \
  RVT.Dataaccess/ReportRuleSearchRepository.cs \
  RVT.Dataaccess/ReportRuleUserSearchRepository.cs \
  RVT.Dataaccess/ReportSearchRepository.cs \
  RVT.Dataaccess/ReportUserSearchRepository.cs \
  RVT.Dataaccess/SiteSearchRepository.cs \
  RVT.Dataaccess/SiteUserRepository.cs \
  RVT.Dataaccess/SiteUserSearchRepository.cs \
  RvtPortal.Spa.Tests/CutoverReadinessTests.cs
git commit -m "fix(sonar): align service signatures"
```

### Task 3: Refactor C# Cognitive Complexity Hotspots

**Files:**
- Modify: `RvtPortal.Spa/Application/Monitors/MonitorListReader.cs`
- Modify: `RVT.BusinessLogic/MonitorData.cs`
- Modify: `RVT.BusinessLogic/AForge/FourierTransform.cs`
- Modify: `RVT.BusinessLogic/AForge/Tools.cs`
- Test: `RvtPortal.Spa.Tests/MonitorWorkflowTests.cs`
- Test: `RvtPortal.Spa.Tests/DashboardMapCalendarTests.cs`
- Test: `RvtPortal.Spa.Tests/ComplexMathTests.cs`

- [x] **Step 1: Add behavior-preserving tests for numerical helpers**

Extend `ComplexMathTests` with cases for `Tools.Log2` and two-dimensional DFT behavior before refactoring vendored AForge code:

```csharp
[Theory]
[InlineData(1, 0)]
[InlineData(2, 1)]
[InlineData(3, 2)]
[InlineData(4, 2)]
[InlineData(65536, 16)]
[InlineData(65537, 17)]
public void Log2_ReturnsCeilingBinaryLog(int value, int expected)
{
    Assert.Equal(expected, Tools.Log2(value));
}
```

If `Tools` is not publicly accessible through current namespace imports, add the necessary `using AForge;` or equivalent used by the source.

- [x] **Step 2: Refactor `Tools.Log2`**

Replace the nested branch tree with a loop or bit-operation implementation that preserves ceiling-log behavior:

```csharp
public static int Log2(int x)
{
    if (x <= 1)
    {
        return 0;
    }

    var power = 0;
    var value = x - 1;
    while (value > 0)
    {
        value >>= 1;
        power++;
    }

    return power;
}
```

- [x] **Step 3: Split `FourierTransform.DFT2` into row and column passes**

Keep the private API, but extract helpers:

```text
private static void TransformRows(Complex[,] data, Direction direction, Complex[] buffer)
private static void TransformColumns(Complex[,] data, Direction direction, Complex[] buffer)
private static void CopyTransformedRow(...)
private static void CopyTransformedColumn(...)
```

Each helper should contain one loop family. The public transform results must stay unchanged.

- [x] **Step 4: Split `MonitorData.PrepareDataForFourierTransform`**

Extract helpers without changing data alignment:

```text
private static int CalculateFourierEntryCount(...)
private static int RoundUpToSupportedPowerOfTwo(...)
private static void FillMissingFourierSamples(...)
private static void SetFourierSample(...)
```

Add a focused test if one does not exist for missing vibration samples being filled with zeroes.

- [x] **Step 5: Decide whether `GetDeploymentData_old` is still reachable**

`MonitorData.GetDeploymentData_old` accounts for one high complexity issue. Before refactoring, search call sites:

```bash
rg -n "GetDeploymentData_old" RVT.BusinessLogic RvtPortal.Spa RvtPortal.Spa.Tests
```

If there are no production call sites, remove the method and add a source guard to prevent reintroduction. If it is still used, split date-range normalization, monitor retrieval, and filter-option construction into helpers.

- [x] **Step 6: Split `MonitorData.GetDeploymentData`**

Extract:

```text
private static (DateTime From, DateTime To) NormalizeDeploymentDateRange(...)
private static MonitorData BuildTraceMonitorData(...)
private static MonitorData BuildEmptyMonitorData(...)
private static void ApplyMonitorDataMetadata(...)
```

Keep the public method signature unchanged because `MonitorDataSource` calls it.

- [x] **Step 7: Split `MonitorListReader.BuildBaseRows`**

Keep EF translation safe. Extract expression-building helpers rather than materializing data:

```text
private IQueryable<MonitorsList> BuildMonitorScope(MonitorTypeEnum? monitorType)
private IQueryable<MonitorWithLastDataTime> AddLatestDataTime(...)
private IQueryable<MonitorListRow> ProjectMonitorRows(...)
```

Run the monitor list tests and inspect generated SQL manually only if EF translation fails.

- [x] **Step 8: Verify and commit**

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --no-restore \
  --filter "FullyQualifiedName~ComplexMathTests|FullyQualifiedName~MonitorWorkflowTests|FullyQualifiedName~DashboardMapCalendarTests" \
  --logger "console;verbosity=minimal"
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
git diff --check
git add \
  RvtPortal.Spa/Application/Monitors/MonitorListReader.cs \
  RVT.BusinessLogic/MonitorData.cs \
  RVT.BusinessLogic/AForge/FourierTransform.cs \
  RVT.BusinessLogic/AForge/Tools.cs \
  RvtPortal.Spa.Tests/ComplexMathTests.cs \
  RvtPortal.Spa.Tests/MonitorWorkflowTests.cs \
  RvtPortal.Spa.Tests/DashboardMapCalendarTests.cs
git commit -m "refactor(sonar): reduce monitor and math complexity"
```

### Task 4: Refactor PowerShell Release Script Complexity

**Files:**
- Modify: `docs/release/export-client-release.ps1`

- [x] **Step 1: Identify the high-complexity function**

Inspect function starting around line 47:

```bash
nl -ba docs/release/export-client-release.ps1 | sed -n '35,120p'
```

- [x] **Step 2: Split validation, path resolution, and archive/export into helpers**

Use helpers shaped like:

```powershell
function Resolve-ClientReleasePath {
    param([string]$RepositoryRoot)
    return Join-Path $RepositoryRoot "RvtPortal.Client"
}

function Assert-ClientReleaseInputs {
    param([string]$ClientPath)
    if (-not (Test-Path $ClientPath)) {
        throw "Client path was not found: $ClientPath"
    }
}
```

- [x] **Step 3: Verify the script parser and commit**

```bash
pwsh -NoProfile -Command "\$null = [scriptblock]::Create((Get-Content docs/release/export-client-release.ps1 -Raw)); 'parsed'"
git diff --check
git add docs/release/export-client-release.ps1
git commit -m "refactor(release): reduce export script complexity"
```

If `pwsh` is unavailable locally, run the parse check in the Windows VM or GitHub Actions.

Local note: `pwsh`/`powershell` were unavailable in the macOS shell on 2026-06-26, so the refactor was verified with source review, `git diff --check`, and the release script parser check remains a CI/Windows follow-up.

### Task 5: Decide And Apply SQL Maintainability Strategy

**Files:**
- Option A modify: `.github/workflows/build.yml`
- Option A modify/create: `docs/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md`
- Option B modify many SQL files under:
  - `database/sqlserver/*.sql`
  - `database/postgres/*.sql`
  - `RVT.DatabaseMigrator/post-load/*.sql`

- [x] **Step 1: Choose policy**

Recommended policy:

```text
Exclude generated/one-off SQL migration drafts from Sonar maintainability issue rules, but keep them in git and retain database-specific smoke validation.
```

Reason: 697 of 728 HIGH maintainability issues are in SQL migration artifacts, mostly duplicated object-name literals and embedded dynamic SQL CR characters. Refactoring those scripts for Sonar may add migration risk without improving runtime app maintainability.

- [x] **Step 2A: If excluding legacy/generated SQL scripts, add scanner exclusions**

In `.github/workflows/build.yml`, extend the scanner begin command with `sonar.exclusions` or multicriteria ignores. Prefer scoped issue ignores over broad exclusion if coverage/visibility of SQL files is still useful:

```powershell
/d:sonar.issue.ignore.multicriteria="sqlDup,sqlCr" `
/d:sonar.issue.ignore.multicriteria.sqlDup.ruleKey="plsql:S1192" `
/d:sonar.issue.ignore.multicriteria.sqlDup.resourceKey="**/*.sql" `
/d:sonar.issue.ignore.multicriteria.sqlCr.ruleKey="plsql:LiteralsNonPrintableCharactersCheck" `
/d:sonar.issue.ignore.multicriteria.sqlCr.resourceKey="database/sqlserver/canonical_*_rewrite*.sql"
```

Document the policy in `docs/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md` and keep DB validation commands in the docs.

- [x] **Step 2B: If remediating SQL instead of excluding** - skipped by accepted policy; scoped Sonar ignores were implemented in Step 2A.

Normalize dynamic SQL literals in `canonical_*_rewrite*.sql` by replacing embedded CR characters with concatenated `CHAR(13) + CHAR(10)` or by using line-feed-only generated literals. For duplicated literals in canonical rename scripts, update the generator/source CSV pipeline so generated output avoids repeated literals where practical. Do not hand-edit generated scripts without also documenting the generator/source change.

- [x] **Step 3: Verify SQL scripts still parse**

For SQL Server scripts, run parse or dry-run on a cloned database. For Postgres scripts, run against the local Docker Postgres/Timescale clone:

```bash
dotnet build RVT.DatabaseMigrator/RVT.DatabaseMigrator.csproj --no-restore -v minimal
```

Use DB-specific validation scripts already established in `docs/database` for live schema rehearsal.

- [x] **Step 4: Commit**

```bash
git add .github/workflows/build.yml docs/sonar/SQL_SCRIPT_ANALYSIS_POLICY.md
git commit -m "ci(sonar): scope SQL migration maintainability analysis"
```

### Task 6: Push, Refresh Sonar, And Update Evidence

**Files:**
- Modify: `project_state.md`
- Optionally modify: `docs/superpowers/plans/2026-06-26-sonar-high-maintainability-plan.md`

- [x] **Step 1: Run local verification**

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"
npm run lint --prefix RvtPortal.Client
npm run test:run --prefix RvtPortal.Client
dotnet build RVT.DatabaseMigrator/RVT.DatabaseMigrator.csproj --no-restore -v minimal
git diff --check
```

Local note: backend tests passed `231/231`, client lint passed, client Vitest passed `51/51`, migrator build passed, and `git diff --check` passed on 2026-06-26. The local PowerShell parser check remains unavailable until a PowerShell runtime is present.

- [x] **Step 2: Push the branch**

```bash
git push -u origin further-improvements
```

Completed on 2026-06-26: `further-improvements` was pushed to `origin` at `04ba164`.

- [ ] **Step 3: Let GitHub Actions/SonarCloud analyze the branch or PR**

After analysis completes, rerun the authenticated Sonar query:

```text
softwareQualities=MAINTAINABILITY
impactSeverities=HIGH
statuses=OPEN,CONFIRMED,REOPENED
```

Expected:

```text
C# high maintainability issues: 0
PowerShell high maintainability issues: 0
SQL high maintainability issues: 0 if excluded/ignored, or reduced to the approved residual baseline if remediated.
```

Current note: `further-improvements` was merged to `master` in `709c5e7`, and Plane issue `#437` was marked Done with merged-master verification evidence. Later Sonar runs now occur from `master`.

- [x] **Step 4: Update project state**

Record:

```text
Branch
Commits
Verification commands and pass/fail counts
SonarCloud analysis date/commit
Remaining HIGH maintainability count
Any explicit SQL-script exclusions or residual accepted issues
```

---

## Recommended Execution Order

1. Task 2 first: it is mostly mechanical C# cleanup and low risk.
2. Task 3 next: higher risk because it touches query projection and numerical helpers.
3. Task 4 next: isolated PowerShell cleanup.
4. Task 5 after a quick approval decision on SQL policy.
5. Task 6 last: push and refresh Sonar.

## Open Decision

The only meaningful product/maintenance decision is SQL policy:

```text
Should generated and one-off database migration scripts be included in Sonar maintainability issue analysis?
```

Recommendation: exclude or multicriteria-ignore the generated/one-off SQL migration script maintainability rules while keeping database migration validation separate and explicit.
