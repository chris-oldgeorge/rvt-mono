# RVT Common Private NuGet Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the current shared monitor projects into `RVT-Group-LTD/rvt-reporting`, publish synchronized private NuGet packages, migrate RVT Monitors to package-only consumption, and establish the evidence and gate required for the independent portal migration and stable release.

**Architecture:** Preserve the current source and assembly boundaries in a dedicated repository: `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` remain three synchronized packages. The common repository builds and tests one source-project graph, but CI validates the generated packages through package-only temporary consumers. RVT Monitors then uses exact central versions from GitHub Packages; private-feed credentials are runtime-only environment values and BuildKit secrets.

**Tech Stack:** .NET SDK 10.0.203 or a compatible .NET 10 patch SDK, MSBuild/NuGet Central Package Management, GitHub Packages, GitHub Actions, `git filter-repo`, MSTest, PostgreSQL 17, Docker BuildKit, Microsoft SBOM Tool 4.1.5.

## Global Constraints

- The extraction baseline is monitor commit `8739750`; execution must stop if `git diff 8739750..HEAD -- rvt-monitor-common` is non-empty until the baseline is deliberately refreshed.
- Initial package version is exactly `0.2.0-rc.1`; stable promotion is exactly `0.2.0` from the accepted source state.
- Publish exactly `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` on one synchronized version train.
- Preserve existing namespaces, public APIs, configuration keys, entity mappings, database behavior, and runtime behavior during extraction and package-reference cutover.
- Keep `Rvt.Monitor.Common.Infrastructure` separate from Common; it depends on the matching Common version.
- Reference `Rvt.Monitor.IntegrationTesting` only from test projects and always with `PrivateAssets="all"`.
- Keep source-project references only inside the authoritative common repository; consumers may not have conditional source/package switches.
- Resolve `Rvt.*` packages only from `https://nuget.pkg.github.com/RVT-Group-LTD/index.json`; resolve public dependencies from NuGet.org.
- Never commit credentials, personal access tokens, generated credential files, live connection strings, destinations, or provider secrets.
- Use `NuGetPackageSourceCredentials_rvt` for developer/CI restore credentials and Docker BuildKit secret `nuget_credentials` for image builds.
- Published versions are immutable and are never overwritten or deleted as a normal rollback mechanism.
- `RVT-Group-LTD/rvt-reporting` owns shared migration artifacts; only the designated RVT deployment operator following `docs/releasing.md` applies them. Monitor and portal containers never apply shared migrations at startup.
- Common and monitor changes use separate branches, commits, reviews, and release gates.
- The portal repository is not available in this workspace. Its code migration requires a portal-specific spec and plan after its exact repository, usage, and baseline are inventoried; `0.2.0` promotion is blocked until that plan supplies accepted staging evidence.

---

### Task 1: Freeze and record the extraction baseline

**Repository:** `rvt-monitors`

**Files:**
- Create: `docs/superpowers/evidence/2026-07-16-rvt-common-extraction-baseline.md`

**Interfaces:**
- Consumes: monitor commit `8739750` and the current six common/test projects under `rvt-monitor-common/`.
- Produces: a reviewed source/reference/test baseline that Task 2 uses as the only extraction input.

- [ ] **Step 1: Verify the approved baseline has not drifted**

Run:

```bash
git status --short --branch
git diff --exit-code 8739750..HEAD -- rvt-monitor-common
git rev-parse 8739750
```

Expected: the common-path diff is empty and the final command prints the full object ID for `8739750`. If the diff is non-empty, stop and update the design and this plan to name a new reviewed baseline before continuing.

- [ ] **Step 2: Capture the project-reference inventory**

Run:

```bash
rg -n '<ProjectReference' --glob '*.csproj' \
  | rg 'rvt-monitor-common|Rvt\.Monitor\.(Common|IntegrationTesting)' \
  | sort
```

Expected: 22 matching references, including five production hosts that reference Infrastructure, six IntegrationTesting references, and the internal common test/project graph.

- [ ] **Step 3: Run the extraction baseline verification**

Run sequentially with the runtime-only PostgreSQL connection supplied to the process:

```bash
dotnet build rvt-monitors.sln --no-restore --nologo
dotnet test rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj --no-build --nologo
dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-build --nologo
dotnet test rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj --no-build --nologo
dotnet test airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --no-build --nologo
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-build --nologo
dotnet test omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --no-build --nologo
dotnet test svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --no-build --nologo
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --no-build --nologo
```

Expected: zero failed tests. Record each project count, the full baseline SHA, SDK version, and the 22-reference inventory in the evidence file; do not record the PostgreSQL connection.

- [ ] **Step 4: Write the evidence document**

Use this exact structure, replacing command-result fields with the observed non-secret output:

```markdown
# RVT Common Extraction Baseline

- Approved short commit: `8739750`
- Approved full commit: the full SHA printed by `git rev-parse 8739750`
- SDK: the value printed by `dotnet --version`
- Common path drift: none
- Common-related project references: 22
- Build: passed with zero errors
- IntegrationTesting tests: passed
- Common tests: passed
- Infrastructure tests: passed
- AirQ tests: passed
- MyATM tests: passed
- Omnidots tests: passed
- Svantek tests: passed
- Reporting tests: passed
- Credential or connection value persisted: no
```

- [ ] **Step 5: Commit the baseline**

```bash
git add docs/superpowers/evidence/2026-07-16-rvt-common-extraction-baseline.md
git commit -m "docs: record rvt common extraction baseline"
```

### Task 2: Extract the authoritative common repository with history

**Repository:** source `rvt-monitors`; target `RVT-Group-LTD/rvt-reporting`

**Files:**
- Move with history: `rvt-monitor-common/Rvt.Monitor.Common/` to `src/Rvt.Monitor.Common/`
- Move with history: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/` to `src/Rvt.Monitor.Common.Infrastructure/`
- Move with history: `rvt-monitor-common/Rvt.Monitor.CommonTests/` to `tests/Rvt.Monitor.CommonTests/`
- Move with history: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/` to `tests/Rvt.Monitor.Common.InfrastructureTests/`
- Move with history: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting/` to `testing/Rvt.Monitor.IntegrationTesting/`
- Move with history: `rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/` to `testing/Rvt.Monitor.IntegrationTesting.Tests/`
- Move with history: `rvt-monitor-common/database/` to `database/`
- Replace: `rvt-monitor-common/rvt-monitor-common.sln` with `rvt-common.sln`

**Interfaces:**
- Consumes: the frozen commit and evidence from Task 1.
- Produces: private repository `RVT-Group-LTD/rvt-reporting` with preserved file history and all six production/test projects. The target repository name does not rename `rvt-common.sln`, package IDs, or namespaces.

- [ ] **Step 1: Establish the required external authorization**

Run:

```bash
gh auth status
git filter-repo --version
gh repo view RVT-Group-LTD/rvt-reporting --json nameWithOwner,isPrivate,isEmpty,url
```

Expected: GitHub authentication is valid, `git filter-repo` is installed, and the pre-provisioned target reports `isPrivate: true` and `isEmpty: true`. Stop rather than overwrite the repository if it is non-empty.

- [ ] **Step 2: Normalize the pre-provisioned repository settings**

```bash
gh repo edit RVT-Group-LTD/rvt-reporting --enable-issues=false --enable-wiki=false
gh repo view RVT-Group-LTD/rvt-reporting \
  --json nameWithOwner,isPrivate,isEmpty,hasIssuesEnabled,hasWikiEnabled,url
```

Expected: GitHub reports `https://github.com/RVT-Group-LTD/rvt-reporting`, `isPrivate: true`, `isEmpty: true`, `hasIssuesEnabled: false`, and `hasWikiEnabled: false`. The user explicitly approved this repository-only retarget on 2026-07-16.

- [ ] **Step 3: Filter the reviewed history into an isolated temporary clone**

Run only when `/private/tmp/rvt-common-extraction` does not exist:

```bash
test ! -e /private/tmp/rvt-common-extraction
git clone --no-local /Users/oldgeorge/Documents/rvt-monitors/rvt-monitors /private/tmp/rvt-common-extraction
cd /private/tmp/rvt-common-extraction
git checkout --detach 8739750
git filter-repo --force \
  --path rvt-monitor-common/Rvt.Monitor.Common/ \
  --path rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/ \
  --path rvt-monitor-common/Rvt.Monitor.CommonTests/ \
  --path rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/ \
  --path rvt-monitor-common/Rvt.Monitor.IntegrationTesting/ \
  --path rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/ \
  --path rvt-monitor-common/database/ \
  --path rvt-monitor-common/rvt-monitor-common.sln \
  --path-rename rvt-monitor-common/Rvt.Monitor.Common/:src/Rvt.Monitor.Common/ \
  --path-rename rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/:src/Rvt.Monitor.Common.Infrastructure/ \
  --path-rename rvt-monitor-common/Rvt.Monitor.CommonTests/:tests/Rvt.Monitor.CommonTests/ \
  --path-rename rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/:tests/Rvt.Monitor.Common.InfrastructureTests/ \
  --path-rename rvt-monitor-common/Rvt.Monitor.IntegrationTesting/:testing/Rvt.Monitor.IntegrationTesting/ \
  --path-rename rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/:testing/Rvt.Monitor.IntegrationTesting.Tests/ \
  --path-rename rvt-monitor-common/database/:database/ \
  --path-rename rvt-monitor-common/rvt-monitor-common.sln:rvt-common.sln
git switch -C main
```

Expected: only the common source, tests, integration support, migrations, and solution history remain.

- [ ] **Step 4: Rebuild the solution so it contains every extracted project and no portal-relative solution item**

Run:

```bash
dotnet new sln -n rvt-common --format sln --force
dotnet sln rvt-common.sln add \
  src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj \
  testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj
dotnet sln rvt-common.sln list
```

Expected: exactly six extracted projects are listed; no reference to `rvtportal-spa-alpha` remains.

- [ ] **Step 5: Repair relative project references after the directory move**

Use these exact target paths:

```xml
<!-- src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj -->
<ProjectReference Include="../Rvt.Monitor.Common/Rvt.Monitor.Common.csproj" />

<!-- tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj -->
<ProjectReference Include="../../src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj" />

<!-- tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj -->
<ProjectReference Include="../../src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj" />

<!-- testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj -->
<ProjectReference Include="../Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj" />
```

Run `dotnet build rvt-common.sln --nologo`. Expected: restore and build succeed.

- [ ] **Step 6: Verify history preservation**

```bash
git log --follow --oneline -- src/Rvt.Monitor.Common/Hosting/MonitorHost.cs
git log --follow --oneline -- src/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs
git log --follow --oneline -- testing/Rvt.Monitor.IntegrationTesting/PostgreSqlIntegrationDatabase.cs
```

Expected: each command shows commits that predate extraction, including the relevant original monitor-repository commits.

- [ ] **Step 7: Publish the extracted `main` branch**

