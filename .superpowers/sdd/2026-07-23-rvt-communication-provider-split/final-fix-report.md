# Communication final-review fix report

Date: 2026-07-25

Base reviewed commit: `5b74806`

## Outcome

The two final-review findings are fixed without changing the singleton,
provider-neutral workflow services.

- `AddMicrosoftGraphMail` and `AddTransmitSms` now register named HTTP clients.
  Their singleton adapter/port instances retain `IHttpClientFactory`, not a
  typed `HttpClient`. Each delivery operation creates one factory-managed
  client and disposes it after the awaited operation, so long-running monitor
  processes do not retain one client indefinitely.
- The existing public adapter constructors that accept `HttpClient` remain
  available for direct consumers and focused tests.
- A non-caller `OperationCanceledException` from an authenticated Graph HTTP
  request becomes a safe transient `EmailDeliveryException` with code
  `Timeout`. A caller-requested cancellation still propagates unchanged.
- Malformed successful Graph draft and upload-session JSON becomes a permanent
  safe typed failure with the existing `InvalidDraftResponse` and
  `InvalidUploadSession` codes. Provider response content is not retained in
  exception text.
- Existing port contracts, provider selection, options, duplicate-registration
  errors, startup validators, request/authentication shapes, status
  classification, and retry-after behavior remain unchanged.

## Root-cause evidence

At `5b74806`, each provider called `AddHttpClient<TAdapter>()`, which registered
the typed adapter transiently, then immediately resolved that adapter inside a
singleton `IEmailDeliveryPort` or `ISmsDeliveryPort` factory. The singleton
neutral workflows retained those singleton ports, so the transient adapter and
its `HttpClient` became process-long captures.

`MicrosoftGraphEmailAdapter.SendAuthenticatedAsync` had a catch filter only for
caller-requested cancellation and a catch for `HttpRequestException`.
Non-caller `OperationCanceledException` therefore escaped. The draft and
upload-session paths called `JsonSerializer.Deserialize` without translating
`JsonException`.

## Strict TDD evidence

Only test files were edited before the clean RED runs. The first Graph attempt
found a test-source raw-string syntax error; that test-only error was corrected
and the focused command was rerun before any production edit.

### Clean RED

Graph focused command:

```text
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj --no-restore --nologo --filter 'FullyQualifiedName~SendAsync_HttpTimeoutIsTransientAndSafe|FullyQualifiedName~SendAsync_MalformedDraftResponseIsSafeTypedFailure|FullyQualifiedName~SendAsync_MalformedUploadSessionResponseIsSafeTypedFailure|FullyQualifiedName~AddMicrosoftGraphMail_SingletonPortUsesFactoryManagedClientPerDelivery|FullyQualifiedName~SendAsync_CallerCancellationPropagatesBeforeTokenOrNetwork'
```

Result: exit 1; 4 failed and 1 passed.

- Timeout: expected `EmailDeliveryException`, actual
  `OperationCanceledException`.
- Malformed draft: expected `EmailDeliveryException`, actual `JsonException`.
- Malformed upload session: expected `EmailDeliveryException`, actual
  `JsonException`.
- Graph lifetime: expected request client IDs `[1, 2]`, actual `[1, 1]`.
- Existing caller cancellation characterization passed 1/1.

TransmitSMS focused command:

```text
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj --no-restore --nologo --filter 'FullyQualifiedName~AddTransmitSms_SingletonPortUsesFactoryManagedClientPerDelivery'
```

Result: exit 1; 1 failed. Expected request client IDs `[1, 2]`, actual
`[1, 1]`.

### Focused GREEN

The same Graph command passed 5/5, and the same TransmitSMS command passed 1/1.
The lifetime tests also confirm that the delivery port remains singleton while
successive delivery operations traverse different clients produced by the
factory.

## Required verification

All commands were run individually or in small groups and completed within the
60-second bound.

### Provider, abstraction, and workflow suites

| Suite | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| `Rvt.Communication.MicrosoftGraphMailTests` | 35 | 0 | 0 | 0 |
| `Rvt.Communication.TransmitSmsTests` | 25 | 0 | 0 | 0 |
| `Rvt.Communication.AbstractionsTests` | 20 | 0 | 0 | 0 |
| `Rvt.CommunicationTests` | 31 | 0 | 0 | 0 |
| **Total** | **111** | **0** | **0** | |

The suites retained pre-existing MSTest analyzer warnings only.

### Focused monitor composition

Each runnable vendor monitor was tested with
`--filter FullyQualifiedName~CommunicationsCompositionTests --no-restore`.

| Monitor | Passed | Failed | Exit |
| --- | ---: | ---: | ---: |
| AirQ | 3 | 0 | 0 |
| MyAtm | 3 | 0 | 0 |
| Omnidots | 3 | 0 | 0 |
| Svantek | 3 | 0 | 0 |
| **Total** | **12** | **0** | |

The first parallel AirQ attempt encountered a shared compiler-output file lock
while other monitor builds were running. The required sequential `-m:1` rerun
passed 3/3. ReportingMonitor's focused test project did not execute because of
the known release-lock `NU1109`: central
`Microsoft.Extensions.Logging.Abstractions` 10.0.4 is lower than the 10.0.9
transitive requirement.

### Scoped builds

Sequential `dotnet build --no-restore --nologo -m:1` commands passed with zero
warnings and zero errors for:

- `Rvt.Communication.MicrosoftGraphMail`
- `Rvt.Communication.TransmitSms`
- AirQMonitor
- MyAtmMonitor
- OmnidotsMonitor
- SvantekMonitor
- ReportingMonitor

The ReportingMonitor host build is green even though its test project's stale
locked graph remains blocked.

### Boundary and cleanliness guards

- `bash tests/verify-rvt-common-source-boundary.test.sh`: exit 0;
  `RVT common source boundary verified.` and
  `Local RVT package prerequisite sequencing verified.`
- `git diff --check`: exit 0 with no output before documentation.

## Known release-lock blocker and excluded work

The ReportingMonitor test `NU1109` and the five retained lock files that still
name removed `Rvt.Monitor.Common.Infrastructure` remain owned by the separate
eleven-package release/lock plan. No central package version, package lock,
package artifact, or aggregate release gate was changed in this wave.

Storage, Portal, reporting behavior, package versions, package locks, and all
other future-pending work were not touched. The pre-existing untracked
`.codegraph/`, `apps/.nuget-packages/`, the three suffixed Portal files, and
the suffixed Portal design document were preserved and excluded from staging.
