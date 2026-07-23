# RVT Portal Sites Application Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a BCL-only `RvtPortal.Application` project and move the complete Sites vertical slice behind application-owned use cases and ports without changing HTTP contracts or persistence behavior.

**Architecture:** The application project owns Sites contracts, policies, orchestration, and inward-facing ports. `RvtPortal.Spa` remains the ASP.NET Core composition root and implements those ports with EF Core, Identity, archive, and logo-storage adapters; controllers retain only HTTP/auth/DTO mapping. The existing shared-connection `EfCoreUnitOfWork` remains the transaction adapter.

**Tech Stack:** .NET 10, ASP.NET Core controller APIs, EF Core 10, xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing`, PostgreSQL/TimescaleDB provider-gated tests, React/Vite/Vitest.

**Design:** `docs/superpowers/specs/2026-07-23-rvtportal-sites-application-boundary-design.md`

## Global Constraints

- `RvtPortal.Application` targets `net10.0`, enables nullable reference types and implicit usings, and has no `PackageReference` or `ProjectReference`.
- `RvtPortal.Application` must not reference ASP.NET Core, EF Core, `RVT.DataAccess`, `RvtPortal.Spa`, Azure, SendGrid, `IConfiguration`, `IHttpClientFactory`, MediatR, or vendor SDKs.
- Application ports use materialized application contracts only; they must not expose `IQueryable`, `DbSet`, `DbContext`, EF entities, `IFormFile`, `ClaimsPrincipal`, or HTTP result types.
- Keep all existing `/api/sites` routes, role attributes, request/response DTOs, status codes, paging defaults, sort rules, and not-found authorization masking.
- Keep EF filtering, counting, sorting, paging, and projection inside host adapters so no full-table regression is introduced.
- Keep the existing scoped shared `DbConnection` and three-context `EfCoreUnitOfWork`; do not introduce a generic repository.
- Use injected `TimeProvider` and explicit UTC values for assignment windows and mutation timestamps.
- Archive export stays outside the database transaction; a failed export must leave the site active and return the existing service-unavailable response.
- Implement with TDD, run the named RED and GREEN commands, and commit after every task.

---

### Task 1: Scaffold the Compile-time Boundary and Make It Executable

**Files:**
- Create: `apps/portal/RvtPortal.Application/RvtPortal.Application.csproj`
- Create: `apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj`
- Create: `apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs`
- Modify: `apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj`
- Modify: `Rvt.Mono.slnx`
- Modify: `apps/portal/RvtPortal.Spa.sln`

**Interfaces:**
- Consumes: repository-root discovery from `RvtPortal.Spa.Tests.Support.RepositoryLayout`.
- Produces: a BCL-only `RvtPortal.Application` assembly referenced by the host, plus a dedicated unit-test project and executable project-graph guards.

- [ ] **Step 1: Write the failing filesystem architecture test**

Create `ApplicationBoundaryArchitectureTests.cs` without referencing the not-yet-created assembly:

```csharp
// File summary: Guards the standalone portal application project and its forbidden dependency set.

using System.Xml.Linq;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

public sealed class ApplicationBoundaryArchitectureTests
{
    private static string ApplicationRoot =>
        Path.Combine(RepositoryLayout.Root, "RvtPortal.Application");

    private static string ApplicationProject =>
        Path.Combine(ApplicationRoot, "RvtPortal.Application.csproj");

