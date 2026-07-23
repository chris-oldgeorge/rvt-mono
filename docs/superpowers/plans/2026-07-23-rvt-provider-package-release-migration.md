# RVT Provider Package Release Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace every three-package release, validation, solution, lock, CI, and SBOM assumption with the approved eleven-package provider train while proving that each adapter can be restored and loaded independently.

**Architecture:** A checked-in tab-separated release catalog is the single ordered source for the eleven package identifiers and project paths. Production and package-only builds consume that catalog, while independent package consumers prove provider isolation and the release tests enforce exact package, SBOM, checksum, and asset counts. Active applications continue to use source references; only the projects under `libs/rvt-monitor-common/package-validation` consume RVT packages.

**Tech Stack:** .NET 10, MSBuild/NuGet locked restore, MSTest 4, Bash strict mode, GitHub Actions, Microsoft SBOM Tool 4.1.5, SPDX 2.2, Python 3 release assertions.

## Global Constraints

- Publish exactly these eleven packages at one exact version: `Rvt.Monitor.Common`, `Rvt.Monitor.IntegrationTesting`, `Rvt.Communication.Abstractions`, `Rvt.Communication`, `Rvt.Communication.SendGridMail`, `Rvt.Communication.MicrosoftGraphMail`, `Rvt.Communication.TransmitSms`, `Rvt.Storage.Abstractions`, `Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3`.
- Use `1.0.0-rc.1` as the checked-in clean-split development and package-validation version.
- Do not publish, reference, restore, or describe `Rvt.Monitor.Common.Infrastructure` as part of the current package graph.
- Every internal package-to-package dependency must use the exact range `[$(PackageVersion)]`.
- Active consumers under `apps/monitors`, `apps/portal`, and `services/reporting` use source `ProjectReference` edges and must not use RVT `PackageReference` edges.
- Package-validation consumers use RVT `PackageReference` edges only and must not reference any RVT source project.
- Provider-neutral projects must not transitively introduce SendGrid, Azure Identity, Azure Storage, or AWS SDKs.
- Keep package feeds credential-free in checked-in files. Release and restore credentials are supplied only at runtime.
- Preserve locked restore, Source Link, symbols, deterministic builds, immutable release preflight, the migration archive, official SBOM validation, and flat release assets.
- The Portal blob-client/service unification and every item under **Future Pending Work** remain outside this implementation.

---

## File Structure

### Release metadata

- Create `libs/rvt-monitor-common/release/package-catalog.tsv`: ordered package identifier/project path source for all eleven packages.
- Modify `libs/rvt-monitor-common/Directory.Build.props`: set the clean-split default version.
- Modify `libs/rvt-monitor-common/Directory.Build.targets`: pin every internal RVT project dependency to the exact release version.
- Modify `libs/rvt-monitor-common/Directory.Packages.props`: retain provider SDK versions centrally while removing any obsolete infrastructure-only entry.

### Package-only consumers

- Modify `libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj`: consume the four provider-neutral runtime packages.
- Modify `libs/rvt-monitor-common/package-validation/RuntimeConsumer/Program.cs`: load all four provider-neutral assemblies and reject vendor assemblies.
- Modify `libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json`: record exact `1.0.0-rc.1` package resolution.
- Keep `libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj` integration-testing-only and regenerate its lock.
- Create one `*.csproj`, `Program.cs`, and `packages.lock.json` below each of:
  - `libs/rvt-monitor-common/package-validation/SendGridMailConsumer`
  - `libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer`
  - `libs/rvt-monitor-common/package-validation/TransmitSmsConsumer`
  - `libs/rvt-monitor-common/package-validation/LocalStorageConsumer`
  - `libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer`
  - `libs/rvt-monitor-common/package-validation/S3StorageConsumer`

### Package validation and boundaries

- Modify `libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs`: validate eleven packages, symbols, assemblies, exact internal dependencies, and consumer isolation.
- Modify `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`: recognize all eleven package identifiers and all eight package-only consumers.
- Modify `apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs`: enforce Portal's SendGrid-only adapter boundary and reject the removed infrastructure assembly.
- Modify `scripts/verify-rvt-common-source-boundary.sh`: validate all eleven source projects and all eight package consumers across all active modules.
- Modify `tests/verify-rvt-common-source-boundary.test.sh`: prove all eleven artifacts are prerequisites before consumer restore.
- Modify `tests/verify-rvt-common-source-boundary-regression.test.sh` and its fixture: preserve a focused source-reference violation.

### Solutions, locks, and build

- Modify `libs/rvt-monitor-common/rvt-common.sln`: remove the infrastructure project/test and include all eleven production projects plus the extracted test projects.
- Modify `Rvt.Mono.slnx`: include every production, test, and package-consumer project.
- Modify `scripts/build-mono.sh`: restore, pack, verify, and evict all eleven packages; validate all eight package consumers.
- Regenerate all production, test, package-consumer, and active-consumer `packages.lock.json` files from the local package train.

### CI, release, and SBOM

- Modify `libs/rvt-monitor-common/.github/workflows/ci.yml`: test, pack, audit, and validate the eleven-package graph.
- Modify `libs/rvt-monitor-common/.github/workflows/release.yml`: publish the eleven packages and upload the exact flat asset set.
- Modify `libs/rvt-monitor-common/scripts/assert-package-version-available.sh`: check all eleven immutable package versions.
- Modify `libs/rvt-monitor-common/scripts/build-release-artifacts.sh`: stage eleven SBOM components and produce 26 final flat assets.
- Modify `libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh`: fake and verify all eleven packages, eight consumer locks, 22 package files, and 26 assets.
- Modify `apps/monitors/.github/workflows/package-consumer-ci.yml`, `apps/monitors/scripts/verify-private-package-builds.sh`, and `apps/monitors/scripts/report-rvt-package-inventory.sh`: remove the obsolete infrastructure identity and recognize the split package families.

### Documentation and persistent state

- Modify `README.md`, `libs/rvt-monitor-common/README.md`, `docs/release/rvt-monitor-common/releasing.md`, `docs/operations/monitors/container-builds.md`, `libs/rvt-monitor-common/.github/CODEOWNERS`, and `project_state.md`.
- Preserve the approved out-of-scope list in `docs/superpowers/specs/2026-07-23-rvt-provider-adapter-project-split-design.md` and repeat it under this plan's **Future Pending Work** heading.

---

### Task 1: Establish the eleven-package catalog and synchronized version policy

**Files:**
- Create: `libs/rvt-monitor-common/release/package-catalog.tsv`
- Modify: `libs/rvt-monitor-common/Directory.Build.props`
- Modify: `libs/rvt-monitor-common/Directory.Build.targets`
- Modify: `libs/rvt-monitor-common/Directory.Packages.props`
- Test: `libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs`

**Interfaces:**
- Consumes: the eleven packable production project paths created by the communication and storage extraction plans.
- Produces: an ordered `package_id<TAB>project_path` catalog and exact internal dependency pinning used by every later release task.

- [ ] **Step 1: Add a failing package-catalog contract test**

Add these members to `PackageArtifactTests`:

```csharp
private static readonly string RepositoryRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "../../../../../"));
private static readonly string PackageCatalogPath = Path.Combine(
    RepositoryRoot,
    "release/package-catalog.tsv");

private static readonly string[] ExpectedPackageIds =
[
    "Rvt.Monitor.Common",
    "Rvt.Monitor.IntegrationTesting",
    "Rvt.Communication.Abstractions",
    "Rvt.Communication",
    "Rvt.Communication.SendGridMail",
    "Rvt.Communication.MicrosoftGraphMail",
    "Rvt.Communication.TransmitSms",
    "Rvt.Storage.Abstractions",
    "Rvt.Storage.Local",
    "Rvt.Storage.AzureBlob",
    "Rvt.Storage.S3"
];

[TestMethod]
public void PackageCatalogDeclaresTheExactApprovedTrain()
{
    var rows = File.ReadAllLines(PackageCatalogPath)
        .Select(line => line.Split('\t'))
        .ToArray();

    Assert.IsTrue(rows.All(row => row.Length == 2));
    CollectionAssert.AreEqual(
        ExpectedPackageIds,
        rows.Select(row => row[0]).ToArray());

    foreach (var row in rows)
    {
        Assert.IsTrue(
            File.Exists(Path.Combine(RepositoryRoot, row[1])),
            $"Catalog project does not exist: {row[1]}");
    }
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  --filter PackageCatalogDeclaresTheExactApprovedTrain --nologo
```

