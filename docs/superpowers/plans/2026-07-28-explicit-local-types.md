# Explicit Local Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace every eligible `var` local declaration in maintained C# and JavaScript/TypeScript source with its exact explicit type and ratchet the repository against regressions.

**Architecture:** Use Roslyn's IDE0008 code fix per solution so replacements are based on compiler semantic types rather than text matching. Keep generated, vendored, migration, build-output, and anonymous-type declarations out of the mechanical rewrite; scan maintained JavaScript/TypeScript separately because Roslyn does not process it.

**Tech Stack:** .NET 10, Roslyn `dotnet format`, C#, JavaScript/TypeScript, EditorConfig, repository shell verification.

## Global Constraints

- Preserve all pre-existing modified, deleted, and untracked files.
- Do not edit `bin`, `obj`, `node_modules`, `dist`, `build`, `artifacts`, `coverage`, generated code, EF migrations, designer files, or minified assets.
- Do not replace `var` when the initializer is an anonymous type that has no nameable explicit C# type.
- Do not alter runtime behavior, public APIs, dependencies, or project boundaries.
- Use `apply_patch` for hand edits; Roslyn formatting is allowed for bulk semantic rewrites.
- Verify the repository scan, build, and relevant tests before claiming completion.

---

### Task 1: Ratchet explicit-type policy

**Files:**
- Modify: `.editorconfig`
- Modify: `apps/portal/.editorconfig`

**Interfaces:**
- Consumes: Existing repository EditorConfig hierarchy.
- Produces: IDE0008 enforcement and `false:error` values for all three C# `var` preferences.

- [ ] **Step 1: Set the root C# preferences**

Set:

```ini
csharp_style_var_for_built_in_types = false:error
csharp_style_var_when_type_is_apparent = false:error
csharp_style_var_elsewhere = false:error
dotnet_diagnostic.IDE0008.severity = error
```

- [ ] **Step 2: Remove the Portal override**

Ensure the final Portal `[*.cs]` section uses the same three preferences and changes `dotnet_diagnostic.IDE0008.severity` from `none` to `error`.

- [ ] **Step 3: Verify effective configuration**

Run:

```bash
rg -n 'csharp_style_var|IDE0008' .editorconfig apps/portal/.editorconfig apps/monitors/.editorconfig services/reporting/.editorconfig
```

Expected: every effective repository rule prefers explicit local types and IDE0008 is not disabled.

### Task 2: Convert Portal declarations

**Files:**
- Modify: maintained `*.cs` files included by `apps/portal/RvtPortal.Spa.sln`

**Interfaces:**
- Consumes: Task 1 EditorConfig policy.
- Produces: Portal source with every IDE0008-fixable declaration explicitly typed.

- [ ] **Step 1: Apply the semantic code fix**

Run each `*.csproj` beneath `apps/portal` separately because
`dotnet-format 10.0.302` incorrectly selects zero documents when `--include`
is used with this solution:

```bash
while IFS= read -r project; do
  RvtEngineeringStandardsMode=Strict dotnet format "$project" style \
    --diagnostics IDE0008 --severity info --no-restore
done < <(find apps/portal -name '*.csproj' -not -path '*/obj/*' -print | sort)
```

Repeat the formatter for a project while its verification still reports
IDE0008 diagnostics; Roslyn can require multiple convergence passes.

- [ ] **Step 2: Verify Portal diagnostics**

Run:

```bash
while IFS= read -r project; do
  RvtEngineeringStandardsMode=Strict dotnet format "$project" style \
    --diagnostics IDE0008 --severity info --no-restore --verify-no-changes
done < <(find apps/portal -name '*.csproj' -not -path '*/obj/*' -print | sort)
```

Expected: exit 0 for every Portal project.

- [ ] **Step 3: Build and test Portal**

Run:

```bash
dotnet build apps/portal/RvtPortal.Spa.sln --no-restore
dotnet test apps/portal/RvtPortal.Spa.sln --no-build --no-restore
```