    [Fact]
    public void ApplicationProject_IsBclOnly()
    {
        Assert.True(File.Exists(ApplicationProject), $"{ApplicationProject} must exist.");

        var project = XDocument.Load(ApplicationProject);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void ApplicationSources_DoNotImportForbiddenFrameworksOrAdapters()
    {
        Assert.True(Directory.Exists(ApplicationRoot), $"{ApplicationRoot} must exist.");

        var forbidden = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "RVT.DataAccess",
            "RvtPortal.Spa",
            "Azure.",
            "SendGrid",
            "IConfiguration",
            "IHttpClientFactory",
            "MediatR",
            "IQueryable",
            "DbContext",
            "DbSet",
            "IFormFile",
            "ClaimsPrincipal"
        };
        var violations = Directory
            .EnumerateFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(row => forbidden.Any(value =>
                row.line.Contains(value, StringComparison.Ordinal)))
            .Select(row => $"{Path.GetRelativePath(ApplicationRoot, row.path)}:{row.number}: {row.line.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }
}
```

- [ ] **Step 2: Run the architecture test and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~ApplicationBoundaryArchitectureTests
```

Expected: FAIL because `RvtPortal.Application/RvtPortal.Application.csproj` does not exist.

- [ ] **Step 3: Create the two projects**

Create `RvtPortal.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

Create `RvtPortal.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\RvtPortal.Application\RvtPortal.Application.csproj" />
  </ItemGroup>
</Project>
```

Add this host reference to `RvtPortal.Spa.csproj`:

```xml
<ProjectReference Include="..\RvtPortal.Application\RvtPortal.Application.csproj" />
```

- [ ] **Step 4: Add both projects to both solutions**

Run:

```bash
dotnet sln Rvt.Mono.slnx add \
  apps/portal/RvtPortal.Application/RvtPortal.Application.csproj \
  --solution-folder "Apps/Portal"
dotnet sln Rvt.Mono.slnx add \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --solution-folder "Apps/Portal/Tests"
dotnet sln apps/portal/RvtPortal.Spa.sln add \
  apps/portal/RvtPortal.Application/RvtPortal.Application.csproj
dotnet sln apps/portal/RvtPortal.Spa.sln add \
  apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj
```

- [ ] **Step 5: Verify GREEN**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~ApplicationBoundaryArchitectureTests
tests/verify-mono-solution.test.sh
dotnet build apps/portal/RvtPortal.Application/RvtPortal.Application.csproj --nologo
```

Expected: architecture tests pass, the monorepo solution guard reports no project/folder mismatch, and the application project builds with zero errors.

- [ ] **Step 6: Commit**

```bash
git add \
  Rvt.Mono.slnx \
  apps/portal/RvtPortal.Spa.sln \
  apps/portal/RvtPortal.Application \
  apps/portal/RvtPortal.Application.Tests \
  apps/portal/RvtPortal.Spa/RvtPortal.Spa.csproj \
  apps/portal/RvtPortal.Spa.Tests/ApplicationBoundaryArchitectureTests.cs
git commit -m "refactor: scaffold portal application boundary"
```

---

### Task 2: Move the Shared User Facts and Define UTC Authorization Policies

**Files:**
- Create: `apps/portal/RvtPortal.Application/Identity/PortalUserContext.cs`
- Create: `apps/portal/RvtPortal.Application/Identity/IPortalUserDirectory.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/SiteAccessScope.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/ActiveSiteAssignment.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/SiteAuthorizationPolicy.cs`
- Create: `apps/portal/RvtPortal.Application.Tests/Sites/SiteAuthorizationPolicyTests.cs`
- Delete: `apps/portal/RVT.BusinessLogic/Application/PortalUserContext.cs`
- Delete: `apps/portal/RVT.BusinessLogic/Application/Users/IPortalUserDirectory.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/CurrentUserContextFactory.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/PortalUserDirectory.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/AlertLevels/AlertLevelApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Dashboard/DashboardApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Dashboard/DashboardBreachApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Installers/InstallerApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Monitors/MonitorAdministrationReadService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Notifications/NotificationApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/ReportRules/ReportRuleApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/AlertLevelsController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/InstallerApiController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/MonitorsController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/NotificationsController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/SitesController.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: BCL `TimeProvider`, `Guid`, `DateTime`, and cancellation primitives.
- Produces:
  - `PortalUserContext(Guid? UserId, string? UserName, Guid? CompanyId, bool IsAdmin, bool IsInstaller, bool IsCompanyUser)`
  - `IPortalUserDirectory.ListUsersAsync(...)` and `FindByIdAsync(...)`
  - `SiteAccessScope` with `All`, `Assigned`, and `None` modes
  - inclusive UTC assignment-window policy.

- [ ] **Step 1: Write policy tests**

Create `SiteAuthorizationPolicyTests.cs`:

```csharp
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteAuthorizationPolicyTests
{
    private static readonly DateTime NowUtc =
        new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReadScope_AdminCanReadAllSites()
    {
        var user = new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false);

        var scope = SiteAuthorizationPolicy.ReadScope(user, NowUtc);

        Assert.Equal(SiteAccessScopeKind.All, scope.Kind);
    }

    [Fact]
    public void ReadScope_CompanyUserCarriesUserAndUtcInstant()
    {
        var userId = Guid.NewGuid();
        var user = new PortalUserContext(userId, "user", Guid.NewGuid(), false, false, true);

        var scope = SiteAuthorizationPolicy.ReadScope(user, NowUtc);

        Assert.Equal(SiteAccessScopeKind.Assigned, scope.Kind);
        Assert.Equal(userId, scope.UserId);
        Assert.Equal(NowUtc, scope.NowUtc);
    }

    [Fact]
    public void AssignmentWindow_IsInclusiveAtBothBounds()
    {
        var userId = Guid.NewGuid();
        var assignment = new SiteAssignmentWindow(userId, NowUtc, NowUtc);

        Assert.True(ActiveSiteAssignment.IsActive(assignment, userId, NowUtc));
        Assert.False(ActiveSiteAssignment.IsActive(
            assignment,
            userId,
            NowUtc.AddTicks(1)));
    }

    [Fact]
    public void ReadScope_RejectsNonUtcClockValues()
    {
        var user = new PortalUserContext(Guid.NewGuid(), "user", null, false, false, true);

        Assert.Throws<ArgumentException>(() =>
            SiteAuthorizationPolicy.ReadScope(
                user,
                DateTime.SpecifyKind(NowUtc, DateTimeKind.Unspecified)));
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteAuthorizationPolicyTests
```

Expected: build fails because the identity and authorization types do not exist.

- [ ] **Step 3: Add the shared identity contracts**

Create `PortalUserContext.cs`:

```csharp
namespace RvtPortal.Application.Identity;

public sealed record PortalUserContext(
    Guid? UserId,
    string? UserName,
    Guid? CompanyId,
    bool IsAdmin,
    bool IsInstaller,
    bool IsCompanyUser);
```

Create `IPortalUserDirectory.cs` with the existing role names and profile behavior:

```csharp
namespace RvtPortal.Application.Identity;

public static class PortalRoleNames
{
    public const string RVTMasterAdmin = "RVTMasterAdmin";
    public const string RVTAdmin = "RVTAdmin";
    public const string RVTInstaller = "RVTInstaller";
    public const string CompanyUser = "CompanyUser";
}

public sealed record PortalUserProfile(
    Guid UserId,
    string UserIdText,
    Guid? CompanyId,
    bool IsDisabled,
    string? Name,
    string Email,
    string? PhoneNumber,
    string? CompanyRole,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles)
{
    public string PrimaryRole => Roles.Count > 0 ? Roles[0] : "";

    public bool IsInRole(string role) =>
        Roles.Contains(role, StringComparer.Ordinal);
}

public interface IPortalUserDirectory
{
    Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(
        CancellationToken cancellationToken);

    Task<PortalUserProfile?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Add the pure authorization policy**

Create the following types:

```csharp
namespace RvtPortal.Application.Sites;

public enum SiteAccessScopeKind
{
    None,
    All,
    Assigned
}

public sealed record SiteAccessScope(
    SiteAccessScopeKind Kind,
    Guid? UserId,
    DateTime NowUtc)
{
    public static SiteAccessScope All(DateTime nowUtc) =>
        new(SiteAccessScopeKind.All, null, RequireUtc(nowUtc));

    public static SiteAccessScope Assigned(Guid userId, DateTime nowUtc) =>
        new(SiteAccessScopeKind.Assigned, userId, RequireUtc(nowUtc));

    public static SiteAccessScope None(DateTime nowUtc) =>
        new(SiteAccessScopeKind.None, null, RequireUtc(nowUtc));

    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Site access time must be UTC.", nameof(value));
}
```

```csharp
namespace RvtPortal.Application.Sites;

public sealed record SiteAssignmentWindow(
    Guid UserId,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);

public static class ActiveSiteAssignment
{
    public static bool IsActive(
        SiteAssignmentWindow assignment,
        Guid userId,
        DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Assignment comparison time must be UTC.", nameof(nowUtc));
        }

        return assignment.UserId == userId
            && assignment.StartDateUtc <= nowUtc
            && (!assignment.EndDateUtc.HasValue || assignment.EndDateUtc.Value >= nowUtc);
    }
}
```

```csharp
using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Sites;

public static class SiteAuthorizationPolicy
{
    public static SiteAccessScope ReadScope(PortalUserContext user, DateTime nowUtc)
    {
        if (user.IsAdmin)
        {
            return SiteAccessScope.All(nowUtc);
        }

        return user.IsCompanyUser && user.UserId.HasValue
            ? SiteAccessScope.Assigned(user.UserId.Value, nowUtc)
            : SiteAccessScope.None(nowUtc);
    }

    public static bool CanManage(PortalUserContext user) => user.IsAdmin;

    public static bool CanUpdateNotificationSetting(
        PortalUserContext user,
        Guid targetUserId) =>
        user.IsAdmin || (user.IsCompanyUser && user.UserId == targetUserId);
}
```

- [ ] **Step 5: Repoint the host to the application-owned identity contracts**

Delete the two superseded `RVT.BusinessLogic/Application` files. Add:

```csharp
using RvtPortal.Application.Identity;
```

to every exact host file listed in this task that uses `PortalUserContext`,
`PortalUserProfile`, `PortalRoleNames`, or `IPortalUserDirectory`. Retain
`using RVT.BusinessLogic.Application;` only in files that still use the legacy
`ApplicationResult` types.

`CurrentUserContextFactory.CreateAsync` must continue constructing the same six
facts, now using `RvtPortal.Application.Identity.PortalUserContext`.
`PortalUserDirectory` must continue mapping ASP.NET Identity users to the same
profile fields, now implementing the application-owned port.

- [ ] **Step 6: Verify GREEN and no unrelated behavior drift**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteAuthorizationPolicyTests
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~ContractSiteOperationsTests.CompanyUserSiteAccess"
```

Expected: application policy tests pass; existing current-user and active-assignment HTTP tests pass.

- [ ] **Step 7: Commit**

```bash
git add \
  apps/portal/RvtPortal.Application \
  apps/portal/RvtPortal.Application.Tests \
  apps/portal/RVT.BusinessLogic/Application \
  apps/portal/RvtPortal.Spa
git commit -m "refactor: move portal user and site access policies"
```

---

### Task 3: Extract Sites Read Contracts and EF Read Adapter

**Files:**
- Create: `apps/portal/RvtPortal.Application/Common/UseCaseResult.cs`
- Create: `apps/portal/RvtPortal.Application/Common/PageRequest.cs`
- Create: `apps/portal/RvtPortal.Application/Common/PagedResult.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/SiteContracts.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/ISiteApplicationService.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteReadPort.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/SiteApplicationService.cs`
- Create: `apps/portal/RvtPortal.Application.Tests/Sites/SiteReadUseCaseTests.cs`
- Create: `apps/portal/RvtPortal.Application.Tests/Sites/SiteTestDoubles.cs`
- Create: `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteReadAdapter.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/SiteReadAdapterTests.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `PortalUserContext`, `SiteAuthorizationPolicy`, `IPortalUserDirectory`, `TimeProvider`, and `RVTDbContext` only inside the host adapter.
- Produces:
  - application-owned `UseCaseResult<T>`, `PageRequest`, and `PagedResult<T>`;
  - all existing Sites read models under `RvtPortal.Application.Sites`;
  - `ISiteReadPort` with materialized query methods;
  - read methods on `ISiteApplicationService`;
  - `EfSiteReadAdapter`.

- [ ] **Step 1: Write failing application read-use-case tests**

Create tests with a recording fake port:

```csharp
using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;
using RvtPortal.Application.Sites;
using RvtPortal.Application.Sites.Ports;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteReadUseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_UsesAssignedScopeAndMasksInvisibleSite()
    {
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var reads = new FakeSiteReadPort { Exists = false };
        var service = CreateService(reads);
        var user = new PortalUserContext(userId, "user", Guid.NewGuid(), false, false, true);

        var result = await service.GetAsync(user, siteId, CancellationToken.None);

        Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
        Assert.Equal(SiteAccessScopeKind.Assigned, reads.LastScope?.Kind);
        Assert.Equal(userId, reads.LastScope?.UserId);
        Assert.Equal(Now.UtcDateTime, reads.LastScope?.NowUtc);
    }

    [Fact]
    public async Task QueryAsync_ForwardsMaterializedPagingRequest()
    {
        var reads = new FakeSiteReadPort
        {
            QueryResult = new PagedResult<SiteListModel>
            {
                Results = [new SiteListModel { Id = Guid.NewGuid(), SiteName = "A" }],
                Total = 1,
                Page = 2,
                PageSize = 10,
                Sort = "siteName",
                SortDir = PageSortDirections.Ascending
            }
        };
        var service = CreateService(reads);
        var request = new SiteQuery(
            null,
            false,
            new PageRequest(
                null,
                2,
                10,
                "siteName",
                PageSortDirections.Ascending));

        var result = await service.QueryAsync(
            new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false),
            request,
            CancellationToken.None);

        Assert.Equal(UseCaseResultKind.Success, result.Kind);
        Assert.Equal(1, result.Value?.Total);
        Assert.Same(request, reads.LastQuery);
    }

    private static SiteApplicationService CreateService(ISiteReadPort reads) =>
        new(reads, new EmptyPortalUserDirectory(), new FixedTimeProvider(Now));

    internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    internal sealed class EmptyPortalUserDirectory : IPortalUserDirectory
    {
        public Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PortalUserProfile>>([]);

        public Task<PortalUserProfile?> FindByIdAsync(Guid id, CancellationToken token) =>
            Task.FromResult<PortalUserProfile?>(null);
    }

    internal class FakeSiteReadPort : ISiteReadPort
    {
        public bool Exists { get; init; }
        public SiteAccessScope? LastScope { get; private set; }
        public SiteQuery? LastQuery { get; private set; }
        public PagedResult<SiteListModel> QueryResult { get; init; } = new();

        public Task<bool> ExistsAsync(Guid siteId, SiteAccessScope scope, CancellationToken token)
        {
            LastScope = scope;
            return Task.FromResult(Exists);
        }

        public Task<PagedResult<SiteListModel>> QueryAsync(
            SiteAccessScope scope,
            SiteQuery query,
            CancellationToken token)
        {
            LastScope = scope;
            LastQuery = query;
            return Task.FromResult(QueryResult);
        }

        // These read methods return explicit empty materialized contracts for this use-case test.
        public Task<SiteOptionsModel> OptionsAsync(Guid? companyId, CancellationToken token) =>
            Task.FromResult(new SiteOptionsModel());
        public Task<SiteDetailModel?> GetAsync(Guid siteId, CancellationToken token) =>
            Task.FromResult<SiteDetailModel?>(null);
        public Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
            Guid siteId, PageRequest page, CancellationToken token) =>
            Task.FromResult(new PagedResult<SiteMonitorModel>());
        public Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
            Guid siteId, PageRequest page, CancellationToken token) =>
            Task.FromResult(new PagedResult<SiteNotificationModel>());
        public Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
            Guid siteId, CancellationToken token) =>
            Task.FromResult<SiteNotificationSettingsData?>(null);
    }
}
```

Place `FixedTimeProvider`, `EmptyPortalUserDirectory`, and
`FakeSiteReadPort` as top-level `internal` types in `SiteTestDoubles.cs` rather
than nesting them in `SiteReadUseCaseTests`; remove their declarations from the
test class after copying the shown bodies. Make the fake port properties
settable and its methods `virtual` so Tasks 4 and 5 can extend the same explicit
test double as the port grows.

- [ ] **Step 2: Run and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteReadUseCaseTests
```

Expected: build fails because the result, Sites contracts, read port, and application service do not exist.

- [ ] **Step 3: Add common result and paging primitives**

Implement `UseCaseResult<T>` with these exact public members:

```csharp
namespace RvtPortal.Application.Common;

public enum UseCaseResultKind
{
    Success,
    NotFound,
    Forbidden,
    Validation,
    Conflict,
    ExternalServiceUnavailable
}

public sealed record UseCaseError(string Field, string Message);

public sealed class UseCaseResult<T>
{
    private UseCaseResult(
        UseCaseResultKind kind,
        T? value,
        IReadOnlyList<UseCaseError> errors,
        string? message,
        int? statusCode)
    {
        Kind = kind;
        Value = value;
        Errors = errors;
        Message = message;
        StatusCode = statusCode;
    }

    public UseCaseResultKind Kind { get; }
    public T? Value { get; }
    public IReadOnlyList<UseCaseError> Errors { get; }
    public string? Message { get; }
    public int? StatusCode { get; }

    public static UseCaseResult<T> Success(T value) =>
        new(UseCaseResultKind.Success, value, [], null, null);
    public static UseCaseResult<T> NotFound(string message) =>
        new(UseCaseResultKind.NotFound, default, [], message, null);
    public static UseCaseResult<T> Forbidden() =>
        new(UseCaseResultKind.Forbidden, default, [], null, null);
    public static UseCaseResult<T> Validation(params UseCaseError[] errors) =>
        new(UseCaseResultKind.Validation, default, errors, null, null);
    public static UseCaseResult<T> Conflict(string message) =>
        new(UseCaseResultKind.Conflict, default, [], message, null);
    public static UseCaseResult<T> ExternalServiceUnavailable(
        string message,
        int? statusCode = null) =>
        new(UseCaseResultKind.ExternalServiceUnavailable, default, [], message, statusCode);
}
```

Implement `PageRequest.cs`:

```csharp
namespace RvtPortal.Application.Common;

public static class PageSortDirections
{
    public const string Ascending = "Ascending";
    public const string Descending = "Descending";

    public static string Normalize(string? value) =>
        string.Equals(value, Descending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
            ? Descending
            : Ascending;
}

public sealed record PageRequest(
    string? SearchText,
    int Page,
    int PageSize,
    string Sort,
    string SortDir);
```

Implement `PagedResult.cs`:

```csharp
namespace RvtPortal.Application.Common;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages =>
        Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasPreviousPage => Page > 1 && Total > 0;
    public bool HasNextPage => Page * PageSize < Total;
    public string SearchText { get; init; } = "";
    public string Sort { get; init; } = "";
    public string SortDir { get; init; } = PageSortDirections.Ascending;
}
```

- [ ] **Step 4: Move the Sites contracts into the application project**

Create `SiteContracts.cs` with the exact transport-neutral shapes below. These
are the existing Sites API-facing application models moved into the new
namespace; `HasCustomerLogo` is the only new response fact.

```csharp
using RvtPortal.Application.Common;

namespace RvtPortal.Application.Sites;

public sealed record SiteQuery(
    Guid? CompanyId,
    bool? IncludeArchived,
    PageRequest Page);

public sealed record SiteMutation(
    string SiteName,
    Guid CompanyId,
    Guid? ContractId,
    string? AddressLine1,
    string? AddressLine2,
    string? Postcode,
    string? City,
    string? County,
    string? StartTime,
    string? EndTime,
    string? SatStartTime,
    string? SatEndTime,
    string? SunStartTime,
    string? SunEndTime,
    IReadOnlyList<SiteOperatingHoursMutation>? OperatingHours);

public sealed record SiteOperatingHoursMutation(
    int DayOfWeek,
    string? StartTime,
    string? EndTime,
    bool IsClosed);

public sealed record SiteOperatingHoursModel(
    int DayOfWeek,
    string DayName,
    string? StartTime,
    string? EndTime,
    bool IsClosed);

public sealed record SiteOptionModel(string Value, string Label);

public sealed record SiteContractModel(
    Guid Id,
    string ContractNumber,
    DateTime OnHireDate,
    DateTime? OffHireDate,
    Guid CompanyId,
    string? CompanyName,
    Guid? SiteId,
    string? SiteName);

public sealed record SiteArchiveModel(
    DateTime Archived,
    string? CreatedBy,
    string? PictureLink);

public sealed record SiteMonitorModel(
    Guid Id,
    Guid DeploymentId,
    string? FleetNumber,
    string? SerialId,
    string? MonitorName,
    string TypeOfMonitor,
    Guid ContractId,
    string ContractNumber,
    DateTime? LastDataTime,
    bool OffLine,
    double? Lat,
    double? Lng,
    string? What3words);

public sealed record SiteNotificationModel(
    Guid Id,
    Guid MonitorId,
    string? FleetNumber,
    string? SerialId,
    string TypeOfMonitor,
    string AlertType,
    string? AlertField,
    double? LimitOn,
    double? Level,
    DateTime NotificationTime,
    Guid ContractId,
    string ContractNumber);

public sealed record SiteNotificationSettingMutation(
    bool Email,
    bool Sms,
    string? StartTime,
    string? EndTime);

public sealed record SiteNotificationSettingModel(
    Guid SiteUserId,
    Guid SiteId,
    Guid UserId,
    string UserEmail,
    string? UserName,
    bool SiteContact,
    bool Email,
    bool Sms,
    string? StartTime,
    string? EndTime);

public sealed class SiteNotificationSettingsModel
{
    public Guid SiteId { get; init; }
    public string? SiteName { get; init; }
    public List<SiteNotificationSettingModel> Settings { get; init; } = [];
}

public class SiteListModel
{
    public Guid Id { get; init; }
    public string SiteName { get; init; } = "";
    public bool Archived { get; init; }
    public DateTime CreateDate { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? Postcode { get; init; }
    public string? City { get; init; }
    public string? County { get; init; }
    public string? SiteAddress { get; init; }
    public string? Contracts { get; init; }
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string? SiteContact { get; init; }
    public int MonitorCount { get; set; }
    public int OpenNotificationCount { get; set; }
}

public sealed class SiteDetailModel : SiteListModel
{
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public string? SatStartTime { get; init; }
    public string? SatEndTime { get; init; }
    public string? SunStartTime { get; init; }
    public string? SunEndTime { get; init; }
    public List<SiteOperatingHoursModel> OperatingHours { get; init; } = [];
    public List<SiteContractModel> ContractList { get; init; } = [];
    public List<SiteMonitorModel> Monitors { get; init; } = [];
    public List<SiteNotificationModel> OpenNotifications { get; init; } = [];
    public SiteArchiveModel? Archive { get; init; }
    public List<SiteOptionModel> Companies { get; init; } = [];
    public List<SiteOptionModel> AvailableContracts { get; init; } = [];
    public bool CanManage { get; set; }
    public bool HasCustomerLogo { get; init; }
}

public sealed class SiteOptionsModel
{
    public List<SiteOptionModel> Companies { get; init; } = [];
    public List<SiteOptionModel> Contracts { get; init; } = [];
}
```

Add the materialized notification-setting adapter shape:

```csharp
public sealed record SiteNotificationAssignment(
    Guid SiteUserId,
    Guid SiteId,
    Guid UserId,
    bool SiteContact,
    bool Email,
    bool Sms,
    string? StartTime,
    string? EndTime);

public sealed record SiteNotificationSettingsData(
    Guid SiteId,
    string? SiteName,
    IReadOnlyList<SiteNotificationAssignment> Assignments);
```

- [ ] **Step 5: Define the read port and read facade**

Create `ISiteReadPort`:

```csharp
using RvtPortal.Application.Common;

namespace RvtPortal.Application.Sites.Ports;

public interface ISiteReadPort
{
    Task<bool> ExistsAsync(
        Guid siteId,
        SiteAccessScope scope,
        CancellationToken cancellationToken);
    Task<PagedResult<SiteListModel>> QueryAsync(
        SiteAccessScope scope,
        SiteQuery query,
        CancellationToken cancellationToken);
    Task<SiteOptionsModel> OptionsAsync(
        Guid? companyId,
        CancellationToken cancellationToken);
    Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken);
    Task<PagedResult<SiteMonitorModel>> QueryMonitorsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);
    Task<PagedResult<SiteNotificationModel>> QueryOpenNotificationsAsync(
        Guid siteId,
        PageRequest page,
        CancellationToken cancellationToken);
    Task<SiteNotificationSettingsData?> GetNotificationSettingsAsync(
        Guid siteId,
        CancellationToken cancellationToken);
}
```

Create the read portion of `ISiteApplicationService`:

```csharp
using RvtPortal.Application.Common;
using RvtPortal.Application.Identity;

namespace RvtPortal.Application.Sites;

public interface ISiteApplicationService
{
    Task<UseCaseResult<PagedResult<SiteListModel>>> QueryAsync(
        PortalUserContext user, SiteQuery request, CancellationToken cancellationToken);
    Task<UseCaseResult<SiteOptionsModel>> OptionsAsync(
        Guid? companyId, CancellationToken cancellationToken);
    Task<UseCaseResult<SiteDetailModel>> GetAsync(
        PortalUserContext user, Guid id, CancellationToken cancellationToken);
    Task<UseCaseResult<PagedResult<SiteMonitorModel>>> QueryMonitorsAsync(
        PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);
    Task<UseCaseResult<PagedResult<SiteNotificationModel>>> QueryOpenNotificationsAsync(
        PortalUserContext user, Guid siteId, PageRequest page, CancellationToken cancellationToken);
    Task<bool> CanReadSiteAsync(
        PortalUserContext user, Guid id, CancellationToken cancellationToken);
    Task<bool> CanManageSiteAsync(
        PortalUserContext user, Guid id, CancellationToken cancellationToken);
    Task<UseCaseResult<SiteNotificationSettingsModel>> GetNotificationSettingsAsync(
        PortalUserContext user, Guid siteId, CancellationToken cancellationToken);
}
```

On the new `SiteApplicationService`, preserve the controller-facing sort
metadata exactly:

```csharp
public const string DefaultSort = "createDate";
public const string MonitorSort = "fleetNumber";
public const string NotificationSort = "notificationTime";

public static readonly IReadOnlySet<string> SortFields =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "siteName",
        "companyName",
        "contracts",
        "createDate",
        "siteAddress"
    };

public static readonly IReadOnlySet<string> MonitorSortFields =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MonitorSort
    };

public static readonly IReadOnlySet<string> NotificationSortFields =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NotificationSort
    };
