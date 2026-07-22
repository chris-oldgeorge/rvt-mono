# RVT Portal SPA

## Overview

RVT Portal SPA is the clean single-page application repository for the RVT monitoring portal. The active artifact is `RvtPortal.Spa`, an ASP.NET Core host that owns Identity, API endpoints, Swagger, static SPA serving, and the publish boundary.

Architecture boundaries:

- API host: `RvtPortal.Spa` exposes `/api/*`, auth, health, SPA fallback, and the publish target that copies the client build into `wwwroot`.
- SPA client: `RvtPortal.Client` is the React/Vite application served by the API host in production and by Vite during local development.
- Shared class libraries: `RVT.BusinessLogic`, `RVT.DataAccess`, `RVT.Entities`, and `RVT.Utilities` retain the migrated domain services, repositories, EF models, DTO/entity types, email/blob helpers, and shared utility code.
- Data access: `ApplicationDbContext`, `RVTDbContext`, and `RVTSearchContext` use the configured `ConnectionStrings:DefaultConnection`; no local credentials are committed.
- Tests: `RvtPortal.Spa.Tests` covers API/host behavior, while client lint, build, Vitest, and Playwright gates run from `RvtPortal.Client`.
- Release boundary: deploy `RvtPortal.Spa` as the single cutover artifact; retired MVC/demo projects and migration-control tooling stay outside this repository.

## Repository Layout

- `RvtPortal.Spa` - ASP.NET Core API host and SPA integration point.
- `RvtPortal.Client` - React/Vite SPA client application.
- `RvtPortal.Spa.Tests` - Automated tests for portal API and host behavior.
- `RVT.BusinessLogic` - Domain services and application workflow logic.
- `RVT.DataAccess` - Data access layer and persistence integration.
- `RVT.Entities` - Shared entity and model definitions.
- `RVT.Utilities` - Shared utility code used across the portal solution.
- `docs/release/PARITY_MATRIX.md` - MVC action/view migration ledger used as release evidence.
- `docs/release/CUTOVER_RUNBOOK.md` - Deployment, smoke, rollback, and go/no-go checklist for the SPA cutover.
- `docs/onboarding/REACT_PORT_ONBOARDING.md` - Short onboarding guide for developers joining the React port.

## Local Development

Install the .NET 10 SDK and Node.js 24.x, then restore dependencies from the repository root.

```bash
dotnet restore RvtPortal.Spa.sln
cd RvtPortal.Client
npm ci
```

Run the API host in one terminal:

```bash
dotnet run --project RvtPortal.Spa/RvtPortal.Spa.csproj --launch-profile https
```

The API listens on `http://localhost:5178` and `https://localhost:5179`.

Run the SPA client in another terminal:

```bash
cd RvtPortal.Client
npm run dev
```

The client runs on Vite, usually `http://localhost:5173`, and proxies API calls to `http://localhost:5178`. To match Visual Studio's strict Vite profile, use `npm run dev:vs`.

Local configuration and secrets:

- Put local database settings in user secrets, environment variables, or an uncommitted local override. Do not commit real values.
- Use `docs/deploy/set-dev-secrets.ps1` to configure the Development user-secrets store for the API host.
- `RvtPortal.Spa/appsettings.json` documents required keys with blank placeholders, including `ConnectionStrings:DefaultConnection`, `ReportGenerationService:BaseUrl`, `ReportGenerationService:InternalApiKey`, `ExternalUrls:OmnidotsAdapterUrl`, `ExternalUrls:OmnidotsAdapterSecret`, `EmailConfiguration:*`, `SmtpServer:*`, `What3Words:ApiKey`, blob storage, Redis, and data-protection settings.
- `RvtPortal.Spa/appsettings.Development.json` is intentionally gitignored and must stay local-only. It may contain sample placeholders, but real connection strings, local passwords, and API keys belong in user secrets, environment variables, or another untracked local override.
- Client API targeting can be overridden with `VITE_RVT_PORTAL_API_URL`; allowed client-side API origins can be extended with `VITE_RVT_PORTAL_ALLOWED_API_HOSTS`.
- Production-only values such as `OmnidotsAdapterSecret`, `What3Words:ApiKey`, SMTP credentials under `SmtpServer`, Redis, Azure Blob, and Key Vault settings must come from the deployment secret store.

## Visual Studio Debugging

Open `RvtPortal.Spa.sln` in Visual Studio. Set `RvtPortal.Spa` as the startup project, select the `https` launch profile, and start debugging.

The project is configured with `SpaProxyServerUrl` `http://localhost:5173` and `SpaProxyLaunchCommand` `npm run dev:vs`. If `RvtPortal.Client/node_modules` is missing in Debug builds, the project runs `npm ci` before launch.

## Test And Build Gates

Run these commands before release handoff:

```bash
dotnet restore RvtPortal.Spa.sln
dotnet build RvtPortal.Spa.sln --configuration Release --no-restore -v minimal
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release -v minimal
dotnet publish RvtPortal.Spa/RvtPortal.Spa.csproj --configuration Release --output artifacts/rvtportal-spa -v minimal
```

Run the client gates from `RvtPortal.Client`:

```bash
npm ci
npm run lint
npm run build
npm run test:run
npm run test:e2e
```

CI runs the same core gates on Windows, publishes the `RvtPortal.Spa` artifact, and then performs SonarCloud analysis after restore/build.

## Release Notes

This clean repository is prepared for the RVT Portal SPA migration stream. The active solution includes only the portal SPA host, SPA client, test project, and supporting RVT class libraries.

Release evidence lives in:

- `docs/release/PARITY_MATRIX.md` - documents migrated, replaced, retired, and deferred MVC actions/views, including legacy demo-only exclusions.
- `docs/release/CUTOVER_RUNBOOK.md` - documents deployment commands, role journey sign-off, data compatibility, rollback, and go/no-go checks.

Legacy exclusions are intentional: `RvtDemo`, `ShardUi`, retired MVC controllers/views, demo/debug routes, and migration-control tooling are not part of the active build or release artifact. Do not reintroduce them without an approved migration exception.
