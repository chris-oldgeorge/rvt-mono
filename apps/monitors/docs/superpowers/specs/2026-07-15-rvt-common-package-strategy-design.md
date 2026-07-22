# RVT Common Source and Package Strategy Design

**Status:** Approved design; implementation planning authorized, implementation not started
**Date:** 2026-07-15; updated 2026-07-16
**Target source repository:** `RVT-Group-LTD/rvt-reporting`
**Package registry:** GitHub Packages under `RVT-Group-LTD`
**Consumer framework:** .NET 10

## Context

The monitor solution contains independently deployed monitor containers that currently reference `rvt-monitor-common/Rvt.Monitor.Common` and `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure` through project references. Monitor test projects also reference `rvt-monitor-common/Rvt.Monitor.IntegrationTesting`. The separately maintained portal repository uses common functionality and may use infrastructure and shared entities. The repositories and their production release cadences must remain independent.

`Rvt.Monitor.Common` already declares basic NuGet metadata, but it remains source-owned by the monitor repository and is not released through a package pipeline. It combines many concerns and carries dependencies for EF Core, PostgreSQL, SQL Server, Azure Blob Storage, AWS S3, MQTT, OpenTelemetry, Quartz, and ASP.NET Core. Provider-specific communications adapters and their dependencies, including SendGrid and Microsoft Graph support, now live in the separate `Rvt.Monitor.Common.Infrastructure` project. Publishing these current assemblies unchanged is useful as a compatibility bridge but is not the desired permanent package boundary.

## Goals

- Maintain one authoritative common source repository.
- Let the portal and each container adopt common releases independently.
- Make the exact common version in every build and deployment observable and reproducible.
- Prevent source copies, Git submodules, floating package versions, and consumer-local patches.
- Preserve public APIs, configuration keys, entity mappings, and runtime behavior during extraction.
- Establish enforceable compatibility, release, rollback, security, and database-migration rules.
- Reduce unnecessary transitive dependencies through capability-based packages after the compatibility cutover.

## Non-goals

- Moving the portal and monitor applications into one repository.
- Publishing RVT packages publicly.
- Merging `Rvt.Monitor.Common.Infrastructure` back into `Rvt.Monitor.Common` or otherwise changing the current project boundary during initial extraction.
- Splitting the current common assemblies into the future capability packages during the initial extraction.
- Changing monitor or portal behavior during the initial migration.
- Changing database schemas during the initial migration.
- Allowing application containers to apply shared migrations concurrently at startup.
- Executing the repository extraction, publishing packages, changing consumer references, or deploying packages while updating this design and its implementation plan.

## Approaches Considered

### Dedicated common repository with private NuGet packages — selected

This provides one source of truth, immutable artifacts, explicit version contracts, independent consumer releases, and a direct route to package-level compatibility validation.

### Publish common from the monitor repository

This reduces initial setup but leaves the monitor repository as the owner of portal dependencies. Monitor builds would continue testing project references while the portal consumes packages, creating artifact-path drift.

### Git submodules or subtrees

This shares source but introduces fragile checkout, revision, CI, and developer workflows. It also fails to provide an explicit binary and API compatibility contract between independently deployed applications.

## Target Architecture

### Source ownership

`RVT-Group-LTD/rvt-reporting` is the only authoritative production source for common packages. The repository name is a hosting identity only; the extracted solution remains `rvt-common.sln`, and the package IDs and namespaces remain `Rvt.Monitor.*`. Consumer repositories contain only package references, configuration, adapter code that is specific to the consumer, and tests of their use of common contracts.

The common repository owns:

- Common public APIs and implementation.
- Package tests and package-validation baselines.
- Package versioning and release notes.
- GitHub Packages publication.
- Shared schema artifacts and compatibility metadata when entity sharing is confirmed.

Consumer repositories own:

- Their selected exact package versions.
- Application-specific composition and configuration.
- Consumer-specific adapters and business behavior.
- Container builds and deployments.
- Consumer integration and regression tests.

### Initial compatibility packages

The first release preserves the current assemblies, namespaces, and dependency direction:

- `Rvt.Monitor.Common`
- `Rvt.Monitor.Common.Infrastructure`, which depends on the matching `Rvt.Monitor.Common` version
- `Rvt.Monitor.IntegrationTesting`, referenced only by test projects with `PrivateAssets="all"`

All three packages use one synchronized version. The first candidate version is `0.2.0-rc.1`, followed by `0.2.0` after monitor and portal staging validation. A prerelease avoids confusing the extracted artifacts with the existing source-declared `0.1.0` version. Consumer repositories pin direct references to exact versions; `Rvt.Monitor.Common.Infrastructure` may not depend on a different common version.