```

Implement these methods in `SiteApplicationService`. Every method must create
one scope using:

```csharp
var scope = SiteAuthorizationPolicy.ReadScope(
    user,
    timeProvider.GetUtcNow().UtcDateTime);
```

`GetAsync`, monitor, notification, and notification-setting reads first call
`ExistsAsync` with that scope and return not-found when false. For notification
settings, join `SiteNotificationSettingsData.Assignments` to
`IPortalUserDirectory.ListUsersAsync`; for a non-admin company user, return
only the row whose `UserId` equals the current user id.

- [ ] **Step 6: Extract the EF read adapter without changing query behavior**

Move the existing read-query helpers from
`RvtPortal.Spa/Application/Sites/SiteApplicationService.cs` into
`Adapters/Sites/EfSiteReadAdapter.cs`:

```text
ApplySiteFilters
ApplySiteSort
BuildOptionsAsync
PopulateSiteCountersAsync
ActiveSiteDeployments
ProjectSiteMonitors
BuildMonitorItemsAsync
BuildOpenNotificationItemsAsync
LoadActiveDeploymentsByMonitorAsync
BuildSiteNotification
BuildSiteListItem
BuildContractItem
LoadSiteDetailAsync
BuildOperatingHoursResponse
BuildAddress
JoinSummary
FormatTime
```

Keep the EF expressions byte-for-byte except for projecting the new application
namespace types. Implement scope filtering exactly as:

```csharp
private IQueryable<Site> VisibleSites(SiteAccessScope scope)
{
    var sites = domainContext.Sites.AsNoTracking();
    return scope.Kind switch
    {
        SiteAccessScopeKind.All => sites,
        SiteAccessScopeKind.Assigned when scope.UserId.HasValue =>
            sites.Where(site => domainContext.SiteUsers.Any(siteUser =>
                siteUser.SiteId == site.Id
                && siteUser.UserId == scope.UserId.Value
                && siteUser.StartDate <= scope.NowUtc
                && (!siteUser.EndDate.HasValue || siteUser.EndDate.Value >= scope.NowUtc))),
        _ => sites.Where(_ => false)
    };
}
```

`ExistsAsync` must call `VisibleSites(scope).AnyAsync(site => site.Id == siteId)`.
`GetAsync` must assemble the existing complete detail model. Leave
`HasCustomerLogo` false in this task; Task 5 enriches that application fact
through `ISiteLogoPort.ExistsAsync` without coupling the EF adapter to storage.

- [ ] **Step 7: Register and test the adapter**

Register:

```csharp
services.AddScoped<ISiteReadPort, EfSiteReadAdapter>();
```

Create `SiteReadAdapterTests` using `SpaTestApplicationFactory`, a scoped
`ISiteReadPort`, seeded admin/company/site/assignment rows, and the fixed
`TimeProvider`. Assert:

```csharp
Assert.True(await reads.ExistsAsync(activeSiteId, assignedScope, CancellationToken.None));
Assert.False(await reads.ExistsAsync(expiredSiteId, assignedScope, CancellationToken.None));
Assert.Equal(activeSiteId, Assert.Single(
    (await reads.QueryAsync(assignedScope, query, CancellationToken.None)).Results).Id);
