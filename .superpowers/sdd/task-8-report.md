# Task 8 Report: Remove Common Communications Infrastructure

## Outcome

Removed the tracked `Rvt.Monitor.Common.Infrastructure` source and test
projects after proving every provider source/test destination from Tasks 3-5
exists. Updated both solutions, exact ownership/reference guards, the
transitional seven-package build bridge, RuntimeConsumer, and boundary
fixtures. Active application source and project references contain no legacy
Infrastructure identity.

The three provider projects now use explicit Microsoft.Extensions packages
instead of `Microsoft.AspNetCore.App`: Configuration.Abstractions,
DependencyInjection.Abstractions, Hosting.Abstractions, and, where typed HTTP
registration is required, Microsoft.Extensions.Http, all centrally pinned to
10.0.9.

## TDD Evidence

- RED ownership test before deletion: 4 failed / 3 passed. Failures proved the
  legacy project still existed and duplicated Graph/TransmitSMS ownership.
- RED shell boundary before deletion: exit 1 with
  `Removed communication infrastructure project still exists`.
- Pre-delete proof: every source and test destination named in Tasks 3-5
  existed.

## GREEN Evidence

- `CommunicationsBoundaryTests`: 7/7 passed.
- SendGridMail tests: 20/20 passed.
- MicrosoftGraphMail tests: 31/31 passed.
- TransmitSms tests: 24/24 passed.
- `CommonPackageBoundaryTests`: 12/12 passed.
- `tests/verify-rvt-common-source-boundary.test.sh`: passed.
- `tests/verify-rvt-common-source-boundary-regression.test.sh`: passed.
- Required active-source scan for Infrastructure, `AddMonitorCommunications`,
  and `CommunicationsOptions`: no matches.
- Scoped temporary-lock restore of `rvt-common.sln`: passed.
- `dotnet build libs/rvt-monitor-common/rvt-common.sln --no-restore --nologo
  -m:1`: passed with zero errors.
- `git diff --check`: passed.

## Environmental / Delegated Constraints

- The plan's `RestorePackagesWithLockFile=false` command fails immediately with
  NU1005 because checked-in lock files exist. Verification used temporary lock
  paths so Task 8 did not regenerate retained locks.
- The deleted Infrastructure source/test locks were removed with their
  explicitly authorized projects. Complete retained lock regeneration remains
  future pending in the eleven-package release plan.
- Reporting boundary restore encounters the existing central
  Logging.Abstractions 10.0.4 versus EF transitive 10.0.9 downgrade.
- Portal boundary compilation remains blocked by preserved untracked duplicate
  `BlobStorageClientFactory 2.cs` and
  `PortalSchemaReadinessHealthCheck 2.cs` files.
- Portal blob client/service unification, customer-logo migration, independent
  reporting-service Azure storage, legacy Portal storage utility, dynamic
  plugins, compatibility tooling, legacy synchronous message removal,
  database, MQTT, scheduling, observability, notification/business/API, and
  persisted-record changes remain future pending.
