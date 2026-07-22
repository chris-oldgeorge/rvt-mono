# Common Communications Ports and Adapters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route every monitor and ReportingMonitor email/SMS send through provider-neutral ports with runtime-selectable SendGrid or Microsoft Graph email and a TransmitSMS adapter.

**Architecture:** `Rvt.Monitor.Common` owns immutable requests, ports, typed failures, notification composition, async delivery, and the legacy `IMessageService` facade. New `Rvt.Monitor.Common.Infrastructure` and test projects own provider configuration, SendGrid, Microsoft Graph, TransmitSMS, and DI; both durable dispatcher implementations and ReportingMonitor consume common abstractions only.

**Tech Stack:** .NET 10, ASP.NET Core DI/configuration, MSTest, xUnit, Moq, SendGrid 9.29.3, Azure.Identity 1.15.0, typed `HttpClient`, Microsoft Graph REST v1.0.

## Global Constraints

- Work only in `.worktrees/common-communications-adapters` on `codex/common-communications-adapters`.
- Preserve exact current notification subject/body strings before extracting templates.
- Communications-provider packages and namespaces may exist only in `Rvt.Monitor.Common.Infrastructure`.
- `RVT__EMAIL_PROVIDER` accepts `SendGrid` or `MicrosoftGraph` case-insensitively and defaults to SendGrid.
- Graph uses `RVT__MICROSOFT_TENANT_ID`, `RVT__MICROSOFT_CLIENT_ID`, `RVT__MICROSOFT_CLIENT_SECRET`, and `RVT__MICROSOFT_SENDER_ADDRESS`.
- Preserve existing SendGrid, TransmitSMS, Reporting test/disabled, and Omnidots compatibility settings.
- Requested cancellation always propagates; adapters never retry internally.
- Delivery is at least once; do not claim exactly-once external delivery.
- Safe errors contain provider/category/status/correlation only—never secrets, tokens, destinations, bodies, attachment names/data, raw responses, or Graph upload URLs.
- Tests never call live email/SMS providers. No database schema change.
- Use TDD, `apply_patch`, focused files, and one independently reviewable commit per task.

---

### Task 1: Provider-neutral contracts and characterized templates

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/EmailAttachment.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/EmailDeliveryRequest.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/IEmailDeliveryPort.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/SmsDeliveryRequest.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/ISmsDeliveryPort.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/DeliveryFailure.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/NotificationDeliveryContracts.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/INotificationMessageComposer.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/NotificationMessageComposer.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Communications/DeliveryContractTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Communications/NotificationMessageComposerTests.cs`

**Interfaces:**
- Produces: `IEmailDeliveryPort.SendAsync(EmailDeliveryRequest, CancellationToken)`.
- Produces: `ISmsDeliveryPort.SendAsync(SmsDeliveryRequest, CancellationToken)`.
- Produces: `DeliveryFailureKind`, abstract `DeliveryException`, `EmailDeliveryException`, `SmsDeliveryException`, and pure notification composition.

- [ ] **Step 1: Write failing contract tests**

```csharp
[TestMethod]
public void EmailAttachment_DefensivelyCopiesContent()
{
    var bytes = new byte[] { 1, 2, 3 };
    var attachment = new EmailAttachment("report.pdf", "application/pdf", bytes);
    bytes[0] = 9;
    using var stream = attachment.OpenRead();
    Assert.AreEqual(1, stream.ReadByte());
    Assert.AreEqual(3, attachment.Length);
}

[TestMethod]
public void EmailFailure_FormatsOnlySafeMetadata()
{
    var error = new EmailDeliveryException(
        "MicrosoftGraph", DeliveryFailureKind.Transient, "429", TimeSpan.FromSeconds(30));
    Assert.AreEqual("MicrosoftGraph email delivery failed (Transient, code 429).", error.Message);
    Assert.AreEqual(TimeSpan.FromSeconds(30), error.RetryAfter);
}
```

Also test blank email/SMS fields, blank attachment name/type, defensive copies, and exact public signatures.

- [ ] **Step 2: Run RED**

Run `dotnet test rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj --no-restore --nologo --filter "FullyQualifiedName~DeliveryContractTests"`.

Expected: build failure because the contracts do not exist.

- [ ] **Step 3: Implement exact contracts**

```csharp
public interface IEmailDeliveryPort
{
    Task SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default);
}