| Package | Initial responsibility | Consumer rule |
| --- | --- | --- |
| `Rvt.Monitor.Common` | Existing provider-neutral contracts, compatibility APIs, data helpers, storage, hosting, scheduling, observability, and shared runtime behavior | Runtime consumers reference the exact synchronized version |
| `Rvt.Monitor.Common.Infrastructure` | Existing provider adapters, configuration validation, and infrastructure composition that currently depend on Common | Only consumers that compose these adapters reference it, at the exact synchronized version |
| `Rvt.Monitor.IntegrationTesting` | Shared PostgreSQL fixture and test support | Test projects only; always `PrivateAssets="all"` |

The common repository also owns `Rvt.Monitor.Common.InfrastructureTests` and `Rvt.Monitor.IntegrationTesting.Tests`, but test projects are not published.

### Target package family

After the compatibility package is stable, extract capability boundaries incrementally:

- `Rvt.Common.Contracts`
  - DTOs, enums, value objects, interfaces, and shared rules.
  - No EF Core, ASP.NET Core, storage-provider, messaging-provider, or scheduler dependency.
- `Rvt.Common.EntityFrameworkCore`
  - Shared entities, provider-neutral mappings, schema constants, and EF conventions when both systems genuinely share them.
- `Rvt.Common.Infrastructure`
  - Shared configuration, notification coordination, and reusable infrastructure abstractions and implementations that are not monitor-host specific.
- `Rvt.Common.Storage`
  - Storage contracts plus Local, Azure Blob, and S3 adapters. Provider packages may be split later if dependency weight or consumer needs justify it.
- `Rvt.Monitor.Hosting`
  - `MonitorHost`, one-shot execution, Quartz scheduling, monitor runtime defaults, and monitor-specific startup conventions.

Packages remain in one repository and initially use one synchronized version train. `Rvt.Monitor.Common` remains a compatibility facade or metapackage during incremental consumer migration.

## Migration Workflow

### Phase 0 — Baseline and short structural freeze

1. Record the exact source commit used for extraction.
2. Inventory every public type consumed by the monitor solution and portal.
3. Classify usage as contracts, entities, hosting, storage, messaging, observability, configuration, or test infrastructure.
4. Record successful builds, tests, and container builds for each consumer.
5. Freeze structural changes to common code during extraction. Urgent fixes are applied to both locations until cutover completes.

No namespace, API, behavior, configuration, mapping, or schema change is allowed in this phase.

### Phase 1 — Create the authoritative repository

Preserve relevant Git history rather than copying only a source snapshot. Use this initial structure:

```text
rvt-reporting/
├── src/
│   ├── Rvt.Monitor.Common/
│   └── Rvt.Monitor.Common.Infrastructure/
├── tests/
│   ├── Rvt.Monitor.CommonTests/
│   └── Rvt.Monitor.Common.InfrastructureTests/
├── testing/
│   ├── Rvt.Monitor.IntegrationTesting/
│   └── Rvt.Monitor.IntegrationTesting.Tests/
├── Directory.Build.props
├── Directory.Packages.props
├── NuGet.config
└── rvt-common.sln
```

Configure branch protection, required checks, scoped `CODEOWNERS`, package metadata, deterministic builds, repository/source metadata, symbols, and package artifacts. Keep source-project references within the authoritative repository so local common builds test one source graph; validate the generated package dependency graph independently. Do not merge or split package boundaries in this phase.

### Phase 2 — Produce the release candidate

The common pipeline restores, builds, tests, packs all three compatibility packages, inspects package content and dependencies, installs the generated packages into temporary runtime and test consumers, and builds those consumers without source project references. Package validation must prove that Infrastructure resolves the synchronized Common version and that IntegrationTesting remains isolated to test consumers. The release workflow then publishes immutable `0.2.0-rc.1` packages to GitHub Packages.

Grant the monitor and portal repositories read access. CI publication uses `GITHUB_TOKEN`; developers use locally stored read credentials. Tokens and clear-text credentials are never committed.

### Phase 3 — Configure consumer package access

Each consumer receives:

- A credential-free `NuGet.config` with GitHub Packages and NuGet.org sources.
- Package-source mapping that resolves `Rvt.*` only from GitHub Packages and public dependencies from NuGet.org.
- `Directory.Packages.props` with central package management and exact versions.
- GitHub Actions read access to the RVT packages.
- Documented developer authentication using a locally stored `read:packages` credential.

### Phase 4 — Migrate the monitor solution