```bash
git remote add origin https://github.com/RVT-Group-LTD/rvt-reporting.git
git add rvt-common.sln src tests testing database
git commit -m "chore: establish authoritative rvt common repository"
git push -u origin main
```

Expected: the private repository contains the preserved history and six-project solution. Then create the native working clone used by later tasks:

```bash
cd /Users/oldgeorge/Documents/rvt-monitors
test ! -e rvt-reporting
git clone https://github.com/RVT-Group-LTD/rvt-reporting.git rvt-reporting
```

### Task 3: Add deterministic package metadata and central dependency versions

**Repository:** `rvt-reporting`

**Files:**
- Create: `Directory.Build.props`
- Create: `Directory.Build.targets`
- Create: `Directory.Packages.props`
- Create: `NuGet.config`
- Create: `.gitignore`
- Create: `README.md`
- Modify: `src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Modify: `testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj`
- Modify: all three extracted test `.csproj` files
- Create: generated `packages.lock.json` beside every project

**Interfaces:**
- Consumes: six-project source graph from Task 2.
- Produces: three packable projects with synchronized `PackageVersion`, deterministic/source metadata, symbols, locked restore, and centrally managed public dependencies.

- [ ] **Step 1: Write a failing metadata inspection**

Run:

```bash
dotnet msbuild src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  -getProperty:PackageId -getProperty:PackageVersion -getProperty:RepositoryUrl -getProperty:IncludeSymbols
dotnet msbuild testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  -getProperty:IsPackable -getProperty:PackageId -getProperty:PackageVersion
```

Expected before implementation: Infrastructure lacks complete package metadata and IntegrationTesting reports `IsPackable=false`.

- [ ] **Step 2: Add common build and package metadata**

Create `Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <PackageVersion Condition="'$(PackageVersion)' == ''">0.2.0-rc.1</PackageVersion>
    <Version>$(PackageVersion)</Version>
    <Authors>RVT</Authors>
    <Company>RVT Group</Company>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/RVT-Group-LTD/rvt-reporting</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageOutputPath>$(MSBuildThisFileDirectory)artifacts/packages</PackageOutputPath>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

Create `Directory.Build.targets` so packability has already been evaluated when the README item is added:

```xml
<Project>
  <ItemGroup Condition="'$(IsPackable)' == 'true'">
    <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Centralize the extracted repository dependencies without changing versions**

Create `Directory.Packages.props` with:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="AWSSDK.S3" Version="4.0.100.3" />
    <PackageVersion Include="Azure.Identity" Version="1.15.0" />
    <PackageVersion Include="Azure.Storage.Blobs" Version="12.25.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="7.0.1" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.4" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="10.0.9" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageVersion Include="Microsoft.SqlServer.TransactSql.ScriptDom" Version="180.37.3" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="MQTTnet" Version="4.3.7.1207" />
    <PackageVersion Include="MQTTnet.Extensions.ManagedClient" Version="4.3.7.1207" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.0.2" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.0.2" />
    <PackageVersion Include="Npgsql" Version="10.0.3" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.16.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.16.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.16.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.16.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.1" />
    <PackageVersion Include="Quartz.Extensions.Hosting" Version="3.18.1" />
    <PackageVersion Include="SendGrid" Version="9.29.3" />
  </ItemGroup>
</Project>
```

Remove every inline version attribute from `PackageReference` elements in the six extracted projects. Preserve `PrivateAssets` and `OutputItemType` metadata unchanged.

- [ ] **Step 4: Add credential-free package sources and mapping**

Create `NuGet.config` with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="rvt" value="https://nuget.pkg.github.com/RVT-Group-LTD/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="rvt">
      <package pattern="Rvt.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Do not add `packageSourceCredentials`; authentication comes only from `NuGetPackageSourceCredentials_rvt`.

- [ ] **Step 5: Make exactly three projects packable**

Set the following project-specific properties inside each project's existing `PropertyGroup`, remove the old Common `<Version>0.1.0</Version>`, and add SourceLink in each project's package `ItemGroup`:

```xml
<!-- src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj -->
<IsPackable>true</IsPackable>
<PackageId>Rvt.Monitor.Common</PackageId>
<Description>Shared monitor contracts, data helpers, storage, hosting, scheduling, observability, and compatibility runtime.</Description>

<!-- src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj -->
<IsPackable>true</IsPackable>
<PackageId>Rvt.Monitor.Common.Infrastructure</PackageId>
<Description>Provider adapters, configuration validation, and infrastructure composition for RVT monitor applications.</Description>

<!-- testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj -->
<IsPackable>true</IsPackable>
<PackageId>Rvt.Monitor.IntegrationTesting</PackageId>
<Description>Shared PostgreSQL integration-test fixture support for RVT test projects.</Description>
```

Add this item to all three packable projects:

```xml
<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
```

Keep `<IsPackable>false</IsPackable>` in all test projects.

- [ ] **Step 6: Add repository documentation and ignore rules**

The README must document the three package responsibilities, .NET 10 requirement, exact-version train, local build/test/pack commands, runtime-only credential format, release policy, and migration ownership. Add these ignore entries:

```gitignore
bin/
obj/
artifacts/
TestResults/
*.nupkg
*.snupkg
appsettings.Development.json
```

Document the restore credential without a real value:

```bash
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
```

- [ ] **Step 7: Generate and verify locked restores**

```bash
dotnet restore rvt-common.sln --use-lock-file
dotnet build rvt-common.sln -c Release --no-restore --nologo
dotnet msbuild src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  -getProperty:PackageId -getProperty:PackageVersion -getProperty:RepositoryUrl -getProperty:IncludeSymbols
dotnet msbuild testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  -getProperty:IsPackable -getProperty:PackageId -getProperty:PackageVersion
```

Expected: all lock files are created, the build passes, PackageVersion is `0.2.0-rc.1`, repository metadata is populated, symbols are enabled, and IntegrationTesting is packable.

- [ ] **Step 8: Commit package metadata**

```bash
git add Directory.Build.props Directory.Build.targets Directory.Packages.props NuGet.config .gitignore README.md src tests testing
git commit -m "build: configure synchronized common packages"
```

### Task 3A: Repair extracted test-layout assumptions before CI

**Repository:** `rvt-reporting`

**Files:**
- Modify: `Directory.Build.targets`
- Modify: `tests/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDeliveryMigrationContractTests.cs`
- Modify: `tests/Rvt.Monitor.CommonTests/Architecture/CommunicationsBoundaryTests.cs`

**Interfaces:**
- Consumes: the standalone `src/`, `tests/`, `testing/`, and `database/` layout from Tasks 2-3 plus the existing ignored local integration settings convention.
- Produces: green extracted common/infrastructure/integration tests whose architecture guards inspect only source owned by `rvt-reporting`; consumer-only guards remain authoritative in `rvt-monitors` and are relocated in Task 8 before source removal.

- [ ] **Step 1: Reproduce all three extraction-layout failure families**

With an ignored `testing/Rvt.Monitor.IntegrationTesting/appsettings.Development.json` present but no credential or connection printed, run:

```bash
dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --no-restore --nologo \
  --filter 'FullyQualifiedName~CreateAsync_UsesGeneratedSchemaAsTheOnlySearchPath|FullyQualifiedName~DisposeAsync_DropsOnlyTheGeneratedSchema|FullyQualifiedName~FixtureCleanup_DropsItsOwnGeneratedSchema'
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-restore --nologo \
  --filter 'FullyQualifiedName~PostgreSqlMigration_CreatesSharedDeliveryOutboxContract|FullyQualifiedName~SqlServerMigration_CreatesSharedDeliveryOutboxContract'
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --no-build --no-restore --nologo \
  --filter 'FullyQualifiedName~CommonContainsNoLegacyTransportOrProviderPackage|FullyQualifiedName~SendGridProviderTypesAndPackageAreConfinedToInfrastructure|FullyQualifiedName~ReportingMessagingContainsNoSendGridBoundary|FullyQualifiedName~ObsoleteSynchronousMessageCallsAreLimitedToExplicitCompatibilityAllowlist'
```

Expected RED: three tests report missing runtime PostgreSQL input because the output-local settings link is absent; two tests report the retired `rvt-monitor-common/database/migrations` path; four tests report that the old monitor repository root cannot be found.

- [ ] **Step 2: Restore the ignored integration-settings copy convention**

Keep the pack README target and append this item group to `Directory.Build.targets`:

```xml
  <ItemGroup Condition="'$(IsTestProject)' == 'true' And Exists('$(MSBuildThisFileDirectory)testing/Rvt.Monitor.IntegrationTesting/appsettings.Development.json')">
    <None Include="$(MSBuildThisFileDirectory)testing/Rvt.Monitor.IntegrationTesting/appsettings.Development.json">
      <Link>rvt-integration.appsettings.Development.json</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

Do not add or read a connection value. Expected: test builds copy the ignored source file under the output filename already consumed by `PostgreSqlIntegrationDatabase`.

- [ ] **Step 3: Retarget migration contract tests to the authoritative root directory**

In `ReadMigration`, replace only the retired prefix:

```csharp
var path = Path.Combine(repositoryRoot, "database", "migrations", fileName);
```

Keep the `.git` root resolver and both migration contract assertions unchanged. Do not duplicate or move migration files.

- [ ] **Step 4: Scope communications architecture guards to repository-owned source**

Apply these exact ownership changes in `CommunicationsBoundaryTests.cs`:

```csharp
private static readonly string[] LegacyTransportFiles =
[
    "src/Rvt.Monitor.Common/Communications/Email" + "Sender.cs",
    "src/Rvt.Monitor.Common/Communications/SmsSender.cs",
    "src/Rvt.Monitor.Common/Communications/CommsClient.cs",
    "src/Rvt.Monitor.Common/Communications/ICommsClient.cs",
    "src/Rvt.Monitor.Common/Sms/TransmitSmsClient.cs"
];

private static readonly string[] SynchronousCompatibilityCallers =
[
    "src/Rvt.Monitor.Common/Rules/RuleAlertNotificationDispatcher.cs"
];
```

Read the common project and common production source from:

```csharp
"src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
"src/Rvt.Monitor.Common"
```

Limit the SendGrid provider scan to `Path.Combine(root, "src")`, and limit the obsolete synchronous-call scan to `ReadProductionSource(root, "src/Rvt.Monitor.Common")`. Remove `ReportingMessagingContainsNoSendGridBoundary` from the extracted test class because `reportingmonitor` is a consumer owned by `rvt-monitors`; Task 8 adds its surviving consumer-side equivalent.

Replace `FindRepositoryRoot` with the worktree-safe `.git` resolver:

```csharp
private static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var gitPath = Path.Combine(directory.FullName, ".git");
        if (Directory.Exists(gitPath) || File.Exists(gitPath))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}
