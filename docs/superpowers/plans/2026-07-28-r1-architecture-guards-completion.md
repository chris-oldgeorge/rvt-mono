# R1 Architecture Guards Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete R1 by making the MyATM and Svantek repository-reading tests
portable to redirected build output, proving representative Mapperly and source
dependency violations are rejected, and recording the evidence before marking
R1 complete.

**Architecture:** `Rvt.Monitor.IntegrationTesting` will own one public
`RepositoryLayout` test-support helper. It first searches upward from
`AppContext.BaseDirectory`, then falls back to the compile-time location of the
helper source file, so ordinary output and `UseArtifactsOutput` directories
outside the checkout both resolve the same worktree. MyATM and Svantek tests
will consume `RepositoryLayout.Root` or the segment-based
`RepositoryLayout.GetPath` method and
delete their private root walkers. A Bash regression test will create a detached
disposable Git worktree, build the real MyATM test assembly once, mutate source
files without rebuilding, and require the focused architecture tests to reject
one Mapperly project-shape violation and one forbidden internal package
dependency.

**Tech Stack:** .NET 10, C# 14, MSTest 4, MSBuild artifacts output,
Bash, Git worktrees, Ruby 3 for deterministic XML mutation.

## Global Constraints

- R1 is already partially complete through `aaa20de`
  (`Repair monorepo test paths`) and `f59d5d1`
  (`Record monorepo path repair verification`). Preserve those monorepo path
  corrections and build on them; do not recreate or revert them.
- The shared helper belongs only in
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting`; it remains
  non-packable test support and must not enter any production dependency graph.
- Repository recognition requires both `Rvt.Mono.slnx` and a `.git` directory
  or worktree `.git` file. A nested or unrelated Git checkout is not sufficient.
- Redirected-output support must work with
  `-p:UseArtifactsOutput=true -p:ArtifactsPath=/tmp/rvt-r1-artifacts`;
  it must not rely only on walking upward from `AppContext.BaseDirectory`.
- Refactor only the listed MyATM and Svantek R1 consumers. Do not migrate AirQ,
  Omnidots, ReportingMonitor, Portal, or shared-library root finders in this
  plan.
- Do not change any monitor production code, runtime behavior, package graph,
  database schema, migration SQL, or architecture policy.
- Do not run PostgreSQL integration tests or require
  `RVT__POSTGRES_INTEGRATION_CONNECTION`. The MyATM integration-test source must
  compile after its path builder is refactored, but its live database category
  remains outside focused R1 execution.
- The mutation harness must operate in a temporary detached Git worktree, must
  restore each mutation before the next case, and must remove the worktree even
  when a command fails.
- A mutation is proved rejected only when `dotnet test` returns nonzero and its
  output contains the expected architecture-policy diagnostic. Restore,
  compile, XML-parse, or infrastructure failures do not count.
- Keep R2 and R3-R8/R10-R11 text, ordering, and checkbox state unchanged.
- Mark R1 complete in the review and project state only after normal,
  redirected-output, and mutation commands all pass.
- Every implementation unit follows focused RED, minimal GREEN, regression
  verification, `git diff --check`, and a dedicated commit.

## File structure and responsibilities

- Create
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/RepositoryLayout.cs`
  for repository discovery and repository-relative path composition.
- Create
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/RepositoryLayoutTests.cs`
  for direct discovery, redirected-output fallback, root-marker, and path
  composition tests.
- Modify the following MyATM tests only to import and use the shared helper:
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs`
  - `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs`
- Modify the following Svantek tests only to import and use the shared helper:
  - `apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs`
  - `apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs`
- Create `tests/verify-r1-architecture-guards.test.sh` as the real disposable
  mutation proof.
- Modify
  `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
  to record the resolved finding, verification evidence, and checked R1 item.
- Modify `project_state.md` to make R1 completion the newest checkpoint and
  identify R2 as the next unchanged roadmap item.
- Do not modify either MyATM or Svantek test `.csproj`: both already reference
  `../../../../libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`.
- Do not modify `Rvt.Mono.slnx`: both helper projects and both monitor test
  projects are already in the solution.

---

### Task 1: Add the portable shared `RepositoryLayout`

**Files:**

- Create:
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/RepositoryLayoutTests.cs`
- Create:
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/RepositoryLayout.cs`
- Verify, do not modify:
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/AssemblyInfo.cs`
- Verify, do not modify:
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`
- Verify, do not modify:
  `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj`

**Interfaces:**

- Consumes: `AppContext.BaseDirectory`,
  `[CallerFilePath]`, `Rvt.Mono.slnx`, and either form of the `.git` marker.
- Produces: `public static string RepositoryLayout.Root { get; }`.
- Produces:
  `public static string RepositoryLayout.GetPath(params string[] segments)`.
- Produces:
  `internal static string RepositoryLayout.FindRepositoryRoot(string outputDirectory, string sourceFilePath)`.
- Uses the existing
  `[assembly: InternalsVisibleTo("Rvt.Monitor.IntegrationTesting.Tests")]`;
  no new friend assembly is required.

- [ ] **Step 1: Write the failing repository-layout tests**

Create
`libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/RepositoryLayoutTests.cs`
with this complete content:

```csharp
using Rvt.Monitor.IntegrationTesting;

