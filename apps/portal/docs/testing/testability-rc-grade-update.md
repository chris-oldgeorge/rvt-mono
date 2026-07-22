# Testability RC Grade Update

Date: 2026-06-26

## Purpose

This update addresses two related review concerns:

- SonarCloud coverage was low even though the repository has a large test suite.
- Some tests read as dry implementation guardrails instead of release-candidate behavior evidence.

## Coverage Baseline

Local coverage was generated before the RC testability changes:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release -v minimal --collect:"XPlat Code Coverage" --results-directory /private/tmp/rvt-coverage -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
npm run test:coverage
```

Observed baseline:

| Area | Coverage |
| --- | ---: |
| Backend all counted files | 51.5% line |
| Backend excluding EF migrations and generated projection models | 62.6% line |
| `RvtPortal.Spa` API assembly | 73.3% line |
| Client | 49.1% line / 48.5% branch |

The backend aggregate was dragged down by EF migrations, EF snapshots, generated search/entity projection models, and low-value infrastructure ballast. The SPA API layer already had much stronger coverage than the aggregate suggested.

## Updated Local Evidence

After adding the first RC behavior scenarios and coverage scope guardrail:

| Area | Coverage |
| --- | ---: |
| Backend all counted files | 51.6% line |
| Backend excluding EF migrations and generated projection models | 62.6% line |
| `RvtPortal.Spa` API assembly | 73.4% line |
| Client | 51.7% line / 50.2% branch |

Verification commands:

```bash
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release -v minimal --collect:"XPlat Code Coverage" --results-directory /private/tmp/rvt-coverage-rc -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
npm run test:coverage
npm run lint
npm run build
```

Results:

- Backend xUnit: `236/236` passed.
- Client Vitest: `53/53` passed.
- Client lint: passed.
- Client build: passed with the existing Vite large-chunk advisory.

## Sonar Coverage Scope

The GitHub Actions Sonar scanner now defines `sonar.coverage.exclusions` for generated or non-runtime artifacts:

- `database/sqlserver/**`
- `RVT.DatabaseMigrator/post-load/**`
- `RVT.DataAccess/Migrations/**`
- `RVT.DataAccess/EntityModels/Models/**`
- `RvtPortal.Client/src/api/schema.d.ts`
- `RvtPortal.Client/dist/**`
- `RvtPortal.Client/coverage/**`

These exclusions are intentionally narrower than a blanket project exclusion. Runtime controllers, command handlers, services, client panels, and shared UI code remain counted.

## Test Style Improvements

New tests added in this update are written as release-candidate scenarios:

- A failed transactional command does not commit partial business work.
- Assigning a company user to a site creates default email notification settings.
- A company user can close an alert on their assigned site but cannot close another site's alert.
- Admins can narrow companies through live suggestions and see the filtered result.
- Admins can search available report recipients without losing assigned recipients.

The existing architecture and source guardrails remain useful, but they should be treated as safety rails. The primary confidence story should come from user-visible and business-visible workflows like the scenarios above.

## Follow-Up Candidates

Highest-value next coverage areas:

- `RVT.BusinessLogic` services that still sit below 25% aggregate coverage.
- `RVT.Utilities` email/blob services, using test doubles for transport and storage boundaries.
- Client panels with large source files and weaker coverage: admin panels, monitor panels, notification panels, data views, and reports.
- Real database integration tests for Timescale/Postgres paths once credentials and schema are stable.
