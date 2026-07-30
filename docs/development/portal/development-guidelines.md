# Development guidelines

Rules distilled from recent work on this repository. Each one exists because
something broke, or nearly did. Where a rule is enforced by a test, the test is
named — if you disagree with a rule, change the guard deliberately rather than
working around it.

## Dates and time zones

PostgreSQL and Npgsql are strict about `DateTime.Kind`, and the schema uses two
column types with **opposite** requirements. There is no single "always UTC"
rule — it depends on the column.

| Column type | Where | Npgsql requires on write | Reads back as |
| --- | --- | --- | --- |
| `timestamp with time zone` (timestamptz) | domain context (`RVTDbContext`) | `Kind == Utc` | `Utc` |
| `timestamp without time zone` | search/telemetry (`RVTSearchContext`) | `Kind == Unspecified` | `Unspecified` |

- **The domain layer stores UTC.** Use `DateTime.UtcNow` or
  `IRvtDateTimeProvider.UtcNow`. Never `DateTime.Now` / `DateTime.Today` for a
  value that will be persisted — `Kind=Local` throws on a timestamptz column.
- **The ingestion/telemetry layer stores `Unspecified`** wall-clock UTC. A
  `Kind=Utc` value thrown at a plain `timestamp` column throws too.
- **Query parameters count.** A filter bound compared against a timestamptz
  column must also be `Kind=Utc`. Two idioms, pick by intent:
  - *local-day* semantics → convert the local calendar day with
    `.LocalToUtc(dateTimeProvider)` (see `DashboardBreachApplicationService`,
    `MonitorData`).
  - *UTC-day* semantics → `DateTime.SpecifyKind(bound, DateTimeKind.Utc)` to fix
    the label without shifting ticks (see `GetCalendarMonthAsync`).
- **Never "fix" Kind with a coercing value converter.** Relabelling a local
  `14:00` as `14:00Z` stores the wrong instant silently. Failing loudly is the
  correct behaviour — that is what `UtcTimestampGuardInterceptor` does.
- Convert to local **only for display**, through `IRvtDateTimeProvider`
  (`UtcToLocal` / `DisplayUtcAsLocal`). Never persist a local value.

Enforced by: `UtcTimestampGuardInterceptor` (throws at `SaveChanges` on a non-UTC
value bound for a timestamptz column), `UtcTimestampGuardTests`,
`CqrsArchitectureTests.DateExtensions_RunsNoTypeLoadCodeAndConvertsThroughInjectedProvider`.

## EF Core, schema, and migrations

- **Three contexts, three chains, one database.** `RVTDbContext` (domain),
  `RVTSearchContext` (time-series) and `ApplicationDbContext` (Identity) map
  disjoint halves and each keeps its **own** migrations-history table. Sharing one
  would make each context read the others' migrations as its own. See
  `docs/database/portal/ef-migrations.md`.
- **The EF migrations do not build the whole database.** Ingestion tables, the
  columns no model maps, and the Timescale views/hypertables live in
  `database/postgres/` and are applied by `RVT.SchemaDeploy`.
- **A column that no EF model maps must never be `NOT NULL` without a database
  default.** EF does not know the column exists, so it omits it from every
  `INSERT` and PostgreSQL rejects the row (`23502`). Give it a `DEFAULT`, or map
  it onto the entity and populate it.
- **`ADD COLUMN IF NOT EXISTS` is a no-op on a column that already exists** — it
  will not add a default to one. Schema changes that must reach existing
  databases need a separate, idempotent repair script (see
  `database/postgres/restore_unmapped_column_defaults.sql`) plus a rollback.
- Prefer a database default over widening the portal's write surface when the
  column is owned by ingestion rather than the portal.

Enforced by: `UnmappedColumnDefaultTests` (fails the build if any unmapped
`ADD COLUMN` is `NOT NULL` without a default), `SchemaValidatorTests`,
`SchemaDeployTests`, `CutoverReadinessTests`.

## Persistence boundaries

- **Data-access code never commits.** Nothing under `RVT.DataAccess` may call
  `SaveChanges`; a use case touching two repositories could otherwise
  half-commit. Persistence is owned solely by the unit of work / transaction
  pipeline.
- **Repositories stage; the unit of work commits.** Persistence ports expose
  reads; they do not expose `AddAsync` / `UpdateAsync` / `DeleteAsync`.
- **Mutating application commands are transactional**, and controllers delegate
  to application services rather than depending on `IMediator` directly.

Enforced by: `DataAccessWriteBoundaryTests`, `CqrsArchitectureTests`
(`MutatingApplicationCommands_AreTransactional`, `ApiControllers_DoNotDependOnMediator`,
and the per-controller delegation cases).

### Help content invariants

