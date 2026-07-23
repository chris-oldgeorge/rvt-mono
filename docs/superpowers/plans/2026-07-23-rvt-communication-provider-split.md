# RVT Communication Provider Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `Rvt.Monitor.Common.Infrastructure` with provider-neutral communication packages and isolated SendGrid, Microsoft Graph, and TransmitSMS packages, then migrate every active communication consumer.

**Architecture:** Transport contracts live in `Rvt.Communication.Abstractions`; notification composition, delivery orchestration, and the retained legacy message-service implementation live in `Rvt.Communication`. Each vendor project owns its SDK, configuration parser, startup validation, adapter, and registration method. Five monitor hosts deliberately reference both email providers plus TransmitSMS to preserve runtime selection; Portal references only SendGrid; both reporting paths send through the provider-neutral email port.

**Tech Stack:** .NET 10, C#, Microsoft.Extensions dependency injection/configuration/hosting/HTTP abstractions, SendGrid 9.29.3, Azure.Identity 1.15.0, MSTest, central package management, locked NuGet restore.

## Global Constraints

- This is a major-version clean split. Do not create a compatibility facade, meta-package, or type-forwarding assembly.
- `Rvt.Communication.Abstractions` has no project or package dependency other than source-link build tooling.
- `Rvt.Communication` depends only on Abstractions and the minimum Microsoft.Extensions dependency-injection abstractions.
- SendGrid, Azure Identity, and TransmitSMS implementation symbols occur only in their owning provider projects.
- Preserve `RVT:` precedence over literal `RVT__` fallback, existing enablement defaults, and existing configuration key names.
- Validation messages name configuration keys but never include secret values, authorization headers, destinations, or provider response bodies.
- Preserve current notification text, delivery classification, cancellation behavior, HTTP routes, and persisted records.
- Keep the synchronous `IMessageService.Sendmessage` and `SendMessage` members operational; their removal is future pending work.
- A provider source file and its test move atomically. Do not compile the same provider implementation in two assemblies.
- Each task finishes with a focused RED/GREEN cycle and a commit.

---

### Task 1: Extract communication abstractions and break the legacy dependency cycle

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/LegacyMessageContracts.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/CommsException.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/CommsException.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/DeliveryFailure.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/DeliveryFailure.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/EmailAttachment.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/EmailAttachment.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/EmailDeliveryRequest.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/EmailDeliveryRequest.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/IEmailDeliveryPort.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/IEmailDeliveryPort.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/IMessageService.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/IMessageService.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/INotificationDeliveryService.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/INotificationDeliveryService.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/INotificationMessageComposer.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/INotificationMessageComposer.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/ISmsDeliveryPort.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/ISmsDeliveryPort.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/NotificationDeliveryContracts.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/NotificationDeliveryContracts.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/SmsDeliveryRequest.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/SmsDeliveryRequest.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Notifications/RvtContactDto.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/RvtContactDto.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/Rvt.Communication.AbstractionsTests.csproj`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Communications/DeliveryContractTests.cs` → `libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/DeliveryContractTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/Architecture/AbstractionsDependencyBoundaryTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/DurableAlertDispatcher.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/EmailAlertDeliveryAdapter.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Alerts/SmsAlertDeliveryAdapter.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Delivery/MonitorDeliveryDispatcher.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rules/RuleAlertNotificationDispatcher.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/MessageService.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/NotificationDeliveryService.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/NotificationMessageComposer.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/AlertDeliveryAdapterTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/DurableAlertDispatcherTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryDispatcherTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rules/SharedRuntimeCompatibilityTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Architecture/SharedRuntimeNamespaceTests.cs`

**Interfaces:**
- Produces: namespace `Rvt.Communication.Abstractions`.
- Produces: existing email/SMS ports, requests, attachments, notification contracts, delivery exceptions, and `CommsException`.
- Produces: `LegacyMessageKind` and `LegacyMessageChannel` as top-level enums.
- Produces: the existing `Rvt.Monitor.Common.Notifications.RvtContactDto` full name from the Abstractions assembly, avoiding an Abstractions → Common reference.
- Consumes: no RVT project and no vendor package.

- [ ] **Step 1: Write the failing dependency and contract tests**