```

- [ ] **Step 5: Run focused GREEN verification**

```bash
dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~CreateAsync_UsesGeneratedSchemaAsTheOnlySearchPath|FullyQualifiedName~DisposeAsync_DropsOnlyTheGeneratedSchema|FullyQualifiedName~FixtureCleanup_DropsItsOwnGeneratedSchema'
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~PostgreSqlMigration_CreatesSharedDeliveryOutboxContract|FullyQualifiedName~SqlServerMigration_CreatesSharedDeliveryOutboxContract'
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  --nologo \
  --filter 'FullyQualifiedName~CommonContainsNoLegacyTransportOrProviderPackage|FullyQualifiedName~SendGridProviderTypesAndPackageAreConfinedToInfrastructure|FullyQualifiedName~ObsoleteSynchronousMessageCallsAreLimitedToExplicitCompatibilityAllowlist'
```

Expected GREEN: 3/3 PostgreSQL fixture tests, 2/2 migration tests, and 3/3 repository-owned communications guards pass. No connection value appears in output.

- [ ] **Step 6: Run the extracted repository regression gate and commit**

```bash
dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj --nologo
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --nologo
dotnet test tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj --nologo
git diff --check
git add Directory.Build.targets \
  tests/Rvt.Monitor.CommonTests/Data/EntityFramework/MonitorDeliveryMigrationContractTests.cs \
  tests/Rvt.Monitor.CommonTests/Architecture/CommunicationsBoundaryTests.cs
git commit -m "test: adapt extracted repository layout"
```

Expected: all three test projects pass with zero failed tests, no credential or connection is tracked, and the commit contains only the three named files.

### Task 4: Validate package contents and package-only consumers

**Repository:** `rvt-reporting`

**Files:**
- Create: `tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj`
- Create: `tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs`
- Create: `tests/Rvt.Monitor.PackageValidationTests/Properties/AssemblyInfo.cs`
- Modify: `Directory.Build.targets`
- Create: `package-validation/NuGet.local.config`
- Create: `package-validation/RuntimeConsumer/RuntimeConsumer.csproj`
- Create: `package-validation/RuntimeConsumer/Program.cs`
- Create: `package-validation/TestConsumer/TestConsumer.csproj`
- Create: `package-validation/TestConsumer/PackageSmokeTests.cs`
- Create: `package-validation/TestConsumer/Properties/AssemblyInfo.cs`
- Modify: `rvt-common.sln`

**Interfaces:**
- Consumes: `.nupkg` and `.snupkg` artifacts from Task 3.
- Produces: automated proof that exactly three packages contain the correct .NET 10 assemblies, Infrastructure targets the synchronized Common version, IntegrationTesting excludes local settings, and consumers compile without source projects.

- [ ] **Step 1: Write failing package artifact tests**

Create the validation test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
  </ItemGroup>
</Project>
```

Create tests with these exact assertions:

```csharp
using System.IO.Compression;
using System.Xml.Linq;

namespace Rvt.Monitor.PackageValidationTests;

[TestClass]
public sealed class PackageArtifactTests
{
    private static readonly string Version =
        Environment.GetEnvironmentVariable("RVT_PACKAGE_VERSION") ?? "0.2.0-rc.1";
    private static readonly string Artifacts = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/packages"));

    [TestMethod]
    public void ReleaseContainsExactlyTheThreeCompatibilityPackages()
    {
        var names = Directory.EnumerateFiles(Artifacts, "*.nupkg")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var symbolNames = Directory.EnumerateFiles(Artifacts, "*.snupkg")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var packageIds = new[]
        {
            "Rvt.Monitor.Common",
            "Rvt.Monitor.Common.Infrastructure",
            "Rvt.Monitor.IntegrationTesting"
        };

        CollectionAssert.AreEqual(
            packageIds.Select(id => $"{id}.{Version}.nupkg").Order(StringComparer.Ordinal).ToArray(),
            names);
        CollectionAssert.AreEqual(
            packageIds.Select(id => $"{id}.{Version}.snupkg").Order(StringComparer.Ordinal).ToArray(),
            symbolNames);
    }

    [TestMethod]
    [DataRow("Rvt.Monitor.Common", "Rvt.Monitor.Common.dll")]
    [DataRow("Rvt.Monitor.Common.Infrastructure", "Rvt.Monitor.Common.Infrastructure.dll")]
    [DataRow("Rvt.Monitor.IntegrationTesting", "Rvt.Monitor.IntegrationTesting.dll")]
    public void PackageContainsOnlyItsExpectedNet10Assembly(string packageId, string assemblyName)
    {
        using var archive = Open(packageId);
        var assemblies = archive.Entries
            .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { $"lib/net10.0/{assemblyName}" }, assemblies);
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.Contains("Tests.dll", StringComparison.Ordinal)));
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void InfrastructureDependencyStartsAtTheSynchronizedCommonVersion()
    {
        using var archive = Open("Rvt.Monitor.Common.Infrastructure");
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var dependency = document.Descendants().Single(element =>
            element.Name.LocalName == "dependency" &&
            (string?)element.Attribute("id") == "Rvt.Monitor.Common");

        Assert.AreEqual($"[{Version}]", (string?)dependency.Attribute("version"));
    }

    private static ZipArchive Open(string packageId) => ZipFile.OpenRead(
        Path.Combine(Artifacts, $"{packageId}.{Version}.nupkg"));
}
```

Add `Properties/AssemblyInfo.cs` to both MSTest validation projects:

```csharp
[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.Parallelize(
    Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel)]
```

Run `dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj --nologo`. Expected: FAIL because packages do not exist yet.

- [ ] **Step 2: Pin the generated Infrastructure-to-Common dependency and pack**

Append this narrowly scoped pack target to `Directory.Build.targets`; it changes only the generated dependency range after NuGet has evaluated project-reference versions:

```xml
  <Target Name="PinCommonDependencyVersionForInfrastructurePackage"
          AfterTargets="_GetProjectReferenceVersions"
          BeforeTargets="GenerateNuspec"
          Condition="'$(PackageId)' == 'Rvt.Monitor.Common.Infrastructure'">
    <ItemGroup>
      <_ProjectReferencesWithVersions Update="@(_ProjectReferencesWithVersions)"
          Condition="'%(_ProjectReferencesWithVersions.Filename)' == 'Rvt.Monitor.Common'">
        <ProjectVersion>[$(PackageVersion)]</ProjectVersion>
      </_ProjectReferencesWithVersions>
    </ItemGroup>
  </Target>
```

Then add the validation project and produce fresh packages:

```bash
dotnet sln rvt-common.sln add tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj
dotnet restore rvt-common.sln --use-lock-file
dotnet build rvt-common.sln -c Release --no-restore --nologo
dotnet pack src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj -c Release --no-build -p:PackageVersion=0.2.0-rc.1
dotnet pack src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj -c Release --no-build -p:PackageVersion=0.2.0-rc.1
dotnet pack testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj -c Release --no-build -p:PackageVersion=0.2.0-rc.1
dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj -c Release --no-build --nologo
```

Expected: three `.nupkg` and three `.snupkg` files are produced, all package artifact tests pass without new MSTest warnings, and the Infrastructure nuspec contains exact dependency `version="[0.2.0-rc.1]"` for Common.

- [ ] **Step 3: Add a package-only local feed**

Create `package-validation/NuGet.local.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-rvt" value="../artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-rvt"><package pattern="Rvt.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

- [ ] **Step 4: Add a runtime package-only consumer**

Create `RuntimeConsumer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="Exists('$(MSBuildProjectDirectory)/packages.lock.json')">true</RestoreLockedMode>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RvtPackageVersion Condition="'$(RvtPackageVersion)' == ''">0.2.0-rc.1</RvtPackageVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Rvt.Monitor.Common" Version="[$(RvtPackageVersion)]" />
    <PackageReference Include="Rvt.Monitor.Common.Infrastructure" Version="[$(RvtPackageVersion)]" />
  </ItemGroup>
</Project>
```

Create `Program.cs`:

```csharp
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Infrastructure.Communications;

Console.WriteLine(typeof(MonitorExecutionMode).Assembly.FullName);
Console.WriteLine(typeof(EmailProvider).Assembly.FullName);
```

- [ ] **Step 5: Add a test package-only consumer**

Create `TestConsumer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="Exists('$(MSBuildProjectDirectory)/packages.lock.json')">true</RestoreLockedMode>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RvtPackageVersion Condition="'$(RvtPackageVersion)' == ''">0.2.0-rc.1</RvtPackageVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.0.2" />
    <PackageReference Include="MSTest.TestFramework" Version="4.0.2" />
    <PackageReference Include="Rvt.Monitor.IntegrationTesting" Version="[$(RvtPackageVersion)]" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Create `PackageSmokeTests.cs`:

```csharp
using Rvt.Monitor.IntegrationTesting;

namespace TestConsumer;

[TestClass]
public sealed class PackageSmokeTests
{
    [TestMethod]
    public void IntegrationFixtureLoadsFromThePackage() =>
        Assert.AreEqual(
            "Rvt.Monitor.IntegrationTesting",
            typeof(PostgreSqlIntegrationDatabase).Assembly.GetName().Name);
}
```

- [ ] **Step 6: Prove both consumers restore solely from packages**

```bash
dotnet restore package-validation/RuntimeConsumer/RuntimeConsumer.csproj --configfile package-validation/NuGet.local.config --force --use-lock-file
dotnet restore package-validation/RuntimeConsumer/RuntimeConsumer.csproj --configfile package-validation/NuGet.local.config --locked-mode
dotnet build package-validation/RuntimeConsumer/RuntimeConsumer.csproj --no-restore --nologo
dotnet run --project package-validation/RuntimeConsumer/RuntimeConsumer.csproj --no-build
dotnet restore package-validation/TestConsumer/TestConsumer.csproj --configfile package-validation/NuGet.local.config --force --use-lock-file
dotnet restore package-validation/TestConsumer/TestConsumer.csproj --configfile package-validation/NuGet.local.config --locked-mode
dotnet test package-validation/TestConsumer/TestConsumer.csproj --no-restore --nologo
rg -n '<ProjectReference' package-validation
```

Expected: both consumers pass; output shows the two runtime assembly identities; project references use `[0.2.0-rc.1]` and lock files normalize that exact singleton range as `[0.2.0-rc.1, 0.2.0-rc.1]`; both locked restores pass; the final search prints no matches; validation projects add no MSTest warning sites.

- [ ] **Step 7: Commit package validation**

```bash
git add Directory.Build.targets rvt-common.sln tests/Rvt.Monitor.PackageValidationTests package-validation
git commit -m "test: validate common package artifacts"
```

### Task 5: Add common pull-request CI

**Repository:** `rvt-reporting`

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `docs/dependency-license-review.md`
- Modify: generated `packages.lock.json` files when restore inputs change

**Interfaces:**
- Consumes: source projects, lock files, package validation tests, and package-only consumers.
- Produces: required PR check `build-test-pack` with PostgreSQL integration coverage and retained package artifacts.