namespace Rvt.Monitor.IntegrationTesting.Tests;

[TestClass]
public sealed class RepositoryLayoutTests
{
    [TestMethod]
    public void FindRepositoryRoot_UsesOutputTreeWhenItContainsRepositoryMarkers()
    {
        using var fixture = TemporaryDirectory.Create();
        var repositoryRoot = fixture.CreateRepository("repository");
        var outputDirectory = fixture.CreateDirectory(
            "repository",
            "artifacts",
            "bin",
            "Rvt.Monitor.IntegrationTesting.Tests");
        var sourceFile = fixture.CreateFile(
            "unrelated-source",
            "RepositoryLayoutTests.cs");

        var actual = RepositoryLayout.FindRepositoryRoot(outputDirectory, sourceFile);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_FallsBackToSourceTreeWhenOutputIsRedirected()
    {
        using var fixture = TemporaryDirectory.Create();
        var repositoryRoot = fixture.CreateRepository("repository");
        var sourceFile = fixture.CreateFile(
            "repository",
            "libs",
            "rvt-monitor-common",
            "testing",
            "Rvt.Monitor.IntegrationTesting",
            "RepositoryLayout.cs");
        var redirectedOutput = fixture.CreateDirectory(
            "redirected-output",
            "bin",
            "Rvt.Monitor.IntegrationTesting.Tests");

        var actual = RepositoryLayout.FindRepositoryRoot(redirectedOutput, sourceFile);

        Assert.AreEqual(repositoryRoot, actual);
    }

    [TestMethod]
    public void FindRepositoryRoot_RejectsGitMarkerWithoutMonoSolution()
    {
        using var fixture = TemporaryDirectory.Create();
        var falseRoot = fixture.CreateDirectory("not-the-monorepo");
        File.WriteAllText(
            System.IO.Path.Combine(falseRoot, ".git"),
            "gitdir: /tmp/not-the-monorepo.git");
        var sourceFile = fixture.CreateFile(
            "not-the-monorepo",
            "src",
            "Probe.cs");
        var redirectedOutput = fixture.CreateDirectory("redirected-output");

        var exception = Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => RepositoryLayout.FindRepositoryRoot(redirectedOutput, sourceFile));

        StringAssert.Contains(exception.Message, redirectedOutput);
        StringAssert.Contains(exception.Message, sourceFile);
    }

