# Storage Provider Split — Task 9 Report

## Outcome

Task 9 wires the storage source graph into the repository solutions without
changing the solution guard, package policy, or repository locks.

- `libs/rvt-monitor-common/rvt-common.sln` contains
  `Rvt.Storage.Abstractions`, `Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`,
  `Rvt.Storage.S3`, and `Rvt.Storage.Tests` exactly once.
- `Rvt.Mono.slnx` contains the same five projects exactly once.
- `apps/monitors/rvt-monitors.sln` contains the four production storage
  projects exactly once. It intentionally does not contain
  `Rvt.Storage.Tests`.

The monitor membership follows the source graph rather than mirroring the
Common test solution: the active Svantek and ReportingMonitor hosts each
directly reference all four production storage projects.

## Baseline and scope

- Worktree: `.worktrees/release-platform-hardening`
- Starting commit: `6b678a5adf9cb78c8e1d23e48069249196d01623`
- CodeGraph was consulted before editing to orient the storage source and
  consumer graph. Project-file inspection then confirmed the exact direct
  references from Svantek and ReportingMonitor.
- Only the three solution files, `project_state.md`, and this report are
  intended for the Task 9 commit.

Preserved and excluded from the commit:

- every `packages.lock.json`;
- central package catalogs and permanent versions;
- the untracked
  `apps/monitors/reportingmonitor/Directory.Packages.props` verification
  override;
- all untracked Portal/reporting future-pending copies and documents;
- the repository solution guard and its project-set derivation.

## Strict RED/GREEN guard evidence

The guard ran before any solution edit:

```text
$ ./tests/verify-mono-solution.test.sh
Solution project count (46) does not match module project count (51).
exit 1
```

The five missing entries were exactly the four production providers plus the
storage test project.

After editing only the solutions:

```text
$ ./tests/verify-mono-solution.test.sh
exit 0
```

The guard was not modified.

## Solution inventory evidence

The following commands all exit 0:

```bash
dotnet sln libs/rvt-monitor-common/rvt-common.sln list
dotnet sln apps/monitors/rvt-monitors.sln list
dotnet sln Rvt.Mono.slnx list
```

Observed storage membership:

| Solution | Abstractions | Local | Azure Blob | S3 | Storage tests |
| --- | ---: | ---: | ---: | ---: | ---: |
| `rvt-common.sln` | 1 | 1 | 1 | 1 | 1 |
| `rvt-monitors.sln` | 1 | 1 | 1 | 1 | 0 |
| `Rvt.Mono.slnx` | 1 | 1 | 1 | 1 | 1 |

## Bounded restore and build evidence

Repository locks are intentionally stale until the later
provider-package-release migration regenerates the full locked graph. Restore
therefore redirected lock output to the temporary directory
`/tmp/rvt-storage-task9-locks.gI98g4` and ran sequentially:

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  -p:RestorePackagesWithLockFile=true \
  -p:RestoreLockedMode=false \
  '-p:NuGetLockFilePath=/tmp/rvt-storage-task9-locks.gI98g4/$(MSBuildProjectName).packages.lock.json' \
  --disable-parallel --nologo -v minimal

dotnet restore Rvt.Mono.slnx \
  -p:RestorePackagesWithLockFile=true \
  -p:RestoreLockedMode=false \
  '-p:NuGetLockFilePath=/tmp/rvt-storage-task9-locks.gI98g4/mono-$(MSBuildProjectName).packages.lock.json' \
  --disable-parallel --nologo -v minimal
```

Both restores exit 0. The root restore reports five existing NU1903 advisories
for `System.Security.Cryptography.Xml` 10.0.7. No tracked lock changed.

The Common solution build is green:

```text
$ dotnet build libs/rvt-monitor-common/rvt-common.sln --no-restore --nologo -v minimal
64 Warning(s)
0 Error(s)
exit 0
```

The first ordinary root build reached and built the new storage projects, then
failed in `RvtPortal.Spa` with eight duplicate-type/member errors because the
preserved untracked `BlobStorageClientFactory 2.cs` and
`PortalSchemaReadinessHealthCheck 2.cs` files are included by the SDK's default
compile glob. Those files are unrelated future-pending work and were not
deleted, renamed, or edited.

A temporary file outside the repository,
`/tmp/rvt-storage-task9-exclude-future.targets`, contained only:

```xml
<Project>
  <ItemGroup>
    <Compile Remove="**/* 2.cs" />
  </ItemGroup>
</Project>
```

With that bounded verification-only import:

```text
$ dotnet build Rvt.Mono.slnx --no-restore --nologo -v minimal \
    -p:CustomAfterMicrosoftCommonTargets=/tmp/rvt-storage-task9-exclude-future.targets
76 Warning(s)
0 Error(s)
exit 0
```

The warnings are existing analyzer warnings plus the five Portal NU1903
advisories. All four storage assemblies and `Rvt.Storage.Tests` compile in the
root build.

## Release and merge constraints carried forward

- Complete atomic lock regeneration and permanent central package policy
  remain delegated to Task 5 of
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`.
- ReportingMonitor's clean locked restore still depends on the delegated
  Logging.Abstractions reconciliation; its untracked central override remains
  verification-only and unstaged.
- Portal and independent reporting-service storage work remains future
  pending. The preserved untracked Portal `* 2.cs` files still block an
  ordinary unoverridden aggregate build.
- Microsoft Graph large-attachment upload-chunk non-caller timeouts still need
  safe transient translation. This remains the carry-forward merge blocker and
  is not changed by storage solution wiring.