Create `AbstractionsDependencyBoundaryTests.cs` with assertions that load `Rvt.Communication.Abstractions.csproj`, require no `ProjectReference`, and reject `SendGrid`, `Azure.`, `AWSSDK.`, `Microsoft.AspNetCore.App`, and `Rvt.Monitor.Common.csproj`. Add reflection assertions that `IMessageService.SendMessageAsync` accepts `LegacyMessageKind`, `LegacyMessageChannel`, `RvtContactDto`, `string`, `string`, and `CancellationToken`.

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/Rvt.Communication.AbstractionsTests.csproj --nologo
```

Expected: FAIL because the project and top-level legacy contracts do not exist.

- [ ] **Step 3: Create the packable abstractions project**

Use:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>Rvt.Communication.Abstractions</AssemblyName>
    <RootNamespace>Rvt.Communication.Abstractions</RootNamespace>
    <IsPackable>true</IsPackable>
    <PackageId>Rvt.Communication.Abstractions</PackageId>
    <Description>Provider-neutral communication contracts and delivery failures for RVT applications.</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Move every source path named in this task's **Files** block and change the
communication contract namespace to `Rvt.Communication.Abstractions`.

- [ ] **Step 4: Define top-level legacy contracts**

Create:

```csharp
namespace Rvt.Communication.Abstractions;

public enum LegacyMessageKind
{
    Password_Set,
    Password_Forgotten,
    Alert,
    Caution,
    Offline,
    Battery_Caution,
    Battery_Alert,
    Report_Weekly,
    Report_Monthly
}

public enum LegacyMessageChannel
{
    Email = 0,
    SMS = 1,
    Both = 2
}
```

Change all three `IMessageService` methods to accept these top-level enums. This removes the invalid Abstractions → `MessageService.MessageContent` dependency.

- [ ] **Step 5: Move `RvtContactDto` without creating an Abstractions → Common edge**

Keep namespace `Rvt.Monitor.Common.Notifications` so existing domain consumers retain their source-level type name. Remove `using Rvt.Monitor.Common.Utilities` and the unused private `ShouldSendAtTime(TimeSpan, TimeSpan?, TimeSpan?)` overload; preserve every public constructor, property, `FromFlags`, `ShouldSendAtTime(DateTime)`, and `ToString`.

- [ ] **Step 6: Update Common to consume Abstractions**

Add:

```xml
<ProjectReference Include="../Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj" />
```

to `Rvt.Monitor.Common.csproj`. Change imports to `Rvt.Communication.Abstractions`. In production and test callers replace:

```text
MessageService.MessageContent.MessageEnum     → LegacyMessageKind
MessageService.MessageContent.MessageTypeEnum → LegacyMessageChannel
```

Keep enum member spellings unchanged.

- [ ] **Step 7: Convert every active contract caller in the same compile-green slice**

Run:

```bash
rg -l '^using Rvt\.Monitor\.Common\.Communications|Rvt\.Monitor\.Common\.Communications\.|MessageService\.MessageContent' libs/rvt-monitor-common apps/monitors apps/portal services/reporting --glob '*.cs' | sort
```

Change every returned contract import to `Rvt.Communication.Abstractions`.
During this task `MessageService`, `NotificationDeliveryService`, and
`NotificationMessageComposer` remain in Common, so change only their contract
imports and legacy enum usages. Task 2 moves those three types and changes
their concrete-type imports atomically. The same command after this task may
still report the three implementation source files scheduled for Task 2, but
no application or test caller.

- [ ] **Step 8: Run GREEN**

Run:

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/Rvt.Communication.AbstractionsTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --nologo
dotnet build Rvt.Mono.slnx --nologo
```

Expected: both projects and the aggregate build pass; `Rvt.Monitor.Common`
depends on Abstractions, Abstractions does not depend on Common, and active
consumers compile against the moved contracts.

- [ ] **Step 9: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Communication.Abstractions libs/rvt-monitor-common/src/Rvt.Monitor.Common libs/rvt-monitor-common/tests apps/monitors apps/portal services/reporting
git commit -m "refactor: extract communication abstractions"
```

### Task 2: Extract the provider-neutral communication workflow

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/MessageService.cs` → `libs/rvt-monitor-common/src/Rvt.Communication/MessageService.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/NotificationDeliveryService.cs` → `libs/rvt-monitor-common/src/Rvt.Communication/NotificationDeliveryService.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Communications/NotificationMessageComposer.cs` → `libs/rvt-monitor-common/src/Rvt.Communication/NotificationMessageComposer.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication/CommunicationServiceCollectionExtensions.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/Rvt.CommunicationTests.csproj`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Communications/MessageServiceAsyncTests.cs` → `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/MessageServiceAsyncTests.cs`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Communications/MessageServiceCompatibilityTests.cs` → `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/MessageServiceCompatibilityTests.cs`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Communications/NotificationDeliveryServiceTests.cs` → `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/NotificationDeliveryServiceTests.cs`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Communications/NotificationMessageComposerTests.cs` → `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/NotificationMessageComposerTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/CommunicationRegistrationTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/Architecture/CommunicationDependencyBoundaryTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: all ports and models from `Rvt.Communication.Abstractions`.
- Produces: `NotificationMessageComposer`, `NotificationDeliveryService`, and `MessageService` in namespace `Rvt.Communication`.
- Produces: `IServiceCollection AddRvtCommunication(this IServiceCollection services)`.

- [ ] **Step 1: Write RED registration and boundary tests**

Assert that calling `AddRvtCommunication()` twice leaves exactly one descriptor for each of `INotificationMessageComposer`, `INotificationDeliveryService`, and `IMessageService`. Assert the project references Abstractions and rejects SendGrid, Azure Identity, Azure Storage, AWS S3, and provider namespaces.

- [ ] **Step 2: Run RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.CommunicationTests/Rvt.CommunicationTests.csproj --nologo
```

