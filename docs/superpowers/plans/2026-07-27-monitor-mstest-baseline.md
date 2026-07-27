# Monitor MSTest Baseline Repair Plan

**Goal:** Remove the monitor test projects' seven MSTest analyzer suppressions, repair every resulting diagnostic without weakening test intent, and enforce the clean baseline as errors.

**Scope:** The four monitor test projects under `apps/monitors`, their shared monitor build policy, focused regression coverage, and the repository state checkpoint. Production behavior is unchanged.

**Toolchain:** .NET SDK 10.0.302, MSTest analyzers, PostgreSQL integration fixtures, shell verification guards.

## Task 1: Establish the diagnostic inventory

1. Rebuild `apps/monitors/rvt-monitors.sln` in Release with `NoWarn` cleared.
2. Record diagnostic counts, affected projects, files, and source locations.
3. Inspect the `MSTEST0030`, `MSTEST0032`, and `MSTEST0001` cases individually before editing.

## Task 2: Apply semantics-preserving analyzer repairs

1. Replace `DataTestMethod` with `TestMethod` for `MSTEST0044`.
2. Remove explicit `DynamicDataSourceType` arguments for `MSTEST0052`.
3. Replace generic assertions with purpose-built MSTest assertions for `MSTEST0037`.
4. Correct expected/actual ordering for `MSTEST0017`.
5. Rebuild unsuppressed after each rule and require its diagnostic count to reach zero.

## Task 3: Repair tests requiring judgment

1. Determine whether the unannotated test class is intentional current coverage. Restore discovery if valid; if it is obsolete imported coverage, remove it and verify the supported suite remains complete.
2. Rewrite each `MSTEST0032` tautological constant assertion to read the public constant through reflection, preserving the frozen-value contract.
3. Prove the constant contracts can fail by temporarily mutating each production constant, running the focused test, and reverting the mutation.
4. Audit the four monitor assemblies for shared mutable or external resources.
5. Declare the safest explicit assembly execution policy. Preserve sequential execution with `DoNotParallelize` where shared PostgreSQL state, process environment, or other external resources make method-level parallelism unsafe.

## Task 4: Enforce the baseline

1. Remove the monitor test `NoWarn` property.
2. Add all seven MSTest rule IDs to `WarningsAsErrors`.
3. Verify evaluated MSBuild properties for every monitor test project.
4. Run a reversible analyzer mutation to prove a selected MSTest warning now fails the ordinary build.

## Task 5: Verify and document

1. Run a fresh non-incremental Release build of the monitor solution with zero warnings and zero errors.
2. Run all four monitor test projects against an isolated PostgreSQL test database, repeating suites as appropriate for the chosen parallelization policy.
3. Run the root solution build and repository verification guards.
4. Run `git diff --check` and review the final diff for unrelated changes.
5. Update `project_state.md` with the branch, decisions, verification evidence, file structure, and command-scoped variable definitions while preserving the required next-session instruction as the final line.

## Execution outcome

The `MSTEST0030` class was proven to be obsolete imported Omnidots coverage: it retained the wrong `MyAtmMonitorTests` namespace and retired direct rule/MQTT expectations. Restoring discovery produced 12 failures while all 392 supported Omnidots tests passed. The stale file was therefore removed instead of ignored or suppressed.