Expected: both commands exit 0.

### Task 3: Convert monitor declarations

**Files:**
- Modify: maintained `*.cs` files included by `apps/monitors/rvt-monitors.sln`

**Interfaces:**
- Consumes: Task 1 EditorConfig policy.
- Produces: Monitor source with every IDE0008-fixable declaration explicitly typed.

- [ ] **Step 1: Apply the semantic code fix**

Run each `*.csproj` beneath `apps/monitors` separately:

```bash
while IFS= read -r project; do
  RvtEngineeringStandardsMode=Strict dotnet format "$project" style \
    --diagnostics IDE0008 --severity info --no-restore
done < <(find apps/monitors -name '*.csproj' -not -path '*/obj/*' -print | sort)
```

- [ ] **Step 2: Verify and build**

Run:

```bash
while IFS= read -r project; do
  RvtEngineeringStandardsMode=Strict dotnet format "$project" style \
    --diagnostics IDE0008 --severity info --no-restore --verify-no-changes
done < <(find apps/monitors -name '*.csproj' -not -path '*/obj/*' -print | sort)
dotnet build apps/monitors/rvt-monitors.sln --no-restore
dotnet test apps/monitors/rvt-monitors.sln --no-build --no-restore
```

Expected: every project verifier and the solution commands exit 0.

### Task 4: Convert shared-library declarations

**Files:**
- Modify: maintained `*.cs` files included by `libs/rvt-monitor-common/rvt-common.sln`

**Interfaces:**
- Consumes: Task 1 EditorConfig policy.
- Produces: Shared-library source with every IDE0008-fixable declaration explicitly typed.

- [ ] **Step 1: Apply the semantic code fixes**

Run each `*.csproj` beneath the shared-library domain separately:

```bash
while IFS= read -r project; do
  RvtEngineeringStandardsMode=Strict dotnet format "$project" style \
    --diagnostics IDE0008 --severity info --no-restore
done < <(
  find libs/rvt-monitor-common \
    -name '*.csproj' -not -path '*/obj/*' -print | sort
)
```

- [ ] **Step 2: Verify, build, and test the shared-library domain**

Run IDE0008 `--verify-no-changes`, `dotnet build --no-restore`, and
`dotnet test --no-build --no-restore` for `libs/rvt-monitor-common/rvt-common.sln`.

Expected: every command exits 0.

### Task 5: Repository-wide residual scan and state checkpoint

**Files:**
- Modify: maintained JavaScript/TypeScript files only when a real `var` declaration exists.
- Modify: `project_state.md`

**Interfaces:**
- Consumes: Tasks 2–4 converted source.
- Produces: A classified residual list containing only anonymous/generated/excluded cases and a saved continuation checkpoint.

- [ ] **Step 1: Scan maintained source**

Run:

```bash
rg -n --glob '*.cs' --glob '*.js' --glob '*.jsx' --glob '*.ts' --glob '*.tsx' \
  --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/node_modules/**' \
  --glob '!**/dist/**' --glob '!**/build/**' --glob '!**/artifacts/**' \
  --glob '!**/coverage/**' --glob '!**/Migrations/**' \
  --glob '!**/*.Designer.cs' --glob '!**/*.g.cs' \
  --glob '!**/*.generated.cs' --glob '!**/*.min.js' '\bvar\b' .
```

Classify every residual as an anonymous-type requirement, comment/string occurrence, or missed declaration.

- [ ] **Step 2: Verify the whole repository**

Run:

```bash
dotnet build Rvt.Mono.slnx --no-restore
dotnet test Rvt.Mono.slnx --no-build --no-restore
git diff --check
```

Expected: all commands exit 0.

- [ ] **Step 3: Save state**

Record changed domains, commands, results, unavoidable residuals, and the next action in `project_state.md` without storing credentials or generated output.