Expected: FAIL because `release/package-catalog.tsv` does not exist.

- [ ] **Step 3: Create the exact package catalog**

Create `libs/rvt-monitor-common/release/package-catalog.tsv` with literal tab separators:

```text
Rvt.Monitor.Common	src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj
Rvt.Monitor.IntegrationTesting	testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj
Rvt.Communication.Abstractions	src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj
Rvt.Communication	src/Rvt.Communication/Rvt.Communication.csproj
Rvt.Communication.SendGridMail	src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj
Rvt.Communication.MicrosoftGraphMail	src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj
Rvt.Communication.TransmitSms	src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj
Rvt.Storage.Abstractions	src/Rvt.Storage.Abstractions/Rvt.Storage.Abstractions.csproj
Rvt.Storage.Local	src/Rvt.Storage.Local/Rvt.Storage.Local.csproj
Rvt.Storage.AzureBlob	src/Rvt.Storage.AzureBlob/Rvt.Storage.AzureBlob.csproj
Rvt.Storage.S3	src/Rvt.Storage.S3/Rvt.Storage.S3.csproj
```

- [ ] **Step 4: Set the clean-split version and generic internal dependency pin**

In `Directory.Build.props`, change the default:

```xml
<PackageVersion Condition="'$(PackageVersion)' == ''">1.0.0-rc.1</PackageVersion>
```

Replace `PinCommonDependencyVersionForInfrastructurePackage` in `Directory.Build.targets` with:

```xml
<Target Name="PinSynchronizedRvtProjectReferenceVersions"
        AfterTargets="_GetProjectReferenceVersions"
        BeforeTargets="GenerateNuspec"
        Condition="'$(IsPackable)' == 'true'">
  <ItemGroup>
    <_ProjectReferencesWithVersions Update="@(_ProjectReferencesWithVersions)"
        Condition="$([System.String]::Copy('%(_ProjectReferencesWithVersions.Filename)').StartsWith('Rvt.'))">
      <ProjectVersion>[$(PackageVersion)]</ProjectVersion>
    </_ProjectReferencesWithVersions>
  </ItemGroup>
</Target>
```

Keep these central SDK entries in `Directory.Packages.props`, each used only by its owning provider project:

```xml
<PackageVersion Include="AWSSDK.S3" Version="4.0.100.3" />
<PackageVersion Include="Azure.Identity" Version="1.15.0" />
<PackageVersion Include="Azure.Storage.Blobs" Version="12.25.0" />
<PackageVersion Include="SendGrid" Version="9.29.3" />
```

- [ ] **Step 5: Run the catalog test and build-target evaluation**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  --filter PackageCatalogDeclaresTheExactApprovedTrain --nologo
dotnet msbuild libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj \
  -getProperty:PackageVersion -nologo
```

Expected: the test passes and MSBuild reports `PackageVersion` as `1.0.0-rc.1`.

- [ ] **Step 6: Commit the catalog and pinning policy**

```bash
git add \
  libs/rvt-monitor-common/release/package-catalog.tsv \
  libs/rvt-monitor-common/Directory.Build.props \
  libs/rvt-monitor-common/Directory.Build.targets \
  libs/rvt-monitor-common/Directory.Packages.props \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs
git commit -m "build: define eleven-package release train"
```

---

### Task 2: Expand package artifact validation to the exact package graph

**Files:**
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/packages.lock.json`

**Interfaces:**
- Consumes: `release/package-catalog.tsv`, locally packed `.nupkg`/`.snupkg` files, and generated nuspec dependency groups.
- Produces: release tests that reject missing, extra, stale, mis-versioned, or incorrectly coupled packages.

- [ ] **Step 1: Replace the three-package assertions with catalog-driven failing assertions**

Rename `ReleaseContainsExactlyTheThreeCompatibilityPackages` to `ReleaseContainsExactlyTheElevenApprovedPackages`. Derive expected package files from `ExpectedPackageIds`:

```csharp
var expectedPackages = ExpectedPackageIds
    .Select(id => $"{id}.{Version}.nupkg")
    .Order(StringComparer.Ordinal)
    .ToArray();
var expectedSymbols = ExpectedPackageIds
    .Select(id => $"{id}.{Version}.snupkg")
    .Order(StringComparer.Ordinal)
    .ToArray();

CollectionAssert.AreEqual(expectedPackages, names);
CollectionAssert.AreEqual(expectedSymbols, symbolNames);
```

Replace the three assembly data rows with these rows:

```csharp
[DataRow("Rvt.Monitor.Common", "Rvt.Monitor.Common.dll")]
[DataRow("Rvt.Monitor.IntegrationTesting", "Rvt.Monitor.IntegrationTesting.dll")]
[DataRow("Rvt.Communication.Abstractions", "Rvt.Communication.Abstractions.dll")]
[DataRow("Rvt.Communication", "Rvt.Communication.dll")]
[DataRow("Rvt.Communication.SendGridMail", "Rvt.Communication.SendGridMail.dll")]
[DataRow("Rvt.Communication.MicrosoftGraphMail", "Rvt.Communication.MicrosoftGraphMail.dll")]
[DataRow("Rvt.Communication.TransmitSms", "Rvt.Communication.TransmitSms.dll")]
[DataRow("Rvt.Storage.Abstractions", "Rvt.Storage.Abstractions.dll")]
[DataRow("Rvt.Storage.Local", "Rvt.Storage.Local.dll")]
[DataRow("Rvt.Storage.AzureBlob", "Rvt.Storage.AzureBlob.dll")]
[DataRow("Rvt.Storage.S3", "Rvt.Storage.S3.dll")]
```

Add the exact internal dependency matrix:

```csharp
private static readonly IReadOnlyDictionary<string, string[]> ExpectedInternalDependencies =
    new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Rvt.Monitor.Common"] = ["Rvt.Communication.Abstractions"],
        ["Rvt.Monitor.IntegrationTesting"] = [],
        ["Rvt.Communication.Abstractions"] = [],
        ["Rvt.Communication"] = ["Rvt.Communication.Abstractions"],
        ["Rvt.Communication.SendGridMail"] = ["Rvt.Communication.Abstractions"],
        ["Rvt.Communication.MicrosoftGraphMail"] = ["Rvt.Communication.Abstractions"],
        ["Rvt.Communication.TransmitSms"] = ["Rvt.Communication.Abstractions"],
        ["Rvt.Storage.Abstractions"] = [],
        ["Rvt.Storage.Local"] = ["Rvt.Storage.Abstractions"],
        ["Rvt.Storage.AzureBlob"] = ["Rvt.Storage.Abstractions"],
        ["Rvt.Storage.S3"] = ["Rvt.Storage.Abstractions"]
    };
```

Add:

```csharp
[TestMethod]
public void InternalPackageDependenciesUseTheExactSynchronizedVersion()
{
    foreach (var expectation in ExpectedInternalDependencies)
    {
        using var archive = Open(expectation.Key);
        var nuspec = archive.Entries.Single(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var actual = document.Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => new
            {
                Id = (string?)element.Attribute("id"),
                Version = (string?)element.Attribute("version")
            })
            .Where(item => item.Id is not null &&
                item.Id.StartsWith("Rvt.", StringComparison.Ordinal))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            expectation.Value.Order(StringComparer.Ordinal).ToArray(),
            actual.Select(item => item.Id!).ToArray(),
            expectation.Key);
        Assert.IsTrue(
            actual.All(item => item.Version == $"[{Version}]"),
            $"{expectation.Key} has a non-exact internal dependency.");
    }
}
```

- [ ] **Step 2: Run package validation and verify RED**

Run:

```bash
RVT_PACKAGE_VERSION=1.0.0-rc.1 dotnet test \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  --nologo
```

Expected: FAIL because the artifact directory still contains the old three-package train or lacks the newly extracted packages.

- [ ] **Step 3: Pack the eleven projects from the catalog**

Run from `libs/rvt-monitor-common`:

```bash
rm -rf artifacts/packages
mkdir -p artifacts/packages
while IFS=$'\t' read -r package_id project_path; do
  dotnet restore "$project_path" -p:PackageVersion=1.0.0-rc.1
  dotnet pack "$project_path" --no-restore -c Release \
    -p:PackageVersion=1.0.0-rc.1
done < release/package-catalog.tsv
```

