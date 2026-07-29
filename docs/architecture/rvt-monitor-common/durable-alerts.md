# Durable alert stack

## Status

As of 2026-07-29 every monitor's alerting flows through the durable alert
stack in `Rvt.Monitor.Common.Alerts`. The legacy synchronous messaging path
(`IMessageService`, `MessageService`, `LegacyMessageKind`/`LegacyMessageChannel`,
`CommsException`, the `Rvt.Monitor.Common.Notifications` contact DTO that lived
in the Abstractions assembly, and the per-monitor inline dispatcher loops —
`RuleAlertNotificationDispatcher`, `OmnidotsRuleProcessor`, MyAtm's
compat-only direct-delivery route) is deleted. `Rules.RvtContactDto` is the
single contact DTO. Two messaging-boundary architecture tests pin the sync
caller allowlists at empty.

## Flow

```
emitter (handler / NoiseRuleEvaluator)
  └─ IAlertIngressPort.AcceptAsync(AlertSignal)
       └─ DurableAlertService            validates; hashes SourceEventKey → NotificationId
            └─ EfAlertCommitStore<TContext>.CommitAsync   (Serializable txn)
                 ├─ occurrence row (source-key dedup authority)
                 ├─ suppression-window query + CautionAlertAcceptancePolicy
                 └─ on Accepted: Notification row + PlanDeliveriesAsync
                      ├─ Mqtt outbox row (when the signal carries the channel)
                      └─ per-contact Email/Sms outbox rows
                         (deployment→contract→site-user→settings join,
                          send-window filtering at delivery-plan time)
  ── later ──
DurableAlertBackgroundService → DurableAlertDispatcher
  └─ claim/lease → adapter (Mqtt/Email/Sms) → complete | retry | dead-letter
                                              (+ audit rows for Email/Sms)
```

## Signal contract

`AlertSignal(Source, SourceEventKey, EventTime, SerialId, AlertType, Field,
Level, Limit, AveragingPeriod, Message, DeliveryChannels, SuppressionWindow)`

- `EventTime` must be UTC-kind.
- `SourceEventKey` is the idempotency key; retried emissions of the same event
  reduce to one occurrence.
- `SuppressionWindow` may be `TimeSpan.Zero` for source-latched emitters
  (rule `IsActive` latches, offline/battery status flags) that dedup at the
  source; negative windows are rejected. Windowed suppression applies to
  `Alert`/`Caution` via `CautionAlertAcceptancePolicy`; the transition-driven
  types (`Offline`, `BatteryAlert`, `BatteryCaution`) are accepted outright.

## Emitters per monitor

| Monitor | Source(s) | Emission path |
| --- | --- | --- |
| Omnidots | `omnidots.webhook`, `omnidots.offline`, `omnidots.battery` | `OmnidotsAlarmTranslator` (configured suppression window); offline/battery handlers (zero window, `Email|Sms`) |
| AirQ | `airq.rules` | `NoiseRuleEvaluator` breaches (`Mqtt|Email|Sms`) and `SignalAlertAsync` for offline/site averages (`Email|Sms`) |
| Svantek | `svantek.rules` | Same evaluator/`SignalAlertAsync` split for rules, offline, battery, site averages |
| MyAtm | — | MyAtm predates this stack with its own durable outbox: `RuleAlertDeliveryPlanner` builds `MyAtmAlertCommit`s and `MonitorDeliveryDispatcher` delivers them |

Rule-driven signals share `RuleAlertSignals.Create`, which builds the
`{serial}:{field}:{averagingPeriod}:{alertType}:{eventTime:O}` idempotency key.

## Two dispatchers, one policy core

`DurableAlertDispatcher` (alerts outbox) and `MonitorDeliveryDispatcher`
(MyAtm's delivery outbox) deliberately remain separate — they encode different
product semantics (adapter registry + dead-letter aggregation vs. failure sink
+ configurable failure modes and fleet aggregate exceptions), each pinned by
its own tests. What is single-sourced:

- `DeliveryDispatchPolicy` — the terminal decision (non-transient
  `DeliveryException` or attempt exhaustion) and safe-error shaping, truncated
  at 1024 characters; only `DeliveryException` messages carry provider detail.
- `DeliveryRetrySchedule` — capped exponential backoff honoring bounded
  `Retry-After`.
- The claim/lease fencing pattern (fenced complete/retry by lease id;
  ownership loss is logged, never re-mutated).

## Composition

Each monitor host registers the stack against its own EF context:

```csharp
services.AddSingleton<IMonitorDbContextFactory<XMonitorContext>>(...);
services.AddSingleton<IMonitorEventPublisher>(...);       // Mqtt adapter dependency
services.AddDurableAlerts<XMonitorContext>();
services.PostConfigure<DurableAlertOptions>(...);         // PortalBaseUrl
```

`AddDurableAlerts` wires the ingress, acceptance policy, EF commit/outbox
stores, the three delivery adapters, the dispatcher, cleanup, and the hosted
background service.
