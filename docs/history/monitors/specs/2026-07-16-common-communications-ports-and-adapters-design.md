# Common Communications Ports and Adapters Design

**Date:** 2026-07-16

**Status:** Approved for implementation planning

**Target branch:** `codex/common-communications-adapters`

**Base:** completed `codex/shared-durable-delivery` foundation and monitor migration work

## Context

The monitor applications and ReportingMonitor currently have two different email architectures. ReportingMonitor owns an application port (`IReportMessageSender`) and a SendGrid adapter, while the shared monitor path exposes `IMessageService` but still hard-wires `EmailSender` and `SmsSender` inside `MessageService`. `EmailSender` is static, creates a SendGrid client itself, reads static configuration, and leaks provider concerns into `Rvt.Monitor.Common`. `CommsClient` also constructs transports directly, and Omnidots has a direct static email call with a hardcoded recipient.

The durable delivery foundation improves persistence and retry behavior, but its dispatcher still calls the mixed legacy `IMessageService`. That leaves transport selection, template composition, provider configuration, and compatibility behavior coupled at the dispatcher boundary.

The target is one provider-neutral communications model with explicit outbound ports, runtime-selected infrastructure adapters, shared template composition, and compatibility only at the legacy edge. SendGrid remains supported. Microsoft 365 outbound delivery uses Microsoft Graph with app-only authentication. TransmitSMS remains the SMS provider behind an explicit adapter.

## Goals

- Route every production email and SMS send through explicit outbound ports.
- Keep communications-provider types and packages outside `Rvt.Monitor.Common`.
- Select SendGrid or Microsoft Graph through environment-backed configuration.
- Preserve ReportingMonitor PDF attachments, test-recipient behavior, and durable per-recipient failure records.
- Preserve current alert, caution, offline, and battery notification text.
- Preserve legacy monitor callers through a temporary `IMessageService` compatibility facade.
- Let the durable dispatcher distinguish retryable failures from permanent or configuration failures.
- Remove static transport entry points, direct adapter construction, and the hardcoded Omnidots destination.
- Keep secrets, tokens, destinations, message bodies, and attachments out of logs and persisted error text.

## Non-goals

- Receiving email through POP3, IMAP, webhooks, or Microsoft Graph subscriptions.
- Converting every legacy synchronous rule-processing path to end-to-end async in this change.
- Replacing SendGrid or TransmitSMS as supported providers.
- Changing notification templates, recipient eligibility, contact schedules, or report-generation business rules.
- Introducing a database schema migration.
- Publishing or extracting the future GitHub Packages repository in this change.

## Project Boundary

Create `rvt-monitor-common/Rvt.Monitor.Common.Infrastructure` as the single current infrastructure assembly. It references `Rvt.Monitor.Common` and owns the provider implementations, provider-specific configuration, and DI composition extension.

`Rvt.Monitor.Common` contains only provider-neutral contracts, immutable requests, failure contracts, notification composition, the async notification delivery service, and the temporary compatibility facade. It no longer references SendGrid. Existing shared storage dependencies remain unchanged; the communications change does not attempt the broader package extraction described in the common-package strategy.

`Rvt.Monitor.Common.Infrastructure` contains:

- `SendGridEmailAdapter`
- `MicrosoftGraphEmailAdapter`
- `TransmitSmsAdapter`
- provider-specific gateways or token abstractions used for deterministic unit tests
- `CommunicationsOptions` and validation
- `AddMonitorCommunications` DI registration

One infrastructure assembly is preferred over one assembly per provider for this migration. It gives hosts one reference and one release unit while still enforcing the ports-and-adapters dependency direction. It also aligns with the approved future `Rvt.Common.Infrastructure` package. Provider-specific packages can be split later without changing the application contracts.

ReportingMonitor keeps `Rvt.Reporting.Messaging` because `IReportMessageSender` is an application-specific port. Its implementation becomes a provider-neutral bridge to `IEmailDeliveryPort`; it does not reference SendGrid, Microsoft Graph, or infrastructure namespaces.

## Provider-Neutral Contracts

Add the following concepts under `Rvt.Monitor.Common.Communications`:

- `IEmailDeliveryPort.SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)`
- `ISmsDeliveryPort.SendAsync(SmsDeliveryRequest request, CancellationToken cancellationToken)`
- `EmailDeliveryRequest` with recipient, subject, optional plain-text body, optional HTML body, and immutable attachments
- `EmailAttachment` with file name, content type, and defensively copied immutable content
- `SmsDeliveryRequest` with recipient and content
- `DeliveryFailureKind` with `Transient`, `Permanent`, and `Configuration`
- typed `EmailDeliveryException` and `SmsDeliveryException` carrying only safe provider, category, status/code, and optional retry delay metadata

The ports complete when the provider accepts the request. They do not claim final mailbox or handset delivery. A requested cancellation is never wrapped.

The requests do not contain credentials or provider selection. Sender identity belongs to the selected adapter configuration. Application code cannot inspect or branch on the provider.

## Notification Composition and Compatibility

Extract the existing alert templates into a pure `INotificationMessageComposer`. It accepts a provider-neutral notification template kind, delivery channel, monitor name, and callback URL, and returns composed subject and content. Exact current text is protected by characterization tests before extraction.

`NotificationDeliveryService` is the async application service for templated monitor notifications. It composes once and invokes `IEmailDeliveryPort` or `ISmsDeliveryPort`. The shared durable dispatcher depends on this service instead of `IMessageService`.

`MessageService` remains temporarily as the source-compatibility facade for legacy rule processors. It maps the current nested enums and contact DTO into `NotificationDeliveryService`. Its default constructor and transport delegates are removed so it cannot construct or select adapters. Its synchronous methods remain only for current callers, are marked obsolete, and translate typed port failures into the existing `CommsException` contract with a safe error and the relevant destination. Architecture tests prohibit new production uses.

The unused `ICommsClient`/`CommsClient`, static `EmailSender`, and internal `SmsSender` are removed after all direct references are migrated.

## Runtime Data Flows

### Durable monitor notification

1. `MonitorDeliveryDispatcher` claims and validates an outbox message as it does today.
2. It maps the payload to a provider-neutral templated notification request.
3. `NotificationDeliveryService` composes the message.
4. The service invokes the selected email port or the SMS port.
5. Provider acceptance completes the outbox item and writes the notification audit.
6. A transient failure schedules the existing bounded backoff retry.
7. A permanent or configuration failure dead-letters immediately and persists only a bounded safe error.
8. Requested cancellation propagates without changing the normal failure classification.

### ReportingMonitor email

1. Reporting keeps `IReportMessageSender` in Core.
2. The messaging bridge applies `EMAIL_ENABLED` and the existing test-recipient override.
3. It maps the subject, HTML/plain-text bodies, and rendered PDF to `EmailDeliveryRequest`.
4. The selected email port sends the request.
5. Success maps to the existing successful `ReportSendResult`.
6. A typed delivery failure maps to a bounded safe failed result, which the existing report workflow persists per recipient while continuing later recipients.
7. Requested cancellation continues to propagate.

### Omnidots operational warning

The direct static email call is replaced by an awaited `IEmailDeliveryPort` call. The destination moves from source code to `RVT__OMNIDOTS_MONITORING_ALERT_TO`. The job is converted only as far as necessary to propagate cancellation and await delivery; unrelated monitoring-time logic remains outside this communications refactor.

## Email Provider Configuration

Provider selection is environment-backed through the standard configuration mapping:

- `RVT__EMAIL_PROVIDER=SendGrid|MicrosoftGraph`
- default: `SendGrid`, preserving existing deployments

SendGrid settings:

- `RVT__SENDGRID_API_KEY`
- `RVT__EMAIL_ALERT_FROM_EMAIL`
- `RVT__EMAIL_ALERT_FROM_NAME`

Microsoft Graph settings:

- `RVT__MICROSOFT_TENANT_ID`
- `RVT__MICROSOFT_CLIENT_ID`
- `RVT__MICROSOFT_CLIENT_SECRET`
- `RVT__MICROSOFT_SENDER_ADDRESS`

Reporting-only behavior remains configured through:

- `RVT__EMAIL_ENABLED`
- `RVT__EMAIL_TEST_MODE`
- `RVT__EMAIL_TEST_REPORT_TO_EMAIL`