public interface ISmsDeliveryPort
{
    Task SendAsync(SmsDeliveryRequest request, CancellationToken cancellationToken = default);
}

public sealed record EmailDeliveryRequest(
    string Recipient,
    string Subject,
    string PlainTextBody,
    string HtmlBody,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record SmsDeliveryRequest(string Recipient, string Content);
public enum DeliveryFailureKind { Transient, Permanent, Configuration }
```

`EmailAttachment` copies input bytes into a private array, exposes `long Length`, and returns a non-writable stream. Abstract `DeliveryException : Exception` exposes `Provider`, `FailureKind`, `Code`, and `RetryAfter`; sealed email/SMS subclasses supply the safe channel name. No constructor accepts raw response text.

- [ ] **Step 4: Characterize all ten template variants**

Use a `DynamicData` matrix for Alert, Caution, Offline, BatteryCaution, and BatteryAlert over Email/Sms. Expected strings are literal copies from current `MessageService.cs`, including whitespace, HTML, line endings, and the Alert SMS URL quotes:

```csharp
var result = new NotificationMessageComposer().Compose(
    kind, channel, "fleet-1", "https://portal.example/Notification/View/1");
Assert.AreEqual(expectedSubject, result.Subject);
Assert.AreEqual(expectedPlain, result.PlainTextBody);
Assert.AreEqual(expectedHtml, result.HtmlBody);
```

- [ ] **Step 5: Implement the pure composer**

```csharp
public enum NotificationMessageKind { Alert, Caution, Offline, BatteryCaution, BatteryAlert }
public enum NotificationChannel { Email, Sms }
public sealed record ComposedNotification(string Subject, string PlainTextBody, string HtmlBody);

public interface INotificationMessageComposer
{
    ComposedNotification Compose(
        NotificationMessageKind kind,
        NotificationChannel channel,
        string monitorName,
        string callbackUrl);
}
```

Use one immutable dictionary keyed by `(kind, channel)`, substitute `{Monitor}` and `{callbackUrl}`, and reject undefined enum values.

- [ ] **Step 6: Run GREEN and commit**

Run focused tests, then the full Common suite. Expected: focused tests and at least the 367-test baseline pass.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Communications rvt-monitor-common/Rvt.Monitor.CommonTests/Communications
git commit -m "feat(common): define communication delivery ports"
```

---

### Task 2: Async notification service and compatibility facade

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/INotificationDeliveryService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common/Communications/NotificationDeliveryService.cs`
- Replace: `rvt-monitor-common/Rvt.Monitor.Common/Communications/MessageService.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Communications/IMessageService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Communications/NotificationDeliveryServiceTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Communications/MessageServiceAsyncTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/EmailAlertDeliveryAdapter.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/SmsAlertDeliveryAdapter.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/AlertDeliveryAdapterTests.cs`

**Interfaces:**
- Consumes: Task 1 contracts/composer.
- Produces: `INotificationDeliveryService.SendAsync(NotificationDeliveryRequest, CancellationToken)`.
- Preserves: current `IMessageService` signatures/nested enums for legacy callers.

- [ ] **Step 1: Write failing routing/facade tests**

```csharp
var request = new NotificationDeliveryRequest(
    NotificationMessageKind.Alert,
    NotificationChannel.Email,
    "ops@example.test",
    "fleet-1",
    "https://portal/1");
composer.Setup(x => x.Compose(request.Kind, request.Channel, request.MonitorName, request.CallbackUrl))
    .Returns(new ComposedNotification("subject", "plain", "<p>html</p>"));
email.Setup(x => x.SendAsync(
        It.Is<EmailDeliveryRequest>(m => m.Recipient == request.Destination && m.Subject == "subject"),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
await service.SendAsync(request, CancellationToken.None);
sms.VerifyNoOtherCalls();
```

Add SMS, cancellation, legacy enum mapping, `Both` rejection, sync waiting, and typed-exception-to-`CommsException` tests.

- [ ] **Step 2: Run RED**

Run the `NotificationDeliveryServiceTests|MessageServiceAsyncTests` filter. Expected: missing types/old constructors.

- [ ] **Step 3: Implement async delivery**

```csharp
public sealed record NotificationDeliveryRequest(
    NotificationMessageKind Kind,
    NotificationChannel Channel,
    string Destination,
    string MonitorName,
    string CallbackUrl);

public interface INotificationDeliveryService
{
    Task SendAsync(NotificationDeliveryRequest request, CancellationToken cancellationToken = default);
}
```

Compose once; map Email to attachment-free `EmailDeliveryRequest`, Sms to `SmsDeliveryRequest`; await exactly one port; propagate cancellation/failures.

- [ ] **Step 4: Replace `MessageService` internals**

Its only constructor accepts `INotificationDeliveryService`. Keep public signatures/enums. Map supported legacy enums to new kinds/channels, reject Password/Report/Both values, mark sync methods `[Obsolete("Use SendMessageAsync. Synchronous delivery remains only for legacy callers.")]`, and translate `DeliveryException` to `CommsException.Of(destination, exception.Message)` without catching cancellation.

- [ ] **Step 5: Migrate Common durable alert adapters**

Change Email/Sms alert adapters to `INotificationDeliveryService`; build `NotificationDeliveryRequest` from the validated envelope/claim. Preserve audit and URL behavior.

- [ ] **Step 6: Run GREEN and commit**

Run focused service/facade/adapter tests, then full Common.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Communications rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests
git commit -m "refactor(common): route notifications through ports"
```

---

### Task 3: Classified failures in both durable dispatchers

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Delivery/MonitorDeliveryDispatcher.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryDispatcherTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Alerts/DurableAlertDispatcher.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Alerts/DurableAlertDispatcherTests.cs`

**Interfaces:**
- Consumes: async notification service and typed failures.
- Preserves: fenced ownership, audits, batch failure modes, and cancellation.

- [ ] **Step 1: Write failing policy tests**

For both dispatchers, inject:

```csharp
new EmailDeliveryException("SendGrid", DeliveryFailureKind.Permanent, "400")
```

Assert immediate dead letter/final audit on attempt 1. Assert Transient retries until max, max-attempt dead letter, Configuration immediate dead letter, `RetryAfter` raises delay but is capped, caller cancellation makes no mutation, and untyped exceptions keep type-only retry behavior.

- [ ] **Step 2: Run RED**

Run `MonitorDeliveryDispatcherTests|DurableAlertDispatcherTests`. Expected: current all-retry behavior fails permanent/configuration cases.

- [ ] **Step 3: Implement classification**

Replace `MonitorDeliveryDispatcher`'s `IMessageService` with `INotificationDeliveryService`. Terminal condition:

```csharp
exception is DeliveryException { FailureKind: not DeliveryFailureKind.Transient }
    || attemptCount >= maxAttempts
```

Retry delay is `min(max(exponential, RetryAfter ?? 0), retryCap)`. Typed errors persist their safe `Message`; other errors remain `Delivery failed ({TypeName}).`. Apply the same policy in `DurableAlertDispatcher`, preserving its rule that only dead letters aggregate after the pass.

Remove destination data—even redacted destination text—from both dispatcher failure log templates. Continue storing the destination only in the existing authoritative notification audit address column.

- [ ] **Step 4: Run GREEN and commit**

Run focused dispatchers then full Common.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common/Delivery rvt-monitor-common/Rvt.Monitor.Common/Alerts rvt-monitor-common/Rvt.Monitor.CommonTests
git commit -m "fix(common): classify durable delivery failures"
```

---

### Task 4: Infrastructure project, configuration, DI, and TransmitSMS

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Properties/AssemblyInfo.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Communications/EmailProvider.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsOptions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsStartupValidationService.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Sms/TransmitSmsClient.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Sms/TransmitSmsAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Communications/CommunicationsOptionsTests.cs`
- Move: `rvt-monitor-common/Rvt.Monitor.CommonTests/Sms/TransmitSmsClientTests.cs`
- Modify: `rvt-monitor-common/rvt-monitor-common.sln`
- Modify: `rvt-monitors.sln`

**Interfaces:**
- Produces: validated `CommunicationsOptions`, `TransmitSmsAdapter`, and startup validation primitives consumed by Task 5.

- [ ] **Step 1: Scaffold and write failing option/SMS tests**

Infrastructure references Common and `Microsoft.AspNetCore.App`; Tasks 5-6 add provider packages when their adapters are implemented. Tests use MSTest/Moq. Cover default/case-insensitive provider, invalid provider, each missing provider setting, disabled email allowing absent email secrets, SMS enabled missing key/secret, and validation messages excluding secret values.

- [ ] **Step 2: Implement exact options**

```csharp
public EmailProvider EmailProvider { get; init; } = EmailProvider.SendGrid;
public bool EmailEnabled { get; init; } = true;
public string SendGridApiKey { get; init; } = string.Empty;
public string FromEmail { get; init; } = "NoReply@rvtgroup.co.uk";
public string FromName { get; init; } = "RVT Cloud";
public string MicrosoftTenantId { get; init; } = string.Empty;
public string MicrosoftClientId { get; init; } = string.Empty;
public string MicrosoftClientSecret { get; init; } = string.Empty;
public string MicrosoftSenderAddress { get; init; } = string.Empty;
public bool SmsEnabled { get; init; }
public string SmsApiKey { get; init; } = string.Empty;
public string SmsApiSecret { get; init; } = string.Empty;
public string SmsSender { get; init; } = "KrakenAlert";
```

Bind `RVT:<UPPER_SNAKE>` then literal `RVT__<UPPER_SNAKE>` fallback. Validate setting names only; skip email/SMS credentials when that channel is disabled.

- [ ] **Step 3: Move TransmitSMS and implement port**

Move current HTTP/JSON code without changing wire behavior. Adapter throws Configuration when disabled, Transient for network/timeout/429/5xx, Permanent for authentication/invalid request/recipient, and never carries raw response text.

- [ ] **Step 4: Implement startup validation primitive**

`CommunicationsStartupValidationService` receives `CommunicationsOptions`; `StartAsync` calls `Validate()` and `StopAsync` completes immediately. Task 5 registers it together with the complete communication graph after the default SendGrid port exists. Do not create a throwing email adapter or incomplete public `AddMonitorCommunications` extension in this task.

- [ ] **Step 5: Run GREEN and commit**

Run Infrastructure and Common suites; add both projects to root/common solutions.

```bash
git add rvt-monitor-common rvt-monitors.sln
git commit -m "feat(common): add communications infrastructure"
```

---

### Task 5: SendGrid adapter

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/ISendGridClientFactory.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/SendGridClientFactory.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/SendGrid/SendGridEmailAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Email/SendGridEmailAdapterTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Communications/CommunicationsServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Produces: default `IEmailDeliveryPort` provider with no hidden retry.

- [ ] **Step 1: Write failing mapping/failure tests**

Add SendGrid 9.29.3 to the Infrastructure project. Capture `SendGridMessage` through mocked `ISendGridClient`. Assert sender, recipient, subject, plain/HTML bodies, filename, MIME type, base64 bytes, 2xx success, 408/429/5xx Transient, other 4xx Permanent, `Retry-After`, cancellation, network failure, and error redaction. DI tests assert default SendGrid selection plus registrations for composer, async service, compatibility facade, SMS port, and startup validation.

- [ ] **Step 2: Run RED**

Run the `SendGridEmailAdapterTests` filter. Expected: missing/shell adapter failure.

- [ ] **Step 3: Implement adapter**

Cache one client from the factory; map every attachment by reading its non-writable stream; accept `IsSuccessStatusCode`; dispose responses; parse headers only. Implement public `AddMonitorCommunications()` to bind options and register the full graph with SendGrid as the default selected `IEmailDeliveryPort`. Classification:

```csharp
status is HttpStatusCode.RequestTimeout or (HttpStatusCode)429
    || (int)status >= 500
        ? DeliveryFailureKind.Transient
        : DeliveryFailureKind.Permanent;
```

- [ ] **Step 4: Run GREEN and commit**

Run focused, full Infrastructure, and Common.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common.Infrastructure rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests
git commit -m "feat(common): add SendGrid email adapter"
```

---

### Task 6: Microsoft Graph app-only email and small attachments

**Files:**
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/IMicrosoftGraphAccessTokenProvider.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/AzureIdentityGraphAccessTokenProvider.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphModels.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphJsonContext.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphEmailAdapter.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Email/MicrosoftGraphEmailAdapterTests.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Communications/CommunicationsServiceCollectionExtensions.cs`

**Interfaces:**
- Produces: authenticated `/users/{sender}/sendMail` flow for attachments below 3 MiB.

- [ ] **Step 1: Write failing Graph tests**

Use a recording `HttpMessageHandler` and fake token provider. Assert POST URL with escaped sender, per-request Bearer token, Text/HTML bodies, recipient, `saveToSentItems=true`, file-attachment type/base64, multiple/no attachments, 202 acceptance, status classification, `Retry-After`, token/network failures, cancellation, and redaction.

- [ ] **Step 2: Run RED**

Run `MicrosoftGraphEmailAdapterTests`. Expected: missing adapter.

- [ ] **Step 3: Implement token provider**

```csharp
private static readonly TokenRequestContext TokenContext =
    new(["https://graph.microsoft.com/.default"]);

public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
    (await credential.GetTokenAsync(TokenContext, cancellationToken)).Token;
```

Add Azure.Identity 1.15.0 to Infrastructure. Reuse one `ClientSecretCredential`. Classify an inner `RequestFailedException` with 408/429/5xx as Transient; classify invalid tenant/client/secret and other authentication failures as Configuration. Never log credential exception details.

- [ ] **Step 4: Implement small-message flow**

Define `SmallAttachmentLimit = 3 * 1024 * 1024`. If every attachment is smaller, serialize source-generated DTOs and POST once. Add Authorization to that request only, accept successful status, dispose all HTTP objects, never read raw error bodies. Extend `AddMonitorCommunications` so its provider switch resolves `MicrosoftGraphEmailAdapter` only when `EmailProvider.MicrosoftGraph` is selected; keep SendGrid as the default branch.

- [ ] **Step 5: Run GREEN and commit**

Run Graph/DI filters then full Infrastructure.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common.Infrastructure rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests
git commit -m "feat(common): add Microsoft Graph email adapter"
```

---

### Task 7: Graph large report attachments

**Files:**
- Modify: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphEmailAdapter.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphModels.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphJsonContext.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Email/MicrosoftGraph/MicrosoftGraphUploadSession.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Email/MicrosoftGraphEmailAdapterTests.cs`

**Interfaces:**
- Extends: Graph delivery for 3-150 MiB attachments.

- [ ] **Step 1: Write failing boundary/chunk tests**

Cover `3 MiB - 1` sendMail, exactly 3 MiB draft/upload/send, mixed small/large attachments, ordered 7 MiB chunks, exact inclusive Content-Range, max 3 MiB chunk, no Authorization on upload PUT, HTTPS upload URL validation/redaction, exactly 150 MiB allowed with bounded buffering, above 150 MiB rejected before token/network, and cancellation before draft send.

- [ ] **Step 2: Run RED**

Run Graph tests. Expected: large-flow failures.

- [ ] **Step 3: Implement state machine**

1. Authenticated POST `/users/{sender}/messages`; require draft ID.
2. POST small files to `/messages/{id}/attachments`.
3. POST `/attachments/createUploadSession` for each large file.
4. Require absolute HTTPS opaque URL.
5. PUT sequential stream chunks up to `3 * 1024 * 1024` with octet-stream, Content-Length, and `bytes {start}-{end}/{total}`; no Authorization.
6. Authenticated POST `/messages/{id}/send` after all attachments.

Never base64 a large file or expose the upload URL.

- [ ] **Step 4: Run GREEN and commit**

Run Graph and full Infrastructure.

```bash
git add rvt-monitor-common/Rvt.Monitor.Common.Infrastructure rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests
git commit -m "feat(common): support Graph report attachments"
```

---

### Task 8: Register infrastructure in all monitor hosts

**Files:**
- Modify: `airqmonitor/AirQMonitor/AirQMonitor.csproj`
- Modify: `airqmonitor/AirQMonitor/api/AirQMonitorServices.cs`
- Modify: `svantekmonitor/SvantekMonitor/SvantekMonitor.csproj`
- Modify: `svantekmonitor/SvantekMonitor/api/SvantekMonitorServices.cs`
- Modify: `myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj`
- Modify: `myatmmonitor/MyAtmMonitor/api/MyAtmMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: relevant composition tests in all four monitor test projects

**Interfaces:**
- Consumes: `AddMonitorCommunications`; preserves `IMessageService` for legacy monitor APIs.

- [ ] **Step 1: Add failing composition tests**

Supply fake SendGrid/from configuration and `SMS_ENABLED=false`; assert `IEmailDeliveryPort`, `ISmsDeliveryPort`, `INotificationDeliveryService`, and `IMessageService` resolve and host startup validation succeeds without provider calls.

- [ ] **Step 2: Run RED**

Run architecture/composition filters for all four monitor suites. Expected: missing infrastructure references/registrations.

- [ ] **Step 3: Replace direct registration**

Add Infrastructure project references and replace every `AddSingleton<IMessageService, MessageService>()` with `AddMonitorCommunications()`. Keep existing legacy consumers unchanged. Ensure Omnidots `AddDurableAlerts` and MyATM/Svantek monitor-delivery dispatchers resolve the async service.

- [ ] **Step 4: Run GREEN and commit**

Run AirQ, Svantek, MyATM non-PostgreSQL, and Omnidots non-PostgreSQL suites.

```bash
git add airqmonitor svantekmonitor myatmmonitor omnidotsmonitor
git commit -m "refactor(monitors): compose communication adapters"
```

---

### Task 9: ReportingMonitor email bridge

**Files:**
- Create: `reportingmonitor/Rvt.Reporting.Messaging/ReportMessageSender.cs`
- Delete: `reportingmonitor/Rvt.Reporting.Messaging/SendGrid/SendGridReportMessageSender.cs`
- Modify: `reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj`
- Modify: `reportingmonitor/ReportingMonitor/ReportingMonitor.csproj`
- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorServices.cs`
- Modify: `reportingmonitor/ReportingMonitor/api/ReportingMonitorOptions.cs`
- Create: `reportingmonitor/ReportingMonitorTests/Messaging/ReportMessageSenderTests.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/Architecture/ReportingDependencyBoundaryTests.cs`
- Modify: `reportingmonitor/ReportingMonitorTests/TestReportingFixture.cs`

**Interfaces:**
- Consumes: `IEmailDeliveryPort`; preserves `IReportMessageSender` and report failure persistence.

- [ ] **Step 1: Write failing bridge tests**

Assert mapping of recipient, `RVT Cloud report for {postcode}`, current plain/HTML body, PDF filename/MIME/bytes; disabled success without port call; test-recipient override; typed safe failure; untyped type-only failure; requested cancellation.

- [ ] **Step 2: Run RED**

Run `ReportMessageSenderTests|ReportingDependencyBoundaryTests`. Expected: missing bridge/vendor-bound source.

- [ ] **Step 3: Implement bridge/composition**

`ReportMessageSender` depends on `IEmailDeliveryPort` plus app-local `EmailEnabled`, `EmailTestMode`, `TestReportToEmail`. It creates one PDF `EmailAttachment`; catches `DeliveryException` to safe failed result, catches other non-cancellation exceptions as `Email delivery failed ({TypeName}).`, and propagates requested cancellation. Replace Messaging's SendGrid package with a Common project reference; add Infrastructure only to the Reporting host. Remove Reporting SendGrid/from/key options. Host calls `AddMonitorCommunications()` and registers `IReportMessageSender, ReportMessageSender`.

- [ ] **Step 4: Run GREEN and commit**

Run complete ReportingMonitor suite with PostgreSQL connection supplied only to the process; expected at least 68 baseline plus new tests.

```bash
git add reportingmonitor
git commit -m "refactor(reporting): use shared email delivery port"
```

---

### Task 10: Omnidots awaited operational warning

**Files:**
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/IOmnidotsMonitoringNotifier.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/EmailOmnidotsMonitoringNotifier.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/UseCases/MonitoringHandler.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/MonitorJobRunner.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/model/config/OmnidotsMonitoringOptions.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitor/api/OmnidotsMonitorServices.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/UseCases/MonitoringHandlerTests.cs`
- Modify: `omnidotsmonitor/OmnidotsMonitorTests/TestMonitorJobScheduling.cs`

**Interfaces:**
- Consumes: `IEmailDeliveryPort`; produces awaited/cancellable warning delivery.

- [ ] **Step 1: Write failing async tests**

```csharp
notifier.Setup(x => x.SendNoDataWarningAsync(Recipient, utcNow.UtcDateTime, token))
    .Returns(Task.CompletedTask);
await handler.RunAsync(token);
```

Assert notifier maps exact existing subject/ISO body to email port, cancellation propagates, Monitoring job awaits, and no hardcoded recipient remains.

- [ ] **Step 2: Run RED**

Run `MonitoringHandlerTests|TestMonitorJobScheduling`. Expected: new async signatures absent.

- [ ] **Step 3: Implement async chain**

```csharp
public interface IOmnidotsMonitoringNotifier
{
    Task SendNoDataWarningAsync(
        string recipient,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
```

Notifier injects `IEmailDeliveryPort`; handler becomes `RunAsync`; flow cancellation through Omnidots API/service/job only for Monitoring. Prefer `RVT__OMNIDOTS_MONITORING_ALERT_TO`, with existing `Omnidots:Monitoring:Recipient` fallback; validation names the setting, not its value.

- [ ] **Step 4: Run GREEN and commit**

Run Omnidots non-PostgreSQL suite.

```bash
git add omnidotsmonitor
git commit -m "refactor(omnidots): await monitoring email delivery"
```

---

### Task 11: Remove legacy transports and guard boundaries

**Files:**
- Delete: `rvt-monitor-common/Rvt.Monitor.Common/Communications/EmailSender.cs`
- Delete: `rvt-monitor-common/Rvt.Monitor.Common/Communications/SmsSender.cs`
- Delete: `rvt-monitor-common/Rvt.Monitor.Common/Communications/CommsClient.cs`
- Delete: `rvt-monitor-common/Rvt.Monitor.Common/Communications/ICommsClient.cs`
- Delete: `rvt-monitor-common/Rvt.Monitor.Common/Sms/TransmitSmsClient.cs`
- Modify: `rvt-monitor-common/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj`
- Modify: `rvt-monitor-common/Rvt.Monitor.CommonTests/Architecture/SharedRuntimeNamespaceTests.cs`
- Create: `rvt-monitor-common/Rvt.Monitor.CommonTests/Architecture/CommunicationsBoundaryTests.cs`
- Modify: reporting/monitor architecture tests that allowlist legacy boundaries

**Interfaces:**
- Removes direct/static transports; retains only `IMessageService` compatibility.

- [ ] **Step 1: Write failing boundary tests**

Assert Common has no SendGrid reference, no source calls `EmailSender.` or `new SmsSender`, Reporting has no SendGrid namespace/package, provider packages/types occur only in Infrastructure, and an explicit existing-file allowlist is the only place obsolete sync `IMessageService` methods are called.

- [ ] **Step 2: Run RED**

Run common/reporting/monitor architecture filters. Expected: legacy files/references violate boundaries.

- [ ] **Step 3: Remove legacy code/references**

Delete listed files, remove SendGrid from Common, update namespace architecture test to assert new ports/service instead of `ICommsClient`. Retain public `RvtConfig` compatibility fields for this release but prove communications code does not read them.

- [ ] **Step 4: Run GREEN and commit**

Run architecture plus full Common/Infrastructure.

```bash
git add -A rvt-monitor-common reportingmonitor airqmonitor myatmmonitor omnidotsmonitor svantekmonitor
git commit -m "refactor(common): remove legacy communication transports"
```

---

### Task 12: Documentation and release gate

**Files:**
- Modify: `README.md`
- Modify: `reportingmonitor/README.md`
- Modify: monitor READMEs containing environment settings
- Modify: `docker-compose.yml`
- Modify: `project_state.md`
- Modify: design spec only for an approved factual implementation adjustment

**Interfaces:**
- Documents: provider selection, Graph permissions, runtime variables, at-least-once behavior, rollout, and rollback.

- [ ] **Step 1: Update operations documentation**

Document all SendGrid/Graph/SMS/Reporting/Omnidots variables; `Mail.Send` plus `Mail.ReadWrite` for 3-150 MiB; sender-mailbox restriction; mailbox display-name behavior; no POP/IMAP; retry/dead-letter/safe errors; at-least-once duplicates; staging/test-recipient rollout; and environment-only SendGrid rollback. Compose contains variable names/defaults only, never secrets.

- [ ] **Step 2: Update `project_state.md`**

Record actual project tree, contracts, variables, commits, test counts, limitations, no live sends, PostgreSQL runtime-only usage, and release/rollback state.

- [ ] **Step 3: Run static release checks**

```bash
dotnet format rvt-monitors.sln --no-restore
dotnet format rvt-monitors.sln --no-restore --verify-no-changes
docker compose config --quiet
git diff --check
rg -n "EmailSender\.|new SmsSender|using SendGrid|SendGrid.Helpers.Mail" --glob '*.cs' --glob '*.csproj' .
```

Expected: format/Compose/diff pass; provider scan matches Infrastructure only.

- [ ] **Step 4: Run complete suites**

Run Common, Infrastructure, AirQ, Svantek, MyATM, Omnidots, and Reporting projects. Supply authorized PostgreSQL only to provider-backed processes. Expected: zero failed/skipped release-gate tests.

- [ ] **Step 5: Build and publish-check**

```bash
dotnet build rvt-monitors.sln --no-restore --nologo -m:1
dotnet publish reportingmonitor/ReportingMonitor/ReportingMonitor.csproj -c Release --no-restore --nologo -o /tmp/rvt-reporting-publish
dotnet publish omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj -c Release --no-restore --nologo -o /tmp/rvt-omnidots-publish
```

Expected: zero warnings/errors; provider assemblies/dependencies are in publish outputs.

- [ ] **Step 6: Review, commit, and push**

```bash
git diff --check main...HEAD
git log --oneline main..HEAD
git add README.md reportingmonitor/README.md airqmonitor/README.md myatmmonitor/README.md omnidotsmonitor/README.md svantekmonitor/README.md docker-compose.yml project_state.md
git commit -m "docs: record communications adapter rollout"
git push -u origin codex/common-communications-adapters
```

Expected: complete verified branch pushed for review; do not merge before final diff review.