Expected: `artifacts/packages` contains 11 `.nupkg` files and 11 `.snupkg` files.

- [ ] **Step 4: Run package validation and verify GREEN**

```bash
RVT_PACKAGE_VERSION=1.0.0-rc.1 dotnet test \
  tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  -c Release --nologo
```

Expected: all package artifact tests pass with zero failures.

- [ ] **Step 5: Commit package graph validation**

```bash
git add \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/packages.lock.json
git commit -m "test: enforce eleven-package artifact graph"
```

---

### Task 3: Add independent package-only adapter consumers

**Files:**
- Modify: `libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj`
- Modify: `libs/rvt-monitor-common/package-validation/RuntimeConsumer/Program.cs`
- Modify: `libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json`
- Modify: `libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj`
- Modify: `libs/rvt-monitor-common/package-validation/TestConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/SendGridMailConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/SendGridMailConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/TransmitSmsConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/TransmitSmsConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/LocalStorageConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/LocalStorageConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer/packages.lock.json`
- Create: `libs/rvt-monitor-common/package-validation/S3StorageConsumer/S3StorageConsumer.csproj`
- Create: `libs/rvt-monitor-common/package-validation/S3StorageConsumer/Program.cs`
- Create: `libs/rvt-monitor-common/package-validation/S3StorageConsumer/packages.lock.json`
- Test: `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`

**Interfaces:**
- Consumes: locally packed `1.0.0-rc.1` packages through `package-validation/NuGet.local.config`.
- Produces: eight locked package-only graphs—one provider-neutral runtime, one integration-test consumer, and six isolated adapter consumers.

- [ ] **Step 1: Add failing package-consumer matrix assertions**

Set the package-validation expectations in `CommonPackageBoundaryTests` to:

```csharp
var expectations = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj"] =
        [
            "Rvt.Monitor.Common",
            "Rvt.Communication.Abstractions",
            "Rvt.Communication",
            "Rvt.Storage.Abstractions"
        ],
    ["libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj"] =
        ["Rvt.Monitor.IntegrationTesting"],
    ["libs/rvt-monitor-common/package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj"] =
        ["Rvt.Communication.SendGridMail"],
    ["libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj"] =
        ["Rvt.Communication.MicrosoftGraphMail"],
    ["libs/rvt-monitor-common/package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj"] =
        ["Rvt.Communication.TransmitSms"],
    ["libs/rvt-monitor-common/package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj"] =
        ["Rvt.Storage.Local"],
    ["libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj"] =
        ["Rvt.Storage.AzureBlob"],
    ["libs/rvt-monitor-common/package-validation/S3StorageConsumer/S3StorageConsumer.csproj"] =
        ["Rvt.Storage.S3"]
};
```

Expand `RvtPackageIds` to the eleven catalog identifiers and keep `ValidatePackageValidationConsumer` rejecting every source `ProjectReference`.

- [ ] **Step 2: Run the boundary test and verify RED**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter PackageValidationConsumers_RetainExactPackageBoundary --nologo
```

Expected: FAIL because the six provider consumer projects and their locks are absent.

- [ ] **Step 3: Make RuntimeConsumer provider-neutral**

Use this package group in `RuntimeConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Monitor.Common" Version="[$(RvtPackageVersion)]" />
  <PackageReference Include="Rvt.Communication.Abstractions" Version="[$(RvtPackageVersion)]" />
  <PackageReference Include="Rvt.Communication" Version="[$(RvtPackageVersion)]" />
  <PackageReference Include="Rvt.Storage.Abstractions" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

Set:

```xml
<RvtPackageVersion Condition="'$(RvtPackageVersion)' == ''">1.0.0-rc.1</RvtPackageVersion>
```

Replace `Program.cs` with:

```csharp
using System.Reflection;

string[] expectedAssemblies =
[
    "Rvt.Monitor.Common",
    "Rvt.Communication.Abstractions",
    "Rvt.Communication",
    "Rvt.Storage.Abstractions"
];

foreach (var expected in expectedAssemblies)
{
    var actual = Assembly.Load(expected).GetName().Name;
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected {expected}, loaded {actual}.");
    }
}

string[] forbiddenAssemblies =
[
    "SendGrid",
    "Azure.Identity",
    "Azure.Storage.Blobs",
    "AWSSDK.S3"
];

var loaded = AppDomain.CurrentDomain.GetAssemblies()
    .Select(assembly => assembly.GetName().Name)
    .ToHashSet(StringComparer.Ordinal);
if (forbiddenAssemblies.Any(loaded.Contains))
{
    throw new InvalidOperationException("A provider-neutral consumer loaded a vendor SDK.");
}
```

- [ ] **Step 4: Create the six exact provider consumer project files**

Each consumer is a `net10.0` executable with this property group:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  <RestoreLockedMode Condition="Exists('$(MSBuildProjectDirectory)/packages.lock.json')">true</RestoreLockedMode>
  <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  <RvtPackageVersion Condition="'$(RvtPackageVersion)' == ''">1.0.0-rc.1</RvtPackageVersion>
</PropertyGroup>
```

Use these exact lock paths and package references:

| Consumer | `NuGetLockFilePath` when `RvtUseArtifactValidationLocks=true` | Direct package |
| --- | --- | --- |
| SendGridMailConsumer | `../../../../artifacts/validation-locks/SendGridMailConsumer.packages.lock.json` | `Rvt.Communication.SendGridMail` |
| MicrosoftGraphMailConsumer | `../../../../artifacts/validation-locks/MicrosoftGraphMailConsumer.packages.lock.json` | `Rvt.Communication.MicrosoftGraphMail` |
| TransmitSmsConsumer | `../../../../artifacts/validation-locks/TransmitSmsConsumer.packages.lock.json` | `Rvt.Communication.TransmitSms` |
| LocalStorageConsumer | `../../../../artifacts/validation-locks/LocalStorageConsumer.packages.lock.json` | `Rvt.Storage.Local` |
| AzureBlobStorageConsumer | `../../../../artifacts/validation-locks/AzureBlobStorageConsumer.packages.lock.json` | `Rvt.Storage.AzureBlob` |
| S3StorageConsumer | `../../../../artifacts/validation-locks/S3StorageConsumer.packages.lock.json` | `Rvt.Storage.S3` |

Use these literal item groups in the corresponding project files:

`SendGridMailConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Communication.SendGridMail" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

`MicrosoftGraphMailConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Communication.MicrosoftGraphMail" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

`TransmitSmsConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Communication.TransmitSms" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

`LocalStorageConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Storage.Local" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

`AzureBlobStorageConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Storage.AzureBlob" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

`S3StorageConsumer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Rvt.Storage.S3" Version="[$(RvtPackageVersion)]" />
</ItemGroup>
```

- [ ] **Step 5: Add assembly-loading smoke programs**

Each provider `Program.cs` uses its literal assembly name:

```csharp
using System.Reflection;

const string expected = "Rvt.Communication.SendGridMail";
var actual = Assembly.Load(expected).GetName().Name;
if (!string.Equals(actual, expected, StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Expected {expected}, loaded {actual}.");
}
```

Use these exact `expected` values in the corresponding files:

```text
Rvt.Communication.SendGridMail
Rvt.Communication.MicrosoftGraphMail
Rvt.Communication.TransmitSms
Rvt.Storage.Local
Rvt.Storage.AzureBlob
Rvt.Storage.S3
```

- [ ] **Step 6: Restore and execute all eight package consumers**

Run from `libs/rvt-monitor-common` after Task 2 has packed the local artifacts:

```bash
for project in \
  package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  package-validation/TestConsumer/TestConsumer.csproj \
  package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj \
  package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj \
  package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj \
  package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj \
  package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj \
  package-validation/S3StorageConsumer/S3StorageConsumer.csproj; do
  dotnet restore "$project" \
    --configfile package-validation/NuGet.local.config \
    --force-evaluate \
    -p:RestoreLockedMode=false \
    -p:RvtPackageVersion=1.0.0-rc.1
done

for project in \
  package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj \
  package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj \
  package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj \
  package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj \
  package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj \
  package-validation/S3StorageConsumer/S3StorageConsumer.csproj; do
  dotnet run --project "$project" --no-restore \
    -p:RvtPackageVersion=1.0.0-rc.1
done

dotnet test package-validation/TestConsumer/TestConsumer.csproj \
  --no-restore --nologo -p:RvtPackageVersion=1.0.0-rc.1
```