- **Help assets are URL metadata, not uploads.** Mutations accept only an
  absolute HTTPS URL or a root-relative `/help-assets/` path. The server derives
  `InternalPath`; clients must never submit it.
- **Persisted Help asset IDs are immutable.** Updates edit matching assets in
  place, create server IDs for new rows, remove omitted rows, and reject foreign
  IDs before staging any change.
- **Editable React rows need immutable UI identity.** Use the persisted asset ID
  or a client-only random key. Never key an editable row by its title, value, or
  array index; preserve and restore focus through ID/key-addressed refs.
- **Release requires clean persisted URLs under the application policy.** The
  retired SQL artifact was removed. The shared BCL-only `HelpAssetUrlPolicy`
  and `RVT.ReleaseAudit help-asset-urls`, which reuses it through a read-only
  audit-row reader, are the sole policy/audit authority. Every release database
  must produce a complete zero-finding receipt before Help Admin can become
  `READY`; an exit `10`, `2`, `3`, or missing receipt blocks release.

Enforced by: `HelpApplicationServiceTests`, `HelpAdapterTests`,
`HelpCmsOperationsTests`, `HelpAssetUrlPolicyTests`, shared-corpus mutation
tests, `HelpAssetUrlAuditTests`, the opt-in PostgreSQL audit-row-reader test,
`HelpAdminPanel.test.tsx`, and `help-admin.spec.ts`.

## Ports and adapters

- **External integrations sit behind a transport-neutral port** declared in the
  application core — the `RvtPortal.Application` assembly, whose `Ports/` trees
  are BCL-only — with the concrete client as an adapter in the host project's
  `Adapters/` tree. Current examples: `IVibrationVendorGateway` (Omnidots) and
  `IEmailDelivery` (email delivery).
- **The application core stays transport-neutral.** It must not depend on
  `IHttpClientFactory`, an ASP.NET Core type, or any vendor SDK. If application
  code needs to call out, add a port and put the transport in an adapter. The
  rule is about the boundary, not about a folder name: whatever the use-case
  folders are called, the assembly holding them is the one that must stay clean.
- `RVT.BusinessLogic` no longer exists — it dissolved into
  `RvtPortal.Application`. Text or code that still routes new ports there is
  stale.

Enforced by: `CqrsArchitectureTests.ApplicationCoreTypes_DoNotDependOnHttpClientFactory`,
`CqrsArchitectureTests.ApplicationCoreAssembly_DoesNotReferenceSendGrid`, and the
[ports and adapters catalog](../../architecture/portal/ports-and-adapters-catalog.md).

## Shared-package boundary

- **The portal remains a zero-package non-consumer of the RVT monitor common
  package family.** Do not add `Rvt.Monitor.Common`,
  `Rvt.Monitor.Common.Infrastructure`, or `Rvt.Monitor.IntegrationTesting`
  package/project/namespace dependencies without a separately approved
  compatibility design and package-access review.
- Keep root `NuGet.config` public-only. Do not add a GitHub Packages source,
  package credential, or `packages: read` workflow permission for the current
  portal architecture.
- Portal-owned EF contexts, migrations, domain contracts, and adapters remain
  independent. Shared behavior may be adopted later only through an explicit
  boundary design.

Enforced by: `RvtCommonDependencyBoundaryTests` and
`RepositoryLayoutTests`.

## Testing

- **The host suite runs on a real database.** `SpaTestApplicationFactory` boots
  the real Spa host against a throwaway PostgreSQL schema on the shared
  integration database, so the production wiring runs unchanged: three EF
  contexts on one scoped Npgsql connection, real transactions, raw SQL and
  `ExecuteUpdate` paths, and startup schema validation against a live
  information schema. The schema is dropped with the factory, so those tests
  need no manual cleanup.
- **Know what the remaining in-memory tests cannot see.** Narrow unit tests
  still use the EF InMemory and SQLite providers, which build their schema
  *from the model*. They are structurally blind to:
  - anything the model does not map (an unmapped `NOT NULL` column simply is not
    in the test database, so the insert succeeds), and
  - `DateTime.Kind` (both providers ignore it).
  A green in-memory test is not evidence that a schema or Kind bug is absent;
  move the assertion onto the PostgreSQL-backed host or an opt-in adapter test.
- **Gate every test that needs the real database.** Use
  `RequiresPostgresFactAttribute` / `RequiresPostgresTheoryAttribute`, which
  read `RVT__POSTGRES_INTEGRATION_CONNECTION` at discovery time. CI provisions a
  TimescaleDB service container and sets that variable on every run, so these
  tests *execute* in CI — the skip exists for a developer machine without the
  container, and that is the only place a skip is expected. Export the same
  variable locally to run what CI runs. An opt-in
  test that writes outside a factory-owned schema must wrap its work in a
  transaction and roll back so it leaves no rows behind.
