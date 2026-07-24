# Task 6 Report: Migrate the five monitor composition roots

## Outcome

- Status: `DONE`
- Intended commit: `refactor: compose monitor communication providers explicitly`
- Scope: AirQ, MyAtm, Omnidots, ReportingMonitor, Svantek, shared
  `MonitorHost`, and the monitor source-boundary guard.

## RED evidence

The provider-selection/composition tests were written before production
changes for all five monitors. They require:

- missing provider configuration to resolve the SendGrid email adapter;
- case-insensitive `MicrosoftGraph` to resolve the Microsoft Graph adapter;
- `RVT:EMAIL_PROVIDER` to take precedence over literal
  `RVT__EMAIL_PROVIDER`;
- invalid selection to throw the exact safe message without the invalid value;
- TransmitSMS, `INotificationDeliveryService`, and `IMessageService` to
  resolve;
- the selected email and TransmitSMS startup validators to succeed while both
  channels are disabled.

The first parallel RED attempt was intentionally discarded as test evidence
for four projects because their shared dependency outputs collided. Omnidots
reached the expected missing-provider-reference compilation failure, while the
other four included `CS2012` shared-`obj` file locks. All later .NET commands
were run sequentially with `-m:1`.

Clean sequential RED:

```text
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj \
  --no-restore --filter FullyQualifiedName~CommunicationsCompositionTests \
  --nologo -m:1
```

Result: failed with `CS0234` for
`Rvt.Communication.MicrosoftGraphMail`,
`Rvt.Communication.SendGridMail`, and
`Rvt.Communication.TransmitSms`. This proved the current
Infrastructure-only host graph did not expose the provider projects required
by the new composition contract.

## Flow and signature changes

- `MonitorHost.RunAsync` changed its optional callback from
  `Action<IServiceCollection>` to
  `Action<IServiceCollection, IConfiguration>`.
- API mode invokes it with
  `(apiBuilder.Services, apiBuilder.Configuration)`.
- Quartz scheduler and one-shot modes invoke it with
  `(services, context.Configuration)`.
- All five `Program.cs` roots accept both callback parameters and pass the
  configuration into their monitor registration extension.
- Every direct test caller was migrated to pass its existing
  `IConfiguration`; the focused `MonitorHostTests` caller was updated for the
  new callback signature.

## Explicit provider composition

Each of the five `*MonitorServices.cs` roots now:

1. calls `AddRvtCommunication()`;
2. reads `RVT:EMAIL_PROVIDER`, then literal `RVT__EMAIL_PROVIDER`, then
   defaults to `SendGrid`;
3. matches `SendGrid` or `MicrosoftGraph` with
   `StringComparison.OrdinalIgnoreCase`;
4. calls the selected provider-owned registration method;
5. throws
   `InvalidOperationException("RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.")`
   for every other value;
6. always calls `AddTransmitSms(configuration)`.

The tests also set a valid hierarchical provider and an invalid literal
fallback simultaneously, proving hierarchical-key precedence. Invalid-value
tests use a sentinel string and assert exact message equality plus absence of
the sentinel.

## Project and boundary graph

All five active host projects retain `Rvt.Monitor.Common`, remove
`Rvt.Monitor.Common.Infrastructure`, and add exactly these five communication
project references:

- `Rvt.Communication.Abstractions`
- `Rvt.Communication`
- `Rvt.Communication.SendGridMail`
- `Rvt.Communication.MicrosoftGraphMail`
- `Rvt.Communication.TransmitSms`

`CommonPackageBoundaryTests` and
`scripts/verify-rvt-common-source-boundary.sh` now recognize and require this
explicit graph, reject Infrastructure from active monitor hosts, reject direct
communication package references in active applications, and retain the
existing Portal/Infrastructure plus Portal/SendGrid graph for later tasks.
No monitor solution or lock-file edit was necessary: the explicit project
graph built with existing assets, and active locks do not retain direct RVT
packages.

The exact communication namespace caller manifest command returned no legacy
callers:

```text
rg -l '^using Rvt\.Monitor\.Common\.Communications|MessageService\.MessageContent' \
  apps/monitors --glob '*.cs' | sort
```

The active-host scan also returned no Infrastructure reference and no
`AddMonitorCommunications` call.

## GREEN and verification evidence

Focused composition suites, all with `--no-restore --nologo -m:1`:

- AirQ: 3 passed, 0 failed.
- MyAtm: 3 passed, 0 failed.
- Omnidots: 3 passed, 0 failed.
- ReportingMonitor: 3 passed, 0 failed.
- Svantek: 3 passed, 0 failed.
- Total: 15 passed, 0 failed.

Additional focused checks:

- `MonitorHostTests`: 3 passed, 0 failed. Existing MSTest analyzer warnings
  remain outside this change.
- `CommonPackageBoundaryTests`: 12 passed, 0 failed.
- `bash scripts/verify-rvt-common-source-boundary.sh`: passed.
- `git diff --check`: passed.

All five individual host builds used `--no-restore --nologo -m:1` and passed
with 0 warnings and 0 errors.

Required aggregate build:

```text
dotnet build apps/monitors/rvt-monitors.sln --no-restore --nologo -m:1
```

Result: succeeded in 1.71 seconds with 0 errors. It retained one known NU1900
warning from `Rvt.Monitor.IntegrationTesting` because NuGet vulnerability
metadata at `api.nuget.org` was unreachable.

## Files changed

- `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Hosting/MonitorHost.cs`
- `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Hosting/MonitorHostTests.cs`
- `apps/monitors/airqmonitor/AirQMonitor/Program.cs`
- `apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs`
- `apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj`
- `apps/monitors/airqmonitor/AirQMonitorTests/CommunicationsCompositionTests.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitor/Program.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- `apps/monitors/myatmmonitor/MyAtmMonitorTests/CommunicationsCompositionTests.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorServiceRegistrationTests.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOperationalConfigurationTests.cs`
- `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitor/Program.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Architecture/CommunicationsCompositionTests.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Architecture/OmnidotsAlertArchitectureTests.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Config/OmnidotsApiSecurityOptionsTests.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsWebhookEndToEndTests.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/UseCases/MonitoringHandlerTests.cs`
- `apps/monitors/reportingmonitor/ReportingMonitor/Program.cs`
- `apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- `apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- `apps/monitors/reportingmonitor/ReportingMonitorTests/CommunicationsCompositionTests.cs`
- `apps/monitors/reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/Program.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- `apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- `apps/monitors/svantekmonitor/SvantekMonitorTests/CommunicationsCompositionTests.cs`
- `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekImportOptionsTests.cs`
- `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs`
- `scripts/verify-rvt-common-source-boundary.sh`
- `project_state.md`
- `.superpowers/sdd/task-6-report.md`

## Self-review and concerns

Self-review found no Critical, Important, or Minor implementation issue:

- all three host modes use their effective configuration object;
- all roots implement the exact precedence, default, case-insensitive match,
  and safe failure contract;
- provider-owned adapters and startup validators remain resolvable while
  disabled;
- each active host has one Common and exactly five communication references;
- no active monitor source or project file depends on Infrastructure;
- unrelated untracked `.codegraph`, package-cache, Portal duplicate, and
  suffixed files remain unstaged.

Non-blocking environmental concern: the aggregate build could not retrieve
NuGet vulnerability metadata and emitted the existing NU1900 warning. No
restore was attempted or required, and compilation/test results were
otherwise complete.