Expected: seven console consumers exit 0 and TestConsumer passes.

- [ ] **Step 7: Add provider-isolation lock assertions**

In `PackageArtifactTests`, load each consumer lock and assert:

```csharp
private static readonly IReadOnlyDictionary<string, string[]> ForbiddenPackagesByConsumer =
    new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["RuntimeConsumer"] = ["SendGrid", "Azure.Identity", "Azure.Storage.Blobs", "AWSSDK.S3"],
        ["SendGridMailConsumer"] = ["Azure.Identity", "Azure.Storage.Blobs", "AWSSDK.S3"],
        ["MicrosoftGraphMailConsumer"] = ["SendGrid", "Azure.Storage.Blobs", "AWSSDK.S3"],
        ["TransmitSmsConsumer"] = ["SendGrid", "Azure.Identity", "Azure.Storage.Blobs", "AWSSDK.S3"],
        ["LocalStorageConsumer"] = ["Azure.Identity", "Azure.Storage.Blobs", "AWSSDK.S3"],
        ["AzureBlobStorageConsumer"] = ["SendGrid", "AWSSDK.S3"],
        ["S3StorageConsumer"] = ["SendGrid", "Azure.Identity", "Azure.Storage.Blobs"]
    };
```

Enumerate every dependency name in every target framework and assert that none equals a forbidden name. Also assert the one direct RVT package defined by the consumer matrix and reject `Rvt.Monitor.Common.Infrastructure` everywhere.

- [ ] **Step 8: Re-run the package boundary and package artifact suites**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter PackageValidationConsumers_RetainExactPackageBoundary --nologo
RVT_PACKAGE_VERSION=1.0.0-rc.1 dotnet test \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj \
  --nologo
```

Expected: both commands pass with zero failures.

- [ ] **Step 9: Commit the independent package consumers**

```bash
git add \
  libs/rvt-monitor-common/package-validation \
  libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs
