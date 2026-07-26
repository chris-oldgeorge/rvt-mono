# RVT Common Direct Project-Reference Decision

## Decision

The mono-repository uses direct `ProjectReference` dependencies for every
internal RVT Common, Communication, Storage, and IntegrationTesting dependency.
The normal build does not create or consume internal RVT NuGet packages.

This supersedes the earlier transitional decision to retain package-only
validation consumers. Reassess this decision only if an external consumer
requires independently versioned distribution.

## Scope

Internal consumers in `apps/monitors`, `apps/portal`, and
`services/reporting` reference the source projects under
`libs/rvt-monitor-common` directly, including:

| Package identity | Source project |
| --- | --- |
| `Rvt.Monitor.Common` | `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj` |
| `Rvt.Monitor.IntegrationTesting` | `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj` |
| `Rvt.Communication.*` | `libs/rvt-monitor-common/src/Rvt.Communication*/` |
| `Rvt.Storage.*` | `libs/rvt-monitor-common/src/Rvt.Storage.*/` |

`Rvt.Monitor.Common.Infrastructure` remains removed.

## Build Sequence

The root build command uses these ordered stages:

1. Run the PostgreSQL-only repository guard.
2. Restore `Rvt.Mono.slnx` from nuget.org for third-party dependencies.
3. Build `Rvt.Mono.slnx`; MSBuild orders internal projects from their
   `ProjectReference` graph.
4. Test `Rvt.Mono.slnx`.

There is no internal package feed, pack stage, package-validation consumer, or
package-feed credential.

## Configuration Boundaries

- Internal RVT projects declare `IsPackable=false`.
- NuGet configurations retain only nuget.org for third-party dependencies.
- Monitor container builds use the monorepo root context and do not mount
  package-feed credentials.
- Do not change non-RVT package versions, production code, database assets,
  or external deployment configuration.

## Verification

- Structural tests reject internal `Rvt.*` `PackageReference` entries and
  packable internal projects.
- The build-sequence test rejects `dotnet pack` and package-validation calls.
- The aggregate solution guard confirms all retained project paths.
- The completed normal build must not require package-feed credentials.

## Risks and Follow-up

- The decision intentionally trades independent binary distribution for one
  coherent source graph.
- External package distribution, if needed later, requires an explicit new
  decision, consumer compatibility tests, versioning, provenance, and release
  automation.