- [ ] **Step 1: Add a deliberately failing CI syntax check before the workflow exists**

Run:

```bash
test -f .github/workflows/ci.yml
```

Expected: FAIL because the workflow does not exist.

- [ ] **Step 2: Create the CI workflow**

Create `.github/workflows/ci.yml`:

```yaml
name: common-ci

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read
  packages: read

jobs:
  build-test-pack:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:17
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: rvt_common_ci
        ports:
          - 5432:5432
        options: >-
          --health-cmd "pg_isready -U postgres -d rvt_common_ci"
          --health-interval 5s
          --health-timeout 5s
          --health-retries 20
    env:
      CI: true
      RVT_PACKAGE_VERSION: 0.2.0-ci.${{ github.run_number }}
      NuGetPackageSourceCredentials_rvt: Username=${{ github.actor }};Password=${{ github.token }};ValidAuthenticationTypes=Basic
      RVT__POSTGRES_INTEGRATION_CONNECTION: Host=127.0.0.1;Port=5432;Database=rvt_common_ci;Username=postgres;Password=postgres
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion="$RVT_PACKAGE_VERSION"
      - name: Reject vulnerable dependencies
        shell: bash
        run: |
          output="$(dotnet list rvt-common.sln package --vulnerable --include-transitive)"
          printf '%s\n' "$output"
          ! grep -q 'has the following vulnerable packages' <<<"$output"
      - run: dotnet format whitespace rvt-common.sln --verify-no-changes --no-restore
      - run: dotnet build rvt-common.sln -c Release --no-restore --nologo -p:PackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj -c Release --no-build --nologo
      - run: dotnet test tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj -c Release --no-build --nologo
      - run: dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj -c Release --no-build --nologo
      - run: dotnet pack src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj -c Release --no-build -p:PackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet pack src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj -c Release --no-build -p:PackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet pack testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj -c Release --no-build -p:PackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj -c Release --no-build --nologo
      - run: dotnet restore package-validation/RuntimeConsumer/RuntimeConsumer.csproj --configfile package-validation/NuGet.local.config --force-evaluate -p:RestoreLockedMode=false -p:RvtPackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet build package-validation/RuntimeConsumer/RuntimeConsumer.csproj --no-restore --nologo -p:RvtPackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet restore package-validation/TestConsumer/TestConsumer.csproj --configfile package-validation/NuGet.local.config --force-evaluate -p:RestoreLockedMode=false -p:RvtPackageVersion="$RVT_PACKAGE_VERSION"
      - run: dotnet test package-validation/TestConsumer/TestConsumer.csproj --no-restore --nologo -p:RvtPackageVersion="$RVT_PACKAGE_VERSION"
      - uses: actions/upload-artifact@v6
        with:
          name: rvt-common-packages
          path: artifacts/packages/*.*nupkg
          if-no-files-found: error
```

Use the whitespace formatter subcommand for this gate. The extracted baseline still emits existing MSTest analyzer diagnostics under the all-diagnostics `dotnet format` mode, while `dotnet format whitespace ... --verify-no-changes` enforces repository formatting independently and is supported by the pinned .NET 10 SDK.

- [ ] **Step 3: Validate the workflow locally and in GitHub**

```bash
test -f .github/workflows/ci.yml
git diff --check
dotnet list rvt-common.sln package --include-transitive --format json \
  > /private/tmp/rvt-reporting-dependency-inventory.json
git add .github/workflows/ci.yml docs/dependency-license-review.md '**/packages.lock.json'
git commit -m "ci: validate common package builds"
git push -u origin codex/rvt-reporting-private-nuget
gh pr create --repo RVT-Group-LTD/rvt-reporting \
  --base main \
  --head codex/rvt-reporting-private-nuget \
  --draft \
  --title "Build and validate private RVT common packages" \
  --body "Implements the reviewed extraction, deterministic package metadata, exact package validation, CI, and release preparation plan."
gh pr checks --repo RVT-Group-LTD/rvt-reporting --watch
```

Expected: required check `build-test-pack` passes and uploads six package/symbol artifacts.

Before committing, reviewers must use the generated dependency inventory plus the resolved packages' `.nuspec` license expression/license URL and repository URL to inspect every distinct direct and transitive dependency. Record package ID, resolved version, direct/transitive status, license expression or URL, source/repository URL, and approval decision in `docs/dependency-license-review.md`; do not infer a license from a package name. Any unresolved or missing license blocks the task for human review. Any later lock-file change requires updating the review in the same PR. NuGet audit output must contain no unresolved vulnerability.

### Task 6: Add immutable release, migration assets, checksums, and SBOM generation

**Repository:** `rvt-reporting`

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `scripts/assert-package-version-available.sh`
- Create: `scripts/build-release-artifacts.sh`
- Create: `.github/workflows/release.yml`
- Create: `docs/releasing.md`

**Interfaces:**
- Consumes: a validated source tag or explicit RC version.
- Produces: immutable packages, symbols, migration archive, checksums, SPDX SBOM, GitHub Packages versions, and GitHub release assets.

- [ ] **Step 1: Pin the SBOM tool and verify the release scripts do not exist**

```bash
test ! -f scripts/build-release-artifacts.sh
dotnet new tool-manifest --force
dotnet tool install Microsoft.Sbom.DotNetTool --version 4.1.5
```

Expected: `.config/dotnet-tools.json` pins `microsoft.sbom.dotnettool` to `4.1.5`.

- [ ] **Step 2: Add the immutable-version preflight**

Create `scripts/assert-package-version-available.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: assert-package-version-available.sh VERSION}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$ ]] || {
  echo "Invalid package version: $version" >&2
  exit 1
}
for package in Rvt.Monitor.Common Rvt.Monitor.Common.Infrastructure Rvt.Monitor.IntegrationTesting; do
  versions=""
  error_file="$(mktemp)"
  if versions="$(gh api --paginate "/orgs/RVT-Group-LTD/packages/nuget/$package/versions?per_page=100" \
      --jq '.[].name' 2>"$error_file")"; then
    if grep -Fxq "$version" <<<"$versions"; then
      rm -f "$error_file"
      echo "Package $package version $version already exists" >&2
      exit 1
    fi
  elif ! grep -q 'HTTP 404' "$error_file"; then
    cat "$error_file" >&2
    rm -f "$error_file"
    exit 1
  fi
  rm -f "$error_file"
done
```

The script validates the version before using it, examines every API page, treats package-not-found as available, and fails closed for an existing match or any other API error. Exercise existing, absent, package-404, and API-error paths with a fake `gh` executable on `PATH`; these tests must not mutate GitHub.

- [ ] **Step 3: Add deterministic release artifact creation**

Create `scripts/build-release-artifacts.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: build-release-artifacts.sh VERSION}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$ ]] || {
  echo "Invalid package version: $version" >&2
  exit 1
}

lock_backup_dir="$(mktemp -d)"
runtime_lock="package-validation/RuntimeConsumer/packages.lock.json"
test_lock="package-validation/TestConsumer/packages.lock.json"
cp "$runtime_lock" "$lock_backup_dir/runtime.packages.lock.json"
cp "$test_lock" "$lock_backup_dir/test.packages.lock.json"
restore_consumer_locks() {
  cp "$lock_backup_dir/runtime.packages.lock.json" "$runtime_lock"
  cp "$lock_backup_dir/test.packages.lock.json" "$test_lock"
  rm -rf "$lock_backup_dir"
}
trap restore_consumer_locks EXIT

rm -rf artifacts/release artifacts/packages
mkdir -p artifacts/release artifacts/packages

dotnet restore rvt-common.sln --locked-mode --force-evaluate -p:PackageVersion="$version"
dotnet clean rvt-common.sln -c Release --nologo
dotnet build rvt-common.sln -c Release --no-restore --nologo \
  -p:PackageVersion="$version"
dotnet test tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj -c Release --no-build --nologo
dotnet test tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj -c Release --no-build --nologo
dotnet test testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj -c Release --no-build --nologo
for project in \
  src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj; do
  dotnet pack "$project" -c Release --no-build -p:PackageVersion="$version"
done

tar -czf "artifacts/release/rvt-common-migrations-$version.tar.gz" database
sbom_dotnet="${SBOM_DOTNET:-dotnet}"
"$sbom_dotnet" tool restore
"$sbom_dotnet" tool run sbom-tool -- generate \
  -b artifacts/packages \
  -bc . \
  -pn rvt-common \
  -pv "$version" \
  -ps "RVT Group" \
  -nsb https://github.com/RVT-Group-LTD/rvt-reporting
"$sbom_dotnet" tool run sbom-tool -- validate \
  -b artifacts/packages \
  -o artifacts/packages/_manifest/spdx-validation.json \
  -mi SPDX:2.2
# Before generation, require every staged project.assets.json project.version to
# equal $version. Parse manifest.spdx.json and fail unless it is SPDX-2.2,
# uniquely describes rvt-common at $version, links that root through hasFiles to
# exactly the six current package/symbol files, and contains exactly the three
# Rvt.Monitor package components at $version with no stale versions. Derive the
# complete expected external package ID/version set from resolved entries in the
# three staged packages.lock.json files and require the SBOM's non-root/non-Rvt
# package set to match it exactly with no duplicates.
mv artifacts/packages/_manifest artifacts/release/sbom
RVT_PACKAGE_VERSION="$version" dotnet test \
  tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  -c Release --no-build --nologo
dotnet restore package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  --configfile package-validation/NuGet.local.config --force-evaluate \
  -p:RestoreLockedMode=false \
  -p:RvtPackageVersion="$version"
dotnet build package-validation/RuntimeConsumer/RuntimeConsumer.csproj --no-restore --nologo \
  -p:RvtPackageVersion="$version"
dotnet restore package-validation/TestConsumer/TestConsumer.csproj \
  --configfile package-validation/NuGet.local.config --force-evaluate \
  -p:RestoreLockedMode=false \
  -p:RvtPackageVersion="$version"
dotnet test package-validation/TestConsumer/TestConsumer.csproj --no-restore --nologo \
  -p:RvtPackageVersion="$version"
release_assets="artifacts/release/assets"
mkdir -p "$release_assets"
cp artifacts/packages/*.*nupkg "$release_assets/"
cp "artifacts/release/rvt-common-migrations-$version.tar.gz" "$release_assets/"
cp artifacts/release/sbom/spdx_2.2/manifest.spdx.json "$release_assets/"
cp artifacts/release/sbom/spdx_2.2/manifest.spdx.json.sha256 "$release_assets/"

if command -v sha256sum >/dev/null 2>&1; then
  checksum_command=(sha256sum)
  checksum_verify_command=(sha256sum -c)
elif command -v shasum >/dev/null 2>&1; then
  checksum_command=(shasum -a 256)
  checksum_verify_command=(shasum -a 256 -c)
else
  echo "No SHA-256 utility found" >&2
  exit 1
fi
find "$release_assets" -maxdepth 1 -type f ! -name SHA256SUMS -print \
  | LC_ALL=C sort \
  | while IFS= read -r file; do
      checksum="$("${checksum_command[@]}" "$file")"
      printf '%s  %s\n' "${checksum%% *}" "$(basename "$file")"
    done > "$release_assets/SHA256SUMS"
(cd "$release_assets" && "${checksum_verify_command[@]}" SHA256SUMS)
```

