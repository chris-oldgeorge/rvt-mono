# Task 9 Follow-up Report: Portal Test Email Provider

## Outcome

The Portal test host starts again after the SendGrid project split. Both test
fixture paths now supply deterministic, non-secret values for the legacy
SendGrid API-key and sender-address settings. Production registration and
fail-fast validation are unchanged.

## Root Cause

Task 7 replaced Portal's manual SendGrid descriptors with
`AddSendGridMail(SendGridMailOptions)`. Portal constructs those options eagerly
inside `Program.ConfigureServices`. The raw `WebApplicationFactory` fixture had
no email settings, while `SpaTestApplicationFactory` initially supplied none.

During GREEN verification, adding the values to the custom factory's existing
`ConfigureAppConfiguration` collection proved too late for the eager options
read. The raw fixture's equivalent `UseSetting` path passed. Moving only the
two custom-factory email values to `UseSetting` made them available during the
initial minimal-host builder configuration and resolved the remaining failure.

## TDD RED Evidence

Before either fixture was edited, each real host-start test was run separately
with a temporary exact-path MSBuild import:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SpaHostSmokeTests.SwaggerDocument_IsAvailable' --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: failed 0/1. Startup reported:
`SendGrid mail configuration is missing required settings:
RVT__SENDGRID_API_KEY, RVT__EMAIL_ALERT_FROM_EMAIL.`

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SpaHostSmokeTests.HealthEndpoints_ExposeLivenessAndReadiness' --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: failed 0/1 with the same expected enabled-SendGrid validation error.
These existing integration tests exercise both real fixture paths, so no
additional source-text or implementation-detail test was added.

## Minimal Fix

- `SpaHostSmokeTests` supplies:
  - `EmailConfiguration:SENDGRID_API_KEY=test-sendgrid-api-key`
  - `EmailConfiguration:Sending_Email_Address=portal-tests@example.test`
- `SpaTestApplicationFactory` supplies the same two values through
  `UseSetting`, which is early enough for Portal's eager options construction.
- No production file, SendGrid option type, provider registration, or
  validation rule changed.

## GREEN Verification

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~SpaHostSmokeTests.SwaggerDocument_IsAvailable|FullyQualifiedName~SpaHostSmokeTests.HealthEndpoints_ExposeLivenessAndReadiness' --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: passed 2/2 with no skips.

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~RvtCommonDependencyBoundaryTests|FullyQualifiedName~RvtCommonEmailDeliveryTests' --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: passed 12/12 with no skips.

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=/private/tmp/rvt-task9-exclude-portal-duplicates.targets
```

Result: passed 381, skipped 8, failed 0, total 389 in 16 seconds. The skips are
the existing opt-in PostgreSQL integration tests. All Portal test commands
retained the five known NU1903 advisories for
`System.Security.Cryptography.Xml` 10.0.7.

The temporary targets file removed only these exact untracked files from the
Portal compile:

- `apps/portal/RvtPortal.Spa/Adapters/Storage/BlobStorageClientFactory 2.cs`
- `apps/portal/RvtPortal.Spa/PortalSchemaReadinessHealthCheck 2.cs`

Neither file was edited, moved, staged, or deleted.

## Future Pending Work

This follow-up changes test configuration only. Portal blob client/service
unification through `IObjectStorageClientFactory`, customer-logo and reporting
storage adoption, the legacy Portal storage utility, dynamic plugins,
external-consumer compatibility tooling, synchronous `IMessageService`
removal, and notification, API/persisted-record, database, MQTT, scheduling,
and observability boundary work remain explicitly future pending.
