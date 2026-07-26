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

Every internal consumer builds these projects through direct
`ProjectReference` entries. The projects declare `IsPackable=false`; package
validation, local package feeds, and package-release automation have been
removed. NuGet restore is retained only for third-party dependencies.

## Releases and migrations

Shared-library changes ship as part of the monorepo commit and the applications
that reference them. There is no independent RVT Common package release train.
Reintroducing independently versioned distribution requires a new architecture
decision and a separate package pipeline.

Database migration ownership remains with the designated application or
shared-schema migration authority. A source change does not apply migrations;
the migration owner must provide forward and rollback artifacts and coordinate
their application before dependent runtime changes are enabled.
