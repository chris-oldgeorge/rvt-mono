# Documentation

This index is the starting point for current repository documentation. Module
entry READMEs remain beside their code; detailed and historical documentation
lives here under `docs/`.

## Architecture

- [Portal ports and adapters](architecture/portal/ports-and-adapters-catalog.md)
- [Reporting service architecture](architecture/reporting/architecture.md)

## Development

- [RVT engineering standards](development/engineering-standards.md)
- [Engineering standards enforcement guide](development/engineering-standards-enforcement.md)
- [Engineering standards enforcement report](reviews/2026-07-27-engineering-standards-enforcement-report.md)
- [Portal development guidelines](development/portal/development-guidelines.md)
- [Active Portal remediation plan](superpowers/plans/2026-07-22-rvt-portal-review-remediation.md)
- [Monitor SonarQube guidance](development/monitors/sonarqube.md)
- [Shared-library dependency license review](development/rvt-monitor-common/dependency-license-review.md)

## Operations

- [Self-hosted SonarQube runner](operations/github-actions/self-hosted-sonar-runner.md)
- [Monitor container builds](operations/monitors/container-builds.md)
- [Portal development secrets reference](operations/portal/dev-secrets-reference.md)
- [Reporting container app](operations/reporting/container-app/README.md)

## Release

- [Monitor client release runbook](release/monitors/client-release-runbook.md)
- [Portal cutover runbook](release/portal/CUTOVER_RUNBOOK.md)
- [Shared-library source delivery decision](release/rvt-monitor-common/releasing.md)

## Database

- [Monitor data-access migration](database/monitors/monitor-data-access-migration.md)
- [Portal database naming standard](database/portal/database-naming-standard.md)
- [Shared-library migrations](database/rvt-monitor-common/migrations/README.md)

## Modules

### Monitors

- [Monitor timer-trigger inventory](modules/monitors/monitor-timer-triggers.md)
- [AirQ](modules/monitors/airqmonitor/README.md)
- [MyAtm](modules/monitors/myatmmonitor/README.md)
- [Omnidots](modules/monitors/omnidotsmonitor/README.md)
- [ReportingMonitor](modules/monitors/reportingmonitor/README.md)
- [Svantek](modules/monitors/svantekmonitor/README.md)

### Portal

- [Portal client](modules/portal/RvtPortal.Client/README.md)
- [Portal authorization](modules/portal/RvtPortal.Spa/AUTHORIZATION.md)

### RVT Monitor Common

- [Pull-request template](modules/rvt-monitor-common/pull-request-template.md)

### Reporting

Reporting is implemented in `apps/monitors/reportingmonitor`; see also the
[ReportingMonitor module guide](modules/monitors/reportingmonitor/README.md).

- [Migration and consolidation notes](modules/reporting/migration-notes.md)

## History

- [Monitor source-removal evidence](history/monitors/evidence/2026-07-17-rvt-common-monitor-source-removal.md)
- [Monitor plans](history/monitors/plans/2026-07-16-common-communications-ports-and-adapters.md)
- [Portal plans](history/portal/plans/2026-07-08-hexagonal-edges.md)
- [Reporting plans](history/reporting/plans/2026-06-25-report-graphs-daily-frequency-plan.md)

## Imports

- [Pinned source manifest](imports/source-manifest.md)
- [Documentation move manifest](documentation-move-manifest.md)
