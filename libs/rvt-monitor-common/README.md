# RVT shared monitor packages

This repository builds the shared .NET 10 package train used by RVT monitor applications:

- `Rvt.Monitor.Common` contains shared monitor contracts, data helpers, storage, hosting, scheduling, observability, and the compatibility runtime.
- `Rvt.Monitor.Common.Infrastructure` contains provider adapters, configuration validation, and infrastructure composition.
- `Rvt.Monitor.IntegrationTesting` contains PostgreSQL integration-test fixture support and is intended only for test projects.

All three packages require .NET 10 and are released together at one exact version. Consumers must pin that exact version rather than using floating versions or ranges.

## Local development

```bash
dotnet restore rvt-common.sln --use-lock-file
dotnet build rvt-common.sln -c Release --no-restore --nologo
dotnet test rvt-common.sln -c Release --no-build --nologo
dotnet pack src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj -c Release --no-build --no-restore --nologo
dotnet pack src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj -c Release --no-build --no-restore --nologo
dotnet pack testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj -c Release --no-build --no-restore --nologo
```

GitHub Packages authentication is supplied only at runtime. Do not store credentials in this repository:

```bash
export GITHUB_USER="your-github-user"
export GITHUB_PACKAGES_TOKEN="your-runtime-token"
export NuGetPackageSourceCredentials_rvt="Username=$GITHUB_USER;Password=$GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
```

## Releases and migrations

Release versions are immutable. Release candidates and stable versions are published as synchronized three-package trains; a correction receives a new SemVer version and is never overwritten. Consumer repositories update their exact pins and verify staging independently before deployment or promotion.

Database migration ownership remains with the designated application or shared-schema migration authority. Publishing these packages does not apply migrations; the migration owner must provide forward and rollback artifacts and coordinate their application before dependent runtime changes are enabled.