    [TestMethod]
    public void GetPath_CombinesSegmentsBelowTheResolvedRoot()
    {
        var expected = System.IO.Path.Combine(
            RepositoryLayout.Root,
            "apps",
            "monitors",
            "myatmmonitor");

        var actual = RepositoryLayout.GetPath(
            "apps",
            "monitors",
            "myatmmonitor");

        Assert.AreEqual(expected, actual);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"rvt-repository-layout-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string CreateRepository(string segment)
        {
            var repositoryRoot = CreateDirectory(segment);
            File.WriteAllText(
                System.IO.Path.Combine(repositoryRoot, ".git"),
                "gitdir: /tmp/rvt-repository-layout.git");
            File.WriteAllText(
                System.IO.Path.Combine(repositoryRoot, "Rvt.Mono.slnx"),
                "<Solution />");
            return repositoryRoot;
        }

        public string CreateDirectory(params string[] segments)
        {
            var path = GetPath(segments);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(params string[] segments)
        {
            var path = GetPath(segments);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private string GetPath(params string[] segments) =>
            System.IO.Path.Combine([Path, .. segments]);
    }
}
```

- [ ] **Step 2: Run the helper tests and verify RED**

Run:

```bash
dotnet test \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~RepositoryLayoutTests'
```

Expected: build FAIL with `CS0103`/`CS0246` because `RepositoryLayout` does not
exist. No PostgreSQL test is selected.

- [ ] **Step 3: Implement the minimal portable helper**

Create
`libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/RepositoryLayout.cs`
with this complete content:

```csharp
using System.Runtime.CompilerServices;

namespace Rvt.Monitor.IntegrationTesting;

public static class RepositoryLayout
{
    private static readonly Lazy<string> _repositoryRoot = new(
        () => FindRepositoryRoot(AppContext.BaseDirectory, SourceFilePath()));

    public static string Root => _repositoryRoot.Value;

    public static string GetPath(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Path.Combine([Root, .. segments]);
    }

    internal static string FindRepositoryRoot(
        string outputDirectory,
        string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        var sourceDirectory = Path.GetDirectoryName(sourceFilePath) ??
            throw new ArgumentException(
                "The source file path must include a directory.",
                nameof(sourceFilePath));
        var startDirectories = new[] { outputDirectory, sourceDirectory }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal);

        foreach (var startDirectory in startDirectories)
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                if (IsRepositoryRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find the RVT monorepository root from output '{outputDirectory}' " +
            $"or source '{sourceFilePath}'.");
    }

    private static bool IsRepositoryRoot(string path)
    {
        var gitPath = Path.Combine(path, ".git");
        return File.Exists(Path.Combine(path, "Rvt.Mono.slnx")) &&
            (Directory.Exists(gitPath) || File.Exists(gitPath));
    }

    private static string SourceFilePath(
        [CallerFilePath] string sourceFilePath = "") =>
        sourceFilePath;
}
```

The `CallerFilePath` call is intentionally inside this non-packable source
project. A normal build resolves from its output tree; a redirected build falls
back to the absolute source location compiled into `RepositoryLayout.cs`.

- [ ] **Step 4: Run normal and explicitly redirected helper tests for GREEN**

Run:

```bash
dotnet test \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~RepositoryLayoutTests'
```

Expected: PASS, 4/4.

Then run the same tests with all build artifacts outside the repository:

```bash
redirect_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-helper.XXXXXX")"
trap 'rm -rf "${redirect_root}"' EXIT

dotnet restore \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/artifacts"

dotnet test \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --no-restore \
  --nologo \
  --filter 'FullyQualifiedName~RepositoryLayoutTests' \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/artifacts"

rm -rf "${redirect_root}"
trap - EXIT
```

Expected: restore PASS and redirected test PASS, 4/4. Verify that the test DLL
reported by the runner is under `${redirect_root}/artifacts`, not under the
checkout.

- [ ] **Step 5: Check formatting and commit the helper**

```bash
dotnet format \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  --no-restore \
  --verify-no-changes
dotnet format \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --no-restore \
  --verify-no-changes
node scripts/engineering-standards/verify.mjs --working-tree
git diff --check
git add \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/RepositoryLayout.cs \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/RepositoryLayoutTests.cs
git commit -m "test: add portable monitor repository layout"
```

Expected: both format checks and `git diff --check` pass; the commit contains
only the helper and its four tests.

---

### Task 2: Refactor the R1 MyATM and Svantek repository consumers

**Files:**

- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs`
- Modify:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs`
- Modify:
  `apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs`
- Modify:
  `apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs`

**Interfaces:**

- Consumes: `RepositoryLayout.Root` and
  `RepositoryLayout.GetPath(params string[] segments)` from Task 1.
- Produces: no new test API and no policy change.
- Deletes: eight MyATM private root finders, the MyATM
  `RepositoryPath(params string[] segments)` duplicate, and two Svantek private
  root finders.
- Retains: `MyAtmSharedOutboxMigrationContractTests.MigrationDirectory()`, but
  changes it to delegate directly to `RepositoryLayout.GetPath`.

- [ ] **Step 1: Record the focused normal-output baseline**

Define the exact filters once:

```bash
myatm_r1_filter='FullyQualifiedName~MyAtmMonitorTests.Architecture.CommonPackageBoundaryTests|FullyQualifiedName~MyAtmMonitorTests.Architecture.ConsumerMessagingBoundaryTests|FullyQualifiedName~MyAtmMonitorTests.Architecture.MyAtmDependencyBoundaryTests|FullyQualifiedName~MyAtmMonitorTests.Architecture.MyAtmScheduledAlertCommitBoundaryTests|FullyQualifiedName~MyAtmMonitorTests.MyAtmOutboxContractTests|FullyQualifiedName~MyAtmMonitorTests.MyAtmServiceCompositionTests|FullyQualifiedName~MyAtmMonitorTests.MyAtmSharedOutboxMigrationContractTests'
svantek_r1_filter='FullyQualifiedName~SvantekMonitorTests.Architecture.SvantekDependencyBoundaryTests|FullyQualifiedName~SvantekMonitorTests.EntityFramework.SvantekPostgreSqlSchemaPatchTests'

dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --nologo \
  --filter "${myatm_r1_filter}"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --nologo \
  --filter "${svantek_r1_filter}"
```

Expected from the `aaa20de` baseline: PASS, 38/38 MyATM and 5/5 Svantek.
This confirms the policy itself is green before changing discovery.

- [ ] **Step 2: Run redirected-output consumers and verify RED**

Run with the variables from Step 1:

```bash
redirect_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-consumers-red.XXXXXX")"
trap 'rm -rf "${redirect_root}"' EXIT

dotnet restore \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"
dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${myatm_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"

dotnet restore \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${svantek_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"
```

Expected: both test commands return nonzero. The MyATM run reports repository
root/path discovery failures in 32 of the 38 selected cases; the three
in-memory declaration rows and three reflection-only cases remain green. The
Svantek run reports root discovery failures in 5/5. The failing output paths
are under `${redirect_root}`, proving the remaining defect is output-location
coupling rather than an architecture violation.

Clean up after recording RED:

```bash
rm -rf "${redirect_root}"
trap - EXIT
```

- [ ] **Step 3: Replace every listed MyATM private root finder**

Add this using directive to the first seven MyATM files listed in this task:

```csharp
using Rvt.Monitor.IntegrationTesting;
```

`MyAtmSharedOutboxMigrationTests.cs` already imports that namespace; preserve
its existing directive and do not add a duplicate.

Apply these exact substitutions:

| File | Replacement |
| --- | --- |
| `Architecture/CommonPackageBoundaryTests.cs` | Replace every `MonoRepositoryRoot()` expression with `RepositoryLayout.Root`; delete `MonoRepositoryRoot()` at lines 326-342. |
| `Architecture/ConsumerMessagingBoundaryTests.cs` | Replace `var root = RepositoryRoot();` with `var root = RepositoryLayout.Root;`; delete `RepositoryRoot()` at lines 55-70. |
| `Architecture/MyAtmDependencyBoundaryTests.cs` | Replace both `FindRepositoryRoot()` expressions with `RepositoryLayout.Root`; delete `FindRepositoryRoot()` at lines 127-143. |
| `Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs` | Replace the three `Path.Combine` calls rooted at `FindRepositoryRoot()` with `RepositoryLayout.GetPath`, preserving all segments beginning with `"apps"`; delete `FindRepositoryRoot()` at lines 145-160. |
| `MyAtmOutboxContractTests.cs` | Replace `var repositoryRoot = FindRepositoryRoot();` with `var repositoryRoot = RepositoryLayout.Root;`; delete `FindRepositoryRoot()` at lines 74-89. |
| `MyAtmServiceCompositionTests.cs` | Replace the `Path.Combine` call rooted at `FindRepositoryRoot()` with `RepositoryLayout.GetPath`, preserving the segments from `"apps"` through `"MyAtmService.cs"`; delete `FindRepositoryRoot()` at lines 64-80. |
| `MyAtmSharedOutboxMigrationContractTests.cs` | Implement `MigrationDirectory()` as the exact expression below; delete `FindRepositoryRoot()` at lines 191-206. |
| `MyAtmSharedOutboxMigrationTests.cs` | Replace all four `RepositoryPath` calls with `RepositoryLayout.GetPath`; delete both `RepositoryPath(params string[] segments)` and `FindRepositoryRoot()`. |

The retained migration-directory helper becomes:

```csharp
private static string MigrationDirectory() =>
    RepositoryLayout.GetPath(
        "apps",
        "monitors",
        "myatmmonitor",
        "database",
        "migrations");
```

The `MyAtmSharedOutboxMigrationTests` setup/reset/migration reads become:

```csharp
var setupSql = File.ReadAllText(RepositoryLayout.GetPath(
    "apps",
    "monitors",
    "myatmmonitor",
    "MyAtmMonitorTests",
    "testdata",
    "create.postgres.sql"));
var resetSql = File.ReadAllText(RepositoryLayout.GetPath(
    "apps",
    "monitors",
    "myatmmonitor",
    "MyAtmMonitorTests",
    "testdata",
    "reset.postgres.sql"));
```

Use the same complete segment list for the `TestInitialize` reset read. Replace
`ApplyMigrationAsync` with:

```csharp
private static async Task ApplyMigrationAsync(string fileName) =>
    await ExecuteAsync(File.ReadAllText(RepositoryLayout.GetPath(
        "apps",
        "monitors",
        "myatmmonitor",
        "database",
        "migrations",
        fileName)));
```

Do not alter `TestCategory("PostgreSqlIntegration")`, SQL text, assertions,
Mapperly rules, allowlists, or source-reference matrices.

- [ ] **Step 4: Replace both Svantek private root finders**

Add:

```csharp
using Rvt.Monitor.IntegrationTesting;
```

to both Svantek files.

In `Architecture/SvantekDependencyBoundaryTests.cs`, replace:

```csharp
var repositoryRoot = FindRepositoryRoot();
var apiFiles = Directory.GetFiles(
    Path.Combine(repositoryRoot, "apps", "monitors", "svantekmonitor", "SvantekMonitor", "api"),
    "SvantekApi*.cs",
    SearchOption.TopDirectoryOnly);
```

with:

```csharp
var repositoryRoot = RepositoryLayout.Root;
var apiFiles = Directory.GetFiles(
    RepositoryLayout.GetPath(
        "apps",
        "monitors",
        "svantekmonitor",
        "SvantekMonitor",
        "api"),
    "SvantekApi*.cs",
    SearchOption.TopDirectoryOnly);
```

Delete `FindRepositoryRoot()` at lines 30-47.

In
`EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs`, replace each
`Path.Combine` expression rooted at `FindRepoRoot()` with
`RepositoryLayout.GetPath`, preserving the existing segment order for:

```text
apps/monitors/svantekmonitor/SvantekMonitorTests/testdata/create.postgres.sql
apps/monitors/svantekmonitor/SvantekMonitorTests/testdata
apps/monitors/svantekmonitor/SvantekMonitor/postgres/2026-06-30-add-status-telemetry-columns.sql
apps/monitors/svantekmonitor/SvantekMonitor/postgres/2026-06-30-reset-demo-monitor-157206.sql
```

Delete `FindRepoRoot()` at lines 143-159. Do not change SQL expectations or
Svantek production code.

- [ ] **Step 5: Prove the private R1 discovery duplicates are gone**

Run:

```bash
if rg -n \
  'FindRepositoryRoot|FindRepoRoot|MonoRepositoryRoot|private static string RepositoryRoot\(|private static string RepositoryPath\(' \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs \
  apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs \
  apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs; then
  printf 'FAIL: a private R1 repository finder/path builder remains.\n' >&2
  exit 1
fi

rg -l 'RepositoryLayout' \
  apps/monitors/myatmmonitor/MyAtmMonitorTests \
  apps/monitors/svantekmonitor/SvantekMonitorTests | sort
```

Expected: the first search prints nothing and returns 1; the second prints
exactly the ten modified consumer files. Do not broaden the edit because other
monitor/Portal/shared projects still have unrelated private root finders.

- [ ] **Step 6: Run focused normal-output tests for GREEN**

Recreate `myatm_r1_filter` and `svantek_r1_filter` exactly as in Step 1, then:

```bash
dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --nologo \
  --filter "${myatm_r1_filter}"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --nologo \
  --filter "${svantek_r1_filter}"
```

Expected: PASS, 38/38 MyATM and 5/5 Svantek.

- [ ] **Step 7: Run focused redirected-output tests for GREEN**

```bash
redirect_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-consumers-green.XXXXXX")"
trap 'rm -rf "${redirect_root}"' EXIT

dotnet restore \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"
dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${myatm_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"

dotnet restore \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${svantek_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"

rm -rf "${redirect_root}"
trap - EXIT
```

Expected: PASS, 38/38 MyATM and 5/5 Svantek, with both test DLLs under the
temporary artifacts directories.

- [ ] **Step 8: Compile the PostgreSQL-category path refactor without running a database**

```bash
dotnet build \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore \
  --nologo
```

Expected: zero errors. This compiles
the `RepositoryLayout.GetPath` calls in `MyAtmSharedOutboxMigrationTests`
without
executing its database category.

- [ ] **Step 9: Check formatting and commit the consumer refactor**

```bash
dotnet format \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore \
  --verify-no-changes \
  --include \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs
dotnet format \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore \
  --verify-no-changes \
  --include \
  apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs \
  apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs
node scripts/engineering-standards/verify.mjs --working-tree
git diff --check
git add \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmScheduledAlertCommitBoundaryTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmServiceCompositionTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationContractTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmSharedOutboxMigrationTests.cs \
  apps/monitors/svantekmonitor/SvantekMonitorTests/Architecture/SvantekDependencyBoundaryTests.cs \
  apps/monitors/svantekmonitor/SvantekMonitorTests/EntityFramework/SvantekPostgreSqlSchemaPatchTests.cs
git commit -m "test: share monitor repository layout discovery"
```

Expected: the commit contains only the ten test-consumer refactors.

---

### Task 3: Add the disposable architecture mutation harness

**Files:**

- Create: `tests/verify-r1-architecture-guards.test.sh`
- Exercise without editing:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- Exercise without editing:
  `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`

**Interfaces:**

- Consumes: a clean committed `HEAD` containing Tasks 1 and 2.
- Produces: executable `tests/verify-r1-architecture-guards.test.sh`.
- Mutation 1 adds correctly shaped analyzer metadata to the wrong project,
  `MyAtmMonitorTests.csproj`, and requires the Mapperly project-shape rule to
  reject it.
- Mutation 2 adds a forbidden `Rvt.Monitor.Common` `PackageReference` to
  `MyAtmMonitor.csproj` and requires the active-consumer source-reference rule
  to reject it.

- [ ] **Step 1: Perform the two mutations manually and verify RED**

Create a disposable detached worktree from the committed Task 2 head:

```bash
manual_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-manual-mutations.XXXXXX")"
manual_worktree="${manual_root}/worktree"
git worktree add --detach "${manual_worktree}" HEAD
```

Build the unmodified focused assembly and prove both policies start green:

```bash
manual_project="${manual_worktree}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"
manual_filter='FullyQualifiedName=MyAtmMonitorTests.Architecture.MyAtmDependencyBoundaryTests.MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy|FullyQualifiedName=MyAtmMonitorTests.Architecture.CommonPackageBoundaryTests.ActiveConsumers_MatchApprovedRvtSourceReferenceMatrix'

dotnet restore "${manual_project}" --locked-mode
dotnet test "${manual_project}" --no-restore --nologo --filter "${manual_filter}"
```

Expected: PASS, 2/2.

Add this exact XML before `</Project>` in
`${manual_worktree}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Riok.Mapperly" PrivateAssets="all" OutputItemType="Analyzer" />
  </ItemGroup>
```

Run without rebuilding:

```bash
dotnet test \
  "${manual_project}" \
  --no-build \
  --no-restore \
  --nologo \
  --filter 'FullyQualifiedName=MyAtmMonitorTests.Architecture.MyAtmDependencyBoundaryTests.MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy'
```

Expected: FAIL and output includes:

```text
apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj: Mapperly is restricted to direct, non-test monitor application projects.
```

Restore the test project:

```bash
git -C "${manual_worktree}" restore \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj
```

Add this exact XML before `</Project>` in
`${manual_worktree}/apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Rvt.Monitor.Common" />
  </ItemGroup>
```

Run without rebuilding:

```bash
dotnet test \
  "${manual_project}" \
  --no-build \
  --no-restore \
  --nologo \
  --filter 'FullyQualifiedName=MyAtmMonitorTests.Architecture.CommonPackageBoundaryTests.ActiveConsumers_MatchApprovedRvtSourceReferenceMatrix'
```

Expected: FAIL and output includes:

```text
apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj: active consumer must not PackageReference Rvt.Monitor.Common.
```

Remove the disposable worktree:

```bash
git -C "${manual_worktree}" restore \
  apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj
git worktree remove --force "${manual_worktree}"
rm -rf "${manual_root}"
```

- [ ] **Step 2: Write the reproducible mutation harness**

Create `tests/verify-r1-architecture-guards.test.sh` with this complete content:

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-architecture-guards.XXXXXX")"
mutation_root="${temp_root}/worktree"
test_output="${temp_root}/test-output.log"

cleanup() {
  local status=$?
  if git -C "${repo_root}" worktree list --porcelain |
      grep -Fqx "worktree ${mutation_root}"; then
    git -C "${repo_root}" worktree remove --force "${mutation_root}" >/dev/null
  fi
  rm -rf "${temp_root}"
  exit "${status}"
}
trap cleanup EXIT

git -C "${repo_root}" worktree add --detach "${mutation_root}" HEAD >/dev/null

test_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"
mapperly_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"
consumer_project="${mutation_root}/apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"
mapperly_filter='FullyQualifiedName=MyAtmMonitorTests.Architecture.MyAtmDependencyBoundaryTests.MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy'
source_filter='FullyQualifiedName=MyAtmMonitorTests.Architecture.CommonPackageBoundaryTests.ActiveConsumers_MatchApprovedRvtSourceReferenceMatrix'
baseline_filter="${mapperly_filter}|${source_filter}"

dotnet restore "${test_project}" --locked-mode
dotnet test \
  "${test_project}" \
  --no-restore \
  --nologo \
  --filter "${baseline_filter}"

assert_mutation_rejected() {
  local label="$1"
  local filter="$2"
  local expected_diagnostic="$3"
  local status

  set +e
  dotnet test \
    "${test_project}" \
    --no-build \
    --no-restore \
    --nologo \
    --filter "${filter}" >"${test_output}" 2>&1
  status=$?
  set -e

  if (( status == 0 )); then
    printf 'FAIL: %s mutation was accepted.\n' "${label}" >&2
    cat "${test_output}" >&2
    exit 1
  fi

  if ! grep -Fq "${expected_diagnostic}" "${test_output}"; then
    printf 'FAIL: %s failed without the expected architecture diagnostic.\n' \
      "${label}" >&2
    cat "${test_output}" >&2
    exit 1
  fi

  printf 'Rejected %s mutation.\n' "${label}"
}

cp "${mapperly_project}" "${mapperly_project}.baseline"
ruby - "${mapperly_project}" <<'RUBY'
path = ARGV.fetch(0)
source = File.read(path, encoding: "utf-8")
closing = "</Project>"
mutation = <<~XML
    <ItemGroup>
      <PackageReference Include="Riok.Mapperly" PrivateAssets="all" OutputItemType="Analyzer" />
    </ItemGroup>
  </Project>
XML
abort "Mapperly mutation anchor not found in #{path}" unless source.include?(closing)
File.write(path, source.sub(closing, mutation), mode: "w", encoding: "utf-8")
RUBY

assert_mutation_rejected \
  "Mapperly test-project shape" \
  "${mapperly_filter}" \
  "Mapperly is restricted to direct, non-test monitor application projects."
mv "${mapperly_project}.baseline" "${mapperly_project}"

cp "${consumer_project}" "${consumer_project}.baseline"
ruby - "${consumer_project}" <<'RUBY'
path = ARGV.fetch(0)
source = File.read(path, encoding: "utf-8")
closing = "</Project>"
mutation = <<~XML
    <ItemGroup>
      <PackageReference Include="Rvt.Monitor.Common" />
    </ItemGroup>
  </Project>
XML
abort "source-dependency mutation anchor not found in #{path}" unless source.include?(closing)
File.write(path, source.sub(closing, mutation), mode: "w", encoding: "utf-8")
RUBY

assert_mutation_rejected \
  "forbidden internal package dependency" \
  "${source_filter}" \
  "active consumer must not PackageReference Rvt.Monitor.Common."
mv "${consumer_project}.baseline" "${consumer_project}"

dotnet test \
  "${test_project}" \
  --no-build \
  --no-restore \
  --nologo \
  --filter "${baseline_filter}"

printf 'R1 architecture guard mutations rejected and baseline restored.\n'
```

- [ ] **Step 3: Make the harness executable and run it for GREEN**

```bash
chmod +x tests/verify-r1-architecture-guards.test.sh
tests/verify-r1-architecture-guards.test.sh
```

Expected:

```text
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
Rejected Mapperly test-project shape mutation.
Rejected forbidden internal package dependency mutation.
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2
R1 architecture guard mutations rejected and baseline restored.
```

The exact test-duration suffix may vary. After the script exits, verify there is
no path containing `rvt-r1-architecture-guards` in:

```bash
git worktree list --porcelain
```

- [ ] **Step 4: Run shell and diff checks and commit the harness**

```bash
bash -n tests/verify-r1-architecture-guards.test.sh
node scripts/engineering-standards/verify.mjs --working-tree
git diff --check
git add tests/verify-r1-architecture-guards.test.sh
git commit -m "test: prove R1 architecture guards reject mutations"
```

Expected: syntax and diff checks pass; the commit contains only the executable
mutation harness.

---

### Task 4: Run final proof and close R1 documentation

**Files:**

- Modify:
  `docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`
- Modify: `project_state.md`

**Interfaces:**

- Consumes: the three committed implementation units from Tasks 1-3 and their
  test evidence.
- Produces: checked R1 only after final proof.
- Produces: a newest project-state checkpoint naming R2 as next.
- Preserves: R2 and every other out-of-scope roadmap entry exactly as pending.

- [ ] **Step 1: Run the complete focused normal proof**

Define the two exact filters from Task 2, then:

```bash
dotnet test \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~RepositoryLayoutTests'
dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --nologo \
  --filter "${myatm_r1_filter}"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --nologo \
  --filter "${svantek_r1_filter}"
```

Expected: PASS, 4/4 helper, 38/38 MyATM, and 5/5 Svantek.

- [ ] **Step 2: Run the complete focused redirected-output proof**

```bash
redirect_root="$(mktemp -d "${TMPDIR:-/tmp}/rvt-r1-final-redirect.XXXXXX")"
trap 'rm -rf "${redirect_root}"' EXIT

dotnet restore \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/helper"
dotnet test \
  libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --no-restore \
  --nologo \
  --filter 'FullyQualifiedName~RepositoryLayoutTests' \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/helper"

dotnet restore \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"
dotnet test \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${myatm_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/myatm"

dotnet restore \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --locked-mode \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"
dotnet test \
  apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj \
  --no-restore \
  --nologo \
  --filter "${svantek_r1_filter}" \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="${redirect_root}/svantek"

rm -rf "${redirect_root}"
trap - EXIT
```

Expected: PASS, 4/4 helper, 38/38 MyATM, and 5/5 Svantek, with every test DLL
under `${redirect_root}`.

- [ ] **Step 3: Run the mutation and repository regression proof**

```bash
tests/verify-r1-architecture-guards.test.sh
tests/verify-mono-layout.test.sh
tests/verify-mono-solution.test.sh
tests/verify-rvt-common-source-boundary.test.sh
tests/verify-rvt-common-source-boundary-regression.test.sh
node scripts/engineering-standards/verify.mjs --working-tree
git diff --check
```

Expected: both R1 mutations are rejected, the restored baseline passes 2/2,
all four existing repository guards pass, and `git diff --check` is clean.

- [ ] **Step 4: Update the architecture review only after Steps 1-3 pass**

In
`docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md`,
rename finding 9 to:

```markdown
### 9. Architecture guards now use portable monorepo paths
```

Replace its stale-path impact/recommendation paragraphs with:

```markdown
The MyATM and Svantek R1 repository-reading tests now resolve the checkout
through the shared `Rvt.Monitor.IntegrationTesting.RepositoryLayout` helper.
The helper recognizes `Rvt.Mono.slnx` plus either a normal `.git` directory or
a worktree `.git` file, searches the ordinary test output tree first, and falls
back to its compile-time source location when MSBuild artifacts are redirected
outside the checkout.

**Resolution (2026-07-28):** The focused normal and redirected-output suites
both pass: 4/4 helper tests, 38/38 MyATM tests, and 5/5 Svantek tests. The
disposable `tests/verify-r1-architecture-guards.test.sh` worktree harness also
proves that a Mapperly reference in `MyAtmMonitorTests.csproj` and a forbidden
`Rvt.Monitor.Common` package dependency in `MyAtmMonitor.csproj` are rejected
by the intended architecture policies before the baseline is restored.
```

Change only the R1 checklist marker:

```markdown
- [x] **R1 — Repair architecture guards.** Replace stale repository paths and
      prove the boundary tests fail for real violations. Completed 2026-07-28
      with portable normal/redirected-output discovery and two disposable
      mutation proofs.
```

Leave R2, R3-R8, R10, and R11 unchecked and otherwise unchanged.

In the verification-baseline section, replace the final stale-failure sentence
with:

```markdown
- R1 completion verification passed 4/4 shared repository-layout tests, 38/38
  focused MyATM tests, and 5/5 focused Svantek tests in both normal and
  externally redirected MSBuild output layouts; the Mapperly project-shape and
  forbidden internal-package mutations were both rejected in a disposable
  worktree.
```

- [ ] **Step 5: Make R1 completion the newest project-state checkpoint**

Replace the opening heading and its R1-next bullets in `project_state.md` with
this exact checkpoint, above the historical R9 material:

```markdown
## R1 architecture guards complete — R2 next 2026-07-28

- Resume instruction: `Read project_state.md to get up to speed`.
- Active implementation branch: `codex/r1-architecture-guards`.
- R1 builds on `aaa20de` (`Repair monorepo test paths`) and `f59d5d1`
  (`Record monorepo path repair verification`). Those commits fixed the stale
  monorepo-relative strings; this branch completes portability and proof.
- `Rvt.Monitor.IntegrationTesting.RepositoryLayout` is now the shared monitor
  test-support authority for the monorepo root and repository-relative paths.
  It requires `Rvt.Mono.slnx` plus a Git directory/worktree file, searches
  normal output first, and falls back to its compile-time source location when
  MSBuild output is redirected outside the checkout.
- Eight MyATM and two Svantek repository-reading test files now use the shared
  helper. No AirQ, Omnidots, ReportingMonitor, Portal, shared-library,
  production monitor, database, package, or roadmap migration was absorbed.
- Focused normal proof passed 4/4 helper tests, 38/38 MyATM tests, and 5/5
  Svantek tests.
- The same 4/4, 38/38, and 5/5 suites passed with
  `UseArtifactsOutput=true` and every artifact rooted in a disposable directory
  outside the repository.
- `tests/verify-r1-architecture-guards.test.sh` creates and removes a detached
  disposable worktree. It proved that Mapperly in the MyATM test project and a
  forbidden `Rvt.Monitor.Common` package dependency in the MyATM production
  project both fail for their intended architecture diagnostics, then proved
  the restored baseline passes 2/2.
- Mono-layout, mono-solution, RVT common source-boundary normal/regression, and
  `git diff --check` verification passed. No PostgreSQL integration credential
  or production database was used.
- The architecture review now marks only R1 complete. R2 Help Admin alignment
  is next; R3-R8 and R10-R11 remain pending and unchanged.
```

Retain the existing R9 integration and historical sections below this new
checkpoint. Do not remove prior evidence.

- [ ] **Step 6: Verify documentation truth and roadmap preservation**

```bash
rg -n '^\- \[[x ]\] \*\*R[1-9]|^\- \[[x ]\] \*\*R1[01]' \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md
rg -n 'R1 architecture guards complete|R2 next|4/4|38/38|5/5|Mapperly|Rvt\.Monitor\.Common' \
  project_state.md
node scripts/engineering-standards/verify.mjs --working-tree
git diff --check
```

Expected: R1 and R9 are checked; R2-R8 and R10-R11 are unchecked; the new
project-state checkpoint contains all proof counts and both mutations; the diff
check passes.

- [ ] **Step 7: Commit the R1 completion record**

```bash
git add \
  docs/reviews/2026-07-27-project-architecture-and-code-quality-review.md \
  project_state.md
git commit -m "docs: record R1 architecture guard completion"
```

Expected: the commit contains only the architecture review and project-state
updates.

- [ ] **Step 8: Perform the final clean-branch audit**

```bash
git status --short
git log -4 --oneline
tests/verify-r1-architecture-guards.test.sh
git diff --check HEAD~4..HEAD
```

Expected: clean worktree; the latest four commits, newest first, are:

```text
docs: record R1 architecture guard completion
test: prove R1 architecture guards reject mutations
test: share monitor repository layout discovery
test: add portable monitor repository layout
```

The mutation harness passes again, creates no persistent worktree, and the
four-commit implementation diff has no whitespace errors. R1 is complete;
implementation must stop before R2.