```

- [ ] **Step 8: Verify GREEN**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteReadUseCaseTests
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~SiteReadAdapterTests
```

Expected: pure read-use-case tests and EF adapter visibility/paging tests pass.

- [ ] **Step 9: Commit**

```bash
git add \
  apps/portal/RvtPortal.Application \
  apps/portal/RvtPortal.Application.Tests \
  apps/portal/RvtPortal.Spa/Adapters/Sites \
  apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs \
  apps/portal/RvtPortal.Spa.Tests/SiteReadAdapterTests.cs
git commit -m "refactor: extract site read boundary"
```

---

### Task 4: Extract Transactional Site Mutations

**Files:**
- Create: `apps/portal/RvtPortal.Application/Common/IApplicationUnitOfWork.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteWritePort.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/SiteMutationValidator.cs`
- Create: `apps/portal/RvtPortal.Application.Tests/Sites/SiteMutationUseCaseTests.cs`
- Create: `apps/portal/RvtPortal.Spa/Adapters/Sites/EfSiteWriteAdapter.cs`
- Create: `apps/portal/RvtPortal.Spa.Tests/SiteWriteAdapterTests.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/ISiteApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/SiteApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: read port validation lookups, `TimeProvider`, and the unchanged EF shared-connection transaction implementation.
- Produces:
  - `IApplicationUnitOfWork.ExecuteInTransactionAsync` and `SaveChangesAsync`;
  - explicit create/update/notification-setting write-port methods;
  - transactional use cases with one save per successful mutation.

- [ ] **Step 1: Write failing transaction and validation tests**

Create `SiteMutationUseCaseTests` with recording read/write/unit-of-work fakes.
Cover these exact outcomes:

```csharp
[Fact]
public async Task CreateAsync_StagesSiteAndSavesOnceInsideTransaction()
{
    var fixture = SiteMutationFixture.Valid();

    var result = await fixture.Service.CreateAsync(
        fixture.Admin,
        fixture.Mutation,
        CancellationToken.None);

    Assert.Equal(UseCaseResultKind.Success, result.Kind);
    Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
    Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    Assert.Equal(1, fixture.Writes.CreateCount);
}

