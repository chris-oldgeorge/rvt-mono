# MSTest Contracts and Parallelization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the three public-constant contract assertions effective at runtime and explicitly adopt a verified MSTest parallelization policy for all seven assemblies currently reporting `MSTEST0001`.

**Architecture:** Constant tests will resolve the declared public static fields with reflection and compare `FieldInfo.GetRawConstantValue()` with the frozen contract values, preventing compile-time folding from eliminating the runtime check. Each affected assembly will declare method-level parallelization in a dedicated `AssemblyInfo.cs`; repeated whole-assembly runs, including a real PostgreSQL-backed integration suite, decide whether that declaration is safe or must be replaced with assembly-level `DoNotParallelize`.

**Tech Stack:** .NET SDK 10.0.302, .NET runtime 10.0.10, MSTest 4 analyzers and runner, reflection, Npgsql, PostgreSQL.

## Global Constraints

- Do not delete or suppress `MSTEST0032` or `MSTEST0001`.
- Preserve the exact public contracts `Alerts:DurableDelivery`, `MyAtm`, and `Svantek`.
- Use `FieldInfo.GetRawConstantValue()` so constant values are read at runtime.
- Prefer `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
- Run every affected assembly at least five consecutive times after enabling parallelization.
- If repeat runs expose genuine shared external state, use `[assembly: DoNotParallelize]` for that assembly and document the evidence.
- Do not change production behavior, dependencies, package locks, or analyzer severity.

---

### Task 1: Repair the public-constant contract tests

**Files:**
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Alerts/DurableAlertOptionsTests.cs`
- Modify: `libs/rvt-monitor-common/tests/Rvt.Monitor.CommonTests/Delivery/MonitorDeliveryOptionsTests.cs`

**Interfaces:**
- Consumes: `System.Reflection.BindingFlags`, `Type.GetField(string, BindingFlags)`, and `FieldInfo.GetRawConstantValue()`.
- Produces: runtime assertions for `DurableAlertOptions.SectionName`, `MonitorDeliveryProducers.MyAtm`, and `MonitorDeliveryProducers.Svantek`.

- [x] **Step 1: Confirm the analyzer RED baseline**

Run:

```bash
/private/tmp/rvt-dotnet-10.0.302/dotnet build Rvt.Mono.slnx \
  --configuration Release --no-restore --no-incremental --nologo -m:1
```

Expected: build succeeds and reports three `MSTEST0032` warnings at the existing literal-versus-constant assertions.

- [x] **Step 2: Prove each repaired assertion observes runtime metadata**

For each production constant, temporarily substitute a distinct mutation value, build its production project, and run the focused test after the reflection edit. Expected: the focused test fails with the approved literal as expected and the mutation value as actual. Restore each production constant with an explicit reverse patch immediately after its mutation run.

- [x] **Step 3: Replace the folded SectionName assertion**

Use:

```csharp
var sectionNameField = typeof(DurableAlertOptions).GetField(
    nameof(DurableAlertOptions.SectionName),
    BindingFlags.Public | BindingFlags.Static);

Assert.IsNotNull(sectionNameField);
Assert.AreEqual("Alerts:DurableDelivery", (string?)sectionNameField.GetRawConstantValue());
```

- [x] **Step 4: Replace the two folded producer assertions**

Resolve both fields independently with `BindingFlags.Public | BindingFlags.Static`, assert that each field exists, then compare each raw constant value with `MyAtm` and `Svantek`.

- [x] **Step 5: Verify the constant-contract slice**

Run the two focused test methods and rebuild the solution. Expected: both methods pass, all three `MSTEST0032` warnings are absent, and only the seven `MSTEST0001` warnings remain.

### Task 2: Declare method-level parallelization in seven assemblies

**Files:**
- Create: `libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting.Tests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.AbstractionsTests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.MicrosoftGraphMailTests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.SendGridMailTests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Communication.TransmitSmsTests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.CommunicationTests/AssemblyInfo.cs`
- Create: `libs/rvt-monitor-common/tests/Rvt.Storage.Tests/AssemblyInfo.cs`

**Interfaces:**
- Consumes: `Microsoft.VisualStudio.TestTools.UnitTesting.ParallelizeAttribute` and `ExecutionScope.MethodLevel`.
- Produces: one explicit assembly-level concurrency policy per warned test project.

- [x] **Step 1: Confirm the analyzer RED baseline**

Build the solution with the fixed SDK. Expected: exactly seven `MSTEST0001` diagnostics naming the seven projects above.

- [x] **Step 2: Add one dedicated assembly declaration per project**

Each new file contains:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
```

- [x] **Step 3: Build and verify the analyzer baseline**

Run a non-incremental Release build. Expected: zero `MSTEST0001`, zero `MSTEST0032`, zero build warnings, and zero build errors.

- [x] **Step 4: Prepare the PostgreSQL repeat-run fixture**

Use a disposable PostgreSQL database and command-scoped `RVT__POSTGRES_INTEGRATION_CONNECTION`. Confirm the integration helper creates `rvt_integration_<guid>` schemas and drops each schema during disposal.

- [x] **Step 5: Stress every assembly five times**

Run each of the seven project files five consecutive times with:

```bash
/private/tmp/rvt-dotnet-10.0.302/dotnet test <project.csproj> \
  --configuration Release --no-build --no-restore --nologo
```

Expected per iteration: Integration Testing 6/6, Communication Abstractions 20/20, Microsoft Graph 37/37, SendGrid 20/20, Transmit SMS 25/25, Communication 31/31, and Storage 154/154.

- [x] **Step 6: Classify any concurrency failure**

For a repeatable collision involving shared external state, replace only that assembly's declaration with:

```csharp
[assembly: DoNotParallelize]
```

Rerun its five-iteration gate. Do not use retry attributes, sleeps, or analyzer suppression.

### Task 3: Final verification and state checkpoint

**Files:**
- Modify: `project_state.md`

**Interfaces:**
- Consumes: final build warning log and repeated test results.
- Produces: a resumable record of the selected assembly policies, test counts, environment variables, and verification commands.

- [x] **Step 1: Run final static verification**

Run `git diff --check`, verify the three reflection calls and seven assembly declarations, and confirm no `MSTEST0001` or `MSTEST0032` remains in a fresh warnings-only build log.

- [x] **Step 2: Run repository guards**

Prepend `/private/tmp/rvt-dotnet-10.0.302` to the normal executable search path and run all nine `tests/verify-*.test.sh` scripts. Expected: 9/9 pass.

- [x] **Step 3: Record the final state**

Append the selected policy for each assembly, five-run totals, fixed SDK path, command-scoped PostgreSQL variable name, and analyzer-clean build result to `project_state.md`. Keep its final line exactly:

```text
Next-session instruction: Read project_state.md to get up to speed
```

- [x] **Step 4: Review the final diff**

Expected: only the two constant-test files, seven `AssemblyInfo.cs` files, this plan, and `project_state.md` are changed. No production file remains modified after mutation verification.
