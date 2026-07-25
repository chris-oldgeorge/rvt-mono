# Task 7 boundary-guard corrective brief

## Root cause

Task 7 correctly migrated `Rvt.Reporting.Storage` from
`Rvt.Monitor.Common` to `Rvt.Storage.Abstractions`, but the shared
source-boundary guard retained its obsolete Common-reference requirement.

## Scope

Update the guard to require the Abstractions project and reject the former
Common project. Extend the behavioral shell regression to prove both outcomes
against an isolated, temporary project graph. Do not change consumer source,
package policy, locks, or the pending Task 10 documentation edits.

## Verification

Run the focused regression, the source-boundary test, and `git diff --check`.