[Fact]
public async Task CreateAsync_InvalidTimePair_DoesNotOpenTransaction()
{
    var fixture = SiteMutationFixture.Valid() with
    {
        Mutation = SiteMutationFixture.ValidMutation() with
        {
            StartTime = "18:00",
            EndTime = "08:00"
        }
    };

    var result = await fixture.Service.CreateAsync(
        fixture.Admin,
        fixture.Mutation,
        CancellationToken.None);

    Assert.Equal(UseCaseResultKind.Validation, result.Kind);
    Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
    Assert.Equal(0, fixture.UnitOfWork.SaveCount);
}
```

Also cover missing/foreign contract, duplicate site name, missing company,
update-not-found, and company-user notification-setting ownership.

Define the fixture and recording collaborators in the same test file:

```csharp
private sealed record SiteMutationFixture(
    SiteApplicationService Service,
    PortalUserContext Admin,
    SiteMutation Mutation,
    RecordingUnitOfWork UnitOfWork,
    RecordingSiteWritePort Writes)
{
    public static SiteMutationFixture Valid()
    {
        var reads = new MutationSiteReadPort
        {
            Exists = true,
            MutationData = new SiteMutationValidationData(
                DuplicateSiteName: false,
                CompanyExists: true,
                ContractExists: true,
                ContractIsUnassigned: true,
                ContractBelongsToCompany: true),
            Detail = new SiteDetailModel
            {
                Id = Guid.NewGuid(),
                SiteName = "Valid Site"
            }
        };
        var writes = new RecordingSiteWritePort(reads.Detail.Id);
        var unitOfWork = new RecordingUnitOfWork();
        var service = new SiteApplicationService(
            reads,
            writes,
            unitOfWork,
            new EmptyPortalUserDirectory(),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)));
        return new SiteMutationFixture(
            service,
            new PortalUserContext(Guid.NewGuid(), "admin", null, true, false, false),
            ValidMutation(),
            unitOfWork,
            writes);
    }

    public static SiteMutation ValidMutation() =>
        new(
            "Valid Site",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unit 1",
            null,
            "AB1 2CD",
            "London",
            null,
            "08:00",
            "17:00",
            null,
            null,
            null,
            null,
            []);
}

private sealed class RecordingUnitOfWork : IApplicationUnitOfWork
{
    public int TransactionCount { get; private set; }
    public int SaveCount { get; private set; }

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        TransactionCount++;
        return await operation(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

private sealed class RecordingSiteWritePort(Guid createdSiteId) : ISiteWritePort
{
    public int CreateCount { get; private set; }

    public Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken)
    {
        CreateCount++;
        return Task.FromResult(createdSiteId);
    }

    public Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

private sealed class MutationSiteReadPort : FakeSiteReadPort
{
    public required SiteMutationValidationData MutationData { get; init; }
    public required SiteDetailModel Detail { get; init; }

    public override Task<SiteMutationValidationData> GetMutationValidationDataAsync(
        SiteMutation request,
        Guid? currentSiteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(MutationData);

    public override Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SiteDetailModel?>(Detail);
}
```

When extending `ISiteReadPort`, add virtual default implementations for
`GetMutationValidationDataAsync` and `GetNotificationSettingTargetAsync` to
the shared `FakeSiteReadPort`; `MutationSiteReadPort` overrides only the
behavior used by these tests.

- [ ] **Step 2: Run and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteMutationUseCaseTests
```

Expected: build fails because mutation ports, validator, and use-case methods do not exist.

- [ ] **Step 3: Define the inward transaction and write ports**

Create:

```csharp
namespace RvtPortal.Application.Common;

public interface IApplicationUnitOfWork
{
    Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

Extend the read port with focused validation/target lookups:

```csharp
Task<SiteMutationValidationData> GetMutationValidationDataAsync(
    SiteMutation request,
    Guid? currentSiteId,
    CancellationToken cancellationToken);

Task<SiteNotificationSettingTarget?> GetNotificationSettingTargetAsync(
    Guid siteId,
    Guid siteUserId,
    CancellationToken cancellationToken);
```

Define:

```csharp
public sealed record SiteMutationValidationData(
    bool DuplicateSiteName,
    bool CompanyExists,
    bool ContractExists,
    bool ContractIsUnassigned,
    bool ContractBelongsToCompany);

public sealed record SiteNotificationSettingTarget(
    Guid SiteUserId,
    Guid SiteId,
    Guid UserId);
```

Create `ISiteWritePort`:

```csharp
namespace RvtPortal.Application.Sites.Ports;

public interface ISiteWritePort
{
    Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken);
    Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken);
    Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken);
}
```

Add these shared test doubles to `SiteTestDoubles.cs`:

```csharp
internal sealed class InlineUnitOfWork : IApplicationUnitOfWork
{
    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken) =>
        await operation(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(1);
}

internal sealed class NoOpSiteWritePort : ISiteWritePort
{
    public Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(Guid.NewGuid());