Run `chmod +x scripts/*.sh` as a mechanical permission change. The lock-file trap must restore the caller's original consumer locks on success and failure; validate this with a non-default prerelease version as well as the planned RC. Behavior tests that fake pack/restore must snapshot and restore the affected production `obj` trees so no fake nuspec or assets metadata remains. Inspect each packaged DLL's `AssemblyInformationalVersion` and require the requested package version. Verify the basename-only `SHA256SUMS` from the flat `artifacts/release/assets` directory, which must be the exact asset set uploaded by both release paths. SBOM Tool 4.1.5 targets .NET 8; CI installs that runtime explicitly alongside the .NET 10 build SDK. A developer whose normal `dotnet` root lacks .NET 8 may set `SBOM_DOTNET` to an isolated .NET 8 SDK host. Do not use major-version roll-forward: it can return success with no detected dependency components, which the manifest guard must reject.

- [ ] **Step 4: Add the release workflow**

Create `.github/workflows/release.yml` with two allowed paths: manual RC publication and protected `v*.*.*` stable tags. The core steps must be exactly:

```yaml
name: release-common

on:
  workflow_dispatch:
    inputs:
      package_version:
        description: Immutable prerelease version
        required: true
        type: string
  push:
    tags: ['v*.*.*']

permissions:
  contents: write
  packages: write

concurrency:
  group: release-common-${{ github.ref_type == 'tag' && github.ref_name || inputs.package_version }}
  cancel-in-progress: false

jobs:
  release:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:17
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: rvt_common_release
        ports: ['5432:5432']
        options: >-
          --health-cmd "pg_isready -U postgres -d rvt_common_release"
          --health-interval 5s --health-timeout 5s --health-retries 20
    env:
      CI: true
      GH_TOKEN: ${{ github.token }}
      NuGetPackageSourceCredentials_rvt: Username=${{ github.actor }};Password=${{ github.token }};ValidAuthenticationTypes=Basic
      RVT__POSTGRES_INTEGRATION_CONNECTION: Host=127.0.0.1;Port=5432;Database=rvt_common_release;Username=postgres;Password=postgres
    steps:
      - uses: actions/checkout@v6
        with: { fetch-depth: 0 }
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: |
            8.0.x
            10.0.x
      - name: Resolve and validate version
        shell: bash
        run: |
          stable_regex='^[0-9]+\.[0-9]+\.[0-9]+$'
          prerelease_regex='^[0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*$'
          if [[ "${GITHUB_REF_TYPE}" == "tag" ]]; then
            version="${GITHUB_REF_NAME#v}"
            [[ "$GITHUB_REF_NAME" == "v$version" && "$version" =~ $stable_regex ]]
          else
            [[ "$GITHUB_REF" == "refs/heads/main" ]]
            version="${{ inputs.package_version }}"
            [[ "$version" =~ $prerelease_regex ]]
          fi
          echo "PACKAGE_VERSION=$version" >> "$GITHUB_ENV"
      - run: scripts/assert-package-version-available.sh "$PACKAGE_VERSION"
      - run: scripts/build-release-artifacts.sh "$PACKAGE_VERSION"
      - run: dotnet nuget push 'artifacts/packages/*.nupkg' --source rvt --api-key "$GITHUB_TOKEN"
      - uses: actions/upload-artifact@v6
        with:
          name: release-${{ env.PACKAGE_VERSION }}
          path: artifacts/release/assets/*
          if-no-files-found: error
      - name: Create stable GitHub release
        if: github.ref_type == 'tag'
        run: >-
          gh release create "$GITHUB_REF_NAME"
          artifacts/release/assets/*
          --verify-tag --generate-notes
```

Do not use `--skip-duplicate`; immutability preflight and NuGet push must fail if a version exists.

- [ ] **Step 5: Document release and rollback operations**

`docs/releasing.md` must specify protected tag creation, RC dispatch from the default `main` branch only, package access grants, exact consumer pins, migration archive ownership, application only by the designated RVT deployment operator, prohibition on container-startup shared migrations, emergency patch-from-tag flow, forward merge, credential revocation, and rollback by pinning an already-published prior version and rebuilding the consumer image.

- [ ] **Step 6: Verify release artifact creation without publication**

```bash
scripts/build-release-artifacts.sh 0.2.0-rc.1
dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj -c Release --no-build --nologo
test -f artifacts/release/rvt-common-migrations-0.2.0-rc.1.tar.gz
test -f artifacts/release/assets/SHA256SUMS
test -f artifacts/release/sbom/spdx_2.2/manifest.spdx.json
(cd artifacts/release/assets && shasum -a 256 -c SHA256SUMS)
git diff --check
```

Expected: all checks pass and no package is published.

- [ ] **Step 7: Commit release automation**

```bash
git add .config .github/workflows/release.yml scripts docs/releasing.md
git commit -m "ci: add immutable common package releases"
```

### Task 7: Protect the repository and publish `0.2.0-rc.1`

**Repository:** `rvt-reporting` and GitHub organization settings

**Files:**
- Create: `.github/CODEOWNERS`
- Create: `.github/pull_request_template.md`
- Modify: `docs/releasing.md` only if the actual organization settings require a documented deviation

**Interfaces:**
- Consumes: green `common-ci` and release workflow from Tasks 5-6.
- Produces: protected authoritative source plus immutable RC packages readable by `RVT-Group-LTD/rvt-monitors` and the identified portal repository.

- [ ] **Step 1: Add ownership and review prompts**

Create `.github/CODEOWNERS`:

```text
# Bootstrap owner until the organization exposes maintainer teams to this repository.
* @chris-oldgeorge
/src/Rvt.Monitor.Common/Data/ @chris-oldgeorge
/src/Rvt.Monitor.Common/Hosting/ @chris-oldgeorge
/src/Rvt.Monitor.Common.Infrastructure/ @chris-oldgeorge
/database/ @chris-oldgeorge
```

The repository currently exposes `@chris-oldgeorge` as its only collaborator and no
accessible maintainer teams. Record this bootstrap deviation in `docs/releasing.md`.
Before adding non-administrator contributors, create or grant access to the
`rvt-common-maintainers`, `rvt-monitor-maintainers`, and `rvt-portal-maintainers` teams;
replace the individual entries with the intended team ownership; and remove the
administrator review bypass.

The PR template must require API/configuration/schema impact, consumer impact, test evidence, package artifact inspection, release classification, and rollback notes.

- [ ] **Step 2: Commit governance files and make CI required**

```bash
git add .github/CODEOWNERS .github/pull_request_template.md
git commit -m "docs: define common repository ownership"
git push
```

In repository settings, protect `main` with pull requests, one approval, CODEOWNERS review, required `build-test-pack`, conversation resolution, no force pushes, and no deletion. Protect `v*` tags so only release maintainers create them. Expected: direct and force pushes to `main` are rejected.

- [ ] **Step 3: Dispatch the RC release**

```bash
gh workflow run release.yml --repo RVT-Group-LTD/rvt-reporting -f package_version=0.2.0-rc.1
run_id="$(gh run list --repo RVT-Group-LTD/rvt-reporting --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId')"
gh run watch "$run_id" --repo RVT-Group-LTD/rvt-reporting --exit-status
```

Expected: the workflow succeeds once and publishes all three package and symbol versions plus downloadable workflow artifacts. A second dispatch with the same version must fail at the availability preflight.

- [ ] **Step 4: Grant consumer package access**

After the packages exist, configure each package to inherit or explicitly grant read access to `RVT-Group-LTD/rvt-monitors`. Grant write/admin only to `RVT-Group-LTD/rvt-reporting` release workflows and maintainers. Task 12 grants portal access only after its exact repository identity is recorded. Expected: RVT Monitors can restore while unrelated repositories cannot.

- [ ] **Step 5: Verify authenticated package restore from a clean directory**

With `NuGetPackageSourceCredentials_rvt` set only in the process environment:

```bash
test ! -e /private/tmp/rvt-common-rc-consumer
dotnet new console -n RvtCommonRcConsumer -o /private/tmp/rvt-common-rc-consumer --framework net10.0
dotnet add /private/tmp/rvt-common-rc-consumer/RvtCommonRcConsumer.csproj package Rvt.Monitor.Common --version 0.2.0-rc.1 --no-restore
dotnet add /private/tmp/rvt-common-rc-consumer/RvtCommonRcConsumer.csproj package Rvt.Monitor.Common.Infrastructure --version 0.2.0-rc.1 --no-restore
dotnet restore /private/tmp/rvt-common-rc-consumer/RvtCommonRcConsumer.csproj --force \
  --configfile /Users/oldgeorge/Documents/rvt-monitors/rvt-reporting/NuGet.config
```

Expected: restore succeeds without a credential file in either repository.

### Task 8: Configure RVT Monitors for exact private packages and add a failing boundary guard

**Repository:** `rvt-monitors`

**Files:**
- Create: `NuGet.config`
- Create: `Directory.Packages.props`
- Create: `rvt-monitor-common/Directory.Packages.props` as a temporary rollback-window opt-out
- Create: `myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Create: `myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Modify: every non-common `.csproj` to remove inline public package versions
- Create: generated `packages.lock.json` beside every non-common project

**Interfaces:**
- Consumes: published `0.2.0-rc.1` packages and runtime credential variable.
- Produces: exact central package versions, a repository-wide test that initially fails on consumer source references, and surviving consumer-owned messaging guards formerly located in the extracted common test project.

- [ ] **Step 1: Add credential-free source mapping**

