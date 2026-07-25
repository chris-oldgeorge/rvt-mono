# Task 7 boundary-guard corrective report

## Result

The stale Reporting Storage assertion now requires
`Rvt.Storage.Abstractions` and rejects `Rvt.Monitor.Common`.

## TDD evidence

The new temporary-project regression first failed against the old guard with
the single expected diagnostic requiring `Rvt.Monitor.Common` despite the
project's `Rvt.Storage.Abstractions` reference. After the minimal guard change,
the regression passed: it accepts the Abstractions-only graph and rejects the
mutated Common-reference graph.

## Verification

- `./tests/verify-rvt-common-source-boundary-regression.test.sh` — passed.
- `./tests/verify-rvt-common-source-boundary.test.sh` — passed.
- `git diff --check` — passed.

No .NET suite was required for this shell-guard correction. Package policy,
locks, consumer source, and the four pending Task 10 documentation edits remain
outside this commit.
