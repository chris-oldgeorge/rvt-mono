# SDD ledger — plan: docs/superpowers/plans/2026-07-23-rvt-storage-provider-split.md
Preflight: Task 6 Step 3 is already satisfied by communication Task 6.
`MonitorHost.RunAsync` and all five monitor composition callbacks already receive
`IConfiguration`; storage work must preserve the current two-argument callbacks
and must not revert AirQ/MyAtm/Omnidots/Reporting/Svantek registrations.
Preflight verification constraint: ReportingMonitor clean restore currently fails
on the known split-induced Logging.Abstractions 10.0.4/10.0.9 mismatch, and
retained locks contain the removed Infrastructure identity. Storage tasks may use
bounded temporary central/lock overrides for verification, but repository package
versions and locks remain owned by the later release/lock plan.
Carry-forward merge blocker: Graph large-attachment upload-chunk non-caller
timeouts still need safe transient translation. No storage task depends on it;
the final overall review must resolve it before merge.
Task 7 boundary-guard follow-up: complete (commits `d134bc0` and `92be3b1`,
review clean). The regression requires `Rvt.Reporting.Storage` to reference
`Rvt.Storage.Abstractions` and rejects `Rvt.Monitor.Common`,
`Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3`; both
temporary roots are cleanup-trapped.
Task 1: complete (commit `da0dfd2`, review clean).
Task 2: complete (commit `406f057`, review clean).
Task 3: complete (commit `e7e6e5b`, review clean).
Task 4: complete (commit `65608a4`, review clean).
Task 5: fix round 1/5 (2 addressed, 1 open; commits `1fde5e6`..`06a69dc`).
Task 5: fix round 2/5 (2 addressed, 1 new documentation issue open;
commits `06a69dc`..`d938c2b`).
Task 5: fix round 3/5 (1 addressed, 0 open; commit `56fbe64`).
Task 5: complete (commits `1fde5e6`..`56fbe64`, review clean).
Task 6: complete (commit `ab7e5e0`, review clean).
Task 7: complete (commit `6854a5c`, review clean; scoped verification uses an
untracked temporary ReportingMonitor central-version/lock override).
Task 8: complete (commit `6b678a5`, review clean).
Task 9: complete (commit `0dc3b51`, review clean).
Task 10: complete (base `92be3b1`; documentation/state/report verification is
recorded in this commit). The documentation-test RED was inapplicable because
no behaviorally useful semantic documentation-test home exists. Fresh guards,
storage 148/148, bounded Common 340/340, Svantek 93/93, ReportingMonitor
74/74, and the exact-path-excluded root build are green; ordinary
environment/lock/root-build blockers and all Future Pending Work remain
explicitly pending.