Create `NuGet.config` at the monitor root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="rvt" value="https://nuget.pkg.github.com/RVT-Group-LTD/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="rvt"><package pattern="Rvt.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
```

Confirm it has no `<packageSourceCredentials>` element.

- [ ] **Step 2: Add central versions without upgrading existing dependencies**

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <RvtCommonVersion>0.2.0-rc.1</RvtCommonVersion>
    <RvtCommonInfrastructureVersion>0.2.0-rc.1</RvtCommonInfrastructureVersion>
    <RvtIntegrationTestingVersion>0.2.0-rc.1</RvtIntegrationTestingVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="BouncyCastle.Cryptography" Version="2.6.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" Condition="'$(MSBuildProjectName)' == 'ReportingMonitorTests'" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" Condition="'$(MSBuildProjectName)' != 'ReportingMonitorTests'" />
    <PackageVersion Include="Microsoft.AspNetCore.TestHost" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.4" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.4" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.9" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" Condition="'$(MSBuildProjectName)' == 'ReportingMonitorTests'" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.0.1" Condition="'$(MSBuildProjectName)' != 'ReportingMonitorTests'" />
    <PackageVersion Include="Microsoft.SqlServer.TransactSql.ScriptDom" Version="180.37.3" />
    <PackageVersion Include="Moq" Version="4.20.69" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.0.2" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.0.2" />
    <PackageVersion Include="Npgsql" Version="10.0.3" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
    <PackageVersion Include="QuestPDF" Version="2026.6.0" />
    <PackageVersion Include="Riok.Mapperly" Version="4.3.1" />
    <PackageVersion Include="Rvt.Monitor.Common" Version="$(RvtCommonVersion)" />
    <PackageVersion Include="Rvt.Monitor.Common.Infrastructure" Version="$(RvtCommonInfrastructureVersion)" />
    <PackageVersion Include="Rvt.Monitor.IntegrationTesting" Version="$(RvtIntegrationTestingVersion)" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
```

Remove inline `Version` attributes from all non-common `PackageReference` elements. Preserve analyzer, runner, and collector metadata exactly.

Keep the extracted source tree buildable only for the rollback window by creating `rvt-monitor-common/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

This temporary opt-out is deleted with the retired source in Task 14; it must never become a consumer source/package switch.

- [ ] **Step 3: Add a repository-wide boundary test before converting references**

Create `CommonPackageBoundaryTests.cs` with tests that enumerate consumer project files outside `rvt-monitor-common`, parse them through `XDocument`, and assert:

```csharp
[TestMethod]
public void ConsumerProjects_DoNotReferenceLocalCommonProjects()
{
    var violations = ConsumerProjects()
        .SelectMany(path => XDocument.Load(path).Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Where(element => ((string?)element.Attribute("Include") ?? string.Empty)
                .Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
            .Select(_ => Relative(path)))
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations);
}

[TestMethod]
public void IntegrationTestingPackage_IsPrivateToTestProjects()
{
    var violations = ConsumerProjects()
        .SelectMany(ValidateIntegrationTestingReference)
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), violations);
}

[TestMethod]
public void CommonRuntimeVersions_AreSynchronized()
{
    var props = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Packages.props"));
    Assert.AreEqual(ReadProperty(props, "RvtCommonVersion"), ReadProperty(props, "RvtCommonInfrastructureVersion"));
}
```

`ValidateIntegrationTestingReference` must require a project name ending in `Tests`, `PrivateAssets="all"`, and no `ProjectReference`. `ConsumerProjects` must exclude `.worktrees`, `bin`, `obj`, and `rvt-monitor-common`; `RepositoryRoot` must walk parents until it finds `.git`; `Relative` must normalize separators to `/`.

- [ ] **Step 4: Relocate the consumer-owned messaging guards**

Create `ConsumerMessagingBoundaryTests.cs` in the MyATM architecture-test folder. It must walk to the `.git` root, enumerate production `.cs` and `.csproj` files only below `myatmmonitor/MyAtmMonitor` and `omnidotsmonitor/OmnidotsMonitor`, exclude `bin`, `obj`, and test paths, and preserve this exact allow-list:

```csharp
private static readonly string[] SynchronousCompatibilityCallers =
[
    "myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs",
    "omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRuleProcessor.cs"
];

[TestMethod]
public void ObsoleteSynchronousMessageCallsAreLimitedToConsumerCompatibilityAllowlist()
{
    var root = RepositoryRoot();
    var callers = new[] { "myatmmonitor/MyAtmMonitor", "omnidotsmonitor/OmnidotsMonitor" }
        .SelectMany(relativeDirectory => ReadProductionSource(root, relativeDirectory))
        .Where(file => file.Text.Contains(".Sendmessage(", StringComparison.Ordinal) ||
            file.Text.Contains(".SendMessage(", StringComparison.Ordinal))
        .Select(file => file.RelativePath)
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(
        SynchronousCompatibilityCallers.Order(StringComparer.Ordinal).ToArray(),
        callers);
}
```

`ReadProductionSource` returns normalized root-relative paths and file text; `RepositoryRoot` uses the same worktree-safe `.git` file-or-directory check as `CommonPackageBoundaryTests`.

Extend `ReportingDependencyBoundaryTests.MessagingAssembly_IsProviderNeutral` with a source scan of `reportingmonitor/Rvt.Reporting.Messaging`:

```csharp
var messagingSource = Directory
    .EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "reportingmonitor", "Rvt.Reporting.Messaging"),
        "*.cs",
        SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Select(File.ReadAllText)
    .ToArray();

Assert.DoesNotContain(messagingSource, text =>
    text.Contains("using " + "SendGrid", StringComparison.Ordinal) ||
    text.Contains("Rvt.Reporting.Messaging.SendGrid", StringComparison.Ordinal) ||
    text.Contains("PackageReference Include=\"SendGrid\"", StringComparison.Ordinal));
```

Run the relocated guards before the old common test project is removed:

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --nologo \
  --filter FullyQualifiedName~ConsumerMessagingBoundaryTests
dotnet test reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo \
  --filter FullyQualifiedName~MessagingAssembly_IsProviderNeutral
```

Expected: both pass while the extracted `rvt-reporting` copy independently guards its package-local synchronous caller and provider boundary.

- [ ] **Step 5: Run the package boundary guard and observe the expected red state**

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --nologo \
  --filter FullyQualifiedName~CommonPackageBoundaryTests
```

Expected: FAIL listing the current consumer project references into `rvt-monitor-common`.

- [ ] **Step 6: Generate lock files and verify public versions remain unchanged**

With the runtime-only GitHub Packages credential exported:

```bash
dotnet restore rvt-monitors.sln --use-lock-file --force-evaluate
dotnet list rvt-monitors.sln package --format json > /private/tmp/rvt-monitor-package-inventory.json
git diff --check
```

Expected: lock files are created, every existing direct public dependency retains its pre-cutover version, and all three RVT versions are `0.2.0-rc.1`.

- [ ] **Step 7: Commit feed configuration and the red guard separately**

```bash
git add NuGet.config Directory.Packages.props rvt-monitor-common/Directory.Packages.props \
  '**/*.csproj' '**/packages.lock.json' \
  myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  myatmmonitor/MyAtmMonitorTests/Architecture/ConsumerMessagingBoundaryTests.cs \
  reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs
git commit -m "build: configure exact rvt package versions"
```

The boundary test is intentionally red until Task 9; do not merge this commit alone into `main`.

### Task 9: Convert all monitor consumers while retaining source for rollback

**Repository:** `rvt-monitors`

**Files:**
- Modify: `airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Modify: `airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj`
- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Modify: `myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj`
- Modify: `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj`
- Modify: `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Modify: `reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj`
- Modify: `reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`
- Modify: `reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj`
- Modify: `Directory.Build.targets`
- Modify: `.gitignore`
- Modify: `README.md`

**Interfaces:**
- Consumes: exact central package versions and red boundary tests from Task 8.
- Produces: package-only consumer graph with the old common source still present only as a rollback checkpoint.

- [ ] **Step 1: Replace runtime source references**

Use these direct package references:

```xml
<!-- AirQMonitor, MyAtmMonitor, OmnidotsMonitor, SvantekMonitor, ReportingMonitor -->
<PackageReference Include="Rvt.Monitor.Common" />
<PackageReference Include="Rvt.Monitor.Common.Infrastructure" />

<!-- Rvt.Reporting.Messaging and Rvt.Reporting.Storage -->
<PackageReference Include="Rvt.Monitor.Common" />

<!-- ReportingMonitorTests retains an explicit runtime reference -->
<PackageReference Include="Rvt.Monitor.Common" />
```

Remove only the corresponding `ProjectReference` elements. Keep application-to-application project references unchanged.

- [ ] **Step 2: Replace integration-test source references**

In AirQ, MyATM, Omnidots, Svantek, and Reporting test projects add:

```xml
<PackageReference Include="Rvt.Monitor.IntegrationTesting" PrivateAssets="all" />
```

Remove each IntegrationTesting `ProjectReference`.

- [ ] **Step 3: Relocate the ignored local PostgreSQL settings path**

Change `Directory.Build.targets` to:

```xml
<Project>
  <ItemGroup Condition="'$(IsTestProject)' == 'true' And Exists('$(MSBuildThisFileDirectory).rvt/rvt-integration.appsettings.Development.json')">
    <None Include="$(MSBuildThisFileDirectory).rvt/rvt-integration.appsettings.Development.json">
      <Link>rvt-integration.appsettings.Development.json</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

Add `.rvt/` to `.gitignore`. Update README setup instructions to use `.rvt/rvt-integration.appsettings.Development.json`; never move or copy a real local connection into Git.

- [ ] **Step 4: Verify the consumer graph is green**

```bash
dotnet restore rvt-monitors.sln --locked-mode --force
dotnet build rvt-monitors.sln --no-restore --nologo
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --no-build --nologo \
  --filter FullyQualifiedName~CommonPackageBoundaryTests
rg -n '<ProjectReference[^>]+rvt-monitor-common' \
  airqmonitor myatmmonitor omnidotsmonitor svantekmonitor reportingmonitor --glob '*.csproj'
```

Expected: build and boundary tests pass; the final search prints no matches.

- [ ] **Step 5: Verify package assets and the synchronized Infrastructure dependency**

```bash
rg -n 'Rvt.Monitor.Common/(0.2.0-rc.1)|Rvt.Monitor.Common.Infrastructure/(0.2.0-rc.1)|Rvt.Monitor.IntegrationTesting/(0.2.0-rc.1)' \
  --glob 'project.assets.json' --glob 'packages.lock.json' .
```

Expected: runtime projects resolve Common and Infrastructure `0.2.0-rc.1`; test projects resolve IntegrationTesting `0.2.0-rc.1` as a private dependency.

- [ ] **Step 6: Commit the reversible consumer cutover**

```bash
git add airqmonitor myatmmonitor omnidotsmonitor svantekmonitor reportingmonitor Directory.Build.targets .gitignore README.md
git commit -m "refactor: consume rvt common packages"
```

Do not remove `rvt-monitor-common/` in this commit.

### Task 10: Secure private restore inside every production container build

**Repository:** `rvt-monitors`

**Files:**
- Modify: `airqmonitor/AirQMonitor/Dockerfile`
- Modify: `myatmmonitor/MyAtmMonitor/Dockerfile`
- Modify: `omnidotsmonitor/OmnidotsMonitor/Dockerfile`
- Modify: `svantekmonitor/SvantekMonitor/Dockerfile`
- Modify: `reportingmonitor/ReportingMonitor/Dockerfile`
- Modify: `docker-compose.yml`
- Create: `scripts/verify-private-package-builds.sh`
- Modify: `README.md`
- Modify: `docs/container-builds.md`
- Modify: `scripts/export-client-release.sh`
- Modify: `docs/release/client-release-runbook.md`
- Create: `.github/workflows/package-consumer-ci.yml`

