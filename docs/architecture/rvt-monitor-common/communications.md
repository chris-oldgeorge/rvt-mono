# Communication provider split

## Status

The source-level communication split is complete and verified. Provider-neutral
contracts and workflow code are separated from SendGrid, Microsoft Graph, and
TransmitSMS adapters, and active application composition roots choose the
adapters directly. The removed `Rvt.Monitor.Common.Infrastructure` project is
not retained as a facade or meta-package.

This is not a release-green state. Storage extraction and the full
eleven-package release/lock pipeline are separate pending work. In particular,
retained lock snapshots and package-validation assets still describe parts of
the old package graph.

## Verified source graph

| Project or host | Direct RVT communication references | Responsibility |
| --- | --- | --- |
| `Rvt.Communication.Abstractions` | None | Provider-neutral email/SMS ports, requests, results, failures, and retained legacy contracts. |
| `Rvt.Communication` | Abstractions | Provider-neutral delivery workflow and compatibility services. |
| `Rvt.Communication.SendGridMail` | Abstractions | SendGrid options, validation, factory, and adapter. |
| `Rvt.Communication.MicrosoftGraphMail` | Abstractions | Microsoft Graph options, validation, authentication, upload, and mail adapter. |
| `Rvt.Communication.TransmitSms` | Abstractions | TransmitSMS options, validation, HTTP client, and adapter. |
| `Rvt.Monitor.Common` | Abstractions | Retained shared runtime and compatibility code; it does not select a provider. |
| AirQ, MyAtm, Omnidots, ReportingMonitor, and Svantek hosts | Common, Abstractions, workflow, SendGrid, Microsoft Graph, and TransmitSMS | Each host owns configuration and provider selection. |
| Portal host | Abstractions and SendGrid | Portal is deliberately SendGrid-only; it maps the existing `EmailConfiguration` keys into `SendGridMailOptions`. |
| Monitor `Rvt.Reporting.Messaging` | Abstractions | Maps reports and attachments to the email port; the ReportingMonitor host selects the provider. |
| Containerized `Rvt.Reporting.Messaging` | Abstractions | Sends a provider-neutral report request; the service host explicitly registers SendGrid. |
| Containerized `Rvt.Reporting.Service` | SendGrid | Owns the service's existing SendGrid-only composition and settings mapping. |

The five monitor hosts register `AddRvtCommunication`, always register
TransmitSMS, and select exactly one mail adapter. `RVT:EMAIL_PROVIDER` takes
precedence over the literal `RVT__EMAIL_PROVIDER` alias. Missing or `SendGrid`
selects SendGrid; `MicrosoftGraph` is matched case-insensitively; every other
value fails registration with
`RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.`.

No active application source or project reference contains
`Rvt.Monitor.Common.Infrastructure`, `AddMonitorCommunications`, or
`CommunicationsOptions`. The source-boundary guard enforces that absence and
the direct-reference graph above.

## Reporting migrations

There are two independent reporting paths:

1. `apps/monitors/reportingmonitor/Rvt.Reporting.Messaging` now depends only on
   communication abstractions. It preserves attachment mapping, disabled
   delivery, test-recipient behavior, cancellation, and result semantics.
   `ReportingMonitor` owns the same explicit SendGrid/Microsoft Graph selection
   as the other monitor hosts and also composes the workflow and TransmitSMS.
2. `services/reporting/src/Rvt.Reporting.Messaging` no longer references or
   constructs the SendGrid SDK. Its `ReportMessageSender` sends one
   provider-neutral attachment through `IEmailDeliveryPort`. The
   `Rvt.Reporting.Service` host explicitly registers SendGrid from the existing
   `RVT:EMAIL_*` and `RVT:SENDGRID_API_KEY` settings. Disabled email remains a
   successful no-op, including for an already-cancelled supplied token.

The Portal remains a third, deliberately SendGrid-only composition root. Its
`RvtCommonEmailDelivery` adapter remains the boundary between the Portal
business result contract and `IEmailDeliveryPort`, including the existing
debug-recipient override and typed failure translation.

## Task 9 verification evidence