Existing SMS settings remain unchanged.

`AddMonitorCommunications` binds and validates the options, registers one selected `IEmailDeliveryPort`, registers `ISmsDeliveryPort`, registers the composer and notification service, and registers `IMessageService` as the compatibility facade. Validation fails before scheduled jobs run when the selected provider lacks required settings. Reporting's existing `EMAIL_ENABLED=false` mode skips provider-credential validation and returns before invoking the port, preserving disabled-email deployments that intentionally have no provider secret. Other monitor hosts remain email-enabled by default. The Microsoft Graph sender mailbox is explicit; its Exchange mailbox display name determines the visible sender name.

## SendGrid Adapter

The SendGrid adapter maps the provider-neutral request into a SendGrid message, including all attachments. It treats successful provider responses as accepted delivery. It classifies HTTP 408, 429, and 5xx responses as transient; request/authentication 4xx responses are permanent unless a documented provider condition requires otherwise. Missing configuration is a configuration failure.

The adapter does not retry internally. Tests use an internal gateway boundary so production tests never call SendGrid.

## Microsoft Graph Adapter

The Graph adapter uses app-only OAuth through the tenant, client ID, and client secret. Token acquisition uses the `.default` Graph scope and a credential object whose normal token cache is reused. Requests send as the explicitly configured mailbox.

For messages without large attachments, the adapter uses the normal Graph mail flow. Attachments smaller than 3 MB are added directly. For an attachment from 3 MB through 150 MB, the adapter creates a draft, creates an attachment upload session, uploads sequential bounded chunks using the opaque pre-authenticated upload URL, and sends the draft. Files above 150 MB are rejected as permanent before a network call. Multiple attachments choose the correct path per attachment.

The Entra application requires admin-approved `Mail.Send`. Supporting 3 MB to 150 MB attachments also requires `Mail.ReadWrite`; access must be restricted to the designated sender mailbox through the tenant's supported application-access controls. Microsoft documents the size thresholds and upload-session permission at <https://learn.microsoft.com/en-us/graph/outlook-large-attachments>.

The adapter accepts Graph's asynchronous accepted response as provider acceptance, not proof of mailbox delivery. It treats network failures, HTTP 408/429/5xx, and documented throttling as transient. It treats invalid payloads and most other 4xx responses as permanent. Authentication or authorization failure is permanent for the message and operationally actionable; missing local credentials is a configuration failure. A safe retry delay may be carried from `Retry-After` metadata.

The implementation uses typed `HttpClient` plumbing and narrow internal token/gateway seams rather than exposing Graph SDK types to the core. No live Microsoft service is called by tests.

## TransmitSMS Adapter

Move the current direct HTTP TransmitSMS behavior behind `ISmsDeliveryPort`. Credentials and sender remain adapter configuration, not request data. The adapter preserves cancellation and classifies network, timeout, throttling, and server failures as transient; invalid recipient/request/authentication failures are permanent or configuration failures as appropriate.

No hidden retry is introduced.

## Failure, Retry, and Audit Policy

Adapters throw only typed provider-neutral delivery failures across the port boundary. Safe failure text may include provider, category, HTTP status or provider code, and a correlation identifier. It must never include credentials, access tokens, authorization headers, recipient values, message content, attachment names/content, or raw provider response bodies.

The durable dispatcher uses failure classification:

- `Transient`: retain the current retry/backoff/maximum-attempt behavior, optionally respecting a safe bounded provider retry delay.
- `Permanent`: dead-letter immediately and write the final notification audit when applicable.
- `Configuration`: dead-letter immediately and surface an operational failure; startup validation should normally prevent this path.

The dispatcher remains the retry owner. Adapter-level retries are disabled to avoid duplicate sends and multiplicative retry timing.

External email and SMS providers do not offer a transaction that is atomic with the local outbox. Delivery is therefore at least once: if a provider accepts a request but the response is lost, a transient retry can produce a duplicate. The dispatcher never retries after a recorded accepted response, and the adapters do not add another retry layer, but the design does not claim exactly-once external delivery.

ReportingMonitor retains its current workflow choice: provider-returned failures and non-cancellation exceptions are persisted per recipient, later recipients continue, the report record is saved, and requested cancellation stops the operation.

