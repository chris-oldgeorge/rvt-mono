# Task 7 Report: Portal and Reporting Mail Migration

## Outcome

Task 7 is complete. The Portal and both reporting message-sender paths now use
the clean communication project boundaries:

- Portal: `Rvt.Communication.Abstractions` plus
  `Rvt.Communication.SendGridMail`.
- Monitor reporting messaging: `Rvt.Communication.Abstractions`.
- Containerized reporting messaging: `Rvt.Communication.Abstractions`.
- Containerized reporting host: explicit
  `Rvt.Communication.SendGridMail` registration.

The provider-neutral senders preserve disabled mode, test-recipient override,
attachment mapping, typed failure translation, safe untyped-failure
translation, and caller cancellation. No storage implementation was changed.

## CodeGraph orientation

CodeGraph was consulted before editing. It identified the existing partial
migration:

- `RvtCommonEmailDelivery` already depended on `IEmailDeliveryPort`.
- Portal composition still manually registered `SendGridMailOptions`,
  `ISendGridClientFactory`, and `SendGridEmailAdapter`, and still referenced
  Infrastructure.
- Monitor `ReportMessageSender` already consumed `IEmailDeliveryPort`, while
  its project still referenced `Rvt.Monitor.Common`.
- Containerized reporting still contained
  `SendGridReportMessageSender`, a direct SendGrid SDK package, and
  provider-specific options inside the messaging project.

CodeGraph warned that its index belonged to the parent worktree, so the
returned on-disk source was used only for orientation and current task files
were inspected directly before tests were written.

## RED evidence

Production code was not changed until the focused tests and dependency guards
had been written.

1. Portal command:

   ```bash
   dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~RvtCommonDependencyBoundaryTests|FullyQualifiedName~RvtCommonEmailDeliveryTests' --nologo
   ```

   Result: exited 1 in 7.0 seconds before test execution on the known,
   preserved untracked duplicate definitions from
   `BlobStorageClientFactory 2.cs` and
   `PortalSchemaReadinessHealthCheck 2.cs`. The source project still visibly
   contained the expected old Infrastructure edge. Attempts to override the
   SDK-wide `DefaultItemExcludes` property were abandoned because one was
   rejected by MSBuild and the other removed normal `obj` exclusions; neither
   production nor untracked source was changed.

2. Monitor reporting:

   ```bash
   dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
   ```

   Result: exited 1 in 3.4 seconds; 6 tests passed and the new dependency test
   failed because the messaging project referenced Common rather than
   Communication Abstractions.

3. Containerized reporting:

   ```bash
   dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
   ```

   Result: exited 1 in 3.3 seconds with the expected compile errors for the
   absent `Rvt.Communication.Abstractions`, `ReportMessageSender`, and
   `ReportMessageSenderOptions` dependencies/types.

4. After the production graph changed, the pre-Task-7 root boundary guard was
   run:

   ```bash
   bash scripts/verify-rvt-common-source-boundary.sh
   ```

   Result: exited 1 in 1.9 seconds because it still required Common for
   monitor reporting messaging and Infrastructure for Portal. The guard was
   updated to enforce the Task 7 graph.

## Implementation

- Replaced the Portal Infrastructure source reference with Communication
  Abstractions and retained SendGridMail.
- Replaced manual Portal SendGrid descriptors with
  `AddSendGridMail(SendGridMailOptions)`, mapped directly from the existing
  `EmailConfiguration` keys.
- Added Portal behavior tests for success mapping, debug recipient, provider
  failure, and caller cancellation.
- Replaced the monitor reporting messaging project's Common reference with
  Communication Abstractions and added a source dependency test.
- Replaced containerized reporting's SendGrid SDK implementation with a
  provider-neutral `ReportMessageSender`.
- Removed the SendGrid package from containerized reporting messaging.
- Registered SendGridMail explicitly in the containerized service host using
  existing `RVT:EMAIL_ENABLED`, `RVT:EMAIL_ALERT_FROM_EMAIL`,
  `RVT:EMAIL_ALERT_FROM_NAME`, and `RVT:SENDGRID_API_KEY` keys.
- Extended the root source-boundary guard to cover the new Portal and both
  reporting dependency graphs, including rejection of a direct SendGrid
  package from either reporting messaging project.

## GREEN verification

Portal focused tests used a temporary file at
`/private/tmp/rvt-task7-exclude-portal-duplicates.targets` which removed only
the two exact untracked duplicate paths from the scoped compile. The files
were not edited, moved, staged, or deleted.

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~RvtCommonDependencyBoundaryTests|FullyQualifiedName~RvtCommonEmailDeliveryTests' --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task7-exclude-portal-duplicates.targets
```

Result: passed 12/12 in 2.8 seconds. Output retained five existing NU1903
advisories for `System.Security.Cryptography.Xml` 10.0.7.

```bash
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
```

Result: passed 7/7 in 3.5 seconds.

```bash
dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
```

Result: passed 6/6 in 4.0 seconds.

```bash
bash scripts/verify-rvt-common-source-boundary.sh
bash tests/verify-rvt-common-source-boundary.test.sh
```

Result: both passed in 5.0 seconds total; the package prerequisite sequencing
harness also passed.

```bash
dotnet build apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task7-exclude-portal-duplicates.targets
dotnet build apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj --no-restore --nologo
dotnet build services/reporting/src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj --no-restore --nologo
```

Results:

- Portal: succeeded, 0 warnings, 0 errors, 3.3 seconds.
- Monitor reporting: succeeded, 0 warnings, 0 errors, 0.9 seconds.
- Service reporting: succeeded, 0 warnings, 0 errors, 2.7 seconds.

```bash
dotnet list apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj reference
dotnet list apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj reference
dotnet list services/reporting/src/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj reference
dotnet list services/reporting/src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj reference
git diff --check
```

Result: exact project graphs matched Task 7 and `git diff --check` passed.

An optional `dotnet list <messaging-project> package` command stalled while
restoring and was cancelled after 60 seconds. It was not rerun; required tests,
compiled assembly assertions, source-boundary guards, and builds had already
verified removal of the direct SendGrid dependency.

## Residual risks and future pending work

- The exact unmodified Portal test/build commands remain blocked by the two
  unrelated untracked duplicate `* 2.cs` files. Scoped verification excludes
  only those paths. They remain preserved and unstaged.
- Portal test output reports existing NU1903 advisories for
  `System.Security.Cryptography.Xml` 10.0.7; dependency remediation is outside
  Task 7.
- Future pending: unify Portal blob storage client/service use through
  `IObjectStorageClientFactory`; customer-logo migration; the independent
  reporting-service Azure storage path; the legacy Portal storage utility;
  dynamic provider plugins; external-consumer compatibility tooling;
  notification/business/API/persisted-record changes; legacy synchronous
  `IMessageService` removal; and database, MQTT, scheduling, and observability
  dependency splits.