Expected: FAIL because the workflow project and registration method do not exist.

- [ ] **Step 3: Create the workflow project**

Use a packable `net10.0` project with a project reference to Abstractions plus `Microsoft.Extensions.DependencyInjection.Abstractions`; do not add `Microsoft.AspNetCore.App`.

- [ ] **Step 4: Move the implementations**

Change namespaces to `Rvt.Communication`, import Abstractions, and replace nested message enums with `LegacyMessageKind` and `LegacyMessageChannel`. Preserve the synchronous wrappers, failure translation, templates, cancellation, and channel dispatch exactly.

- [ ] **Step 5: Add idempotent neutral registration**

Implement:

```csharp
public static class CommunicationServiceCollectionExtensions
{
    public static IServiceCollection AddRvtCommunication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<INotificationMessageComposer, NotificationMessageComposer>();
        services.TryAddSingleton<INotificationDeliveryService, NotificationDeliveryService>();
        services.TryAddSingleton<IMessageService, MessageService>();
        return services;
    }
}
```

- [ ] **Step 6: Keep existing hosts compile-green**

Add a temporary Infrastructure project reference to `Rvt.Communication`.
Change its registration imports to `Rvt.Communication` and retain the same
three provider-neutral service descriptors through `AddRvtCommunication()`.
The temporary Infrastructure dependency is deleted in Task 8.

- [ ] **Step 7: Move tests and run GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.CommunicationTests/Rvt.CommunicationTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --nologo
dotnet build Rvt.Mono.slnx --nologo
```

Expected: all moved behavior tests, Common tests, and the aggregate build pass.

- [ ] **Step 8: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Communication libs/rvt-monitor-common/src/Rvt.Monitor.Common libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure libs/rvt-monitor-common/tests/Rvt.CommunicationTests libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests
git commit -m "refactor: extract communication workflow"
```

### Task 3: Extract SendGrid mail

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/SendGridMailOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/SendGridMailStartupValidationService.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/SendGridMailServiceCollectionExtensions.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/ISendGridClientFactory.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/ISendGridClientFactory.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/SendGridClientFactory.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/SendGridClientFactory.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/SendGridEmailAdapter.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/SendGridEmailAdapter.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/Rvt.Communication.SendGridMailTests.csproj`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Email/SendGridEmailAdapterTests.cs` → `libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/SendGridEmailAdapterTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/SendGridMailOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/SendGridMailRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`

**Interfaces:**

```csharp
public sealed record SendGridMailOptions
{
    public bool Enabled { get; init; } = true;
    public string ApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = "NoReply@rvtgroup.co.uk";
    public string FromName { get; init; } = "RVT Cloud";
    public static SendGridMailOptions FromConfiguration(IConfiguration configuration);
    public void Validate();
}

public static IServiceCollection AddSendGridMail(
    this IServiceCollection services,
    IConfiguration configuration);

public static IServiceCollection AddSendGridMail(
    this IServiceCollection services,
    SendGridMailOptions options);
```

- [ ] **Step 1: Write RED options, registration, and duplicate-port tests**

Cover `RVT:EMAIL_ENABLED`, `RVT:SENDGRID_API_KEY`, `RVT:EMAIL_ALERT_FROM_EMAIL`, and `RVT:EMAIL_ALERT_FROM_NAME`, including literal `RVT__` fallback and `RVT:` precedence. Assert disabled email permits missing credentials. Assert enabled email reports missing key names without configured values. Assert registering a second `IEmailDeliveryPort` throws `InvalidOperationException("An email delivery provider is already registered.")`.

- [ ] **Step 2: Run RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/Rvt.Communication.SendGridMailTests.csproj --nologo
```

Expected: FAIL because the package does not exist.

- [ ] **Step 3: Move implementation and replace union options**

Change namespace to `Rvt.Communication.SendGridMail`; change the adapter constructor to:

```csharp
public SendGridEmailAdapter(
    ISendGridClientFactory clientFactory,
    SendGridMailOptions options)
