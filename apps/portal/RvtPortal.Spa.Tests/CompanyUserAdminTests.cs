// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-07-09 pending Added organic admin-user validation coverage for account workflow refactoring.
// - 2026-06-26 pending Added RC-grade site assignment default notification-setting scenario coverage.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class CompanyUserAdminTests
{
    private const string AdminEmail = "admin.admin@rvt.test";
    private const string MasterEmail = "admin.master@rvt.test";
    private const string Password = "P8sSw0rd9$";

    [Fact]
    // Function summary: Handles the company crud validates unique names and deletes company users workflow for this module.
    public async Task CompanyCrud_ValidatesUniqueNamesAndDeletesCompanyUsers()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var companyUser = await factory.SeedUserAsync(
            "company.member@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainCompaniesAsync(new Company { Id = companyId, CompanyName = "Alpha Monitoring", Contracts = [] });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var duplicate = await client.PostAsJsonAsync("/api/companies", new CompanyMutationRequest { CompanyName = "Alpha Monitoring" });
        var create = await client.PostAsJsonAsync("/api/companies", new CompanyMutationRequest { CompanyName = "Beta Monitoring" });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<CompanyDetailResponse>>();
        var update = await client.PutAsJsonAsync($"/api/companies/{created!.Item!.Id}", new CompanyMutationRequest { CompanyName = "Beta Updated" });
        var detail = await client.GetFromJsonAsync<EntityResponse<CompanyDetailResponse>>($"/api/companies/{created.Item.Id}");
        var delete = await client.DeleteAsync($"/api/companies/{companyId}");
        var deletedCompanyUser = await client.GetAsync($"/api/users/{companyUser.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Beta Updated", detail?.Item?.CompanyName);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedCompanyUser.StatusCode);
    }

    [Fact]
    // Function summary: Applies r administration enforces role rules and supports status and link actions to the current configuration.
    public async Task UserAdministration_EnforcesRoleRulesAndSupportsStatusAndLinkActions()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        await factory.SeedDomainCompaniesAsync(new Company { Id = companyId, CompanyName = "RVT Customer", Contracts = [] });
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(MasterEmail, Password, RoleNames.RVTMasterAdmin);
        var existingCompanyUser = await factory.SeedUserAsync(
            "existing.company.user@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var list = await client.GetFromJsonAsync<QueryUsersResponse>($"/api/users?companyId={companyId}&sort=email");
        var create = await client.PostAsJsonAsync("/api/users", new UserMutationRequest
        {
            Email = "new.company.user@rvt.test",
            Name = "New Company User",
            Role = RoleNames.CompanyUser,
            CompanyId = companyId,
            CompanyRole = "Project lead",
            MobilePhone = "07123456789"
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<UserDetailResponse>>();
        var resend = await client.PostAsync($"/api/users/{created!.Item!.Id}/resend-confirmation", null);
        var reset = await client.PostAsync($"/api/users/{existingCompanyUser.Id}/reset-password-link", null);
        var disable = await client.PostAsync($"/api/users/{created.Item.Id}/disable", null);
        var disabled = await disable.Content.ReadFromJsonAsync<EntityResponse<UserDetailResponse>>();
        var enable = await client.PostAsync($"/api/users/{created.Item.Id}/enable", null);
        var masterUpdate = await client.PutAsJsonAsync($"/api/users/{(await factory.SeedUserAsync("second.master@rvt.test", Password, RoleNames.RVTMasterAdmin)).Id}", new UserMutationRequest
        {
            Email = "second.master@rvt.test",
            Name = "Blocked Master",
            Role = RoleNames.RVTMasterAdmin
        });
        var delete = await client.DeleteAsync($"/api/users/{created.Item.Id}");

        Assert.NotNull(list);
        Assert.Contains(list!.Results, user => user.Id == existingCompanyUser.Id && user.CanEdit && user.CanDelete);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("new.company.user@rvt.test", created.Item.Email);
        Assert.False(created.Item.EmailConfirmed);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.True(disabled?.Item?.IsDisabled);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, masterUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    // Function summary: Verifies admin user validation rejects duplicate emails and company users without a company.
    public async Task UserAdministration_RejectsDuplicateEmailAndMissingCompany()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        await factory.SeedDomainCompaniesAsync(new Company { Id = companyId, CompanyName = "Validation Customer", Contracts = [] });
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var existing = await factory.SeedUserAsync(
            "existing.validation@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var duplicateEmail = await client.PostAsJsonAsync("/api/users", new UserMutationRequest
        {
            Email = existing.Email!,
            Name = "Duplicate Email",
            Role = RoleNames.CompanyUser,
            CompanyId = companyId
        });
        var missingCompany = await client.PostAsJsonAsync("/api/users", new UserMutationRequest
        {
            Email = "missing.company@rvt.test",
            Name = "Missing Company",
            Role = RoleNames.CompanyUser
        });
        var search = await client.GetFromJsonAsync<QueryUsersResponse>("/api/users?searchText=missing.company@rvt.test");

        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingCompany.StatusCode);
        Assert.Empty(search!.Results);
    }

    [Fact]
    // Function summary: Handles the site assignments add contact and remove company users workflow for this module.
    public async Task SiteAssignments_AddContactAndRemoveCompanyUsers()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var user = await factory.SeedUserAsync(
            "site.assignment.user@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Site Assignment Co", Contracts = [] },
            new Site { Id = siteId, SiteName = "Athens Plant", Contracts = [] },
            new Contract { Id = Guid.NewGuid(), ContractNumber = "P3-001", CompanyId = companyId, SiteiD = siteId });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var initial = await client.GetFromJsonAsync<EntityResponse<SiteAssignmentResponse>>($"/api/users/site-assignments/{siteId}");
        var add = await client.PostAsJsonAsync("/api/users/site-assignments", new SiteUserMutationRequest
        {
            SiteId = siteId,
            UserId = Guid.Parse(user.Id)
        });
        var added = await add.Content.ReadFromJsonAsync<EntityResponse<SiteAssignmentResponse>>();
        var notificationSetting = ReadNotificationSettingsFor(factory, Guid.Parse(user.Id), siteId);
        var contact = await client.PostAsJsonAsync("/api/users/site-assignments/contact", new SiteUserMutationRequest
        {
            SiteId = siteId,
            UserId = Guid.Parse(user.Id)
        });
        var contacted = await contact.Content.ReadFromJsonAsync<EntityResponse<SiteAssignmentResponse>>();
        var unset = await client.DeleteAsync($"/api/users/site-assignments/contact/{siteId}/{user.Id}");
        var unsetResult = await unset.Content.ReadFromJsonAsync<EntityResponse<SiteAssignmentResponse>>();
        var remove = await client.DeleteAsync($"/api/users/site-assignments/{siteId}/{user.Id}");
        var removed = await remove.Content.ReadFromJsonAsync<EntityResponse<SiteAssignmentResponse>>();

        Assert.Equal(siteId, initial?.Item?.SiteId);
        Assert.Contains(initial!.Item!.AvailableUsers, candidate => candidate.Id == user.Id);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        Assert.Contains(added!.Item!.AssignedUsers, assigned => assigned.Id == user.Id && !assigned.SiteContact);
        Assert.NotNull(notificationSetting);
        Assert.True(notificationSetting.Email);
        Assert.False(notificationSetting.SMS);
        Assert.Equal(new TimeSpan(8, 0, 0), notificationSetting.StartTime);
        Assert.Equal(new TimeSpan(18, 0, 0), notificationSetting.EndTime);
        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);
        Assert.Contains(contacted!.Item!.AssignedUsers, assigned => assigned.Id == user.Id && assigned.SiteContact);
        Assert.Equal(HttpStatusCode.OK, unset.StatusCode);
        Assert.Contains(unsetResult!.Item!.AssignedUsers, assigned => assigned.Id == user.Id && !assigned.SiteContact);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.DoesNotContain(removed!.Item!.AssignedUsers, assigned => assigned.Id == user.Id);
        Assert.Contains(removed.Item.AvailableUsers, candidate => candidate.Id == user.Id);
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(SpaTestApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    // Function summary: Handles the login workflow for this module.
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });
    }

    // Function summary: Reads the default notification settings created when a user is assigned to a site.
    private static NotificationSettings? ReadNotificationSettingsFor(
        SpaTestApplicationFactory factory,
        Guid userId,
        Guid siteId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        var siteUser = context.SiteUsers.SingleOrDefault(item => item.UserId == userId && item.SiteId == siteId);
        return siteUser == null
            ? null
            : context.NotificationSettings.SingleOrDefault(item => item.SiteUserId == siteUser.Id);
    }
}