Legacy synchronous callers receive `CommsException` so current audit behavior remains compatible while new paths use typed async failures.

## Security and Observability

- Secrets enter through configuration providers and are never written to tracked files.
- No secret, OAuth token, authorization header, destination, body, or attachment content is logged.
- Logs identify provider, delivery kind, safe correlation/outbox ID, category, and status only.
- Graph's opaque upload URL is treated as a credential and is never logged or persisted.
- Architecture tests prevent provider namespaces and packages from re-entering the common core, reporting application code, or monitor handlers.
- The Omnidots operational destination is environment configuration, not a source constant.
- Graph application access is restricted to the designated sender mailbox despite the broad wording of application permissions.

## Testing Strategy

Use test-driven implementation with characterization tests first.

Core tests cover:

- exact current template output
- notification channel routing and cancellation
- immutable request validation
- compatibility facade mapping, obsolete sync behavior, and safe exception translation
- durable dispatcher retry versus immediate dead-letter behavior

Infrastructure tests cover:

- provider selection and startup validation
- SendGrid mapping, attachments, accepted/error responses, cancellation, and redaction
- Graph token use, no-attachment email, small attachment, exact 3 MB boundary, large chunked upload, multiple attachments, 150 MB rejection, accepted/error responses, `Retry-After`, cancellation, and redaction
- TransmitSMS mapping and failure classification

Consumer tests cover:

- Reporting test-recipient override, attachment mapping, disabled behavior, safe failure result, persistence, later-recipient continuation, and cancellation
- Omnidots configured destination, awaited port invocation, and absence of the hardcoded address
- durable delivery audits and ownership behavior remain unchanged around successful sends

Architecture tests enforce:

- communications-provider packages/types exist only in `Rvt.Monitor.Common.Infrastructure`
- `Rvt.Monitor.Common` does not reference SendGrid or Graph SDK packages
- Reporting Core/Messaging and monitor application handlers do not reference vendor namespaces
- production code does not call static `EmailSender`, construct `SmsSender`, or add new synchronous `IMessageService` calls

The release gate runs complete common, infrastructure, ReportingMonitor, and affected monitor suites; the PostgreSQL-backed suites receive the connection only at process runtime. It also runs the root solution build, formatter verification, Docker Compose validation, `git diff --check`, and code-index synchronization if available. Tests never send live email or SMS.

## Implementation Sequence

1. Add characterization tests and provider-neutral contracts in `Rvt.Monitor.Common`.
2. Extract the composer and implement async `NotificationDeliveryService`.
3. Create `Rvt.Monitor.Common.Infrastructure` with DI/options validation.
4. Implement and test SendGrid and TransmitSMS adapters.
5. Implement and test Microsoft Graph small and large attachment flows.
6. Convert `MessageService` into a compatibility facade and remove direct transport construction.
7. Change the durable dispatcher to the async notification service and classified failures.
8. Change Reporting messaging to the provider-neutral email bridge.
9. Change Omnidots operational warning to configured async email delivery.
10. Register infrastructure in every monitor composition root and update solution/project references.
11. Remove static/unused transport classes and direct vendor references from common/reporting.
12. Add architecture guards, update deployment documentation and `project_state.md`, then run the full release gate.

## Rollout and Rollback

Deploy first with the default SendGrid provider so the code boundary changes without a provider change. Configure the Entra application and Graph secrets in staging, use ReportingMonitor test-recipient mode, then switch one container with `RVT__EMAIL_PROVIDER=MicrosoftGraph`. Verify provider acceptance, reporting delivery records, durable notification audits, retry scheduling, and dead letters before switching other deployments.

Both provider credentials may coexist during migration. Rollback is an environment-only change back to `RVT__EMAIL_PROVIDER=SendGrid` followed by a container restart/redeployment. There is no database rollback.

## Branch and Integration Strategy

Implementation occurs in the isolated `.worktrees/common-communications-adapters` worktree on `codex/common-communications-adapters`. Its base includes the completed shared durable dispatcher and subsequent monitor migrations, avoiding a second competing dispatcher implementation. The already-completed durable-delivery worktree is not edited. Integration with `main` happens only after verification and review.