All commands were run sequentially against existing assets with a 60-second
bound. Tests used `--no-restore`. Portal compilation used a temporary MSBuild
target that removed only the two preserved untracked duplicate files:
`BlobStorageClientFactory 2.cs` and
`PortalSchemaReadinessHealthCheck 2.cs`.

### Communication libraries

| Test project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Abstractions | 20 | 0 | 0 |
| Workflow | 31 | 0 | 0 |
| SendGridMail | 20 | 0 | 0 |
| MicrosoftGraphMail | 31 | 0 | 0 |
| TransmitSms | 24 | 0 | 0 |
| **Total** | **126** | **0** | **0** |

The four runnable vendor-monitor `CommunicationsCompositionTests` also passed
12/12: 3 each for AirQ, MyAtm, Omnidots, and Svantek. ReportingMonitor's
focused tests cannot currently reach test execution because of the release-plan
dependency mismatch described below.

### Active consumer suites

| Suite | Result | Classification |
| --- | --- | --- |
| AirQ | 87 passed, 33 failed, 120 total | Failures require the absent PostgreSQL integration connection. Existing no-restore assets also warn about the removed Infrastructure project. |
| MyAtm | 139 passed, 69 failed, 208 total | Failures are the absent PostgreSQL integration connection and retained pre-monorepo source/migration path baselines. |
| Omnidots | 337 passed, 64 failed, 401 total | Failures require the absent PostgreSQL integration connection. Existing no-restore assets also warn about the removed Infrastructure project. |
| ReportingMonitor | No tests executed | `NU1109`: central `Microsoft.Extensions.Logging.Abstractions` 10.0.4 conflicts with the provider graph's transitive 10.0.9 requirement. |
| Svantek | 86 passed, 40 failed, 126 total | Failures are the absent PostgreSQL integration connection and retained pre-monorepo path baselines. Existing no-restore assets also warn about the removed Infrastructure project. |
| Portal | 381 passed, 0 failed, 8 skipped, 389 total | The eight skips are the known opt-in PostgreSQL provider tests. The run retained five known `NU1903` advisories for `System.Security.Cryptography.Xml` 10.0.7. |
| Containerized reporting service | 14 passed, 0 failed, 0 skipped | Green. |

No full-suite failure identified a communication composition or adapter
regression. The failed monitor results are not counted as a green aggregate
gate.

### Isolation and aggregate gates

- `dotnet list` for `Rvt.Communication.Abstractions` resolved only SourceLink
  and its build helpers.
- `dotnet list` for `Rvt.Communication` resolved Dependency Injection
  Abstractions, SourceLink, and SourceLink's build helpers.
- Neither neutral project listing contained SendGrid, Azure Identity, Azure
  Storage, or AWS S3.
- `tests/verify-rvt-common-source-boundary.test.sh` passed both the source
  boundary and local package prerequisite checks.
- `git diff --check` passed before documentation edits.
- The bounded aggregate `Rvt.Mono.slnx --no-restore` build was **not green**:
  it ended with 62 warnings and 3 errors. The errors were the ReportingMonitor
  `NU1109` mismatch, `RuntimeConsumer` `NETSDK1064` for the not-yet-packed
  `Rvt.Communication` 0.2.0-rc.1 artifact, and `TestConsumer` `CS0246` after its
  retained package assets could not supply the expected RVT type.

Five retained lock snapshots still contain
`Rvt.Monitor.Common.Infrastructure`:

- `apps/monitors/airqmonitor/AirQMonitorTests/packages.lock.json`
- `apps/monitors/myatmmonitor/MyAtmMonitorTests/packages.lock.json`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/packages.lock.json`
- `apps/monitors/svantekmonitor/SvantekMonitorTests/packages.lock.json`
- `libs/rvt-monitor-common/package-validation/RuntimeConsumer/packages.lock.json`

Those locks, package artifacts, and the Logging.Abstractions alignment belong
to the dedicated package-release plan. They were not changed in this
documentation-only task.

## Future pending work

- Remove legacy synchronous `IMessageService.Sendmessage` and `SendMessage`
  only after its remaining callers receive a separate compatibility plan.
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