```

Replace `EmailEnabled`, `SendGridApiKey`, `FromEmail`, and `FromName` reads with the provider-owned fields. Remove the old email-provider enum check because composition selects the adapter.

- [ ] **Step 4: Implement explicit registration and startup validation**

Both overloads register exactly one `IEmailDeliveryPort`, `ISendGridClientFactory`, the options instance, and a SendGrid-specific `IHostedService`. Inspect existing service descriptors before adding the port and reject duplicates with the exact message above.

- [ ] **Step 5: Keep the old project compile-green temporarily**

Add a project reference from Infrastructure to SendGridMail. Update its temporary `AddMonitorCommunications()` implementation to create `SendGridMailOptions` from `IConfiguration`, register the moved factory/adapter directly, and keep its existing runtime selector. This temporary source dependency disappears in Task 8; it is not published as the new major-version facade.

- [ ] **Step 6: Move existing tests and run GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/Rvt.Communication.SendGridMailTests.csproj --nologo
dotnet build libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj --nologo
```

Expected: SendGrid behavior/options/registration pass and the remaining old project still builds.

- [ ] **Step 7: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests
git commit -m "refactor: extract SendGrid mail adapter"
```

### Task 4: Extract Microsoft Graph mail

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphMailOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphMailStartupValidationService.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphMailServiceCollectionExtensions.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/IMicrosoftGraphAccessTokenProvider.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/IMicrosoftGraphAccessTokenProvider.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/AzureIdentityGraphAccessTokenProvider.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/AzureIdentityGraphAccessTokenProvider.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphEmailAdapter.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphEmailAdapter.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphJsonContext.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphJsonContext.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphModels.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphModels.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphUploadSession.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/MicrosoftGraphUploadSession.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Email/MicrosoftGraphEmailAdapterTests.cs` → `libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/MicrosoftGraphEmailAdapterTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/MicrosoftGraphMailOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/MicrosoftGraphMailRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`

**Interfaces:**

```csharp
public sealed record MicrosoftGraphMailOptions
{
    public bool Enabled { get; init; } = true;
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string SenderAddress { get; init; } = string.Empty;
    public static MicrosoftGraphMailOptions FromConfiguration(IConfiguration configuration);
    public void Validate();
}
```

Registration overloads match Task 3 with names `AddMicrosoftGraphMail`.

- [ ] **Step 1: Write RED configuration, duplicate-port, and SDK-boundary tests**

Cover `EMAIL_ENABLED`, `MICROSOFT_TENANT_ID`, `MICROSOFT_CLIENT_ID`, `MICROSOFT_CLIENT_SECRET`, and `MICROSOFT_SENDER_ADDRESS`; verify alias precedence, disabled behavior, all four missing-key messages, secret-safe failures, one Graph port, and duplicate email-port rejection.

- [ ] **Step 2: Run RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj --nologo
```

Expected: FAIL because the project does not exist.

- [ ] **Step 3: Move all Graph implementation files**

Change namespace to `Rvt.Communication.MicrosoftGraphMail`; replace `CommunicationsOptions` with `MicrosoftGraphMailOptions` in `MicrosoftGraphEmailAdapter` and `AzureIdentityGraphAccessTokenProvider`. Preserve the Graph base URI, JSON source-generation context, attachment thresholds, upload chunks, retry-after handling, caller cancellation, and safe failure classification.

- [ ] **Step 4: Implement provider registration**

Register the options instance, Graph token provider, typed `HttpClient`, Graph adapter, one `IEmailDeliveryPort`, and Graph-specific startup validator. Reject a pre-existing email port before adding descriptors.

- [ ] **Step 5: Keep Infrastructure compile-green temporarily**

Add its project reference to GraphMail and update the old selector to construct the moved Graph adapter from `MicrosoftGraphMailOptions`. Keep runtime choice behavior until the five hosts migrate.

- [ ] **Step 6: Move existing tests and run GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj --nologo
dotnet build libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj --nologo
```

Expected: Graph tests and the transitional build pass.

- [ ] **Step 7: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests
git commit -m "refactor: extract Microsoft Graph mail adapter"
```

### Task 5: Extract TransmitSMS

**Files:**
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/TransmitSmsOptions.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/TransmitSmsStartupValidationService.cs`
- Create: `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/TransmitSmsServiceCollectionExtensions.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Sms/TransmitSmsClient.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/TransmitSmsClient.cs`
- Move: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Sms/TransmitSmsAdapter.cs` → `libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/TransmitSmsAdapter.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Sms/TransmitSmsClientTests.cs` → `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/TransmitSmsClientTests.cs`
- Move: `libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/Sms/TransmitSmsAdapterTests.cs` → `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/TransmitSmsAdapterTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/TransmitSmsOptionsTests.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/TransmitSmsRegistrationTests.cs`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`

**Interfaces:**

```csharp
public sealed record TransmitSmsOptions
{
    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
    public string Sender { get; init; } = "KrakenAlert";
    public static TransmitSmsOptions FromConfiguration(IConfiguration configuration);
    public void Validate();
}
```