**Interfaces:**
- Consumes: package-only projects and runtime credential variable.
- Produces: BuildKit-secret restores with no credential in Dockerfiles, image environment, layers, build arguments, repository files, or curated client payload.

- [ ] **Step 1: Add a failing container-policy verifier**

Create `scripts/verify-private-package-builds.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

dockerfiles=(
  airqmonitor/AirQMonitor/Dockerfile
  myatmmonitor/MyAtmMonitor/Dockerfile
  omnidotsmonitor/OmnidotsMonitor/Dockerfile
  svantekmonitor/SvantekMonitor/Dockerfile
  reportingmonitor/ReportingMonitor/Dockerfile
)

for file in "${dockerfiles[@]}"; do
  grep -Fq '# syntax=docker/dockerfile:1.10' "$file"
  grep -Fq -- '--mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_rvt' "$file"
  if grep -Eqi 'github_packages_token|read:packages|Password=[^$]' "$file"; then
    echo "Potential credential material in $file" >&2
    exit 1
  fi
done

grep -Fq 'nuget_credentials:' docker-compose.yml
grep -Fq 'environment: NuGetPackageSourceCredentials_rvt' docker-compose.yml
```

Run it. Expected: FAIL because Dockerfiles do not yet mount the secret.

- [ ] **Step 2: Update all Dockerfiles to mount one ephemeral credential environment value**

Add the syntax line before `FROM`, and use the corresponding exact publish command in each Dockerfile:

```dockerfile
# syntax=docker/dockerfile:1.10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_rvt \
    dotnet publish airqmonitor/AirQMonitor/AirQMonitor.csproj -c Release -o /app/publish /p:UseAppHost=false
```

Use `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`, `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`, `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`, and `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj` in the other four Dockerfiles. Keep each final runtime stage unchanged. Do not use `ARG`, `ENV`, `dotnet nuget add source`, or a copied credential-bearing config.

Dockerfile frontend `1.10` is required because Docker introduced the secret mount's `env` option in Dockerfile v1.10.0.

- [ ] **Step 3: Add the Compose build secret to all five services**

Add to each service's `build` section:

```yaml
      secrets:
        - nuget_credentials
```

Add one top-level secret:

```yaml
secrets:
  nuget_credentials:
    environment: NuGetPackageSourceCredentials_rvt
```

- [ ] **Step 4: Document developer and CI builds**

Document:

```bash
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
docker compose build
```

Explain that the variable is read only by the BuildKit secret mount and is not a runtime application secret. CI must use `github.actor` and an authorized token, not a committed `.env` file.

- [ ] **Step 5: Preserve private-feed build files in curated releases**

Extend the required-file loop in `scripts/export-client-release.sh`:

```bash
for required in README.md rvt-monitors.sln docker-compose.yml NuGet.config Directory.Packages.props; do
```

Update the release runbook verification to assert both new files exist and record that `RVT-Group-LTD/rvt-monitors` requires read access to all three packages.

- [ ] **Step 6: Add consumer CI with runtime-only package credentials**

Create `.github/workflows/package-consumer-ci.yml`:

```yaml
name: package-consumer-ci

on:
  pull_request:
  push:
    branches: [main, release-candidate]

permissions:
  contents: read
  packages: read

env:
  NuGetPackageSourceCredentials_rvt: Username=${{ github.actor }};Password=${{ secrets.RVT_PACKAGES_READ_TOKEN || github.token }};ValidAuthenticationTypes=Basic

jobs:
  build-test-containers:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:17
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: rvt_monitor_ci
        ports: ['5432:5432']
        options: >-
          --health-cmd "pg_isready -U postgres -d rvt_monitor_ci"
          --health-interval 5s --health-timeout 5s --health-retries 20
    env:
      RVT__POSTGRES_INTEGRATION_CONNECTION: Host=127.0.0.1;Port=5432;Database=rvt_monitor_ci;Username=postgres;Password=postgres
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: 10.0.x }
      - run: dotnet restore rvt-monitors.sln --locked-mode
      - run: dotnet format rvt-monitors.sln --verify-no-changes --no-restore
      - run: dotnet build rvt-monitors.sln --no-restore --nologo
      - run: dotnet test rvt-monitors.sln --no-build --nologo
      - run: scripts/verify-private-package-builds.sh
      - run: docker compose config --quiet
      - run: docker compose build
```

The source repository must store `RVT_PACKAGES_READ_TOKEN` only as a GitHub Actions secret with `read:packages`. In `RVT-Group-LTD/rvt-monitors`, use its authorized `GITHUB_TOKEN` instead after package repository access is granted. The workflow must pass the same environment value to `docker build --secret id=nuget_credentials,env=NuGetPackageSourceCredentials_rvt`; it must never print the value.

- [ ] **Step 7: Build and inspect all images**

With the runtime-only credential set:

```bash
scripts/verify-private-package-builds.sh
docker compose config --quiet
docker compose build
for image in rvt/airqmonitor:local rvt/myatmmonitor:local rvt/omnidotsmonitor:local rvt/svantekmonitor:local rvt/reportingmonitor:local; do
  docker history --no-trunc "$image" | rg -i 'Password=|read:packages|github_pat_' && exit 1 || true
done
```

Expected: verifier, Compose validation, and all builds pass; history scan prints no credential material.

- [ ] **Step 8: Commit secure container restore**

```bash
git add airqmonitor/AirQMonitor/Dockerfile myatmmonitor/MyAtmMonitor/Dockerfile \
  omnidotsmonitor/OmnidotsMonitor/Dockerfile svantekmonitor/SvantekMonitor/Dockerfile \
  reportingmonitor/ReportingMonitor/Dockerfile docker-compose.yml scripts \
  README.md docs/container-builds.md docs/release/client-release-runbook.md \
  .github/workflows/package-consumer-ci.yml
git commit -m "build: restore private rvt packages securely"
```

### Task 11: Verify and stage the monitor RC, including rollback and version inventory

**Repository:** `rvt-monitors`

**Files:**
- Create: `scripts/report-rvt-package-inventory.sh`
- Create: `docs/superpowers/evidence/2026-07-16-rvt-common-monitor-rc-validation.md`

**Interfaces:**
- Consumes: package-only monitor branch and five secure images.
- Produces: full test/container/staging evidence, deployed-version inventory, and a tested source-reference rollback checkpoint.

- [ ] **Step 1: Add a package inventory script**

Create `scripts/report-rvt-package-inventory.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

images=(
  rvt/airqmonitor:local
  rvt/myatmmonitor:local
  rvt/omnidotsmonitor:local
  rvt/svantekmonitor:local
  rvt/reportingmonitor:local
)

for image in "${images[@]}"; do
  container="$(docker create "$image")"
  output="/private/tmp/${image//[\/:]/_}.deps.json"
  app="$(docker inspect "$image" --format '{{json .Config.Entrypoint}}' | sed -E 's/.*"([^"]+)\.dll".*/\1/')"
  docker cp "$container:/app/$app.deps.json" "$output"
  docker rm "$container" >/dev/null
  common="$(grep -o 'Rvt.Monitor.Common/[0-9][^"]*' "$output" | head -1 | cut -d/ -f2)"
  infrastructure="$(grep -o 'Rvt.Monitor.Common.Infrastructure/[0-9][^"]*' "$output" | head -1 | cut -d/ -f2)"
  printf '%s\t%s\t%s\n' "$image" "$common" "$infrastructure"
done
```

Run `chmod +x scripts/report-rvt-package-inventory.sh`.

- [ ] **Step 2: Run all monitor tests against an ephemeral PostgreSQL 17 database**

```bash
docker run --name rvt-common-migration-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=rvt_monitor_package_ci \
  -p 127.0.0.1:55432:5432 -d postgres:17
export RVT__POSTGRES_INTEGRATION_CONNECTION='Host=127.0.0.1;Port=55432;Database=rvt_monitor_package_ci;Username=postgres;Password=postgres'
dotnet restore rvt-monitors.sln --locked-mode
dotnet format rvt-monitors.sln --verify-no-changes --no-restore
dotnet build rvt-monitors.sln --no-restore --nologo
dotnet test rvt-monitors.sln --no-build --nologo
```

Expected: formatter, build, and all suites pass with zero failed/skipped tests. Stop and remove the disposable container after evidence is captured; do not persist the connection.

- [ ] **Step 3: Verify production images contain synchronized runtime packages**

```bash
docker compose config --quiet
docker compose build
scripts/report-rvt-package-inventory.sh
```

Expected: each row reports Common `0.2.0-rc.1` and Infrastructure `0.2.0-rc.1`. The test-only package must not appear in runtime `.deps.json` files.

- [ ] **Step 4: Exercise staging-equivalent flows**

Using untracked staging configuration, exercise each host's liveness/readiness, one-shot dispatcher, Quartz startup, API startup, storage configuration, provider-disabled communications startup, and the existing `scripts/run-testlocal-suite.sh`. No live email, SMS, or destructive production database operation is allowed. Expected: package-only behavior matches the Task 1 baseline.

- [ ] **Step 5: Test the source-reference rollback before source deletion**

Resolve the conversion commit and create an isolated rollback worktree at its parent:

```bash
conversion_commit="$(git log -1 --format=%H --grep='refactor: consume rvt common packages')"
test -n "$conversion_commit"
git worktree add /private/tmp/rvt-monitors-source-rollback "$conversion_commit^"
git -C /private/tmp/rvt-monitors-source-rollback status --short --branch
dotnet restore /private/tmp/rvt-monitors-source-rollback/rvt-monitors.sln --force --no-cache \
  --configfile /private/tmp/rvt-monitors-source-rollback/NuGet.config
dotnet build /private/tmp/rvt-monitors-source-rollback/rvt-monitors.sln --no-restore --nologo
```

Expected: the pre-cutover source graph still builds, proving a consumer rollback path until Task 14 deletes local source. Record the commit and result; remove the temporary worktree after review.

- [ ] **Step 6: Write validation evidence**

Record commit SHAs, exact package versions, test counts, image identifiers, image package inventory, staging checks, rollback commit, and absence of stored credentials. Never include tokens, connection strings, destinations, or provider responses.

- [ ] **Step 7: Commit monitor RC evidence**

```bash
git add scripts/report-rvt-package-inventory.sh docs/superpowers/evidence/2026-07-16-rvt-common-monitor-rc-validation.md
git commit -m "test: record common package monitor validation"
```

### Task 12: Inventory the portal and create its independent migration plan

**Repository:** `rvt-reporting` plus the separately authorized portal checkout

**Files:**
- Create in common repo: `docs/consumers/portal-handoff.md`
- Create in portal repo: `docs/superpowers/specs/2026-07-16-rvt-common-package-consumer-design.md`
- Create in portal repo: `docs/superpowers/plans/2026-07-16-rvt-common-package-consumer.md`
- Create after portal staging: `docs/superpowers/evidence/2026-07-16-rvt-common-portal-rc-validation.md`