1. Add the RC source and exact version.
2. Replace runtime `Rvt.Monitor.Common` project references with exact package references.
3. Replace runtime `Rvt.Monitor.Common.Infrastructure` project references with exact package references only where the infrastructure adapters are composed.
4. Replace integration-testing project references in test projects with `PrivateAssets="all"` package references.
5. Build and test every monitor.
6. Build every production container.
7. Exercise one-shot, Quartz, API, health, storage, messaging, and database paths in staging.
8. Verify all runtime common assembly versions in published container output.
9. Remove `rvt-monitor-common/` only after all common and integration-testing project references are gone.
10. Add architecture enforcement that prevents local common source, projects, or conditional source/package switches from returning.

Reference conversion and source removal are separate commits so the cutover can be reviewed and reverted independently.

### Phase 5 — Migrate the portal

1. Inventory portal usage of common contracts, infrastructure adapters, and entities.
2. Replace existing source or binary references with the required RC compatibility packages at exact synchronized versions.
3. Compile all portal projects and run unit, API, authorization, persistence, background-work, and UI integration tests.
4. Compare EF model metadata if shared entities are consumed.
5. Build and deploy a staging image.
6. Exercise database, notification, storage, and background-processing workflows.
7. Confirm that the package-only change introduced no schema, configuration, serialization, or runtime difference.

### Phase 6 — Promote the compatibility release

After both consumers pass staging, publish `0.2.0` from the same accepted source state. Consumer repositories update through independent pull requests. Prerelease packages remain immutable and are not overwritten or deleted.

### Phase 7 — Split package boundaries

Extract one capability boundary per release after compatibility adoption. Consumers first move to additive APIs; old APIs remain available through the compatibility package until usage is removed. Breaking removals wait for a future major version.

Before extracting entities, nominate one migration authority and document schema ownership and deployment ordering.

## Release Workflow

### Repository governance

Protect `main` with required pull requests, required CI, no direct or force pushes, and scoped ownership. Contract/entity changes require portal-aware review; monitor-hosting and ingestion changes require monitor-aware review.

Consumers never patch common source locally. A required fix begins in `rvt-reporting`, is released, and is adopted as a package update.

### Semantic versioning

- Patch: backward-compatible reliability, performance, security, or defect correction.
- Minor: additive backward-compatible API or capability.
- Major: removed/renamed API, incompatible signature or behavior, or incompatible schema expectation.
- Prerelease: immutable release candidate for cross-repository validation.

All compatibility packages initially share a version. Production consumers pin exact stable versions and never use floating ranges. CI rejects a consumer that references mismatched `Rvt.Monitor.Common` and `Rvt.Monitor.Common.Infrastructure` versions.
### Pull-request validation

Every common PR runs:

1. Locked restore.
2. Formatting and static analysis.
3. Release build.
4. Unit and provider integration tests.
5. Package creation without publication.
6. Package-content and dependency inspection.
7. Temporary-consumer restore and compile from `.nupkg` artifacts.
8. API/package compatibility validation against the latest stable release.
9. Dependency vulnerability and license checks.
10. Consumer smoke compilation for hosting changes.

Repository-local architecture tests inspect only source and projects owned by `RVT-Group-LTD/rvt-reporting`. Guards for ReportingMonitor, MyATM, Omnidots, or other consumer code remain in `rvt-monitors` and must survive removal of the old common source tree. Extracted tests resolve the standalone repository through `.git`, use root-relative `src/` and `database/` paths, and preserve the ignored runtime integration-settings copy convention without tracking a connection value.

PR packages remain workflow artifacts. Permanent GitHub package versions are created only by explicit release workflows.

### Release candidates

Public contract, entity, mapping, DI, configuration, serialization, hosting, scheduling, storage, messaging, or notification changes require an RC. RC package-update PRs run full monitor and portal CI, container builds, and staging validation. A rejected RC is replaced with the next immutable RC number.

### Stable publication

A protected `vX.Y.Z` tag on `main` triggers publication. The workflow rebuilds and retests from the tag, verifies version/tag alignment and nonexistence of the package version, generates packages, symbols, checksums, dependency metadata, and an SBOM, publishes with `GITHUB_TOKEN`, and creates GitHub release notes linked to the source commit.

### Consumer adoption

Automation opens a version-update PR in each consumer repository. It never commits directly or deploys automatically. Consumer CI builds and tests the application and its container. Each product chooses its own staging and production deployment time.

Publishing a common package never redeploys a consumer by itself.

### Deployment inventory