Registration overloads are `AddTransmitSms(IServiceCollection, IConfiguration)` and `AddTransmitSms(IServiceCollection, TransmitSmsOptions)`.

- [ ] **Step 1: Write RED configuration, registration, and failure-safety tests**

Cover `SMS_ENABLED`, `SMS_API_KEY`, `SMS_API_SECRET`, and `SMS_SENDER`, including defaults and aliases. Verify disabled validation succeeds, enabled validation names missing keys, configured secrets never appear, one SMS port is registered, and a duplicate SMS port throws `InvalidOperationException("An SMS delivery provider is already registered.")`.

- [ ] **Step 2: Run RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj --nologo
```

Expected: FAIL because the project does not exist.

- [ ] **Step 3: Move implementation and replace union options**

Change namespace to `Rvt.Communication.TransmitSms`; change the adapter constructor to `(HttpClient httpClient, TransmitSmsOptions options)`. Preserve the endpoint, Basic authorization construction, form fields, cancellation, retry-after parsing, provider error-code handling, and safe delivery-exception mapping.

- [ ] **Step 4: Implement explicit registration and validation**

Register the options, typed `HttpClient`, adapter, one `ISmsDeliveryPort`, and TransmitSMS-specific startup validator. Reject duplicate SMS ports before adding descriptors.

- [ ] **Step 5: Keep Infrastructure compile-green temporarily**

Add its project reference to TransmitSms and update old composition to use the moved types and provider-owned options.

- [ ] **Step 6: Move existing tests and run GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj --nologo
dotnet build libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj --nologo
```

Expected: TransmitSMS tests and transitional build pass.

- [ ] **Step 7: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests
git commit -m "refactor: extract TransmitSMS adapter"
```

### Task 6: Migrate the five monitor composition roots

**Files:**
- Modify: `libs/rvt-monitor-common/src/Rvt.Monitor.Common/Hosting/MonitorHost.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/Program.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/AirQMonitorServices.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/Program.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorServiceRegistrationTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOperationalConfigurationTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/Program.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Architecture/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Architecture/OmnidotsAlertArchitectureTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/Config/OmnidotsApiSecurityOptionsTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/EntityFramework/OmnidotsWebhookEndToEndTests.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/UseCases/MonitoringHandlerTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/Program.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Create: `apps/monitors/reportingmonitor/ReportingMonitorTests/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/Program.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/CommunicationsCompositionTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekImportOptionsTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/AirQApi.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitor/api/AirQRuleProcessor.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestAirQApi.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestAirQApiException.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestAirQApiNoiseLevels.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestRules.cs`
- Modify: `apps/monitors/airqmonitor/AirQMonitorTests/TestUtil.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmApi.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitor/api/MyAtmRuleProcessor.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmOutboxDispatcherTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/OfflineAlertCommitTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/ProcessDustLevelsAlertCommitTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestMyAtmApi.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestMyAtmApiExceptions.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestMyAtmApiMonitors.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestRules.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestRules2.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/TestUtil.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/OmnidotsRuleProcessor.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitor/api/UseCases/EmailOmnidotsMonitoringNotifier.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApi.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestOmnidotsApiException.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestRules.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/TestUtil.cs`
- Modify: `apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/UseCases/EmailOmnidotsMonitoringNotifierTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/Messaging/ReportMessageSenderTests.cs`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/ReportMessageSender.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekApi.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitor/api/SvantekRuleProcessor.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/StoreNoiseLevelsParsingTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekJobCancellationTests.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestCheckForSoundRecordings.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestRules.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestSvantekApi.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestSvantekApiException.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestSvantekApiNoiseLevels.cs`
- Modify: `apps/monitors/svantekmonitor/SvantekMonitorTests/TestUtil.cs`

**Interfaces:**
- Changes: `MonitorHost.RunAsync` receives `Action<IServiceCollection, IConfiguration>? configureServices`.
- Changes: each `Add*Monitor` method receives `IConfiguration configuration`.
- Produces: each monitor explicitly references Abstractions, Communication, SendGridMail, MicrosoftGraphMail, and TransmitSms.

- [ ] **Step 1: Add RED provider-selection tests to every monitor**

For AirQ, MyAtm, Omnidots, ReportingMonitor, and Svantek, test:

- missing `EMAIL_PROVIDER` selects `SendGridEmailAdapter`;
- `MicrosoftGraph` case-insensitively selects `MicrosoftGraphEmailAdapter`;
- invalid value throws a message containing only `RVT__EMAIL_PROVIDER`, never the invalid configured value;
- `TransmitSmsAdapter`, `INotificationDeliveryService`, and `IMessageService` resolve;
- startup validation succeeds when `EMAIL_ENABLED=false` and `SMS_ENABLED=false`.

- [ ] **Step 2: Run RED**

```bash
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --filter FullyQualifiedName~CommunicationsCompositionTests --nologo
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --filter FullyQualifiedName~CommunicationsCompositionTests --nologo
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --filter FullyQualifiedName~CommunicationsCompositionTests --nologo
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~CommunicationsCompositionTests --nologo
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --filter FullyQualifiedName~CommunicationsCompositionTests --nologo
```

Expected: FAIL because hosts still call `AddMonitorCommunications`.

- [ ] **Step 3: Pass configuration into monitor composition**

Change the API invocation to
`configureServices?.Invoke(apiBuilder.Services, apiBuilder.Configuration)`.
Change the scheduler and one-shot host invocations to
`configureServices?.Invoke(services, context.Configuration)`. Change each
Program lambda to:

```csharp
configureServices: (services, configuration) =>
    services.AddAirQMonitor(configuration)
