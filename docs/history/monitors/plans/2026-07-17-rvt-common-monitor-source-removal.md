# RVT Common Monitor Source Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the existing monitor package migration by deleting the retired local Common source graph and proving every actual consumer builds, tests, and packages exclusively against the immutable private `0.2.0-rc.1` package train.

**Architecture:** Extend the existing MyATM-owned package-boundary tests before deletion, then remove the retired projects from every active solution and delete `rvt-monitor-common/`. Keep the already validated exact package references, secure restore paths, and lock files unchanged; update active documentation and CI policy so local source cannot return unnoticed.

**Tech Stack:** .NET 10, MSTest, central package management, NuGet lock files, GitHub Packages, GitHub Actions, Docker BuildKit, Docker Compose, Bash.

## Global Constraints

- Work only in `/Users/oldgeorge/Documents/rvt-monitors/rvt-monitors/.worktrees/rvt-common-private-nuget-migration` on branch `codex/rvt-common-private-nuget-migration`.
- Preserve the exact synchronized package version `0.2.0-rc.1`; do not publish, overwrite, float, or promote packages.
- Treat `RVT-Group-LTD/rvt-reporting@f00d5b8a320945ed08e248da8641ca0c3f7e3b82` as the authoritative Common source.
- Add package references only to direct consumers named in the approved design; unrelated projects receive no RVT package.
- Keep `Rvt.Monitor.IntegrationTesting` test-only with `PrivateAssets="all"`.
- Keep credentials process-only. Never print, persist, stage, or document a token or connection string.
- Preserve historical specifications, plans, evidence, branches, and sibling worktrees.
- Do not change runtime behavior, configuration keys, public APIs, EF mappings, schemas, vendor calls, or provider behavior.
- Do not add a conditional local-source fallback.
- Do not run live vendor, email, SMS, production database, migration, or deployment operations.

---

### Task 1: Enforce and establish the package-only source boundary

**Files:**
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs`
- Modify: `rvt-monitors.sln`
- Modify: `airqmonitor/airqmonitor.sln`
- Modify: `myatmmonitor/myatmmonitor.sln`
- Modify: `omnidotsmonitor/omnidotsmonitor.sln`
- Modify: `svantekmonitor/svantekmonitor.sln`
- Delete: `rvt-monitor-common/`

**Interfaces:**
- Consumes: the existing exact package references and `Directory.Packages.props` version properties `RvtCommonVersion`, `RvtCommonInfrastructureVersion`, and `RvtIntegrationTestingVersion`.
- Produces: an active checkout with 14 consumer projects, no retired Common solution entries or source tree, and an executable package-reference matrix guard.

- [ ] **Step 1: Add the failing local-source, solution, package-matrix, and conditional-switch tests**

Add these fields to `CommonPackageBoundaryTests`:

```csharp
private static readonly string[] ActiveSolutions =
[
    "rvt-monitors.sln",
    "airqmonitor/airqmonitor.sln",
    "myatmmonitor/myatmmonitor.sln",
    "omnidotsmonitor/omnidotsmonitor.sln",
    "svantekmonitor/svantekmonitor.sln"
];

private static readonly IReadOnlyDictionary<string, string[]> ExpectedRvtPackages =
    new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["airqmonitor/AirQMonitor/AirQMonitor.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
        ["airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj"] =
            ["Rvt.Monitor.IntegrationTesting"],
        ["myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
        ["myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj"] =
            ["Rvt.Monitor.IntegrationTesting"],
        ["omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
        ["omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj"] =
            ["Rvt.Monitor.IntegrationTesting"],
        ["svantekmonitor/SvantekMonitor/SvantekMonitor.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
        ["svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj"] =
            ["Rvt.Monitor.IntegrationTesting"],
        ["reportingmonitor/ReportingMonitor/ReportingMonitor.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.Common.Infrastructure"],
        ["reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj"] =
            ["Rvt.Monitor.Common", "Rvt.Monitor.IntegrationTesting"],
        ["reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj"] =
            ["Rvt.Monitor.Common"],
        ["reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj"] =
            ["Rvt.Monitor.Common"]
    };
```

Add these tests and helper to the same class:

```csharp
[TestMethod]
public void LocalCommonSourceTree_DoesNotExist() =>
    Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot(), "rvt-monitor-common")));

