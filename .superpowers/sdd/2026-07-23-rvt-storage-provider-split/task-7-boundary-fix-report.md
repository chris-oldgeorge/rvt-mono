# Task 7 boundary-guard corrective report

## Result

The stale Reporting Storage assertion now requires
`Rvt.Storage.Abstractions` and rejects `Rvt.Monitor.Common`,
`Rvt.Storage.Local`, `Rvt.Storage.AzureBlob`, and `Rvt.Storage.S3`.

## TDD evidence

The new temporary-project regression first failed against the old guard with
the single expected diagnostic requiring `Rvt.Monitor.Common` despite the
project's `Rvt.Storage.Abstractions` reference. After the minimal guard change,
the regression passed: it accepts the Abstractions-only graph and rejects the
mutated Common-reference graph.

## Review fix round 1/5

Review found that the first correction did not enforce the global rule that
`Rvt.Reporting.Storage` uses only the provider-neutral Abstractions project for
storage. The new provider-reference mutations first failed because the guard
accepted an added `Rvt.Storage.Local` reference. The guard now explicitly
rejects Local, Azure Blob, and S3 references; the same isolated regression
proves each rejection while preserving the Abstractions reference. The cleanup
handler is now installed before either temporary graph can be created, so both
temporary roots are removed on every shell exit.

## Verification

- `./tests/verify-rvt-common-source-boundary-regression.test.sh` — passed.
- `./tests/verify-rvt-common-source-boundary.test.sh` — passed.
- `bash -n tests/verify-rvt-common-source-boundary-regression.test.sh scripts/verify-rvt-common-source-boundary.sh` — passed.
- `git diff --check` — passed.

No .NET suite was required for this shell-guard correction. Package policy,
locks, consumer source, and the four pending Task 10 documentation edits remain
outside this commit.
