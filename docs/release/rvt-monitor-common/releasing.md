# Shared-library source delivery

RVT Common is delivered as source inside this monorepo. Its runtime,
communication, storage, and integration-testing projects are built through
direct `ProjectReference` entries from their consumers.

## Current contract

- Internal RVT projects declare `IsPackable=false`.
- `scripts/build-mono.sh` restores, builds, and tests `Rvt.Mono.slnx`.
- The build does not create, validate, publish, or restore internal RVT NuGet
  packages.
- NuGet remains available only for third-party dependencies from nuget.org.
- Monitor container builds use the monorepo root as their build context, so
  shared source is present without a package feed or package credential.

## Shipping a shared-library change

1. Change the project under `libs/rvt-monitor-common`.
2. Update all affected consumers in the same branch.
3. Run the source-boundary guards and aggregate build.
4. Merge the reviewed commit and deploy the rebuilt applications through their
   normal release processes.

The source commit is the shared-library version boundary. Applications cannot
select a different internal RVT package version.

## Reconsidering independent distribution

Independent package publishing is intentionally unsupported. If an external
consumer later requires it, make a new architecture decision before adding any
pack or publish step. That decision must define compatibility, versioning,
provenance, vulnerability review, package-consumer tests, credentials, and
release ownership.

## Migration ownership

Source delivery does not apply database migrations. The designated migration
owner must still provide forward and rollback artifacts and coordinate schema
deployment before dependent application changes are enabled.