```

Use `AddMyAtmMonitor`, `AddOmnidotsMonitor`, `AddReportingMonitor`, and
`AddSvantekMonitor` in the other four Program files. Update every direct test
caller named in this task's **Files** block to pass its existing
`IConfiguration`.

- [ ] **Step 4: Update the exact communication namespace caller manifest**

Use this command as a completeness check against the paths in the **Files**
block:

```bash
rg -l '^using Rvt\.Monitor\.Common\.Communications|MessageService\.MessageContent' apps/monitors --glob '*.cs' | sort
```

The command must return only paths named in this task. Use
`Rvt.Communication.Abstractions` for contracts/enums and `Rvt.Communication`
only where concrete workflow types are named.

- [ ] **Step 5: Compose providers explicitly in every monitor**

In each `*MonitorServices.cs`, call `AddRvtCommunication()`, select email at registration time from:

```csharp
var configuredProvider = configuration["RVT:EMAIL_PROVIDER"]
    ?? configuration["RVT__EMAIL_PROVIDER"]
    ?? "SendGrid";
```

Call `AddSendGridMail(configuration)` or `AddMicrosoftGraphMail(configuration)` based on a case-insensitive exact match; throw `InvalidOperationException("RVT__EMAIL_PROVIDER must be SendGrid or MicrosoftGraph.")` otherwise. Always call `AddTransmitSms(configuration)` so the disabled adapter remains resolvable and preserves current behavior.

- [ ] **Step 6: Replace monitor project references**

In all five host `.csproj` files remove Infrastructure and add project references to:

```text
Rvt.Communication.Abstractions
Rvt.Communication
Rvt.Communication.SendGridMail
Rvt.Communication.MicrosoftGraphMail
Rvt.Communication.TransmitSms
```

Retain each existing `Rvt.Monitor.Common` reference.

- [ ] **Step 7: Run GREEN**

Run the five focused commands from Step 2, then:

```bash
dotnet build apps/monitors/rvt-monitors.sln --nologo
```

Expected: all composition tests and the complete monitor solution build pass
with no active monitor reference to Infrastructure.

- [ ] **Step 8: Commit**

```bash
git add libs/rvt-monitor-common/src/Rvt.Monitor.Common/Hosting apps/monitors
git commit -m "refactor: compose monitor communication providers explicitly"
```

### Task 7: Migrate Portal and both reporting paths

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `apps/portal/RvtPortal.Spa/Adapters/Notifications/RvtCommonEmailDelivery.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/RvtCommonEmailDeliveryTests.cs`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`
- Modify: `apps/monitors/reportingmonitor/Rvt.Reporting.Messaging/ReportMessageSender.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/Messaging/ReportMessageSenderTests.cs`
- Modify: `services/reporting/src/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`
- Move: `services/reporting/src/Rvt.Reporting.Messaging/SendGrid/SendGridReportMessageSender.cs` → `services/reporting/src/Rvt.Reporting.Messaging/ReportMessageSender.cs`
- Modify: `services/reporting/src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj`
- Modify: `services/reporting/src/Rvt.Reporting.Service/Program.cs`
- Create: `services/reporting/tests/Rvt.Reporting.Service.Tests/ReportMessageSenderTests.cs`
- Modify: `services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj`

**Interfaces:**
- Portal consumes Abstractions and SendGridMail only.
- Both reporting message senders consume `IEmailDeliveryPort`.
- The containerized reporting host explicitly registers SendGridMail.

- [ ] **Step 1: Write RED Portal boundary and adapter tests**

Require Portal host project references to Abstractions and SendGridMail. Reject Communication, GraphMail, TransmitSms, Infrastructure, AWS S3, and Graph namespaces. Add adapter tests for successful delivery, debug-recipient override, provider failure translation, and caller cancellation.

- [ ] **Step 2: Write RED reporting tests**

For the containerized service, assert `ReportMessageSender` maps report bytes to one `EmailAttachment`, honors disabled/test-recipient modes, converts `DeliveryException` to `ReportSendResult`, and propagates caller cancellation. Add a compiled dependency assertion that `Rvt.Reporting.Messaging` no longer references the SendGrid assembly.