    public Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

Update `SiteReadUseCaseTests.CreateService` to call the expanded constructor:

```csharp
private static SiteApplicationService CreateService(ISiteReadPort reads) =>
    new(
        reads,
        new NoOpSiteWritePort(),
        new InlineUnitOfWork(),
        new EmptyPortalUserDirectory(),
        new FixedTimeProvider(Now));
```

- [ ] **Step 4: Move validation into the application project**

Move the pure validation and normalization rules from the current Sites service
and `SiteCommands.cs` into `SiteMutationValidator`:

```text
site-name required/max length
duplicate-name result mapping
address/city/county/postcode max lengths
company required/existence
create requires a contract
contract exists, is unassigned, and belongs to selected company
legacy and seven-day operating-hours normalization
optional HH:mm parsing
paired start/end requirement
start strictly before end
```

The pure shape entry point is:

```csharp
public static SiteMutationValidationResult ValidateShape(SiteMutation request)
```

The database-fact entry point is:

```csharp
public static SiteMutationValidationResult ValidateBusinessRules(
    SiteMutationValidationResult shape,
    SiteMutationValidationData data,
    bool requireContract)
```

where:

```csharp
public sealed record ValidatedSiteOperatingHours(
    int DayOfWeek,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    bool IsClosed);

public sealed record ValidatedSiteMutation(
    SiteMutation Source,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    TimeSpan? SaturdayStartTime,
    TimeSpan? SaturdayEndTime,
    TimeSpan? SundayStartTime,
    TimeSpan? SundayEndTime,
    IReadOnlyList<ValidatedSiteOperatingHours> OperatingHours);

public sealed record SiteMutationValidationResult(
    IReadOnlyList<UseCaseError> Errors,
    ValidatedSiteMutation? Value)
{
    public bool IsValid => Errors.Count == 0 && Value is not null;
}
```

Use `TimeSpan.TryParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture, out ...)`
and return a field error instead of throwing for malformed wire input.

- [ ] **Step 5: Implement transactional use cases**

Extend `ISiteApplicationService` and `SiteApplicationService` with:

```csharp
Task<UseCaseResult<SiteDetailModel>> CreateAsync(
    PortalUserContext user,
    SiteMutation request,
    CancellationToken cancellationToken);
Task<UseCaseResult<SiteDetailModel>> UpdateAsync(
    PortalUserContext user,
    Guid id,
    SiteMutation request,
    CancellationToken cancellationToken);
Task<UseCaseResult<SiteNotificationSettingModel>> UpdateNotificationSettingAsync(
    PortalUserContext user,
    Guid siteId,
    Guid siteUserId,
    SiteNotificationSettingMutation request,
    CancellationToken cancellationToken);
```

The successful write pattern is:

```csharp
var shape = SiteMutationValidator.ValidateShape(request);
if (!shape.IsValid)
{
    return UseCaseResult<SiteDetailModel>.Validation([.. shape.Errors]);
}

return await unitOfWork.ExecuteInTransactionAsync(
    async token =>
    {
        var data = await reads.GetMutationValidationDataAsync(
            request,
            currentSiteId: null,
            token);
        var validation = SiteMutationValidator.ValidateBusinessRules(
            shape,
            data,
            requireContract: true);
        if (!validation.IsValid)
        {
            return UseCaseResult<SiteDetailModel>.Validation([.. validation.Errors]);
        }

        var siteId = await writes.CreateAsync(
            validation.Value!,
            timeProvider.GetUtcNow().UtcDateTime,
            token);
        await unitOfWork.SaveChangesAsync(token);
        var detail = await reads.GetAsync(siteId, token)
            ?? throw new InvalidOperationException(
                $"Site '{siteId}' was not readable after a successful create.");
        detail.CanManage = SiteAuthorizationPolicy.CanManage(user);
        return UseCaseResult<SiteDetailModel>.Success(detail);
    },
    cancellationToken);
```

Run parsing, field length, and time-pair validation before opening the
transaction. Read name/company/contract facts and stage the mutation inside the
same transaction so a stale contract assignment cannot cause a partial create.
Do not call `SaveChangesAsync` when the in-transaction business validation
returns errors.

- [ ] **Step 6: Implement EF write and unit-of-work adapters**

`EfSiteWriteAdapter` must move the existing entity mutation code without
calling `SaveChangesAsync`. `CreateAsync` adds the `Site`, assigns the contract,
and returns the generated Guid. It copies text/address/company/contract values
from `ValidatedSiteMutation.Source`, time values from the parsed `TimeSpan`
properties, and operating hours from `ValidatedSiteMutation.OperatingHours`;
the adapter performs no parsing or business validation. `UpdateAsync` loads
operating hours and returns false when the site does not exist.
`UpsertNotificationSettingAsync` stages the existing insert/update.

Make `EfCoreUnitOfWork` implement both interfaces:

```csharp
public sealed class EfCoreUnitOfWork :
    RvtPortal.Spa.Application.Common.IUnitOfWork,
    RvtPortal.Application.Common.IApplicationUnitOfWork
```

Its existing public method bodies satisfy both interfaces unchanged.

Register:

```csharp
services.AddScoped<IApplicationUnitOfWork>(
    provider => provider.GetRequiredService<EfCoreUnitOfWork>());
services.AddScoped<ISiteWritePort, EfSiteWriteAdapter>();
```

Register `EfCoreUnitOfWork` itself once as scoped, and map both unit-of-work
interfaces to that same scoped instance.

- [ ] **Step 7: Add adapter transaction tests**

`SiteWriteAdapterTests` must use relational SQLite and the real
`EfCoreUnitOfWork` to prove:

```csharp
Assert.Equal(1, await context.Sites.CountAsync());
Assert.Equal(siteId, await context.Contracts
    .Where(contract => contract.Id == contractId)
    .Select(contract => contract.SiteiD)
    .SingleAsync());
Assert.Equal(7, await context.SiteOperatingHours
    .CountAsync(hours => hours.SiteId == siteId));
```

Add a failing-operation case that throws after staging and asserts the site and
contract link are rolled back.

- [ ] **Step 8: Verify GREEN**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteMutationUseCaseTests
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~SiteWriteAdapterTests|FullyQualifiedName~EfCoreUnitOfWorkTests"
```

Expected: validation, single-save, commit, and rollback tests pass.

- [ ] **Step 9: Commit**

```bash
git add \
  apps/portal/RvtPortal.Application \
  apps/portal/RvtPortal.Application.Tests \
  apps/portal/RvtPortal.Spa/Adapters/Sites \
  apps/portal/RvtPortal.Spa/Application/Common/EfCoreUnitOfWork.cs \
  apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs \
  apps/portal/RvtPortal.Spa.Tests/SiteWriteAdapterTests.cs
git commit -m "refactor: extract transactional site mutations"
```

---

### Task 5: Extract Archive and Customer-logo Workflows

**Files:**
- Create: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteArchivePort.cs`
- Create: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteLogoPort.cs`
- Create: `apps/portal/RvtPortal.Application.Tests/Sites/SiteExternalWorkflowTests.cs`
- Create: `apps/portal/RvtPortal.Spa/Adapters/Sites/SiteArchiveAdapter.cs`
- Create: `apps/portal/RvtPortal.Spa/Adapters/Sites/SiteLogoAdapter.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/ISiteApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/SiteApplicationService.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteReadPort.cs`
- Modify: `apps/portal/RvtPortal.Application/Sites/Ports/ISiteWritePort.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: existing host `ISiteArchiveService` and `ICustomerLogoStorage`.
- Produces: SDK-free archive/logo ports and complete application use cases for archive, save/delete logo, and protected logo reads.

- [ ] **Step 1: Write failing external-workflow tests**

Cover these exact cases with recording fakes:

```csharp
[Fact]
public async Task ArchiveAsync_ExportFailureDoesNotOpenDatabaseTransaction()
{
    var fixture = SiteExternalFixture.ReadableAdmin();
    fixture.Archive.Result = SiteArchiveExportResult.Failed(
        "The site archive could not be created, so the site was not archived. Please try again.");

    var result = await fixture.Service.ArchiveAsync(
        fixture.Admin,
        fixture.SiteId,
        "admin",
        CancellationToken.None);

    Assert.Equal(UseCaseResultKind.ExternalServiceUnavailable, result.Kind);
    Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
    Assert.Equal(0, fixture.Writes.ArchiveCount);
}

[Fact]
public async Task SaveLogoAsync_UnauthorizedSiteDoesNotCallStorage()
{
    var fixture = SiteExternalFixture.InvisibleCompanyUser();
    await using var stream = new MemoryStream([1, 2, 3]);

    var result = await fixture.Service.SaveCustomerLogoAsync(
        fixture.User,
        fixture.SiteId,
        new SiteLogoUpload(stream, 3, "image/png", "logo.png"),
        CancellationToken.None);

    Assert.Equal(UseCaseResultKind.NotFound, result.Kind);
    Assert.Equal(0, fixture.Logos.SaveCount);
}
```

Also test successful archive ordering (export before transaction), idempotent
already-archived response, invalid logo outcome, delete authorization, and
protected read authorization.

Define the fixture with complete recording outcomes:

```csharp
private sealed record SiteExternalFixture(
    Guid SiteId,
    PortalUserContext User,
    PortalUserContext Admin,
    SiteApplicationService Service,
    ExternalUnitOfWork UnitOfWork,
    ExternalWritePort Writes,
    FakeArchivePort Archive,
    FakeLogoPort Logos,
    ExternalReadPort Reads)
{
    public static SiteExternalFixture ReadableAdmin()
    {
        var siteId = Guid.NewGuid();
        var reads = new ExternalReadPort
        {
            Exists = true,
            ArchiveState = new SiteArchiveState(siteId, false),
            Detail = new SiteDetailModel { Id = siteId, SiteName = "Site" }
        };
        var writes = new ExternalWritePort();
        var unitOfWork = new ExternalUnitOfWork();
        var archive = new FakeArchivePort();
        var logos = new FakeLogoPort();
        var admin = new PortalUserContext(
            Guid.NewGuid(), "admin", null, true, false, false);
        var service = new SiteApplicationService(
            reads,
            writes,
            unitOfWork,
            new EmptyPortalUserDirectory(),
            archive,
            logos,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)));
        return new(
            siteId,
            admin,
            admin,
            service,
            unitOfWork,
            writes,
            archive,
            logos,
            reads);
    }

    public static SiteExternalFixture InvisibleCompanyUser()
    {
        var fixture = ReadableAdmin();
        var user = new PortalUserContext(
            Guid.NewGuid(), "user", Guid.NewGuid(), false, false, true);
        fixture.Reads.Exists = false;
        return fixture with { User = user };
    }
}

private sealed class ExternalUnitOfWork : IApplicationUnitOfWork
{
    public int TransactionCount { get; private set; }

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        TransactionCount++;
        return await operation(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(1);
}

private sealed class ExternalWritePort : ISiteWritePort
{
    public int ArchiveCount { get; private set; }

    public Task MarkArchivedAsync(
        Guid siteId,
        string createdBy,
        string archiveUrl,
        DateTime archivedUtc,
        CancellationToken cancellationToken)
    {
        ArchiveCount++;
        return Task.CompletedTask;
    }

    public Task<Guid> CreateAsync(
        ValidatedSiteMutation mutation,
        DateTime createDateUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(Guid.NewGuid());
    public Task<bool> UpdateAsync(
        Guid siteId,
        ValidatedSiteMutation mutation,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);
    public Task UpsertNotificationSettingAsync(
        Guid siteUserId,
        SiteNotificationSettingMutation request,
        TimeSpan? startTime,
        TimeSpan? endTime,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

private sealed class FakeArchivePort : ISiteArchivePort
{
    public SiteArchiveExportResult Result { get; set; } =
        SiteArchiveExportResult.Success("https://archive.example/site.zip");

    public Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result);
}

private sealed class FakeLogoPort : ISiteLogoPort
{
    public int SaveCount { get; private set; }

    public Task<bool> ExistsAsync(Guid siteId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<SiteLogoSaveResult> SaveAsync(
        Guid siteId,
        SiteLogoUpload upload,
        CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(
            new SiteLogoSaveResult(SiteLogoSaveOutcome.Saved, null));
    }

    public Task DeleteAsync(Guid siteId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<SiteLogoFile?> OpenReadAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SiteLogoFile?>(null);
}

private sealed class ExternalReadPort : FakeSiteReadPort
{
    public new bool Exists { get; set; }
    public required SiteArchiveState ArchiveState { get; init; }
    public required SiteDetailModel Detail { get; init; }

    public override Task<bool> ExistsAsync(
        Guid siteId,
        SiteAccessScope scope,
        CancellationToken cancellationToken) =>
        Task.FromResult(Exists);

    public override Task<SiteArchiveState?> GetArchiveStateAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SiteArchiveState?>(ArchiveState);

    public override Task<SiteDetailModel?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SiteDetailModel?>(Detail);
}
```

When adding `GetArchiveStateAsync` to `ISiteReadPort`, add a virtual
null-returning implementation to the shared `FakeSiteReadPort`; the external
fixture overrides it with the explicit state shown above.

- [ ] **Step 2: Run and verify RED**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteExternalWorkflowTests
```

Expected: build fails because archive/logo contracts and use cases do not exist.

- [ ] **Step 3: Define application-owned external ports**

Create:

```csharp
namespace RvtPortal.Application.Sites.Ports;

public sealed record SiteArchiveExportResult(
    bool Succeeded,
    string? ArchiveUrl,
    string? ErrorMessage)
{
    public static SiteArchiveExportResult Success(string url) =>
        new(true, url, null);
    public static SiteArchiveExportResult Failed(string message) =>
        new(false, null, message);
}

public interface ISiteArchivePort
{
    Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken);
}
```

```csharp
namespace RvtPortal.Application.Sites.Ports;

public sealed record SiteLogoUpload(
    Stream Content,
    long Length,
    string ContentType,
    string FileName);

public sealed record SiteLogoFile(
    Stream Content,
    string ContentType,
    string FileName);

public enum SiteLogoSaveOutcome
{
    Saved,
    Invalid
}

public sealed record SiteLogoSaveResult(
    SiteLogoSaveOutcome Outcome,
    string? Message);