**Interfaces:**
- Consumes: exact portal repository identity, portal baseline commit, public-type usage inventory, and published RC packages.
- Produces: an independently approved portal design/plan and accepted package-only staging evidence. Stable Task 13 may not start without that evidence.

- [ ] **Step 1: Record the exact portal repository before making changes**

The handoff must record the GitHub repository URL, default branch, baseline full SHA, target framework, solution files, build/test/container commands, common source/binary references, consumed namespaces/types, EF entity/mapping usage, infrastructure/provider usage, and database migration ownership. Do not infer the repository from the historical `rvtportal-spa-alpha` solution item.

After recording and approving that identity, grant that repository read access to each required compatibility package and verify unrelated repositories remain unauthorized.

- [ ] **Step 2: Baseline the portal**

Run its documented restore, build, unit, API, authorization, persistence, background-work, UI integration, and container commands. Record exact non-secret counts and image digest. Expected: the source/binary-reference baseline is green before package changes.

- [ ] **Step 3: Write and approve the portal-specific design**

The design must preserve behavior and schema, choose only the required compatibility packages, use exact synchronized `0.2.0-rc.1` versions, add the credential-free source map, use secure container restore, compare EF model metadata when entities are consumed, and define rollback to the portal baseline container/commit.

- [ ] **Step 4: Write and execute the portal-specific implementation plan**

The plan must name every actual portal file and test. It must not reuse monitor paths or guess at portal architecture. Execute it in the portal repository with its own review checkpoints.

- [ ] **Step 5: Produce portal staging evidence**

The evidence must show package-only restore, no common source/binary copies, exact Common/Infrastructure versions where used, green tests, container build, database/notification/storage/background flow checks, EF metadata comparison where applicable, rollback result, and no persisted credentials.

- [ ] **Step 6: Commit the cross-repository handoff evidence**

Commit the portal design, plan, and evidence in the portal repository. Update and commit `docs/consumers/portal-handoff.md` in `rvt-reporting` with links to those immutable portal commits.

### Task 13: Promote the accepted source state to `0.2.0` and update consumers

**Repositories:** `rvt-reporting`, `rvt-monitors`, and the portal repository

**Files:**
- Modify in common repo: `Directory.Build.props` only after stable publication to make local default version `0.2.0`
- Modify in monitor repo: `Directory.Packages.props`
- Modify in portal repo: its central package version file named by Task 12
- Create in common repo: `docs/releases/0.2.0.md`

**Interfaces:**
- Consumes: accepted monitor and portal `0.2.0-rc.1` staging evidence from Tasks 11-12.
- Produces: protected `v0.2.0`, immutable stable packages from the accepted source state, independent consumer update commits, and stable deployment inventory.

- [ ] **Step 1: Prove the stable source matches the accepted RC source**

Compare the release workflow source SHA for `0.2.0-rc.1` with the proposed stable tag commit. Only documentation or release metadata changes may differ; any source, project, dependency, migration, API, mapping, configuration, or behavior change requires a new immutable RC and renewed consumer staging.

- [ ] **Step 2: Create and push the protected stable tag**

```bash
git tag -a v0.2.0 -m "RVT Common 0.2.0"
git push origin v0.2.0
run_id="$(gh run list --repo RVT-Group-LTD/rvt-reporting --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId')"
gh run watch "$run_id" --repo RVT-Group-LTD/rvt-reporting --exit-status
```

Expected: release workflow publishes all three `0.2.0` packages, symbols, checksums, migrations, and SBOM, then creates GitHub release `v0.2.0`. Re-running must fail immutability checks.

- [ ] **Step 3: Update RVT Monitors independently**

Set all three version properties in `Directory.Packages.props` to `0.2.0`, regenerate lock files, run the complete Task 11 verification, build fresh images, record digests/version inventory, and commit:

```bash
git add Directory.Packages.props '**/packages.lock.json' docs/superpowers/evidence
git commit -m "build: adopt rvt common 0.2.0"
```

- [ ] **Step 4: Update the portal independently**

Follow the portal plan from Task 12, pin exact `0.2.0` package versions, rerun its complete verification, build a fresh image, and record its digest/version inventory in the portal repository. Publishing Common must not deploy either consumer automatically.

- [ ] **Step 5: Test first-release rollback**

In isolated consumer worktrees, repin the accepted `0.2.0-rc.1` versions, restore, build, test, and rebuild images. Expected: both consumers can return to the accepted RC artifact without modifying or deleting stable packages. Record the limitation that future releases roll back to the previous stable version; `0.2.0` is the first stable baseline.

- [ ] **Step 6: Publish the stable release record**

`docs/releases/0.2.0.md` must record source/tag SHA, package checksums, migration requirement, schema compatibility, monitor and portal adoption commits, image digests, staging evidence links, and rollback evidence.

- [ ] **Step 7: Establish `0.2.0` as the future API/package baseline**

After stable packages are readable, add to the common repository `Directory.Build.props`:

```xml
<EnablePackageValidation Condition="'$(IsPackable)' == 'true'">true</EnablePackageValidation>
<PackageValidationBaselineVersion Condition="'$(IsPackable)' == 'true' And '$(PackageVersion)' != '0.2.0'">0.2.0</PackageValidationBaselineVersion>
```

Set the local default PackageVersion to `0.2.0`, regenerate lock files, and run `dotnet pack` once with `0.2.1-rc.1` to prove API compatibility is evaluated against `0.2.0`. Do not publish the proof package. Commit:

```bash
git add Directory.Build.props '**/packages.lock.json' docs/releases/0.2.0.md
git commit -m "build: baseline common package compatibility"
```

### Task 14: Remove monitor-owned common source and enforce the final boundary

**Repository:** `rvt-monitors`

**Files:**
- Delete: `rvt-monitor-common/`
- Modify: `rvt-monitors.sln`
- Modify: `README.md`
- Modify: `observability/README.md`
- Modify: `docs/container-builds.md`
- Modify: `docs/release/client-release-runbook.md`
- Modify: `myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify: historical current-facing schema/document links that still instruct users to read runtime source under `rvt-monitor-common/`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: stable package adoption and rollback evidence from Task 13.
- Produces: one authoritative common source repository, package-only monitor consumers, architecture enforcement, updated operations docs, and final project state.

- [ ] **Step 1: Strengthen the boundary test before deleting source**

Add:

```csharp
[TestMethod]
public void LocalCommonSourceTree_DoesNotExist() =>
    Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot(), "rvt-monitor-common")));

[TestMethod]
public void ConsumerProjects_DoNotContainConditionalCommonSourceSwitches()
{
    var violations = ConsumerProjects()
        .Where(path => File.ReadAllText(path).Contains("UseLocalRvtCommon", StringComparison.OrdinalIgnoreCase) ||
                       File.ReadAllText(path).Contains("rvt-monitor-common", StringComparison.OrdinalIgnoreCase))
        .Select(Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    CollectionAssert.AreEqual(Array.Empty<string>(), violations);
}
```

Run the focused tests. Expected: `LocalCommonSourceTree_DoesNotExist` fails before deletion.

- [ ] **Step 2: Remove common projects from the solution and delete the retired source**

```bash
dotnet sln rvt-monitors.sln remove \
  rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj \
  rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj \
  rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj \
  rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj \
  rvt-monitor-common/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj \
  rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj
git rm -r rvt-monitor-common
```

This destructive source removal is authorized only after stable adoption and rollback evidence are reviewed. Preserve migration artifacts and source history in `RVT-Group-LTD/rvt-reporting`; do not copy them back into the monitor repository.

- [ ] **Step 3: Update active documentation**

Replace active instructions that describe local common source with exact packages, GitHub repository ownership, authentication, release assets, migration archive, version inventory, and rollback workflow. Historical plans/specs remain historical and are not rewritten. Update observability guidance so package versions are taken from the published package and container `.deps.json`, not a local project.

- [ ] **Step 4: Run the final architecture and reference scans**

```bash
dotnet test myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --nologo \
  --filter FullyQualifiedName~CommonPackageBoundaryTests
test ! -d rvt-monitor-common
if rg -n '<ProjectReference[^>]+rvt-monitor-common|UseLocalRvtCommon' --glob '*.csproj' --glob '*.props' --glob '*.targets' .; then exit 1; fi
if dotnet sln rvt-monitors.sln list | rg -q 'Rvt.Monitor.Common|Rvt.Monitor.IntegrationTesting'; then exit 1; fi
```

Expected: tests pass; directory test succeeds; both searches print no matches.

- [ ] **Step 5: Run the complete release gate**

```bash
dotnet restore rvt-monitors.sln --locked-mode
dotnet format rvt-monitors.sln --verify-no-changes --no-restore
dotnet build rvt-monitors.sln --no-restore --nologo
dotnet test rvt-monitors.sln --no-build --nologo
scripts/verify-private-package-builds.sh
docker compose config --quiet
docker compose build
scripts/report-rvt-package-inventory.sh
scripts/export-client-release.sh /private/tmp/rvt-monitors-client-release
git diff --check
```

Expected: all tests/builds pass, every image reports synchronized stable `0.2.0`, curated release contains `NuGet.config` and `Directory.Packages.props` but no common source, and no secret value is persisted.

- [ ] **Step 6: Update project state and commit source removal**

Record authoritative repository, stable versions, package access, monitor/portal adoption commits, package and image verification, migration ownership, rollback result, and remaining future package-splitting work in `project_state.md`.

```bash
git add -A
git commit -m "refactor: remove monitor-owned common source"
git push
```

Expected: `RVT-Group-LTD/rvt-reporting` is the only production source, monitors restore exact immutable packages, and the accepted state is synchronized to GitHub.

## Final Acceptance Gate

- [ ] Protected `RVT-Group-LTD/rvt-reporting` is authoritative and preserves source history.
- [ ] Exactly three synchronized packages exist at `0.2.0` with symbols, checksums, migration assets, and SBOM.
- [ ] Common PRs validate locked restore, formatting, build, unit/provider tests, package contents, package-only consumers, and API/package compatibility.
- [ ] RVT Monitors and the portal independently pin exact stable versions and have no common source copy, binary copy, submodule, or conditional project-reference switch.
- [ ] All private restores use credential-free tracked configuration plus runtime-only credentials; Docker builds use BuildKit secrets.
- [ ] Runtime image inventories identify Common and Infrastructure versions and reject mismatches.
- [ ] Initial cutover changes no public API, configuration, entity mapping, schema, or runtime behavior.
- [ ] One authority owns shared migrations, and release notes state schema/deployment/rollback requirements.
- [ ] Rollback is verified from rebuilt consumer images without overwriting or deleting package versions.
- [ ] `project_state.md` records the final source, package, consumer, deployment, and rollback state without secrets.