[TestMethod]
public void ActiveSolutions_DoNotListRetiredCommonProjects()
{
    var violations = ActiveSolutions
        .Where(relative => File.ReadAllText(Path.Combine(RepositoryRoot(), relative))
            .Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
}

[TestMethod]
public void ConsumerProjects_MatchApprovedRvtPackageMatrix()
{
    var violations = ConsumerProjects()
        .SelectMany(ValidateRvtPackageMatrix)
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
}

[TestMethod]
public void ConsumerProjects_DoNotContainConditionalCommonSourceSwitches()
{
    var root = RepositoryRoot();
    var paths = ConsumerProjects()
        .Concat(Directory.EnumerateFiles(root, "*.props", SearchOption.TopDirectoryOnly))
        .Concat(Directory.EnumerateFiles(root, "*.targets", SearchOption.TopDirectoryOnly));
    var violations = paths
        .Where(path => File.ReadAllText(path).Contains("UseLocalRvtCommon", StringComparison.OrdinalIgnoreCase) ||
            File.ReadAllText(path).Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
        .Select(Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
}

private static IEnumerable<string> ValidateRvtPackageMatrix(string projectPath)
{
    var relative = Relative(projectPath);
    var references = XDocument.Load(projectPath)
        .Descendants()
        .Where(element => element.Name.LocalName == "PackageReference")
        .Where(element => ((string?)element.Attribute("Include") ?? string.Empty)
            .StartsWith("Rvt.Monitor.", StringComparison.Ordinal))
        .ToArray();
    var actual = references
        .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
        .Order(StringComparer.Ordinal)
        .ToArray();
    var expected = ExpectedRvtPackages.TryGetValue(relative, out var packages)
        ? packages.Order(StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
    {
        yield return $"{relative}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].";
    }

    foreach (var reference in references.Where(reference =>
                 ReadMetadata(reference, "Version") is not null ||
                 ReadMetadata(reference, "VersionOverride") is not null))
    {
        yield return $"{relative}: {(string?)reference.Attribute("Include")} must use the central exact version.";
    }
}
```

Replace `CommonRuntimeVersions_AreSynchronized` with an exact three-package assertion:

```csharp
[TestMethod]
public void RvtPackageVersions_AreExactAndSynchronized()
{
    var props = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props"));
    var common = ReadProperty(props, "RvtCommonVersion");
    var infrastructure = ReadProperty(props, "RvtCommonInfrastructureVersion");
    var integrationTesting = ReadProperty(props, "RvtIntegrationTestingVersion");

    Assert.AreEqual("0.2.0-rc.1", common);
    Assert.AreEqual(common, infrastructure);
    Assert.AreEqual(common, integrationTesting);
}
```

Delete `CommonProjects_DoNotReferenceMapperly` from `MyAtmDependencyBoundaryTests`; after source removal, Common project policy belongs to the authoritative package repository, while `MapperlyPackageReferences_FollowMonitorAppAnalyzerPolicy` continues enforcing this consumer repository.

- [ ] **Step 2: Run the new focused boundary test and verify the intended red state**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo \
  --filter FullyQualifiedName~LocalCommonSourceTree_DoesNotExist
```

Expected: one test fails only because `rvt-monitor-common/` still exists. If it passes or fails for another reason, stop and correct the test before deleting source.

- [ ] **Step 3: Remove retired projects from every active solution**

From the worktree root, run:

```bash
dotnet sln rvt-monitors.sln remove \
  rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj \
  rvt-monitor-common/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj

dotnet sln airqmonitor/airqmonitor.sln remove rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
dotnet sln myatmmonitor/myatmmonitor.sln remove rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
dotnet sln omnidotsmonitor/omnidotsmonitor.sln remove rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
dotnet sln svantekmonitor/svantekmonitor.sln remove rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
```

Run `dotnet sln <solution> list` for all five solutions. Expected: the root lists 14 projects; each vendor solution lists only its app and test project.

- [ ] **Step 4: Delete the retired local Common source tree**

Run the explicitly authorized source removal:

```bash
git rm -r rvt-monitor-common
```

Expected: Common implementation, Common/Infrastructure tests, IntegrationTesting source/tests, database migration copies, and the retired Common solution are staged for deletion. Do not delete `Directory.Packages.props`, `NuGet.config`, package lock files, or any sibling worktree.

- [ ] **Step 5: Verify the boundary tests are green**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo \
  --filter 'FullyQualifiedName~CommonPackageBoundaryTests|FullyQualifiedName~MyAtmDependencyBoundaryTests'
```

Expected: all selected tests pass, the package matrix matches exactly, the three versions equal `0.2.0-rc.1`, and no test enumerates sibling `.worktrees`.

- [ ] **Step 6: Commit the source boundary**

Run:

```bash
git add rvt-monitors.sln airqmonitor/airqmonitor.sln myatmmonitor/myatmmonitor.sln \
  omnidotsmonitor/omnidotsmonitor.sln svantekmonitor/svantekmonitor.sln \
  myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  myatmmonitor/MyAtmMonitorTests/Architecture/MyAtmDependencyBoundaryTests.cs
git diff --cached --check
git commit -m "refactor: remove monitor-owned common source"
```

### Task 2: Update active documentation and executable policy

**Files:**
- Modify: `AGENTS.md`
- Modify: `README.md`
- Modify: `observability/README.md`
- Modify: `docs/monitor-data-access-migration.md`
- Modify: `docs/container-builds.md`
- Modify: `docs/release/client-release-runbook.md`
- Modify: `myatmmonitor/README.md`
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify: `scripts/export-client-release.sh`
- Modify: `scripts/verify-private-package-builds.sh`
- Modify: `.github/workflows/package-consumer-ci.yml`

**Interfaces:**
- Consumes: the package-only tree from Task 1 and the immutable RC workflow artifact `release-0.2.0-rc.1` from run `29496427667`.
- Produces: active instructions and executable policy that never reference local Common source and that verify the source cannot reappear in CI or curated releases.

- [ ] **Step 1: Add a failing active-documentation boundary test**

Add this active-file list and test to `CommonPackageBoundaryTests`:

```csharp
private static readonly string[] ActiveBoundaryDocuments =
[
    "AGENTS.md",
    "README.md",
    "observability/README.md",
    "docs/monitor-data-access-migration.md",
    "docs/container-builds.md",
    "docs/release/client-release-runbook.md",
    "myatmmonitor/README.md"
];

[TestMethod]
public void ActiveDocumentation_DoesNotReferenceRetiredCommonSource()
{
    var violations = ActiveBoundaryDocuments
        .Where(relative => File.ReadAllText(Path.Combine(RepositoryRoot(), relative))
            .Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations, string.Join(Environment.NewLine, violations));
}
```

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo \
  --filter FullyQualifiedName~ActiveDocumentation_DoesNotReferenceRetiredCommonSource
```

Expected: failure listing the active documents that still name the retired directory.

- [ ] **Step 2: Replace active local-source guidance with package authority**

Make these exact policy changes:

- `AGENTS.md`: prohibit Mapperly in the shared packages and identify `RVT-Group-LTD/rvt-reporting` as their owner instead of naming a local directory.
- `README.md`: remove the `rvt-monitor-common/` contents row; describe private exact packages, root source mapping, runtime-only authentication, and `rvt-monitors.sln` as 14 consumer projects.
- `observability/README.md`: say OpenTelemetry is supplied by `Rvt.Monitor.Common` `0.2.0-rc.1`, with package dependency versions observable in container `.deps.json` files.
- `docs/monitor-data-access-migration.md`: replace local implementation/test paths with the package IDs, the authoritative repository, and consumer-side test commands.
- `docs/container-builds.md`: explicitly state that Docker builds contain no local Common source fallback and that `scripts/report-rvt-package-inventory.sh` must report `0.2.0-rc.1` for Common and Infrastructure.
- `docs/release/client-release-runbook.md`: require the curated payload to contain no `rvt-monitor-common/` path and to restore only through GitHub Packages.
- `myatmmonitor/README.md`: replace both local shared-migration paths with the immutable RC artifact flow below.

Use this executable migration-asset retrieval in `myatmmonitor/README.md`:

```bash
artifact_dir=/private/tmp/rvt-common-0.2.0-rc.1
gh run download 29496427667 \
  --repo RVT-Group-LTD/rvt-reporting \
  --name release-0.2.0-rc.1 \
  --dir "$artifact_dir"
tar -xzf "$artifact_dir/rvt-common-migrations-0.2.0-rc.1.tar.gz" -C "$artifact_dir"

psql '<connection-string>' -v ON_ERROR_STOP=1 \
  -f "$artifact_dir/database/migrations/2026-07-15-add-monitor-delivery-outbox.postgres.sql"
sqlcmd -S '<server>' -d '<database>' -E -b \
  -i "$artifact_dir/database/migrations/2026-07-15-add-monitor-delivery-outbox.sqlserver.sql"
```

State that the artifact must be checksum-verified with its retained `SHA256SUMS` before an operator applies migrations, and that the separately designated migration authority remains responsible.

- [ ] **Step 3: Make the release exporter reject a returned Common tree**

Add this predicate to the `blocked_output` `find` expression in `scripts/export-client-release.sh`:

```bash
-path '*/rvt-monitor-common/*' -o \
```

Do not add `rvt-monitor-common/**` to the exclusions list; a returned source tree must fail export rather than be silently omitted.

- [ ] **Step 4: Strengthen the package policy verifier**

Append these checks to `scripts/verify-private-package-builds.sh` before Dockerfile inspection:

```bash
test ! -d rvt-monitor-common

if rg -n '<ProjectReference[^>]+rvt-monitor-common|UseLocalRvtCommon' \
  --glob '*.csproj' --glob '*.props' --glob '*.targets' .; then
  echo "A local RVT Common source reference or switch is present." >&2
  exit 1
fi

solutions=(
  rvt-monitors.sln
  airqmonitor/airqmonitor.sln
  myatmmonitor/myatmmonitor.sln
  omnidotsmonitor/omnidotsmonitor.sln
  svantekmonitor/svantekmonitor.sln
)

for solution in "${solutions[@]}"; do
  if dotnet sln "$solution" list | rg -q 'Rvt\.Monitor\.(Common|IntegrationTesting)'; then
    echo "Retired Common project remains in $solution" >&2
    exit 1
  fi
done
```

- [ ] **Step 5: Extend package-consumer CI through inventory and release export**

After `docker compose build` in `.github/workflows/package-consumer-ci.yml`, add:

```yaml
      - run: scripts/report-rvt-package-inventory.sh
      - run: scripts/export-client-release.sh /tmp/rvt-monitors-client-release
      - run: test ! -d /tmp/rvt-monitors-client-release/rvt-monitor-common
```

The workflow retains `contents: read`, `packages: read`, and the process-only `NuGetPackageSourceCredentials_rvt` value built from `github.actor` and `github.token`.

- [ ] **Step 6: Verify the active-documentation and executable policies**

Run:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-restore --nologo \
  --filter 'FullyQualifiedName~CommonPackageBoundaryTests|FullyQualifiedName~MyAtmDependencyBoundaryTests'
scripts/verify-private-package-builds.sh
scripts/export-client-release.sh /private/tmp/rvt-monitors-client-release
test ! -d /private/tmp/rvt-monitors-client-release/rvt-monitor-common
if rg -n 'rvt-monitor-common|UseLocalRvtCommon' \
  AGENTS.md README.md observability/README.md docs/monitor-data-access-migration.md \
  docs/container-builds.md docs/release/client-release-runbook.md myatmmonitor/README.md; then
  exit 1
fi
```

Expected: tests and scripts pass; the final active-file scan prints no matches.

- [ ] **Step 7: Commit active documentation and policy**

Run:

```bash
git add AGENTS.md README.md observability/README.md docs/monitor-data-access-migration.md \
  docs/container-builds.md docs/release/client-release-runbook.md myatmmonitor/README.md \
  myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  scripts/export-client-release.sh scripts/verify-private-package-builds.sh \
  .github/workflows/package-consumer-ci.yml
git diff --cached --check
git commit -m "docs: complete package-only common ownership"
```

### Task 3: Run the final package-only release gate and record evidence

**Files:**
- Create: `docs/superpowers/evidence/2026-07-17-rvt-common-monitor-source-removal.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: the package-only source boundary and active policy from Tasks 1–2, the runtime-only NuGet credential, and a disposable PostgreSQL 17 database.
- Produces: fresh final acceptance evidence, updated project state, and a clean branch ready for review/push rather than automatic merge.

- [ ] **Step 1: Confirm the credential without exposing it**

Run:

```bash
test -n "${NuGetPackageSourceCredentials_rvt:-}"
```

Expected: exit 0 with no output. If absent, stop and obtain a runtime-only `read:packages` credential; do not write it into any file or command output.

- [ ] **Step 2: Start a disposable PostgreSQL 17 fixture**

Run:

```bash
docker run --name rvt-common-source-removal-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=rvt_monitor_package_ci \
  -p 127.0.0.1:55432:5432 -d postgres:17
export RVT__POSTGRES_INTEGRATION_CONNECTION='Host=127.0.0.1;Port=55432;Database=rvt_monitor_package_ci;Username=postgres;Password=postgres'

for attempt in {1..30}; do
  if docker exec rvt-common-source-removal-postgres \
    pg_isready -U postgres -d rvt_monitor_package_ci; then
    break
  fi
  sleep 1
done
docker exec rvt-common-source-removal-postgres \
  pg_isready -U postgres -d rvt_monitor_package_ci
```

The fixture is local, disposable, and contains no production data. If any later verification fails, run the cleanup commands in Step 5 before stopping.

- [ ] **Step 3: Run clean locked restore, formatting, build, and tests**

Use an isolated package cache so the result proves package availability rather than relying on stale global packages:

```bash
package_cache=/private/tmp/rvt-common-source-removal-packages
rm -rf "$package_cache"
NUGET_PACKAGES="$package_cache" dotnet restore rvt-monitors.sln --locked-mode
NUGET_PACKAGES="$package_cache" dotnet format rvt-monitors.sln --verify-no-changes --no-restore
NUGET_PACKAGES="$package_cache" dotnet build rvt-monitors.sln --no-restore --nologo
NUGET_PACKAGES="$package_cache" dotnet test rvt-monitors.sln --no-build --nologo
```

Expected: restore and formatter pass; the 14-project solution builds with zero warnings/errors; the five consumer test suites pass 914 tests with zero failed/skipped. If counts differ, reconcile the test list before documenting completion.

- [ ] **Step 4: Run package, container, runtime-inventory, and export gates**

Run:

```bash
scripts/verify-private-package-builds.sh
docker compose config --quiet
docker compose build
scripts/report-rvt-package-inventory.sh
for image in \
  rvt/airqmonitor:local \
  rvt/myatmmonitor:local \
  rvt/omnidotsmonitor:local \
  rvt/svantekmonitor:local \
  rvt/reportingmonitor:local; do
  docker image inspect "$image" --format '{{index .RepoTags 0}}\t{{.Id}}'
done
for solution in \
  rvt-monitors.sln \
  airqmonitor/airqmonitor.sln \
  myatmmonitor/myatmmonitor.sln \
  omnidotsmonitor/omnidotsmonitor.sln \
  svantekmonitor/svantekmonitor.sln; do
  dotnet sln "$solution" list
done
scripts/export-client-release.sh /private/tmp/rvt-monitors-client-release
test ! -d /private/tmp/rvt-monitors-client-release/rvt-monitor-common
git diff --check
```

Expected: all five images build; the inventory prints five rows with Common and Infrastructure both `0.2.0-rc.1`; IntegrationTesting is absent from runtime outputs; the curated export contains `NuGet.config` and `Directory.Packages.props` but no local Common tree or secret-bearing file.

- [ ] **Step 5: Stop and remove disposable resources**

Run:

```bash
docker rm -f rvt-common-source-removal-postgres
rm -rf /private/tmp/rvt-common-source-removal-packages /private/tmp/rvt-monitors-client-release
unset RVT__POSTGRES_INTEGRATION_CONNECTION
```

Expected: the database container and temporary caches are absent. Do not unset or print the user's separately managed package credential.

- [ ] **Step 6: Write exact final evidence**

Create `docs/superpowers/evidence/2026-07-17-rvt-common-monitor-source-removal.md` only after Steps 3–5 pass. Record these verified facts:

- authoritative repository and exact source commit `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`;
- exact package version `0.2.0-rc.1` and the unchanged lock hashes already recorded in the RC evidence;
- branch from `git branch --show-current` and the source-removal commit SHA from
  `git log -1 --format=%H --grep='refactor: remove monitor-owned common source'`;
- 14 root solution projects and two projects in each vendor solution;
- locked restore result, formatter result, build warning/error counts, and exact test-suite counts;
- five image IDs and five Common/Infrastructure inventory rows;
- absence of IntegrationTesting from runtime outputs;
- Compose, package-policy, active-document, solution, export, and diff-check results;
- deletion of `rvt-monitor-common/` and the intentional deviation from stable-first deletion;
- rollback commit `924ed20deee37fba17452612ae40eae8e0fe6168`;
- confirmation that no credential, connection string, live provider call, migration, deployment, or production database operation occurred.

Do not copy tokens, the fixture connection string, destinations, provider responses, or environment dumps into evidence.

- [ ] **Step 7: Update project state**

Add a new top section to `project_state.md` recording the package-only boundary, exact package/source versions, source-removal and documentation commits, fresh verification counts, image inventory, RC limitation, rollback commit, and the next action: review/push the branch without merging automatically.

Preserve historical sections that describe the earlier source checkpoint; mark the new top section as the superseding current state instead of rewriting audit history.

- [ ] **Step 8: Commit final evidence and state**

Run:

```bash
git add docs/superpowers/evidence/2026-07-17-rvt-common-monitor-source-removal.md project_state.md
git diff --cached --check
git commit -m "test: record package-only common verification"
git status --short --branch
```

Expected: the worktree is clean and remains on `codex/rvt-common-private-nuget-migration`. Do not push, open/retarget a pull request, mark it ready, merge, delete branches, or remove sibling worktrees without separate authorization.

## Final Acceptance Gate

- [ ] The root solution contains exactly 14 consumer projects; vendor solutions contain only their app and test projects.
- [ ] The active checkout contains no `rvt-monitor-common/` tree, local Common project, or source-switch fallback.
- [ ] Direct consumers match the approved package matrix; unrelated projects have no RVT package.
- [ ] Common, Infrastructure, and IntegrationTesting resolve exactly to synchronized `0.2.0-rc.1` versions.
- [ ] IntegrationTesting remains test-only with `PrivateAssets="all"`.
- [ ] Local restore, CI, Docker, runtime inventory, and curated release are package-only and credential-safe.
- [ ] Locked restore, formatter, 14-project build, 914 consumer tests, five container builds, five package inventories, Compose, export, scans, and diff checks pass with fresh evidence.
- [ ] Active documentation names `RVT-Group-LTD/rvt-reporting` as source authority and contains no retired local source path.
- [ ] Historical specifications/evidence and rollback history remain intact.
- [ ] The final branch is clean and unmerged pending explicit review/publish authorization.
