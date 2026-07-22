# MediatR Transaction Pipeline And Unit Of Work Plan

Date: 2026-06-25

## Objective

Address the review finding that write workflows can rely on independent `SaveChangesAsync()` calls without a consistent transaction coordination layer.

## Implementation Scope

- Add a Unit of Work abstraction over the RVT EF Core domain context.
- Add a MediatR `IPipelineBehavior` that wraps opted-in commands in one transaction and one final `SaveChangesAsync()`.
- Use an `ITransactionalRequest` marker so query/read handlers and non-transactional commands are not wrapped accidentally.
- Keep in-memory test providers transaction-safe by skipping explicit database transactions where EF cannot support them.
- Migrate the called-out monitor contract assignment workflows into command handlers:
  - `AssignMonitorToContractCommand`
  - `RemoveMonitorFromContractCommand`
- Migrate unattached monitor archive/delete into `RemoveUnattachedMonitorCommand`.
- Extract monitor removal impact counting into `IMonitorRemovalImpactReader` for read endpoints and command reuse.

## Notes

- Controllers now map HTTP-specific concerns only: route parameters, model-state errors, not-found responses, and API DTO rebuilding.
- Transactional handlers mutate tracked EF entities but do not call `SaveChangesAsync()` directly.
- The pipeline owns the save-and-commit boundary for `ITransactionalRequest` commands.
- Existing controller write paths not migrated in this first pass should be moved behind transactional command handlers incrementally.

## Verification

- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --filter "FullyQualifiedName~ContractAssignment_AddsAndRemovesCurrentDeployment|FullyQualifiedName~Login_DoesNotRedirectToHttps_InDevelopmentApiProxyPath" --logger "console;verbosity=minimal"` passed: `2/2`.
- `dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --no-restore --logger "console;verbosity=minimal"` passed: `212/212`.
