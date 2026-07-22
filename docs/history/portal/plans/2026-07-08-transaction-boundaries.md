# Transaction Boundaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Coordinate RVT database writes through one EF Core Unit of Work while keeping file/blob/reporting side effects behind explicit non-database boundaries.

**Architecture:** Database-backed command handlers should use `EfCoreUnitOfWork` as the authoritative transaction boundary for `RVTDbContext`, `RVTSearchContext`, and `ApplicationDbContext`. External side effects cannot be part of the EF transaction, so storage adapters must use safe replacement/compensation behavior.

**Tech Stack:** .NET 10, EF Core 10, MediatR, ASP.NET Identity, SQL Server/PostgreSQL providers, SQLite relational tests.

## Global Constraints

- Preserve existing API endpoints and request/response contracts.
- Keep ASP.NET Identity physical database names framework-managed.
- Do not add manual transaction handling to controllers.
- Add tests before implementation and verify the red/green cycle.
- Update source comments and project state for architectural changes.

---

### Task 1: Coordinated EF Unit of Work

**Files:**
- Modify: `RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj`
- Create: `RvtPortal.Spa.Tests/EfCoreUnitOfWorkTests.cs`
- Modify: `RVT.DataAccess/Configuration/RvtDatabaseServiceCollectionExtensions.cs`
- Modify: `RvtPortal.Spa/Program.cs`
- Modify: `RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs`

**Interfaces:**
- Consumes: `IUnitOfWork.ExecuteInTransactionAsync`, `IUnitOfWork.SaveChangesAsync`
- Produces: `EfCoreUnitOfWork(RVTDbContext, RVTSearchContext, ApplicationDbContext)`

- [x] **Step 1: Write failing relational tests**

Create tests that:
- persist domain, search, and Identity changes through one `SaveChangesAsync`;
- roll back an Identity write saved inside the handler when a later domain save fails;
- roll back a search/report write saved inside the handler when a later domain save fails.

- [x] **Step 2: Verify red**

Run:

```powershell
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter "FullyQualifiedName~EfCoreUnitOfWorkTests"
```

Expected: fail before implementation because the unit of work does not expose or coordinate `ApplicationDbContext`.

- [x] **Step 3: Implement shared relational context registration**

Add a `DbConnection` overload for `UseRvtDatabaseProvider`, register one scoped provider connection in `Program.ConfigureDatabases`, and configure all three EF contexts from that connection.

- [x] **Step 4: Implement multi-context transaction enlistment**

Update `EfCoreUnitOfWork` to enlist domain, search, and Identity contexts before running transactional operations, then save all three contexts through the shared boundary.

- [x] **Step 5: Verify green**

Run the focused tests and then the broader backend test set.

### Task 2: Safe Storage Boundary

**Files:**
- Create: `RvtPortal.Spa.Tests/StorageAdapterTests.cs`
- Modify: `RVT.BusinessLogic/Ports/Storage/StoragePorts.cs`
- Modify: `RvtPortal.Spa/Adapters/Storage/CustomerLogoStorage.cs`
- Modify: `RvtPortal.Spa/Adapters/Storage/MonitorPictureStorage.cs`

**Interfaces:**
- Consumes: `ICustomerLogoStorage.SaveAsync`
- Produces: atomic local customer-logo replacement and explicit monitor-picture cleanup capability.

- [x] **Step 1: Write failing storage test**

Create a disk-backed test proving failed customer-logo replacement preserves the old logo bytes.

- [x] **Step 2: Verify red**

Run:

```powershell
dotnet test .\RvtPortal.Spa.Tests\RvtPortal.Spa.Tests.csproj --filter "FullyQualifiedName~StorageAdapterTests"
```

Expected: fail before implementation because the current adapter deletes the old logo before copying the replacement.

- [x] **Step 3: Implement safe replacement**

Write uploads to a temporary file in the target directory, move the completed file into place, and delete old extensions only after the replacement succeeds.

- [x] **Step 4: Add monitor-picture cleanup port**

Extend `IMonitorPictureStorage` with `DeleteAsync(string storedLink, CancellationToken)`, implement local/blob cleanup, and document that monitor-picture handlers must compensate saved files if a database save fails.

- [x] **Step 5: Verify green**

Run focused storage tests, architecture tests, full build, and the SPA test suite.