- [ ] **Step 3: Run RED**

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --filter 'FullyQualifiedName~RvtCommonDependencyBoundaryTests|FullyQualifiedName~RvtCommonEmailDeliveryTests' --nologo
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --filter FullyQualifiedName~ReportMessageSenderTests --nologo
```

Expected: all three commands fail on the old dependency graph.

- [ ] **Step 4: Migrate Portal to SendGridMail**

Retain `PortalEmailOptions` binding. Read the same `EmailConfiguration` section at registration time and call:

```csharp
services.AddSendGridMail(new SendGridMailOptions
{
    Enabled = true,
    ApiKey = configuration["EmailConfiguration:SENDGRID_API_KEY"] ?? string.Empty,
    FromEmail = configuration["EmailConfiguration:Sending_Email_Address"] ?? string.Empty,
    FromName = "RVT Cloud"
});
```

Remove manual `CommunicationsOptions`, `ISendGridClientFactory`, and `SendGridEmailAdapter` descriptors. Update the portal adapter to import Abstractions. Replace the Infrastructure project reference with Abstractions and SendGridMail.

- [ ] **Step 5: Migrate the monitor reporting sender**

Change its project reference from Common to Abstractions and update only its communication namespace. Preserve `ReportMessageSenderOptions`, attachment mapping, test-recipient override, cancellation, and result semantics.

- [ ] **Step 6: Migrate the containerized reporting sender**

Remove direct SendGrid SDK code and package reference. Implement `ReportMessageSender(IEmailDeliveryPort, IOptions<ReportMessageSenderOptions>)` with the same request/result behavior as the monitor reporting sender. Keep only `EmailEnabled`, `EmailTestMode`, and `TestReportToEmail` in its own options. Add a SendGridMail project reference to `Rvt.Reporting.Service`; register provider options in `Program.cs` from existing `RVT:EMAIL_*` and `RVT:SENDGRID_API_KEY` keys.

- [ ] **Step 7: Run GREEN**

Run all commands from Step 3. Expected: all pass and neither reporting messaging project references SendGrid directly.

- [ ] **Step 8: Commit**

```bash
git add apps/portal apps/monitors/reportingmonitor/Rvt.Reporting.Messaging apps/monitors/reportingmonitor/ReportingMonitorTests services/reporting
git commit -m "refactor: migrate portal and reporting mail adapters"
```

### Task 8: Remove `Rvt.Monitor.Common.Infrastructure` and close source boundaries

**Files:**
- Delete: `libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/`
- Delete: `libs/rvt-monitor-common/tests/Rvt.Monitor.Common.InfrastructureTests/`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Architecture/CommunicationsBoundaryTests.cs`
- Modify: `apps/monitors/myatmmonitor/MyAtmMonitorTests/Architecture/CommonPackageBoundaryTests.cs`
- Modify: `apps/monitors/reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/RvtCommonDependencyBoundaryTests.cs`
- Modify: `libs/rvt-monitor-common/rvt-common.sln`
- Modify: `Rvt.Mono.slnx`
- Modify: `libs/rvt-monitor-common/Directory.Packages.props`
- Modify: `scripts/verify-rvt-common-source-boundary.sh`
- Modify: `tests/verify-rvt-common-source-boundary.test.sh`
- Modify: `tests/verify-rvt-common-source-boundary-regression.test.sh`
- Modify: `tests/fixtures/rvt-common-source-boundary/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj`
- Delegated: every `packages.lock.json` change is owned by Task 5,
  **Regenerate the complete locked dependency graph**, in
  `docs/superpowers/plans/2026-07-23-rvt-provider-package-release-migration.md`.

**Interfaces:**
- Consumes: all migrated consumers from Tasks 6 and 7.
- Produces: no active project, source, solution, test, or lock reference to Infrastructure.

- [ ] **Step 1: Write RED exact-ownership guards**

Update architecture tests to require:

- SendGrid package/namespaces only below `src/Rvt.Communication.SendGridMail`;
- Azure Identity and Graph implementation only below `src/Rvt.Communication.MicrosoftGraphMail`;
- TransmitSMS implementation only below `src/Rvt.Communication.TransmitSms`;
- no vendor reference in Abstractions, Communication, or `Rvt.Monitor.Common`;
- no active `Rvt.Monitor.Common.Infrastructure` reference;
- Portal uses only Abstractions and SendGridMail;
- five monitors deliberately reference all three communication providers.

