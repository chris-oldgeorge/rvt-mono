# Task 9 report: communication verification gate and recorded state

## Outcome

Task 9 verified and documented the completed source-level communication split.
The neutral libraries and source-boundary/isolation checks are green. The
aggregate/locked gate is explicitly not green because the separate release/lock
plan has not migrated retained locks, locally packed artifacts, or the monitor
central package version.

No production code, tests, projects, packages, central versions, or lock files
were changed.

## Execution controls

- Base commit: `e5cb1c1` (`fix: configure Portal test email provider`).
- Commands ran sequentially with a 60-second alarm.
- Test and build commands used existing assets with `--no-restore`.
- The first in-sandbox vstest invocation compiled but aborted before test
  discovery because the sandbox denied its local socket bind. The command was
  rerun outside the filesystem sandbox with local socket access and passed;
  every test count below comes from the successful fresh executions.
- Portal tests and the aggregate build used
  `/private/tmp/rvt-task9-exclude-portal-duplicates.targets`. It removed only
  these exact preserved untracked files from compilation:
  - `apps/portal/RvtPortal.Spa/Adapters/Storage/BlobStorageClientFactory 2.cs`
  - `apps/portal/RvtPortal.Spa/PortalSchemaReadinessHealthCheck 2.cs`
- All unrelated/untracked files were preserved.

## Verified source graph

- `Rvt.Communication.Abstractions`: no RVT project dependency.
- `Rvt.Communication`: directly references Abstractions.
- `Rvt.Communication.SendGridMail`: directly references Abstractions.
- `Rvt.Communication.MicrosoftGraphMail`: directly references Abstractions.
- `Rvt.Communication.TransmitSms`: directly references Abstractions.
- `Rvt.Monitor.Common`: directly references Abstractions for retained
  compatibility types; it does not select providers.
- AirQ, MyAtm, Omnidots, ReportingMonitor, and Svantek host projects directly
  reference Common, Abstractions, workflow, SendGrid, Microsoft Graph, and
  TransmitSMS.
- Portal directly references only Abstractions and SendGrid for communication.
- Both reporting messaging projects directly reference only Abstractions.
  ReportingMonitor owns its selectable provider composition; the containerized
  reporting service directly references and registers SendGrid.
- Active source/project-reference scans and the guard contain no legacy
  Infrastructure composition identity. The removed
  `Rvt.Monitor.Common.Infrastructure` project is not a facade.

## Library verification

All five required commands passed:

| Command target | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| `Rvt.Communication.AbstractionsTests` | 20 | 0 | 0 | 0 |
| `Rvt.CommunicationTests` | 31 | 0 | 0 | 0 |
| `Rvt.Communication.SendGridMailTests` | 20 | 0 | 0 | 0 |
| `Rvt.Communication.MicrosoftGraphMailTests` | 31 | 0 | 0 | 0 |
| `Rvt.Communication.TransmitSmsTests` | 24 | 0 | 0 | 0 |
| **Total** | **126** | **0** | **0** | |

The output retained existing MSTest analyzer warnings; no warning represented a
communication test failure.

## Consumer verification

| Command target | Exact result | Classification |
| --- | --- | --- |
| `AirQMonitorTests` | 87 passed, 33 failed, 0 skipped, 120 total; exit 1 | Every reported failure was gated by the absent `RVT__POSTGRES_INTEGRATION_CONNECTION`. The no-restore build also warned that stale assets reference removed Infrastructure. |
| `MyAtmMonitorTests` | 139 passed, 69 failed, 0 skipped, 208 total; exit 1 | Failures included the absent PostgreSQL setting and retained paths rooted at the module's former monorepo location. |
| `OmnidotsMonitorTests` | 337 passed, 64 failed, 0 skipped, 401 total; exit 1 | Reported failures were PostgreSQL-gated. The no-restore build also warned that stale assets reference removed Infrastructure. |
| `ReportingMonitorTests` | No test execution; exit 1 | `NU1109`: centrally defined `Microsoft.Extensions.Logging.Abstractions` 10.0.4 is lower than the provider graph's 10.0.9 transitive requirement. |
| `SvantekMonitorTests` | 86 passed, 40 failed, 0 skipped, 126 total; exit 1 | Failures included the absent PostgreSQL setting and retained paths rooted at the former module location. The no-restore build also warned that stale assets reference removed Infrastructure. |
| `RvtPortal.Spa.Tests` | 381 passed, 0 failed, 8 skipped, 389 total; exit 0 | Eight existing opt-in PostgreSQL provider skips. Five known `NU1903` advisories for `System.Security.Cryptography.Xml` 10.0.7 remained. |
| `Rvt.Reporting.Service.Tests` | 14 passed, 0 failed, 0 skipped, 14 total; exit 0 | Green. |