Applications record application SHA, container digest, common package version, schema compatibility version, and deployment timestamp in startup telemetry and deployment metadata. Internal version endpoints may expose only non-sensitive version data.

An automated inventory reports the Common and Infrastructure versions used by portal production, each monitor container, staging deployments, and consumer default branches. A deployment with mismatched runtime package versions is invalid.

## Compatibility Policy

- Patch releases remain binary and behaviorally compatible.
- Minor releases add APIs without removing or renaming existing members.
- Deprecated APIs remain for at least two minor releases and 90 days.
- Breaking changes require a major version and approved migration design.
- Consumers remain no more than one minor release behind unless explicitly excepted.
- Security-critical releases define a mandatory adoption deadline.

Cross-repository features use additive sequencing: publish new API, update consumers, adopt the API, verify old usage is gone, and remove only in a later major release. Coordinated same-minute deployments are not required.

## Entity and Database Policy

Shared entity packages may contain entity definitions, provider-neutral mappings, schema constants, and compatibility metadata. One designated migration authority owns migration generation and application. Monitor and portal containers do not independently apply shared migrations at startup.

Schema changes use expand-and-contract delivery:

1. Add compatible nullable structures or indexes.
2. Apply the migration through the designated authority.
3. Release code compatible with the transition state.
4. Upgrade consumers independently.
5. Confirm old application versions are retired.
6. Remove obsolete schema only in a later release.

Release notes state minimum schema version, migration requirement, deployment order, supported overlap, and rollback limitations.

## Hotfix and Rollback Workflow

An emergency fix branches from the affected release tag, applies the smallest compatible patch, passes full patch validation, merges forward to `main`, and publishes a new patch version. Affected consumers receive urgent update PRs and rebuilt container images. Published versions are never overwritten.

Normal rollback pins the previous stable package, runs consumer CI, rebuilds the container, deploys the verified digest, and confirms runtime version metadata. Database-affecting rollback is permitted only while the expanded schema remains backward-compatible; otherwise prefer a forward corrective patch.

Compromised-package response includes credential revocation, prevention of new affected restores where necessary, publication of a clean replacement, rebuild/redeployment of affected consumers, credential rotation, and preserved audit evidence.

## Local Development

Normal consumer development uses published packages, matching CI and production. Common-repository development uses its internal source-project graph and validates generated packages through package-only temporary consumers. Coordinated common-plus-consumer work uses a temporary local NuGet feed and one unique synchronized prerelease version for all three packages. Local feed paths and credentials are not committed. Team and CI validation use a published RC.

Permanent conditional project-reference switches are prohibited because they cause local builds to test source while CI and production use different package artifacts.

## Acceptance Criteria

- Common production source exists only in `RVT-Group-LTD/rvt-reporting`.
- Consumer repositories contain no copied common source or common Git submodule.
- `Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and `Rvt.Monitor.IntegrationTesting` are published from one protected source state with synchronized versions.
- All consumers use exact package references and restore `Rvt.*` from GitHub Packages; test support is always private to test projects.
- Common packages are immutable and traceable to a protected source tag.
- API compatibility is validated against the latest stable baseline.
- Portal and monitor deployments remain independent.
- Runtime inventory identifies the Common and Infrastructure package versions in every production container.
- One authority owns shared database migrations.
- Initial extraction changes no public behavior, configuration, mapping, or schema.
- Rollback to the previous stable package is tested before production cutover.

## Risks and Mitigations

- **Large transitive dependency surface:** use the current assembly only as a compatibility bridge, then extract capability packages.
- **Infrastructure/Common version mismatch:** publish from one version train, require matching direct versions, inspect the generated dependency graph, and reject mismatches in consumer CI.
- **Portal/monitor API breakage:** baseline package validation, RC consumer PRs, and additive sequencing.
- **Private-feed authentication failures:** explicit repository package access, `GITHUB_TOKEN` in Actions, local credential-store setup, and no committed secrets.
- **Consumer version drift:** exact central versions, automated update PRs, and deployed-version inventory.
- **Schema races:** one migration authority and no container-startup migration ownership.
- **Big-bang migration risk:** compatibility-first extraction and separate consumer cutovers.
- **Local/package behavior drift:** package references by default and local NuGet artifacts for coordinated work.

## References

- [GitHub Packages NuGet registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
- [GitHub Packages permissions](https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages)
- [Publishing and installing packages with GitHub Actions](https://docs.github.com/en/packages/managing-github-packages-using-github-actions-workflows/publishing-and-installing-a-package-with-github-actions)
- [NuGet Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [NuGet Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [.NET package validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview)
- [.NET baseline package validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator)