- [ ] **Step 2: Run RED**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter FullyQualifiedName~CommunicationsBoundaryTests --nologo
bash tests/verify-rvt-common-source-boundary.test.sh
```

Expected: fail while Infrastructure remains.

- [ ] **Step 3: Delete the old project and tests**

Before deletion, verify every provider source/test named in Tasks 3–5 exists in its destination. Delete the two old directories. Remove their solution entries and replace them with all five new source projects and all five new test projects.

- [ ] **Step 4: Update central dependencies and locked restores**

Keep SendGrid and Azure Identity central versions for their owning projects. Add exact Microsoft.Extensions abstraction package versions required by new projects. Restore:

```bash
dotnet restore libs/rvt-monitor-common/rvt-common.sln \
  -p:RestorePackagesWithLockFile=false -p:RestoreLockedMode=false --nologo
dotnet restore Rvt.Mono.slnx \
  -p:RestorePackagesWithLockFile=false -p:RestoreLockedMode=false --nologo
```

- [ ] **Step 5: Run GREEN**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --filter FullyQualifiedName~CommunicationsBoundaryTests --nologo
bash tests/verify-rvt-common-source-boundary.test.sh
rg -n 'Rvt\.Monitor\.Common\.Infrastructure|AddMonitorCommunications|CommunicationsOptions' libs/rvt-monitor-common/src apps/monitors apps/portal services/reporting --glob '*.cs' --glob '*.csproj' --glob '*.sln' --glob '*.slnx'
```

Expected: tests pass and `rg` returns exit code 1 with no active matches.

- [ ] **Step 6: Commit**

```bash
git add Rvt.Mono.slnx libs/rvt-monitor-common apps scripts tests services/reporting
git commit -m "refactor: remove common communications infrastructure"
```

### Task 9: Run the complete communication verification gate and record state

**Files:**
- Modify: `libs/rvt-monitor-common/README.md`
- Modify: `apps/monitors/README.md`
- Create: `docs/architecture/rvt-monitor-common/communications.md`
- Modify: `project_state.md`

**Interfaces:**
- Consumes: Tasks 1–8.
- Produces: a verified source-level communication split ready for the separate eleven-package release-pipeline task.

- [ ] **Step 1: Run all five library test projects**

```bash
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/Rvt.Communication.AbstractionsTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.CommunicationTests/Rvt.CommunicationTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/Rvt.Communication.SendGridMailTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/Rvt.Communication.MicrosoftGraphMailTests.csproj --nologo
dotnet test libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/Rvt.Communication.TransmitSmsTests.csproj --nologo
```

Expected: zero failures.

- [ ] **Step 2: Run all active consumer tests**

```bash
dotnet test apps/monitors/airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj --nologo
dotnet test apps/monitors/myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj --nologo
dotnet test apps/monitors/omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj --nologo
dotnet test apps/monitors/reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj --nologo
dotnet test apps/monitors/svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj --nologo
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --nologo
dotnet test services/reporting/tests/Rvt.Reporting.Service.Tests/Rvt.Reporting.Service.Tests.csproj --nologo
```

Expected: zero communication-split regressions; record unrelated provider-gated skips separately.

- [ ] **Step 3: Run aggregate and dependency-isolation gates**

```bash
dotnet build Rvt.Mono.slnx --no-restore --nologo
dotnet list libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj package --include-transitive
dotnet list libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj package --include-transitive
bash tests/verify-rvt-common-source-boundary.test.sh
git diff --check
```

Expected: build and guards pass; neither package listing contains SendGrid, Azure Identity, Azure Storage, or AWS S3.

- [ ] **Step 4: Update documentation and project state**

Document the exact new project graph, explicit monitor provider choice, Portal SendGrid-only choice, both reporting migrations, test counts, and the absence of Infrastructure. Keep storage and package-release work described as pending rather than completed.

- [ ] **Step 5: Commit**

```bash
git add libs/rvt-monitor-common/README.md apps/monitors/README.md docs/architecture/rvt-monitor-common/communications.md project_state.md
git commit -m "docs: record communication provider split"
```

## Future Pending Work

The following items remain explicitly outside this implementation:

- Remove legacy synchronous `IMessageService.Sendmessage` and `SendMessage` after its remaining callers receive a separate compatibility plan.
- Change notification templates, recipients, delivery business rules, or retry policy only under a separate product specification.
- Add dynamic provider discovery or runtime assembly loading only if deployments require providers to be installed without rebuilding a host.
- Add external-consumer compatibility tooling only if coordinated major-version migration proves impossible.
- Change public HTTP APIs or persisted monitor/report records only under an explicit compatibility and data-migration design.
- Unify Portal `BlobStorageClientFactory` and the new storage service through the separately approved `IObjectStorageClientFactory` work.
- Decide customer-logo and reporting-service storage adoption after the Portal blob-unification slice.
- Review database, MQTT, scheduling, and observability dependency boundaries after communication and storage isolation are complete.
- Update the full eleven-package pack, package-consumer, lock, SBOM, vulnerability, release-manifest, and release-asset pipeline in the dedicated package-release plan.
