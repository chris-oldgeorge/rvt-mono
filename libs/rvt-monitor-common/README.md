# RVT shared monitor projects

Detailed shared-library documentation is centralized in the
[repository documentation index](../../docs/index.md#rvt-monitor-common).
The verified communication graph and gate evidence are documented in
[communications.md](../../docs/architecture/rvt-monitor-common/communications.md).

This repository builds the shared .NET 10 runtime, communication, and
integration-test projects used by RVT applications:

- `Rvt.Monitor.Common` contains shared monitor data, hosting, scheduling,
  observability, storage compatibility, and delivery runtime code. It
  references `Rvt.Communication.Abstractions` for retained compatibility
  contracts.
- `Rvt.Communication.Abstractions` owns the provider-neutral email and SMS
  ports, requests, results, failures, and legacy communication contracts.
- `Rvt.Communication` owns the provider-neutral workflow and compatibility
  services and references only `Rvt.Communication.Abstractions`.
- `Rvt.Communication.SendGridMail`,
  `Rvt.Communication.MicrosoftGraphMail`, and
  `Rvt.Communication.TransmitSms` are independent adapters. Each references
  only `Rvt.Communication.Abstractions` from the RVT project graph.
- `Rvt.Monitor.IntegrationTesting` contains PostgreSQL integration-test
  fixture support and is intended only for test projects.

`Rvt.Monitor.Common.Infrastructure` has been removed. Active composition roots
select provider projects directly; no application should add an Infrastructure
reference or treat it as a facade.

## Local development

Use existing restore assets when verifying the source graph:

```bash
dotnet build rvt-common.sln --no-restore --nologo
dotnet test tests/Rvt.Communication.AbstractionsTests/Rvt.Communication.AbstractionsTests.csproj --no-restore --nologo
dotnet test tests/Rvt.CommunicationTests/Rvt.CommunicationTests.csproj --no-restore --nologo
dotnet test tests/Rvt.Communication.SendGridMailTests/Rvt.Communication.SendGridMailTests.csproj --no-restore --nologo
dotnet test tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj --no-restore --nologo
dotnet test tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj --no-restore --nologo
```

The source-level communication split is verified, but packaging is not yet
migrated. Retained monitor and package-validation locks still contain the
removed Infrastructure identity, and the current package-validation assets do
not represent the new package set. Do not publish from those retained assets.

GitHub Packages authentication is supplied only at runtime. Do not store
credentials in this repository:

```bash
export GITHUB_USER="your-github-user"
export GITHUB_PACKAGES_TOKEN="your-runtime-token"
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
```

## Releases and migrations

The dedicated package-release plan must update the full eleven-package pack,
package-consumer, lock, SBOM, vulnerability, release-manifest, and release-asset
pipeline before the split can be released. That plan replaces the obsolete
three-package assumptions and establishes the clean-split release baseline; it
is pending, not complete.

Release versions remain immutable. Consumers must use coordinated exact
versions rather than floating versions or ranges, and a correction receives a
new SemVer version rather than overwriting an existing package.

Database migration ownership remains with the designated application or
shared-schema migration authority. Publishing packages does not apply
migrations; the migration owner must provide forward and rollback artifacts
and coordinate their application before dependent runtime changes are enabled.