To separate the known full-suite environment/path failures from communication
behavior, the four runnable vendor-monitor
`CommunicationsCompositionTests` were run directly:

- AirQ: 3/3 passed.
- MyAtm: 3/3 passed.
- Omnidots: 3/3 passed.
- Svantek: 3/3 passed.
- Total: 12/12 passed.

ReportingMonitor's equivalent 3-test focus cannot reach execution until the
known `NU1109` release/lock mismatch is resolved. No executed consumer failure
identified a communication-split regression.

## Aggregate and dependency-isolation gates

### Aggregate build

The bounded command was:

```bash
dotnet build Rvt.Mono.slnx --no-restore --nologo \
  -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: exit 1, 62 warnings, 3 errors in approximately 3 seconds.

The three error outcomes were:

1. ReportingMonitor `NU1109` for centrally pinned
   Logging.Abstractions 10.0.4 versus transitive 10.0.9.
2. RuntimeConsumer `NETSDK1064` because the not-yet-packed
   `Rvt.Communication` 0.2.0-rc.1 artifact is absent.
3. TestConsumer `CS0246` because its retained package inputs could not supply
   the expected RVT type.

This aggregate/locked gate is not green and was not retried or repaired in this
documentation-only task.

### Neutral dependency listings

`Rvt.Communication.Abstractions` exited 0 and listed:

- Top-level: `Microsoft.SourceLink.GitHub` 8.0.0.
- Transitive: `Microsoft.Build.Tasks.Git` 8.0.0 and
  `Microsoft.SourceLink.Common` 8.0.0.

`Rvt.Communication` exited 0 and listed:

- Top-level: `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9
  and `Microsoft.SourceLink.GitHub` 8.0.0.
- Transitive: `Microsoft.Build.Tasks.Git` 8.0.0 and
  `Microsoft.SourceLink.Common` 8.0.0.

Neither listing contains SendGrid, Azure Identity, Azure Storage, or AWS S3.

### Guards

- `bash tests/verify-rvt-common-source-boundary.test.sh`: exit 0;
  `RVT common source boundary verified.` and
  `Local RVT package prerequisite sequencing verified.`
- `git diff --check` before documentation: exit 0 with no output.

### Retained lock identities

Exactly five retained locks still contain
`Rvt.Monitor.Common.Infrastructure`:

1. `apps/monitors/airqmonitor/AirQMonitorTests/packages.lock.json`
2. `apps/monitors/myatmmonitor/MyAtmMonitorTests/packages.lock.json`
3. `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/packages.lock.json`
4. `apps/monitors/svantekmonitor/SvantekMonitorTests/packages.lock.json`
5. `libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json`

They were not edited. Regeneration and the related local package artifacts
belong to the dedicated eleven-package release/lock plan.

## Documentation updates

- `libs/rvt-monitor-common/README.md` now states the source graph, removal of
  Infrastructure, correct local verification, and pending packaging work.
- `apps/monitors/README.md` now states explicit provider choice, the Portal and
  reporting exceptions, test prerequisites, and the non-green lock state.
- `docs/architecture/rvt-monitor-common/communications.md` records the complete
  graph, both reporting migrations, exact verification evidence, blockers, and
  pending work.
- `project_state.md` records the handoff state for the next session.

## Future pending work

- Remove legacy synchronous `IMessageService.Sendmessage` and `SendMessage`
  after its remaining callers receive a separate compatibility plan.
- Change notification templates, recipients, delivery business rules, or retry
  policy only under a separate product specification.
- Add dynamic provider discovery or runtime assembly loading only if
  deployments require providers to be installed without rebuilding a host.
- Add external-consumer compatibility tooling only if coordinated
  major-version migration proves impossible.
- Change public HTTP APIs or persisted monitor/report records only under an
  explicit compatibility and data-migration design.
- Unify Portal `BlobStorageClientFactory` and the new storage service through
  the separately approved `IObjectStorageClientFactory` work.
- Decide customer-logo and reporting-service storage adoption after the Portal
  blob-unification slice.
- Review database, MQTT, scheduling, and observability dependency boundaries
  after communication and storage isolation are complete.
- Update the full eleven-package pack, package-consumer, lock, SBOM,
  vulnerability, release-manifest, and release-asset pipeline in the dedicated
  package-release plan.