git commit -m "test: validate provider packages independently"
```

---

### Task 4: Migrate source-boundary guards and both solutions

**Files:**
- Modify: `scripts/verify-rvt-common-source-boundary.sh`
- Modify: `tests/verify-rvt-common-source-boundary.test.sh`
- Modify: `tests/verify-rvt-common-source-boundary-regression.test.sh`
- Modify: `tests/fixtures/rvt-common-source-boundary/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs`
- Modify: `libs/rvt-monitor-common/rvt-common.sln`
- Modify: `Rvt.Mono.slnx`

**Interfaces:**
- Consumes: the final source `ProjectReference` graph produced by the communication/storage consumer-migration tasks and the package catalog from Task 1.
- Produces: executable guards that reject package/source boundary drift and solutions containing every current project.

- [ ] **Step 1: Write RED assertions for the removed infrastructure project**

In `CommonPackageBoundaryTests`, add:

```csharp
[TestMethod]
public void RemovedInfrastructureIdentityIsAbsentFromCurrentBuildFiles()
{
    var root = MonoRepositoryRoot();
    var offenders = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetExtension(path) is ".csproj" or ".props" or ".targets" or ".sln" or ".slnx")
        .Where(path => !HasGeneratedDirectory(root, path))
        .Where(path => File.ReadAllText(path).Contains(
            "Rvt.Monitor.Common.Infrastructure",
            StringComparison.Ordinal))
        .Select(Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();

    CollectionAssert.AreEqual(Array.Empty<string>(), offenders, string.Join(Environment.NewLine, offenders));
}
```

In `RvtCommonDependencyBoundaryTests`, replace the infrastructure source test with:

```csharp
[Fact]
public void HostAdapter_UsesOnlyTheSendGridSourceAdapterWithoutRvtPackages()
{
    var projectPath = Path.Combine(RepositoryLayout.Root, "RvtPortal.Spa", "RvtPortal.Spa.csproj");
    var project = System.Xml.Linq.XDocument.Load(projectPath);
    var packageReferences = project.Descendants()
        .Where(element => element.Name.LocalName == "PackageReference")
        .Select(element => (string?)element.Attribute("Include"))
        .Where(package => package?.StartsWith("Rvt.", StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();
    var rvtSourceReferences = project.Descendants()
        .Where(element => element.Name.LocalName == "ProjectReference")
        .Select(element => ((string?)element.Attribute("Include"))?.Replace('\\', '/'))
        .Where(reference => reference?.Contains(
            "libs/rvt-monitor-common/",
            StringComparison.Ordinal) == true)
        .ToArray();

    Assert.Empty(packageReferences);
    Assert.Contains(rvtSourceReferences, reference =>
        reference!.EndsWith(
            "src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj",
            StringComparison.Ordinal));
    Assert.DoesNotContain(rvtSourceReferences, reference =>
        reference!.Contains("MicrosoftGraphMail", StringComparison.Ordinal) ||
        reference.Contains("TransmitSms", StringComparison.Ordinal) ||
        reference.Contains("Rvt.Monitor.Common.Infrastructure", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the architecture tests and verify RED**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter "RemovedInfrastructureIdentityIsAbsentFromCurrentBuildFiles" --nologo
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "HostAdapter_UsesOnlyTheSendGridSourceAdapterWithoutRvtPackages" --nologo
```

Expected: FAIL while the old infrastructure project or reference remains.

- [ ] **Step 3: Generalize the shell source-boundary guard**

In `verify-rvt-common-source-boundary.sh`, replace the three scalar project variables with catalog-derived arrays:

```bash
catalog="${repo_root}/libs/rvt-monitor-common/release/package-catalog.tsv"
rvt_projects=()
rvt_packages=()
while IFS=$'\t' read -r package project; do
  rvt_packages+=("$package")
  rvt_projects+=("libs/rvt-monitor-common/$project")
done <"$catalog"
```

Make `reject_active_package_references` scan:

```bash
active_scopes=(
  "${repo_root}/apps/monitors"
  "${repo_root}/apps/portal"
  "${repo_root}/services/reporting"
)
```

Define these eight exact package consumers:

```bash
package_consumers=(
  libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj
  libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj
  libs/rvt-monitor-common/package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj
  libs/rvt-monitor-common/package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj
  libs/rvt-monitor-common/package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj
  libs/rvt-monitor-common/package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj
  libs/rvt-monitor-common/package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj
  libs/rvt-monitor-common/package-validation/S3StorageConsumer/S3StorageConsumer.csproj
)
```

For each consumer, reject source references to every project in `rvt_projects`. Require the exact direct package set from Task 3. Explicitly fail if any current build file names `Rvt.Monitor.Common.Infrastructure`.

- [ ] **Step 4: Preserve a focused regression fixture**

Keep the fixture RuntimeConsumer package references provider-neutral:

```xml
<PackageReference Include="Rvt.Monitor.Common" />
<PackageReference Include="Rvt.Communication.Abstractions" />
<PackageReference Include="Rvt.Communication" />
<PackageReference Include="Rvt.Storage.Abstractions" />
<ProjectReference Include="../../testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj" />
```

Update `verify-rvt-common-source-boundary-regression.test.sh` to require this exact diagnostic:

```text
libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj must not source-reference libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj
```

- [ ] **Step 5: Update the module solution**

Remove:

```text
src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj
tests/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj
```

Add all eleven catalog projects and these eight extracted test projects:

```text
tests/Rvt.Communication.Tests/Rvt.Communication.Tests.csproj
tests/Rvt.Communication.SendGridMail.Tests/Rvt.Communication.SendGridMail.Tests.csproj
tests/Rvt.Communication.MicrosoftGraphMail.Tests/Rvt.Communication.MicrosoftGraphMail.Tests.csproj
tests/Rvt.Communication.TransmitSms.Tests/Rvt.Communication.TransmitSms.Tests.csproj
tests/Rvt.Storage.Abstractions.Tests/Rvt.Storage.Abstractions.Tests.csproj
tests/Rvt.Storage.Local.Tests/Rvt.Storage.Local.Tests.csproj
tests/Rvt.Storage.AzureBlob.Tests/Rvt.Storage.AzureBlob.Tests.csproj
tests/Rvt.Storage.S3.Tests/Rvt.Storage.S3.Tests.csproj
```

Retain:

```text
tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj
tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj
testing/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj
```

The final module solution contains 22 projects: 11 production and 11 test projects. Package-only consumers remain outside `rvt-common.sln` because their packages do not exist before the pack phase.

- [ ] **Step 6: Update the root solution**

Under `/Libraries/RVT Monitor Common/`, include the eleven production projects and these seven console consumers:

```text
package-validation/RuntimeConsumer/RuntimeConsumer.csproj
package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj
package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj
package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj
package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj
package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj
package-validation/S3StorageConsumer/S3StorageConsumer.csproj
```

Under `/Libraries/RVT Monitor Common/Tests/`, include the eleven test projects plus:

```text
package-validation/TestConsumer/TestConsumer.csproj
```

The library module contributes exactly 30 projects to `Rvt.Mono.slnx`. With the 31 non-library module projects present after the Portal application-boundary work, the aggregate solution contains 61 projects.

- [ ] **Step 7: Run boundary and solution tests**

```bash
tests/verify-rvt-common-source-boundary.test.sh
tests/verify-rvt-common-source-boundary-regression.test.sh
tests/verify-mono-solution.test.sh
dotnet sln libs/rvt-monitor-common/rvt-common.sln list
dotnet sln Rvt.Mono.slnx list
```

Expected:

```text
RVT common source boundary verified.
Local RVT package prerequisite sequencing verified.
```

The regression test exits 0; the module solution lists 22 projects; the root solution lists 61 projects; the solution guard reports no mismatch.

- [ ] **Step 8: Re-run the architecture tests and verify GREEN**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter "RemovedInfrastructureIdentityIsAbsentFromCurrentBuildFiles|PackageValidationConsumers_RetainExactPackageBoundary" \
  --nologo
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "HostAdapter_UsesOnlyTheSendGridSourceAdapterWithoutRvtPackages" --nologo
```

Expected: all focused architecture tests pass.

- [ ] **Step 9: Commit solutions and source boundaries**

```bash
git add \
  scripts/verify-rvt-common-source-boundary.sh \
  tests/verify-rvt-common-source-boundary.test.sh \
  tests/verify-rvt-common-source-boundary-regression.test.sh \
  tests/fixtures/rvt-common-source-boundary \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs \
  apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs \
  libs/rvt-monitor-common/rvt-common.sln \
  Rvt.Mono.slnx
git commit -m "build: migrate solutions and source boundaries"
```

---

### Task 5: Regenerate the complete locked dependency graph

**Files:**
- Modify: all eleven production `packages.lock.json` files beside the catalog projects.
- Modify: all eleven library test-project lock files.
- Modify: all eight `package-validation/*/packages.lock.json` files.
- Modify: the twelve tracked active monitor consumer lock files.
- Test: `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`

**Interfaces:**
- Consumes: the final source project graph and locally packed `1.0.0-rc.1` artifacts.
- Produces: deterministic committed locks without a direct RVT package in source consumers and exact direct RVT entries in package consumers.

- [ ] **Step 1: Capture the current tracked lock set**

```bash
git ls-files '*packages.lock.json' | LC_ALL=C sort > /private/tmp/rvt-provider-locks.before
```

Expected: the file contains every tracked lock path and no generated artifact lock.

- [ ] **Step 2: Regenerate production and test locks**

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  --force-evaluate \
  -p:RestoreLockedMode=false \
  -p:PackageVersion=1.0.0-rc.1
```

Expected: eleven production locks and eleven test locks resolve successfully.

- [ ] **Step 3: Pack the catalog and regenerate all package-consumer locks**

Run from `libs/rvt-monitor-common`:

```bash
rm -rf artifacts/packages
mkdir -p artifacts/packages
while IFS=$'\t' read -r package_id project_path; do
  dotnet pack "$project_path" -c Release \
    -p:PackageVersion=1.0.0-rc.1
done < release/package-catalog.tsv

for project in \
  package-validation/RuntimeConsumer/RuntimeConsumer.csproj \
  package-validation/TestConsumer/TestConsumer.csproj \
  package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj \
  package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj \
  package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj \
  package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj \
  package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj \
  package-validation/S3StorageConsumer/S3StorageConsumer.csproj; do
  dotnet restore "$project" \
    --configfile package-validation/NuGet.local.config \
    --force-evaluate \
    -p:RestoreLockedMode=false \
    -p:RvtPackageVersion=1.0.0-rc.1
done
```

Expected: all eight committed consumer locks resolve direct RVT entries at `1.0.0-rc.1`.

- [ ] **Step 4: Regenerate source-consumer locks through the root local-feed sequence**

```bash
RVT_PACKAGE_VERSION=1.0.0-rc.1 scripts/build-mono.sh
```

Expected: active source-consumer locks contain no direct RVT dependency; package-consumer restores use `artifacts/validation-locks` and do not overwrite their committed policy locks.

- [ ] **Step 5: Run lock-shape assertions**

```bash
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter "ActiveConsumerLocks_DoNotRetainDirectRvtPackages|PackageValidationConsumers_RetainExactPackageBoundary" \
  --nologo
git diff --exit-code -- artifacts/validation-locks
```

Expected: focused tests pass and no generated artifact lock is tracked.

- [ ] **Step 6: Commit the regenerated locks**

```bash
git add '**/packages.lock.json'
git commit -m "build: lock clean-split package dependencies"
```

---

### Task 6: Generalize the root local-package build sequence

**Files:**
- Modify: `scripts/build-mono.sh`
- Modify: `tests/verify-rvt-common-source-boundary.test.sh`

**Interfaces:**
- Consumes: `release/package-catalog.tsv`, eight package consumers, and the root local feed override `RVT_PACKAGE_FEED_DIR`.
- Produces: one credential-free command that packs all eleven packages before any package-only or aggregate restore.

- [ ] **Step 1: Expand the fake package prerequisite test and verify RED**

Update the fake `dotnet pack` handler to derive the package identifier from the matching catalog row rather than the project filename. Set:

```bash
missing_artifact="${empty_feed}/Rvt.Storage.S3.1.0.0-rc.1.nupkg"
```

Require that none of these restores occurs before all eleven artifacts exist:

```text
package-validation/RuntimeConsumer/RuntimeConsumer.csproj
package-validation/TestConsumer/TestConsumer.csproj
package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj
package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj
package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj
package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj
package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj
package-validation/S3StorageConsumer/S3StorageConsumer.csproj
Rvt.Mono.slnx
```

Run:

```bash
tests/verify-rvt-common-source-boundary.test.sh
```

Expected: FAIL because `build-mono.sh` still packs only the old project list.

- [ ] **Step 2: Make the root build catalog-driven**

Set:

```bash
package_version="${RVT_PACKAGE_VERSION:-1.0.0-rc.1}"
catalog="${repo_root}/libs/rvt-monitor-common/release/package-catalog.tsv"
```

Replace the three restore/pack commands and artifact loops with:

```bash
while IFS=$'\t' read -r package_id relative_project; do
  project="${repo_root}/libs/rvt-monitor-common/${relative_project}"
  dotnet restore "$project" --packages "$nuget_packages" \
    -p:PackageVersion="$package_version"
  dotnet pack "$project" --no-restore --output "$package_feed" \
    -p:PackageVersion="$package_version"
  package_artifact="${package_feed}/${package_id}.${package_version}.nupkg"
  if [[ ! -f "$package_artifact" ]]; then
    printf 'Missing package artifact: %s\n' "$package_artifact" >&2
    exit 1
  fi
done <"$catalog"
```

Evict all eleven packages:

```bash
while IFS=$'\t' read -r package_id _; do
  normalized="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
  rm -rf "${nuget_packages:?}/${normalized}/${package_version}"
done <"$catalog"
```

Restore the eight exact consumers from Task 4 with:

```bash
--force-evaluate
-p:RestoreLockedMode=false
-p:RvtUseArtifactValidationLocks=true
-p:RvtPackageVersion="$package_version"
```

After the root solution build, run all seven console consumers with `dotnet run --no-build --no-restore`, then run TestConsumer and the root solution tests.

- [ ] **Step 3: Make the fake tool create all catalog artifacts**

Pass the catalog path to the fake command through:

```bash
RVT_PACKAGE_CATALOG="${repo_root}/libs/rvt-monitor-common/release/package-catalog.tsv"
```

For a `dotnet pack` call, resolve the project argument against column two and touch:

```bash
"${package_output}/${package_id}.1.0.0-rc.1.nupkg"
```

Assert exactly eleven pack calls precede the first package-consumer restore.

- [ ] **Step 4: Run RED/GREEN build-sequence verification**

```bash
tests/verify-rvt-common-source-boundary.test.sh
RVT_PACKAGE_VERSION=1.0.0-rc.1 scripts/build-mono.sh
```

Expected: the fake test prints `Local RVT package prerequisite sequencing verified.`; the real build produces eleven `.nupkg`, builds the root solution, runs seven console consumers, and passes TestConsumer.

- [ ] **Step 5: Commit the root build migration**

```bash
git add scripts/build-mono.sh tests/verify-rvt-common-source-boundary.test.sh
git commit -m "build: pack the eleven-package local train"
```

---

### Task 7: Update CI and standalone consumer validation

**Files:**
- Modify: `libs/rvt-monitor-common/.github/workflows/ci.yml`
- Modify: `apps/monitors/.github/workflows/package-consumer-ci.yml`
- Modify: `apps/monitors/scripts/verify-private-package-builds.sh`
- Modify: `apps/monitors/scripts/report-rvt-package-inventory.sh`
- Test: `libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh`
- Test: `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`

**Interfaces:**
- Consumes: module solution, catalog, local packages, eight package consumers, and exact package version.
- Produces: CI gates that test the source graph, audit every transitive graph, pack 22 NuGet files, and validate standalone package-consuming images without the removed assembly.

- [ ] **Step 1: Add failing CI policy assertions**

In `release-automation-tests.sh`, require the CI workflow to contain:

```text
release/package-catalog.tsv
dotnet list rvt-common.sln package --vulnerable --include-transitive
package-validation/SendGridMailConsumer/SendGridMailConsumer.csproj
package-validation/MicrosoftGraphMailConsumer/MicrosoftGraphMailConsumer.csproj
package-validation/TransmitSmsConsumer/TransmitSmsConsumer.csproj
package-validation/LocalStorageConsumer/LocalStorageConsumer.csproj
package-validation/AzureBlobStorageConsumer/AzureBlobStorageConsumer.csproj
package-validation/S3StorageConsumer/S3StorageConsumer.csproj
```

Reject `Rvt.Monitor.Common.Infrastructure`.

Run:

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
```

Expected: FAIL because CI still packs the three-package graph.

- [ ] **Step 2: Make common CI build and test the split graph**

Keep the versioned locked restore and vulnerable-package gate. Replace individual pre-pack tests with:

```yaml
- run: dotnet test rvt-common.sln -c Release --no-build --nologo --filter "FullyQualifiedName!~PackageArtifactTests"
- name: Pack synchronized package train
  shell: bash
  run: |
    while IFS=$'\t' read -r package_id project; do
      dotnet pack "$project" -c Release --no-build -p:PackageVersion="$RVT_PACKAGE_VERSION"
    done < release/package-catalog.tsv
- run: dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj -c Release --no-build --nologo
```

Restore and run all eight package consumers exactly as in Task 3, using `$RVT_PACKAGE_VERSION`. Upload:

```yaml
path: artifacts/packages/*.*nupkg
```

Expected artifact cardinality: 22 files.

- [ ] **Step 3: Remove the old identity from standalone package checks**

In `verify-private-package-builds.sh`, make `is_retired_common_identity` recognize local project identities that collide with any released family:

```bash
case "$normalized" in
  rvt.monitor.common*|rvt.monitor.integrationtesting*|rvt.communication*|rvt.storage*)
    return 0
    ;;
esac
```

The script checks local project/assembly/package identities only; it must continue allowing package references to the released identifiers.

In `report-rvt-package-inventory.sh`:

- require exactly one `EXPECTED_RVT_VERSION` argument;
- remove the obsolete `RvtCommonVersion`, `RvtCommonInfrastructureVersion`, and `RvtIntegrationTestingVersion` fallback;
- reject `Rvt.Monitor.IntegrationTesting` and `Rvt.Monitor.Common.Infrastructure`;
- enumerate every dependency whose name begins `Rvt.Monitor.`, `Rvt.Communication.`, or `Rvt.Storage.`;
- require `Rvt.Monitor.Common`;
- require every observed RVT runtime dependency to equal the supplied exact version;
- print `image<TAB>package<TAB>version` for every observed RVT runtime package.

Set in `package-consumer-ci.yml`:

```yaml
env:
  RVT_PACKAGE_VERSION: 1.0.0-rc.1
```

Call:

```yaml
- run: scripts/report-rvt-package-inventory.sh "$RVT_PACKAGE_VERSION"
```

- [ ] **Step 4: Run CI policy and script-focused tests**

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj \
  --filter "PackageInventoryScript_UsesPortableTemporaryDirectoryFallback|PackageVerificationScript_UsesRunnerPortableSearchTools" \
  --nologo
bash -n \
  apps/monitors/scripts/verify-private-package-builds.sh \
  apps/monitors/scripts/report-rvt-package-inventory.sh
```

Expected: release automation policy checks pass, both architecture tests pass, and Bash syntax validation exits 0.

- [ ] **Step 5: Commit CI migration**

```bash
git add \
  libs/rvt-monitor-common/.github/workflows/ci.yml \
  libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh \
  apps/monitors/.github/workflows/package-consumer-ci.yml \
  apps/monitors/scripts/verify-private-package-builds.sh \
  apps/monitors/scripts/report-rvt-package-inventory.sh \
  apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs
git commit -m "ci: validate split provider package graphs"
```

---

### Task 8: Expand immutable-version and SBOM release tests

**Files:**
- Modify: `libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh`
- Test: `libs/rvt-monitor-common/scripts/assert-package-version-available.sh`
- Test: `libs/rvt-monitor-common/scripts/build-release-artifacts.sh`

**Interfaces:**
- Consumes: the catalog, eight consumer locks, fake GitHub API, and fake dotnet/SBOM tools.
- Produces: deterministic RED tests for eleven preflight queries, eleven SBOM components, 22 package files, and 26 final assets.

- [ ] **Step 1: Expand fake state backup to all production objects and consumer locks**

Derive `production_obj_dirs` from the catalog:

```bash
production_obj_dirs=()
while IFS=$'\t' read -r _ project; do
  production_obj_dirs+=("$repository_root/${project%/*}/obj")
done <"$repository_root/release/package-catalog.tsv"
```

Define all eight consumer locks:

```bash
consumer_locks=(
  package-validation/RuntimeConsumer/packages.lock.json
  package-validation/TestConsumer/packages.lock.json
  package-validation/SendGridMailConsumer/packages.lock.json
  package-validation/MicrosoftGraphMailConsumer/packages.lock.json
  package-validation/TransmitSmsConsumer/packages.lock.json
  package-validation/LocalStorageConsumer/packages.lock.json
  package-validation/AzureBlobStorageConsumer/packages.lock.json
  package-validation/S3StorageConsumer/packages.lock.json
)
```

Back up and restore each lock by its unique consumer directory name on success and every failure path.

- [ ] **Step 2: Expand immutable-version preflight expectations**

Change:

```bash
[[ "$(wc -l <"$gh_log" | tr -d ' ')" == "11" ]] ||
  fail "all eleven packages were not queried"
```

Keep pagination, 404-as-not-published, non-404 fail-closed, and unsafe-version-before-network assertions.

- [ ] **Step 3: Expand fake restore and pack behavior**

On fake solution restore, create `project.assets.json` beneath all eleven catalog project directories and set `project.version` to the requested version.

On fake pack, map the project path to the package identifier using `release/package-catalog.tsv`, then create:

```text
artifacts/packages/PACKAGE_ID.VERSION.nupkg
artifacts/packages/PACKAGE_ID.VERSION.snupkg
PROJECT_DIRECTORY/obj/Release/PACKAGE_ID.VERSION.nuspec
```

The fake SBOM generator must require exactly:

```text
11 *.csproj
11 packages.lock.json
11 project.assets.json
11 *.nuspec
```

- [ ] **Step 4: Expand the fake SPDX document**

Use this exact package list in the fake Python generator:

```python
package_ids = [
    "Rvt.Monitor.Common",
    "Rvt.Monitor.IntegrationTesting",
    "Rvt.Communication.Abstractions",
    "Rvt.Communication",
    "Rvt.Communication.SendGridMail",
    "Rvt.Communication.MicrosoftGraphMail",
    "Rvt.Communication.TransmitSms",
    "Rvt.Storage.Abstractions",
    "Rvt.Storage.Local",
    "Rvt.Storage.AzureBlob",
    "Rvt.Storage.S3",
]
```

Generate two files for every identifier. The root package `hasFiles` must contain exactly the 22 unique file SPDX identifiers.

- [ ] **Step 5: Assert exact release cardinalities**

Change the success assertions to:

```bash
[[ "$(find "$assets" -maxdepth 1 -type f | wc -l | tr -d ' ')" == "26" ]] ||
  fail "flat release asset count is not exact"
[[ "$(wc -l <"$assets/SHA256SUMS" | tr -d ' ')" == "25" ]] ||
  fail "not every published asset has a checksum entry"
```

Keep mutation failures for:

```text
root-only SBOM
stale RVT version
official validation failure
wrong project.assets.json version
extra external dependency
missing external dependency
wrong external dependency version
duplicate external dependency
wrong root file link
```

- [ ] **Step 6: Run release automation tests and verify RED**

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
```

Expected: FAIL because the production preflight and builder still enumerate three packages and nine pre-checksum assets.

- [ ] **Step 7: Commit the RED release tests**

```bash
git add libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
git commit -m "test: require eleven-package release assets"
```

---

### Task 9: Implement the eleven-package preflight, SBOM, and release builder

**Files:**
- Modify: `libs/rvt-monitor-common/scripts/assert-package-version-available.sh`
- Modify: `libs/rvt-monitor-common/scripts/build-release-artifacts.sh`
- Modify: `libs/rvt-monitor-common/.github/workflows/release.yml`
- Test: `libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh`

**Interfaces:**
- Consumes: `release/package-catalog.tsv`, exact release version, module solution, local package consumers, SBOM Tool 4.1.5.
- Produces: an immutable eleven-package release with 11 binaries, 11 symbol packages, migration archive, SPDX manifest/checksum, and one complete checksum manifest.

- [ ] **Step 1: Make immutable-version preflight catalog-driven**

Set:

```bash
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
catalog="$repository_root/release/package-catalog.tsv"
```

Replace the three-package loop with:

```bash
while IFS=$'\t' read -r package _; do
  error_file="$(mktemp)"
  versions=""
  if versions="$(gh api --paginate \
      "/orgs/RVT-Group-LTD/packages/nuget/$package/versions?per_page=100" \
      --jq '.[].name' 2>"$error_file")"; then
    if grep -Fxq "$version" <<<"$versions"; then
      rm -f "$error_file"
      echo "Package $package version $version already exists" >&2
      exit 1
    fi
  elif grep -q 'HTTP 404' "$error_file"; then
    :
  else
    cat "$error_file" >&2
    rm -f "$error_file"
    exit 1
  fi
  rm -f "$error_file"
done <"$catalog"
```

- [ ] **Step 2: Make restore, test, and pack catalog-driven**

Keep version validation, locked solution restore, Release clean/build, and test all non-package-artifact tests:

```bash
dotnet test rvt-common.sln -c Release --no-build --nologo \
  --filter "FullyQualifiedName!~PackageArtifactTests"
```

Pack:

```bash
while IFS=$'\t' read -r package_id project; do
  dotnet pack "$project" -c Release --no-build \
    -p:PackageVersion="$version"
done < release/package-catalog.tsv
```

- [ ] **Step 3: Stage all eleven SBOM projects**

Keep the `stage_sbom_project` function and invoke it using catalog data:

```bash
while IFS=$'\t' read -r package_id project; do
  project_dir="${project%/*}"
  project_file="${project##*/}"
  stage_sbom_project "$project_dir" "$project_file" "$package_id"
done < release/package-catalog.tsv
```

Require each staged `project.assets.json` `project.version` to equal the requested version before invoking the SBOM tool.

- [ ] **Step 4: Validate the exact SPDX package and file sets**

Replace `expected_rvt_packages` in the Python validator with:

```python
expected_rvt_packages = {
    "Rvt.Monitor.Common",
    "Rvt.Monitor.IntegrationTesting",
    "Rvt.Communication.Abstractions",
    "Rvt.Communication",
    "Rvt.Communication.SendGridMail",
    "Rvt.Communication.MicrosoftGraphMail",
    "Rvt.Communication.TransmitSms",
    "Rvt.Storage.Abstractions",
    "Rvt.Storage.Local",
    "Rvt.Storage.AzureBlob",
    "Rvt.Storage.S3",
}
```

Require:

```python
if len(lock_paths) != 11:
    raise SystemExit("SBOM component input does not contain exactly eleven package lock files")
```

Keep exact external dependency equality. Set expected file names from all eleven identifiers and both extensions; require exactly 22 files and exactly 22 root `hasFiles` links. Print computed totals:

```python
print(
    f"SPDX manifest validated: {len(packages)} total packages, "
    f"{len(expected_dependencies)} resolved external dependencies, "
    "11 synchronized Rvt packages"
)
```

- [ ] **Step 5: Validate all eight package consumers**

Back up all eight committed locks before restore and restore them in the EXIT trap. Restore each with the requested version and local config. Run seven console consumers and TestConsumer exactly as in Task 3.

- [ ] **Step 6: Build the exact flat asset set**

Copy both package files for every catalog row:

```bash
while IFS=$'\t' read -r package_id _; do
  cp "artifacts/packages/$package_id.$version.nupkg" "$assets_dir/"
  cp "artifacts/packages/$package_id.$version.snupkg" "$assets_dir/"
done < release/package-catalog.tsv
```

Also copy:

```text
rvt-common-migrations-VERSION.tar.gz
manifest.spdx.json
manifest.spdx.json.sha256
```

Set:

```bash
expected_asset_count=25
```

Then generate `SHA256SUMS`, yielding 26 total flat files and 25 checksum entries.

- [ ] **Step 7: Keep release workflow publication flat and immutable**

Keep:

```yaml
- run: scripts/assert-package-version-available.sh "$PACKAGE_VERSION"
- run: scripts/build-release-artifacts.sh "$PACKAGE_VERSION"
- run: dotnet nuget push 'artifacts/packages/*.nupkg' --source rvt --api-key "$GITHUB_TOKEN"
```

Both artifact upload and stable GitHub release continue using:

```text
artifacts/release/assets/*
```

Do not add `--skip-duplicate`, repository secrets, or recursive release uploads.

- [ ] **Step 8: Run release automation tests and verify GREEN**

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
bash -n \
  libs/rvt-monitor-common/scripts/assert-package-version-available.sh \
  libs/rvt-monitor-common/scripts/build-release-artifacts.sh
```

Expected:

```text
release automation tests passed
```

Bash syntax checks exit 0.

- [ ] **Step 9: Commit release implementation**

```bash
git add \
  libs/rvt-monitor-common/scripts/assert-package-version-available.sh \
  libs/rvt-monitor-common/scripts/build-release-artifacts.sh \
  libs/rvt-monitor-common/.github/workflows/release.yml
git commit -m "release: publish eleven-package SBOM asset set"
```

---

### Task 10: Update release documentation, ownership, and future-pending state

**Files:**
- Modify: `README.md`
- Modify: `libs/rvt-monitor-common/README.md`
- Modify: `docs/release/rvt-monitor-common/releasing.md`
- Modify: `docs/operations/monitors/container-builds.md`
- Modify: `libs/rvt-monitor-common/.github/CODEOWNERS`
- Modify: `docs/superpowers/specs/2026-07-23-rvt-provider-adapter-project-split-design.md`
- Modify: `project_state.md`
- Test: `libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh`

**Interfaces:**
- Consumes: the verified release behavior and approved design.
- Produces: current operator documentation for the eleven-package train and a durable boundary around deferred work.

- [ ] **Step 1: Add failing current-documentation assertions**

Extend `release-automation-tests.sh` to require the release documentation to contain all eleven package identifiers and these exact statements:

```text
eleven synchronized packages
22 package and symbol files
26 flat release assets
1.0.0-rc.1
Portal blob-client/service unification remains future pending work
```

Reject current documentation that describes `Rvt.Monitor.Common.Infrastructure` as published.

Run:

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
```

Expected: FAIL because current documentation still describes a three-package release.

- [ ] **Step 2: Update root and module READMEs**

Document:

- all eleven package identifiers;
- provider-neutral versus provider-specific responsibilities;
- active source references versus package-only validation;
- `scripts/build-mono.sh` creating 11 `.nupkg` and 11 `.snupkg` files;
- exact synchronized version `1.0.0-rc.1`;
- `Rvt.Monitor.Common.Infrastructure` removal.

Replace individual manual pack commands in the module README with:

```bash
while IFS=$'\t' read -r package_id project; do
  dotnet pack "$project" -c Release --no-build --no-restore \
    -p:PackageVersion=1.0.0-rc.1
done < release/package-catalog.tsv
```

- [ ] **Step 3: Update release operator documentation**

In `docs/release/rvt-monitor-common/releasing.md`:

- replace every three-package count with eleven;
- use `1.0.0-rc.1` for release-candidate examples and `v1.0.0` for the first stable clean-split example;
- state that preflight checks all eleven identifiers;
- state that the package portion of the artifact contains 11 packages and 11 symbol packages;
- state that the final flat directory contains 26 files and `SHA256SUMS` has 25 entries;
- list the exact package identifiers in the consumer pin example;
- retain protected tags, `workflow_dispatch`, main/default branch, designated operator, exact version, container-startup prohibition, forward merge, credential revocation, and already-published rollback requirements.

- [ ] **Step 4: Update container inventory and CODEOWNERS documentation**

In `docs/operations/monitors/container-builds.md`, remove the requirement that every image contain the old infrastructure package. Require each image to report every present `Rvt.Monitor.*`, `Rvt.Communication.*`, and `Rvt.Storage.*` runtime package at the one expected exact version.

In `CODEOWNERS`, replace:

```text
/src/Rvt.Monitor.Common.Infrastructure/
```

with:

```text
/src/Rvt.Communication*/
/src/Rvt.Storage*/
/release/
```

Keep the existing owner until organization maintainer teams exist.

- [ ] **Step 5: Record completion and Future Pending Work**

In `project_state.md`, record:

- eleven package identifiers and `1.0.0-rc.1`;
- 22 package/symbol files;
- eight package-only consumers;
- 11 staged SBOM component locks;
- 22 SPDX package files;
- 25 pre-checksum assets and 26 final flat assets;
- solution project counts;
- exact verification commands and outcomes.

Under the design's `Future Pending Work`, retain every item listed in this plan's section below. Change the design status to:

```text
Status: Approved architecture; release-migration implementation complete.
```

- [ ] **Step 6: Re-run documentation and release tests**

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
tests/verify-documentation-layout.test.sh
rg -n "three compatibility packages|three-package|Rvt\\.Monitor\\.Common\\.Infrastructure" \
  README.md \
  libs/rvt-monitor-common/README.md \
  docs/release/rvt-monitor-common/releasing.md \
  docs/operations/monitors/container-builds.md
```

Expected: release automation and documentation layout tests pass; the current-documentation scan returns no matches.

- [ ] **Step 7: Commit documentation and durable state**

```bash
git add \
  README.md \
  libs/rvt-monitor-common/README.md \
  docs/release/rvt-monitor-common/releasing.md \
  docs/operations/monitors/container-builds.md \
  libs/rvt-monitor-common/.github/CODEOWNERS \
  docs/superpowers/specs/2026-07-23-rvt-provider-adapter-project-split-design.md \
  project_state.md \
  libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
git commit -m "docs: record provider package release boundary"
```

---

### Task 11: Run the final release and repository gates

**Files:**
- Verify only: all files changed by Tasks 1–10.

**Interfaces:**
- Consumes: the complete eleven-package implementation.
- Produces: final evidence that source builds, package-only builds, vulnerability checks, release automation, SBOM assertions, and repository guards agree.

- [ ] **Step 1: Restore and build the module in locked mode**

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  --locked-mode --force-evaluate \
  -p:PackageVersion=1.0.0-rc.1
dotnet build libs/rvt-monitor-common/rvt-common.sln \
  -c Release --no-restore --nologo \
  -p:PackageVersion=1.0.0-rc.1
```

Expected: restore and build complete with zero errors.

- [ ] **Step 2: Run formatting, tests, and vulnerability audit**

```bash
dotnet format libs/rvt-monitor-common/rvt-common.sln \
  --verify-no-changes --no-restore
dotnet test libs/rvt-monitor-common/rvt-common.sln \
  -c Release --no-build --nologo \
  --filter "FullyQualifiedName!~PackageArtifactTests"
audit_output="$(dotnet list libs/rvt-monitor-common/rvt-common.sln package \
  --vulnerable --include-transitive)"
printf '%s\n' "$audit_output"
! grep -q 'has the following vulnerable packages' <<<"$audit_output"
```

Expected: formatting and tests pass; vulnerability output contains no vulnerable-package section.

- [ ] **Step 3: Run source, solution, and documentation guards**

```bash
tests/verify-rvt-common-source-boundary.test.sh
tests/verify-rvt-common-source-boundary-regression.test.sh
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
tests/verify-documentation-layout.test.sh
```

Expected: every guard exits 0.

- [ ] **Step 4: Run the complete local package train**

```bash
RVT_PACKAGE_VERSION=1.0.0-rc.1 scripts/build-mono.sh
find artifacts/packages -maxdepth 1 -type f -name '*.nupkg' | wc -l
find artifacts/packages -maxdepth 1 -type f -name '*.snupkg' | wc -l
```

Expected: build and tests pass; counts are 11 and 11.

- [ ] **Step 5: Run release automation and verify exact assets**

```bash
libs/rvt-monitor-common/scripts/tests/release-automation-tests.sh
```

Expected:

```text
release automation tests passed
```

The fake release contains 11 RVT SBOM components, 11 component locks, 22 package files, 25 pre-checksum assets, 26 final flat files, and 25 checksum entries.

- [ ] **Step 6: Run final stale-identity and whitespace checks**

```bash
rg -n "Rvt\\.Monitor\\.Common\\.Infrastructure" \
  --glob '!docs/history/**' \
  --glob '!docs/superpowers/plans/**' \
  --glob '!docs/superpowers/specs/**' \
  --glob '!project_state.md' \
  --glob '!**/bin/**' \
  --glob '!**/obj/**'
git diff --check
git status --short
```

Expected: no current source, project, workflow, script, or operator document references the removed identity; whitespace check is clean; status contains only intentional task changes and pre-existing unrelated files.

- [ ] **Step 7: Commit any verification-only lock normalization**

If the final locked restore changed tracked lock content, inspect the diff and commit only deterministic lock normalization:

```bash
git add '**/packages.lock.json'
git commit -m "build: normalize provider release locks"
```

If no tracked lock changed, do not create an empty commit.

---

## Future Pending Work

These items are explicitly outside the provider-package extraction and release migration. They remain recorded for separate design, prioritization, and approval:

1. Portal blob-client/service unification: migrate `MonitorPictureStorage` and `SiteArchiveService` to `IObjectStorageClientFactory` while preserving protected API streaming, local fallback, atomic writes, existing `blob://` monitor references, persisted archive URLs, and report/archive resource boundaries.
2. Customer-logo storage: decide explicitly whether customer logos adopt the shared object-storage abstraction after monitor pictures and site archives.
3. Reporting-service Azure storage: evaluate the independent reporting adapter after the monitor/reporting shared-storage migration is stable.
4. Legacy `RVT.Utilities.AzureBlobService`: make an independent deprecation or migration decision; do not use it as the source for the shared storage contract.
5. Dynamic provider plugin discovery or runtime assembly loading: consider only when deployments require adding providers without rebuilding a host.
6. External-consumer compatibility tooling: design only if an external consumer cannot coordinate the approved clean major-version migration.
7. Notification business-rule or message-content changes: require a separate product specification and regression suite.
8. Public HTTP API or persisted monitor/report record changes: require an explicit compatibility and data-migration design.
9. Legacy synchronous `IMessageService` removal: migrate its remaining caller under a separate compatibility plan.
10. Database, MQTT, scheduling, and observability project splits: evaluate as later dependency-isolation initiatives after communications and storage are complete.

No implementation task in this plan may absorb these items.

---

## Plan Self-Review

- **Spec coverage:** Tasks 1–11 cover all eleven packages, exact internal versions, source/package boundaries, eight package consumers, both solutions, locks, root build, CI, vulnerability scanning, immutable preflight, SBOM input/output, release asset counts, documentation, and durable project state.
- **Placeholder scan:** The plan contains no deferred implementation marker; every package identifier, project path, test command, and release count is explicit.
- **Type consistency:** Package identifiers, project paths, consumer names, version `1.0.0-rc.1`, package count 11, package-file count 22, consumer-lock count 8, SBOM-lock count 11, pre-checksum asset count 25, final asset count 26, and checksum-entry count 25 are consistent throughout.
