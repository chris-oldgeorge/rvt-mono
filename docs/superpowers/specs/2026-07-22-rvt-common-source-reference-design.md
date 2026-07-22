# RVT Common Source-Reference Migration Design

## Decision

The mono-repository will use source `ProjectReference` dependencies for every
active monitor and portal consumer of `Rvt.Monitor.Common`,
`Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting`.
The `libs/rvt-monitor-common/package-validation` projects remain package
consumers and validate the packages built from the in-repository source.

This is an intentional mono-repository build-boundary decision. Reassess it if
the shared library again needs independent versioned distribution or a consumer
must build from a released package rather than the mono-repository source.

## Scope

Convert active package consumers in `apps/monitors` and `apps/portal` to the
following source projects:

| Package identity | Source project |
| --- | --- |
| `Rvt.Monitor.Common` | `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj` |
| `Rvt.Monitor.Common.Infrastructure` | `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj` |
| `Rvt.Monitor.IntegrationTesting` | `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj` |

The active consumers include the five monitor applications, their shared-test
consumers, ReportingMonitor's component projects and tests, and the portal
host. The package-validation `RuntimeConsumer` and `TestConsumer` are excluded
from conversion.

## Build Sequence

The root build command will become a script with these ordered stages:

1. Restore and build the three shared source projects.
2. Pack those projects at the configured `0.2.0-rc.1` version into the root
   `artifacts/packages` local feed.
3. Restore `Rvt.Mono.slnx` with that local feed as the sole `Rvt.*` source and
   `nuget.org` for third-party packages.
4. Build and test `Rvt.Mono.slnx`.

Normal application build order is determined by the added project references.
The explicit pack step exists only because package-validation intentionally
tests package artifacts rather than source projects.

## Configuration Boundaries

- Remove RVT package-version entries from `apps/monitors/Directory.Packages.props` after their consumers become project references.
- Remove unused RVT private-feed mappings and credentials from monitor/portal
  NuGet configuration; do not remove `nuget.org` sources.
- Replace the shared library's package-validation restore source with a local
  artifact feed. Keep package references and version pinning in its validation
  projects.
- Do not change non-RVT package versions, production code, database assets,
  or external deployment configuration.

## Verification

- Add structural tests proving active application projects have no
  `PackageReference` to the three RVT common packages and use the intended
  source project paths.
- Add structural tests proving package-validation remains package-based.
- Add a build-sequence test or dry-run guard verifying package artifacts exist
  before package-validation restore.
- Run the root source build script, the aggregate-solution guard, and the
  package-validation test project. The completed normal build must not require
  GitHub Packages credentials.

## Risks and Follow-up

- Package-validation lock files may need intentional regeneration because
  locally built artifacts have new content hashes. Regenerate only through the
  defined local-feed sequence and verify the package identities/versions.
- The decision trades independent-consumer realism in active applications for
  a self-contained mono-repository development build. The package-validation
  stage retains a package-consumption check.
