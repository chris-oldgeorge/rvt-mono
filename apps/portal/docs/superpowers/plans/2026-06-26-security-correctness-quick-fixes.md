# Security Correctness Quick Fixes - 2026-06-26

## Scope

This note records the quick security and correctness fixes implemented on `review-improvements` after review validation of repository disposal, local secret handling, installer object authorization, and admin email link origin handling.

## Findings Addressed

- `GenericRepository` disposed DI-owned scoped `DbContext` instances. The repository no longer implements `IDisposable` and no longer calls `Context.Dispose()`.
- Local development settings were easy to re-commit. `**/appsettings.Development.json` is now ignored and the README documents that real local values belong in user secrets, environment variables, or untracked overrides.
- Installer endpoints could load monitors or deployments by identifier without company scoping. Installer inventory, monitor detail, status, and deployment update routes are now limited to the installer's assigned company.
- Admin-created reset and confirmation links could inherit request host data. Admin link generation now prefers `Spa:PublicBaseUrl` before falling back to request origin for local development.

## Evidence

- Red guardrails failed before implementation for the repository disposal, `.gitignore`, and installer cross-company access checks.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~CqrsArchitectureTests.GenericRepository_DoesNotUseDirtyReadsOrReflectionMapping|FullyQualifiedName~CqrsArchitectureTests.SecuritySensitiveLocalSettingsAndEmailLinks_AreGuarded" --logger "console;verbosity=minimal"` passed `2/2`.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --filter "FullyQualifiedName~MonitorWorkflowTests" --logger "console;verbosity=minimal"` passed `15/15`.
- `dotnet build RvtPortal.Spa/RvtPortal.Spa.csproj --configuration Release --no-restore` passed with `0` warnings/errors.
- `npm run test:run -- src/App.test.tsx` passed `43/43`.
- `npm run lint` passed.
- `npm run build` passed with only the existing Vite large-chunk advisory.
- `git diff --check` passed.

## Remaining External Remediation

- Rotate the historical SQL Server `sa` password and purge the old value from git history. This patch prevents easy re-addition of local development settings, but history rewrite and credential rotation remain external operational actions.
- A full backend test run still has one unrelated dirty-worktree failure: `CqrsArchitectureTests.ReportRuleRecipientQueries_AreRoutedThroughReader` expects the in-progress report recipient reader wiring to be finished.