- **Guard the invariant statically where you can**, so CI fails rather than
  production. This repo prefers tests that read the shipped SQL/source over
  documentation nobody re-reads (`UnmappedColumnDefaultTests`,
  `CutoverReadinessTests`, `DataAccessWriteBoundaryTests`).
- **Round-trip a new guard.** Prove it fails without the fix and passes with it;
  a guard that cannot fail is not a guard.
- **Provider translation is a real failure mode.** `ToQueryString()` compiles a
  query against a provider without a connection — use it to prove a projection
  translates (`MonitorListReaderSqlTests`, `MonitorOwnershipWindowSqlTests`).

## Configuration and secrets

- **Never commit a real credential.** `appsettings.json` carries blank
  placeholders that document the *shape* only. Real values come from .NET
  user-secrets in development and the deployment secret store (environment /
  Key Vault) in production. `appsettings.Development.json` is gitignored and must
  stay local-only.
- **A new integration is not done until its keys are wired in.** Add them to
  `docs/deploy/set-dev-secrets.ps1` (behind a `-Configure*` switch, and to
  `-ConfigureAll`) and describe them in `docs/operations/portal/dev-secrets-reference.md`.
  Keys added to `appsettings.json` but not to the script are the drift to avoid.
- Document what a key *does* and whether it is sensitive — never its value.
- **Account-action links never derive their origin from the request.** Configure
  `Spa:PublicBaseUrl` as an absolute HTTPS URI in every deployed environment and
  keep `AllowedHosts` non-wildcard with that exact host. The checked-in
  `AllowedHosts` values are local-only; deployment must override both settings.
- **Confirmed-account email edits are pending until the requested address
  confirms them.** Self-service and admin-managed edits must leave the current
  confirmed `Email`, `UserName`, and `EmailConfirmed` values unchanged while
  applying independent profile fields. Confirmation must update email and
  username inside one Identity database transaction; failed results and
  exceptions roll back so the same confirmation token can be retried.
- **An unconfirmed invitation may replace its destination, but it must restart
  onboarding.** Update the unconfirmed email and username together, keep
  `EmailConfirmed = false`, rotate the security stamp to invalidate every old
  confirmation link, and send the normal password-set/confirmation message to
  the replacement address. Do not send a change-email link or make password
  reset/login available before the new recipient confirms.
- **Forwarded headers require an explicit immediate-peer allowlist.** Populate
  `ForwardedHeaders:KnownProxies` and/or `ForwardedHeaders:KnownNetworks` for the
  deployed proxy topology. The host clears framework defaults, accepts only
  forwarded client IP and scheme, and processes them before redirects, rate
  limiting, authentication, and CSRF checks. Never enable forwarded-host trust
  for account-link generation.

Enforced by: `SpaHostSmokeTests.ProductionHost_WithoutPublicBaseUrl_FailsConfigurationValidation`
and the public-origin, pending-email, transactional rollback/retry, invitation
replacement, and forwarded-header cases
in `SecurityHardeningTests`.

## Documentation and client releases

- **Internal design and planning documents never ship to the client.** The
  sanitized export (`scripts/export-client-release.sh`) copies only git-tracked
  files and applies `docs/release/client-release-exclusions.txt`;
  `scripts/verify-client-release.sh` re-checks the result against the same
  policy. When you add an internal docs folder, add it to that exclusion list in
  the same change.
- **`docs/release/**` is excluded wholesale**, so the release evidence
  (`PARITY_MATRIX`, `CUTOVER_RUNBOOK`, `FUNCTIONALITY_READINESS_MATRIX`) is
  *internal* evidence and does not ship. What ships to an operator is the module
  READMEs and deploy documentation outside that tree. Keep the two categories
  distinct, and check the policy rather than assuming.
- The exclusion format has **no negation**: you cannot exclude a folder and
  re-include one file. If one file in a folder must ship, list the excluded files
  individually rather than excluding the folder — for example
  `docs/development/portal/sonar/` must keep shipping
  `globalization-suppressions.md`, which code `[SuppressMessage]` justifications
  point at.
- **Record deliberate non-changes.** When something looks like a bug but is a
  product decision, mark it as a design decision at the code, with what changing
  it would take — see the calendar time-zone note in
  `DashboardApplicationService.GetCalendarMonthAsync`.

## Working on a macOS/SMB checkout

The repository is often mounted over SMB, where macOS writes AppleDouble sidecar
files (`._*`) next to real files. They match `*.sql` / `*.dll` globs and break
builds, script runners, and file-globbing tests. The tooling already skips them
(`RVT.SchemaDeploy`'s script runner, the `.csproj` content globs, the release
exporter). If a build or test fails with "bad image" or an unexpected `._`
filename, delete them and retry rather than changing the code:

```bash
find . -path ./.git -prune -o -name '._*' -delete
```
