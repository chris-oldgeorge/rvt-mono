# Task 1 report: eleven-package catalog and synchronized version policy

## Outcome

Task 1 is complete from source-split base `e8089dd`.

- `release/package-catalog.tsv` now declares the exact approved eleven-package
  train in the required order, with literal tab delimiters and project-relative
  paths.
- The clean-split default `PackageVersion` is `1.0.0-rc.1`.
- `PinSynchronizedRvtProjectReferenceVersions` runs after
  `_GetProjectReferenceVersions` and before `GenerateNuspec` for every packable
  project. It changes every RVT project dependency (filename beginning `Rvt.`)
  to the exact range `[$(PackageVersion)]`.
- The central catalog was reviewed. No obsolete infrastructure-only entry is
  present, so `Directory.Packages.props` requires no textual change. It retains
  `AWSSDK.S3` `4.0.100.3`, `Azure.Identity` `1.15.0`,
  `Azure.Storage.Blobs` `12.25.0`, and `SendGrid` `9.29.3`.
- No lockfile or active-consumer reference was created, removed, or edited.
  Unrelated untracked files were preserved.

## Strict TDD evidence

The test was written before the catalog existed. Its expected package IDs are
literal and ordered independently of the TSV. Each real catalog row must have
exactly two tab-separated columns, and each declared project path must resolve
to an existing file beneath `libs/rvt-monitor-common`.

### RED

Command (restore disabled):

```text
dotnet test tests/Rvt.Monitor.PackageValidationTests/Rvt.Monitor.PackageValidationTests.csproj --no-restore --filter FullyQualifiedName~PackageCatalogDeclaresTheExactApprovedTrain --logger 'console;verbosity=normal' -m:1
```

Result: exit `1`, total `1`, failed `1`.

Expected failure:

```text
System.IO.DirectoryNotFoundException: Could not find a part of the path
'.../libs/rvt-monitor-common/release/package-catalog.tsv'.
```

The test compiled and executed; it failed because the requested catalog feature
was absent, not because of a test syntax or setup error. One pre-existing
`MSTEST0037` warning remains at the old infrastructure dependency assertion.

### GREEN

The identical focused command, still with restore disabled, passed:

```text
Total tests: 1
     Passed: 1
Test Run Successful.
```

The required property probe passed:

```text
dotnet msbuild src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj -getProperty:PackageVersion -nologo
1.0.0-rc.1
```

An additional no-restore pack probe was written only to
`/private/tmp/rvt-task1-pack-probe.v2IuMi`:

```text
dotnet pack src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj --no-restore -m:1 -p:PackageVersion=1.0.0-rc.1 -o /private/tmp/rvt-task1-pack-probe.v2IuMi
```

It succeeded. The generated nuspec contains:

```xml
<dependency id="Rvt.Communication.Abstractions" version="[1.0.0-rc.1]" exclude="Build,Analyzers" />
<dependency id="SendGrid" version="9.29.3" exclude="Build,Analyzers" />
```

This exercises the real NuGet pack boundary and proves both the generalized
project-reference target and one required central SDK pin.

## Files and variables

- `libs/rvt-monitor-common/release/package-catalog.tsv`: eleven rows of
  `package id<TAB>project path`.
- `libs/rvt-monitor-common/Directory.Build.props`: default
  `PackageVersion=1.0.0-rc.1`; `Version` continues to derive from it.
- `libs/rvt-monitor-common/Directory.Build.targets`:
  `PinSynchronizedRvtProjectReferenceVersions` updates
  `_ProjectReferencesWithVersions` metadata `ProjectVersion`.
- `libs/rvt-monitor-common/tests/Rvt.Monitor.PackageValidationTests/PackageArtifactTests.cs`:
  `PackageRoot` identifies `libs/rvt-monitor-common`; `Artifacts` derives from
  it; `rows` is the parsed TSV; `expectedPackageIds` is the independent ordered
  eleven-ID oracle.
- `.superpowers/sdd/2026-07-23-rvt-provider-package-release-migration/progress.md`
  and `project_state.md`: record completion and resume state.

`Directory.Packages.props` was intentionally reviewed but left byte-for-byte
unchanged because every central entry is still referenced and no obsolete
infrastructure-only entry exists.

## Scope controls

- All verification that could invoke NuGet used `--no-restore`.
- The pack artifact went to an isolated `/private/tmp` directory.
- `git diff --name-only -- '*packages.lock.json'` produced no paths.
- Existing unrelated untracked files, including `.codegraph/`,
  `apps/.nuget-packages/`, the ReportingMonitor override, duplicate Portal
  copies, and the duplicated application-boundary spec, remain untouched.