public interface ISiteLogoPort
{
    Task<bool> ExistsAsync(
        Guid siteId,
        CancellationToken cancellationToken);
    Task<SiteLogoSaveResult> SaveAsync(
        Guid siteId,
        SiteLogoUpload upload,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid siteId, CancellationToken cancellationToken);
    Task<SiteLogoFile?> OpenReadAsync(
        Guid siteId,
        CancellationToken cancellationToken);
}
```

Extend the read port with:

```csharp
Task<SiteArchiveState?> GetArchiveStateAsync(
    Guid siteId,
    CancellationToken cancellationToken);
```

where:

```csharp
public sealed record SiteArchiveState(Guid SiteId, bool Archived);
```

Extend the write port with:

```csharp
Task MarkArchivedAsync(
    Guid siteId,
    string createdBy,
    string archiveUrl,
    DateTime archivedUtc,
    CancellationToken cancellationToken);
```

Add no-op external ports to `SiteTestDoubles.cs`:

```csharp
internal sealed class NoOpSiteArchivePort : ISiteArchivePort
{
    public Task<SiteArchiveExportResult> ExportAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult(SiteArchiveExportResult.Success("https://archive.example/site.zip"));
}

internal sealed class NoOpSiteLogoPort : ISiteLogoPort
{
    public Task<bool> ExistsAsync(Guid siteId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    public Task<SiteLogoSaveResult> SaveAsync(
        Guid siteId,
        SiteLogoUpload upload,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SiteLogoSaveResult(SiteLogoSaveOutcome.Saved, null));
    public Task DeleteAsync(Guid siteId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    public Task<SiteLogoFile?> OpenReadAsync(
        Guid siteId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SiteLogoFile?>(null);
}
```

Add a no-op `MarkArchivedAsync` implementation to `NoOpSiteWritePort` and
`RecordingSiteWritePort`. Update the Task 3 and Task 4 service constructions to
pass `new NoOpSiteArchivePort()` and `new NoOpSiteLogoPort()` immediately before
the `FixedTimeProvider`. This keeps all earlier tests compiling against the
final constructor:

```csharp
public SiteApplicationService(
    ISiteReadPort reads,
    ISiteWritePort writes,
    IApplicationUnitOfWork unitOfWork,
    IPortalUserDirectory userDirectory,
    ISiteArchivePort archive,
    ISiteLogoPort logos,
    TimeProvider timeProvider)
```

- [ ] **Step 4: Implement archive and logo use cases**

Extend `ISiteApplicationService` with:

```csharp
Task<UseCaseResult<SiteDetailModel>> ArchiveAsync(
    PortalUserContext user, Guid id, string createdBy, CancellationToken cancellationToken);
Task<UseCaseResult<SiteDetailModel>> SaveCustomerLogoAsync(
    PortalUserContext user, Guid id, SiteLogoUpload upload, CancellationToken cancellationToken);
Task<UseCaseResult<SiteDetailModel>> DeleteCustomerLogoAsync(
    PortalUserContext user, Guid id, CancellationToken cancellationToken);
Task<UseCaseResult<SiteLogoFile>> OpenCustomerLogoAsync(
    PortalUserContext user, Guid id, CancellationToken cancellationToken);
```

Archive sequence:

```csharp
var state = await reads.GetArchiveStateAsync(id, cancellationToken);
if (state is null)
{
    return UseCaseResult<SiteDetailModel>.NotFound($"Site '{id}' was not found.");
}

if (!state.Archived)
{
    var export = await archive.ExportAsync(id, cancellationToken);
    if (!export.Succeeded || string.IsNullOrWhiteSpace(export.ArchiveUrl))
    {
        return UseCaseResult<SiteDetailModel>.ExternalServiceUnavailable(
            export.ErrorMessage
                ?? "The site archive could not be created, so the site was not archived. Please try again.");
    }

    await unitOfWork.ExecuteInTransactionAsync(
        async token =>
        {
            await writes.MarkArchivedAsync(
                id,
                createdBy,
                export.ArchiveUrl,
                timeProvider.GetUtcNow().UtcDateTime,
                token);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        },
        cancellationToken);
}
```

Logo save/delete require `CanManageSiteAsync`; protected open requires
`CanReadSiteAsync`. Map `SiteLogoSaveOutcome.Invalid` to a validation result
whose message matches the existing invalid-logo problem detail.

After every successful detail read, set:

```csharp
detail.HasCustomerLogo = await logos.ExistsAsync(detail.Id, cancellationToken);
```

Change `SiteDetailModel.HasCustomerLogo` to `get; set;` when adding this
enrichment. Create/update/archive/logo responses must all pass through the same
detail-enrichment helper so the protected link never depends on controller-side
file-system inspection.

- [ ] **Step 5: Implement host adapters**

`SiteArchiveAdapter` catches non-cancellation exceptions from the existing
`ISiteArchiveService.Process` and returns `SiteArchiveExportResult.Failed`.
It must rethrow `OperationCanceledException`.

`SiteLogoAdapter.ExistsAsync` returns whether
`ICustomerLogoStorage.BuildProtectedLink(siteId)` is non-null.
`SiteLogoAdapter` maps application `SiteLogoUpload` to an `IUploadedContent`
implementation backed by the supplied stream, invokes `ICustomerLogoStorage`,
and maps `StorageValidationException` to `SiteLogoSaveOutcome.Invalid`.
`OpenReadAsync` maps the existing stored file to `SiteLogoFile`.

Register all four application-facing adapters:

```csharp
services.AddScoped<ISiteArchivePort, SiteArchiveAdapter>();
services.AddScoped<ISiteLogoPort, SiteLogoAdapter>();
```

- [ ] **Step 6: Verify GREEN**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj \
  --filter FullyQualifiedName~SiteExternalWorkflowTests
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~SiteArchiveServiceSecurityTests|FullyQualifiedName~StorageAdapterTests"
```

Expected: application failure-ordering tests and existing archive/storage adapter tests pass.

- [ ] **Step 7: Commit**

```bash
git add \
  apps/portal/RvtPortal.Application \
  apps/portal/RvtPortal.Application.Tests \
  apps/portal/RvtPortal.Spa/Adapters/Sites \
  apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs
git commit -m "refactor: extract site archive and logo workflows"
```

---

### Task 6: Cut the Controller Over and Remove the Superseded Sites Layer

**Files:**
- Modify: `apps/portal/RvtPortal.Spa/Api/SitesController.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/Mappers/SiteApiMapper.cs`
- Modify: `apps/portal/RvtPortal.Spa/Api/ApiResultMapper.cs`
- Modify: `apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/CqrsArchitectureTests.cs`
- Modify: `apps/portal/RvtPortal.Spa.Tests/ContractSiteOperationsTests.cs`
- Delete: `apps/portal/RvtPortal.Spa/Application/Sites/ActiveSiteAssignment.cs`
- Delete: `apps/portal/RvtPortal.Spa/Application/Sites/SiteApplicationService.cs`
- Delete: `apps/portal/RvtPortal.Spa/Application/Sites/SiteCommands.cs`
- Delete: `apps/portal/RVT.BusinessLogic/Sites/SiteApplicationModels.cs`

**Interfaces:**
- Consumes: complete application `ISiteApplicationService`.
- Produces: unchanged `/api/sites` HTTP behavior with no direct EF, MediatR, archive, or customer-logo storage dependency in `SitesController`.

- [ ] **Step 1: Strengthen the controller architecture test and verify RED**

Replace the current Sites assertion with:

```csharp
[Fact]
public void SitesController_DependsOnlyOnSiteUseCasesAndHttpMappers()
{
    var constructorParameters = ConstructorParameters(typeof(SitesController));

    Assert.Contains(typeof(RvtPortal.Application.Sites.ISiteApplicationService), constructorParameters);
    Assert.Contains(typeof(ICurrentUserContextFactory), constructorParameters);
    Assert.Contains(typeof(IApiResultMapper), constructorParameters);
    Assert.DoesNotContain(typeof(RVT.DataAccess.Context.RVTDbContext), constructorParameters);
    Assert.DoesNotContain(typeof(Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>), constructorParameters);
    Assert.DoesNotContain(typeof(MediatR.IMediator), constructorParameters);
    Assert.DoesNotContain(typeof(ICustomerLogoStorage), constructorParameters);
    Assert.DoesNotContain(
        constructorParameters,
        type => type.FullName == "RvtPortal.Spa.Adapters.Archive.ISiteArchiveService");
}
```

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter FullyQualifiedName~SitesController_DependsOnlyOnSiteUseCasesAndHttpMappers
```

Expected: FAIL because the controller still depends on the legacy Sites service
and `ICustomerLogoStorage`.

- [ ] **Step 2: Add application-result HTTP mapping**

Add a second `IApiResultMapper.ToActionResult` overload for
`RvtPortal.Application.Common.UseCaseResult<TModel>`. Map its six result kinds
to exactly the same HTTP responses as the legacy `ApplicationResult<T>`:

```csharp
UseCaseResultKind.Success => map(result.Value)
UseCaseResultKind.NotFound => 404 ProblemDetails
UseCaseResultKind.Forbidden => Forbid()
UseCaseResultKind.Validation => 400 ValidationProblemDetails
UseCaseResultKind.Conflict => 409 ProblemDetails
UseCaseResultKind.ExternalServiceUnavailable => result.StatusCode ?? 503
```

Do not alter the existing overload used by other slices.

- [ ] **Step 3: Repoint the mapper and controller**

Change `SiteApiMapper` to consume `RvtPortal.Application.Common` and
`RvtPortal.Application.Sites` contracts. Preserve every API DTO assignment.
Add this overload so the controller can retain the existing normalization
factory without leaking its legacy paging type into the application project:

```csharp
public static RvtPortal.Application.Common.PageRequest ToApplicationPage(
    RVT.BusinessLogic.Application.Paging.PageRequest page) =>
    new(
        page.SearchText,
        page.Page,
        page.PageSize,
        page.Sort,
        page.SortDir);
```

In `SitesController.Query`, keep `PageRequestFactory.Create` and
`PageRequestFactory.IsInvalidSort` on the legacy normalized value, then pass
`SiteApiMapper.ToApplicationPage(page)` into the new `SiteQuery`. Change
`BuildFixedSortPageRequest` to return
`RvtPortal.Application.Common.PageRequest` directly while preserving
`GetNormalizedPage`, `GetNormalizedPageSize`, and `GetNormalizedSortDir`.

Set the customer-logo URL at the HTTP edge:

```csharp
var customerLogoUrl = model.HasCustomerLogo
    ? $"/api/sites/{model.Id}/customer-logo"
    : null;
return SiteApiMapper.ToDetailResponse(model, customerLogoUrl);
```

Change `SitesController` constructor to:

```csharp
public SitesController(
    RvtPortal.Application.Sites.ISiteApplicationService sites,
    ICurrentUserContextFactory currentUserContextFactory,
    IApiResultMapper resultMapper)
```

Map logo endpoints as:

```csharp
if (logo is null)
{
    return BadRequest(new ProblemDetails
    {
        Title = "Customer logo required",
        Detail = "Choose a logo image before uploading."
    });
}

await using var content = logo.OpenReadStream();
var result = await sites.SaveCustomerLogoAsync(
    await CreateUserContextAsync(),
    id,
    new SiteLogoUpload(
        content,
        logo.Length,
        logo.ContentType,
        logo.FileName),
    cancellationToken);
return resultMapper.ToActionResult(this, result, ToSiteDetailEntity);
```

and:

```csharp
var result = await sites.OpenCustomerLogoAsync(
    await CreateUserContextAsync(),
    id,
    cancellationToken);
return ToLogoActionResult(id, result);
```

Add this exact helper so no stream is wrapped in JSON:

```csharp
private IActionResult ToLogoActionResult(
    Guid siteId,
    UseCaseResult<SiteLogoFile> result)
{
    if (result.Kind == UseCaseResultKind.Success && result.Value is not null)
    {
        return File(
            result.Value.Content,
            result.Value.ContentType,
            result.Value.FileName);
    }

    return result.Kind switch
    {
        UseCaseResultKind.NotFound => SiteNotFound(siteId),
        UseCaseResultKind.Forbidden => Forbid(),
        _ => StatusCode(
            StatusCodes.Status500InternalServerError,
            ApiProblems.Create(
                HttpContext,
                StatusCodes.Status500InternalServerError,
                "Unexpected application result.",
                "The customer logo could not be read."))
    };
}
```

- [ ] **Step 4: Cut DI over and remove duplicate implementations**

Register:

```csharp
services.AddScoped<
    RvtPortal.Application.Sites.ISiteApplicationService,
    RvtPortal.Application.Sites.SiteApplicationService>();
```

Remove the legacy Sites service registration. Delete the three superseded host
Sites files and the legacy BusinessLogic Sites model file only after:

```bash
rg -n "RvtPortal\\.Spa\\.Application\\.Sites|RVT\\.BusinessLogic\\.Sites" \
  apps/portal/RvtPortal.Spa apps/portal/RvtPortal.Spa.Tests
```

shows no live consumer other than deletion targets or comments being updated in
this task.

- [ ] **Step 5: Run focused compatibility tests and fix only boundary drift**

Run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~CqrsArchitectureTests|FullyQualifiedName~ContractSiteOperationsTests"
```

Expected: architecture tests and all contract/site CRUD, archive, logo,
assignment-window, and notification-setting HTTP tests pass without route or
payload changes.

- [ ] **Step 6: Add response-contract assertions**

In `ContractSiteOperationsTests`, retain existing tests and add assertions that
the create response still supplies the `Location` header and that protected
logo not-found remains 404:

```csharp
Assert.Equal($"/api/sites/{siteId}", create.Headers.Location?.AbsolutePath);
Assert.Equal(HttpStatusCode.NotFound, missingAfterDelete.StatusCode);
```

Run the same focused command and expect all tests to pass.

- [ ] **Step 7: Commit**

```bash
git add \
  apps/portal/RVT.BusinessLogic/Sites \
  apps/portal/RvtPortal.Spa/Api \
  apps/portal/RvtPortal.Spa/Application/Sites \
  apps/portal/RvtPortal.Spa/ServiceCollectionExtensions.cs \
  apps/portal/RvtPortal.Spa.Tests
git commit -m "refactor: cut sites over to application boundary"
```

---

### Task 7: Close Architecture Documentation and Run the Full Gate

**Files:**
- Modify: `docs/architecture/portal/ports-and-adapters-catalog.md`
- Modify: `docs/architecture/portal/hexagonal-edges-change-log.md`
- Modify: `project_state.md`
- Modify: `docs/superpowers/plans/2026-07-23-rvtportal-sites-application-boundary.md`

**Interfaces:**
- Consumes: the completed application slice and verification evidence.
- Produces: current architecture documentation, resumable project state, and merge-ready evidence.

- [ ] **Step 1: Update the ports and adapters catalog**

Replace the current Sites row and dependency diagram with:

```text
RvtPortal.Spa.Api.SitesController
  -> RvtPortal.Application.Sites.ISiteApplicationService
    -> ISiteReadPort / ISiteWritePort
    -> IApplicationUnitOfWork
    -> IPortalUserDirectory
    -> ISiteArchivePort / ISiteLogoPort
      -> RvtPortal.Spa.Adapters.Sites
        -> EF Core / Identity / archive export / customer-logo storage
```

Record that `RVT.BusinessLogic` remains a legacy boundary for slices not yet
extracted and must not be moved opportunistically.

- [ ] **Step 2: Record the change and exact dependency rules**

Append a dated entry to `hexagonal-edges-change-log.md` containing:

```text
- Added BCL-only RvtPortal.Application and RvtPortal.Application.Tests.
- Moved the complete Sites use-case surface behind application-owned ports.
- Kept controller routes/payloads and EF SQL-side paging/projection unchanged.
- Retained EfCoreUnitOfWork and the shared three-context transaction adapter.
- Removed the duplicate host Sites service/MediatR command implementations.
- Added application, adapter, architecture, and HTTP compatibility tests.
```

- [ ] **Step 3: Run the full verification gate**

Run:

```bash
dotnet test apps/portal/RvtPortal.Application.Tests/RvtPortal.Application.Tests.csproj --nologo
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --nologo
dotnet build apps/portal/RvtPortal.Spa.sln --no-restore --nologo
npm run test:run --prefix apps/portal/RvtPortal.Client
npm run build --prefix apps/portal/RvtPortal.Client
tests/verify-mono-solution.test.sh
tests/verify-mono-layout.test.sh
tests/verify-rvt-common-source-boundary.test.sh
tests/verify-documentation-layout.test.sh
git diff --check
```

Expected:

```text
RvtPortal.Application.Tests: all pass, no skips
RvtPortal.Spa.Tests: all ordinary tests pass; only explicitly provider-gated PostgreSQL/TimescaleDB tests may skip
RvtPortal.Spa.sln: build succeeds with zero errors
RvtPortal.Client: 68 or more tests pass and production build succeeds
all repository guards pass
git diff --check emits no output
```

- [ ] **Step 4: Run provider-gated verification when configured**

If `RVT_TEST_POSTGRES_CONNECTION` is set, run:

```bash
dotnet test apps/portal/RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj \
  --filter "FullyQualifiedName~Postgres|FullyQualifiedName~Timestamptz"
```

Expected: all discovered PostgreSQL/TimescaleDB tests pass. If the variable is
unset, record the exact skipped count in `project_state.md` and do not claim
provider closure.

- [ ] **Step 5: Save the final resumable state**

Append to `project_state.md`:

```text
branch and final commit
new project/file ownership
application and host adapter registrations
public port/interface names
verification counts and provider-gated skips
known NU1903 advisories
remaining next slice candidates without selecting one
```

Mark every completed checkbox in this plan.

- [ ] **Step 6: Commit**

```bash
git add \
  docs/architecture/portal/ports-and-adapters-catalog.md \
  docs/architecture/portal/hexagonal-edges-change-log.md \
  docs/superpowers/plans/2026-07-23-rvtportal-sites-application-boundary.md \
  project_state.md
git commit -m "docs: record sites application extraction"
```

- [ ] **Step 7: Independent review before merge**

Use `superpowers:requesting-code-review` against the full branch diff from
`main` to `HEAD`. Resolve every validated Critical or Important finding with a
fresh focused RED/GREEN cycle, rerun the full gate, and only then present the
branch for merge.
