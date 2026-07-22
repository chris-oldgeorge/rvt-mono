// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-24 pending Added customer logo upload, protected read, and reporting-service fetch coverage.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-08 pending Added per-day site operating-hours regression coverage.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;
namespace RvtPortal.Spa.Tests;

public class ContractSiteOperationsTests
{
    private const string AdminEmail = "contracts.admin@rvt.test";
    private const string CompanyUserEmail = "contracts.company@rvt.test";
    private const string Password = "P8sSw0rd9$";
    private const string ReportContentKey = "test-report-content-key";

    // The weekly schedule a site is expected to persist and round-trip verbatim. Declared once so
    // the create request and the read-back assertion share a single source of truth: closed days
    // carry no times, and DayName is derived server-side from DayOfWeek (1 = Monday .. 7 = Sunday).
    private sealed record DaySchedule(int DayOfWeek, string DayName, string? StartTime, string? EndTime, bool IsClosed);

    private static readonly DaySchedule[] SiteWeeklyHours =
    [
        new(DayOfWeek: 1, DayName: "Monday",    StartTime: "07:00", EndTime: "17:00", IsClosed: false),
        new(DayOfWeek: 2, DayName: "Tuesday",   StartTime: "08:00", EndTime: "18:00", IsClosed: false),
        new(DayOfWeek: 3, DayName: "Wednesday", StartTime: "09:00", EndTime: "19:00", IsClosed: false),
        new(DayOfWeek: 4, DayName: "Thursday",  StartTime: null,    EndTime: null,    IsClosed: true),
        new(DayOfWeek: 5, DayName: "Friday",    StartTime: "08:30", EndTime: "16:30", IsClosed: false),
        new(DayOfWeek: 6, DayName: "Saturday",  StartTime: "10:00", EndTime: "14:00", IsClosed: false),
        new(DayOfWeek: 7, DayName: "Sunday",    StartTime: null,    EndTime: null,    IsClosed: true),
    ];

    [Fact]
    // Function summary: Handles the contract crud validates duplicate dates and site company rules workflow for this module.
    public async Task ContractCrud_ValidatesDuplicateDatesAndSiteCompanyRules()
    {
        // Values shared between what we seed/submit and what we later assert are named once, so the
        // relationship (e.g. "the duplicate reuses the existing contract's number") is explicit.
        const string siteName = "London Works";
        const string existingContractNumber = "P4-001";
        const string createdContractNumber = "P4-004";
        const string renamedContractNumber = "P4-004A";

        using var factory = new SpaTestApplicationFactory();
        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var existingContractId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = alphaId, CompanyName = "Alpha Hire", Contracts = [] },
            new Company { Id = betaId, CompanyName = "Beta Hire", Contracts = [] },
            new Site { Id = siteId, SiteName = siteName, CreateDate = DateTime.UtcNow, Contracts = [] },
            new Contract
            {
                Id = existingContractId,
                ContractNumber = existingContractNumber,
                CompanyId = alphaId,
                SiteiD = siteId,
                OnHireDate = new DateTime(2026, 1, 1)
            });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // Reusing the seeded contract's number is rejected as a duplicate.
        var duplicate = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = existingContractNumber,
            CompanyId = alphaId,
            OnHireDate = new DateTime(2026, 1, 2)
        });

        // Off-hire dated before on-hire is rejected.
        var invalidDates = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = "P4-002",
            CompanyId = alphaId,
            OnHireDate = new DateTime(2026, 2, 2),
            OffHireDate = new DateTime(2026, 2, 1)
        });

        // A site already belongs to Alpha, so binding it to a Beta contract is rejected.
        var conflictingSiteCompany = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = "P4-003",
            CompanyId = betaId,
            SiteId = siteId,
            OnHireDate = new DateTime(2026, 3, 1)
        });

        // A valid contract is created, renamed, listed by its shared "P4" prefix, then deleted.
        var create = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = createdContractNumber,
            CompanyId = betaId,
            OnHireDate = new DateTime(2026, 4, 1)
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<ContractDetailResponse>>();
        var update = await client.PutAsJsonAsync($"/api/contracts/{created!.Item!.Id}", new ContractMutationRequest
        {
            ContractNumber = renamedContractNumber,
            CompanyId = betaId,
            OnHireDate = new DateTime(2026, 4, 2)
        });
        var list = await client.GetFromJsonAsync<QueryContractsResponse>("/api/contracts?searchText=P4&sort=contractNumber");
        var delete = await client.DeleteAsync($"/api/contracts/{created.Item.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDates.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, conflictingSiteCompany.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(renamedContractNumber, (await update.Content.ReadFromJsonAsync<EntityResponse<ContractDetailResponse>>())?.Item?.ContractNumber);
        Assert.Contains(list!.Results, contract => contract.ContractNumber == existingContractNumber && contract.SiteName == siteName);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }
    [Fact]
    // Function summary: Handles the site crud validates contract and times then archives workflow for this module.
    public async Task SiteCrud_ValidatesContractAndTimesThenArchives()
    {
        const string siteName = "Contract Site";
        const string updatedSiteName = "Contract Site Updated";
        // A single valid open/close pair; the invalid-times case submits it reversed.
        const string openTime = "08:00";
        const string closeTime = "18:00";

        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Site Owner", Contracts = [] },
            new Contract
            {
                Id = contractId,
                ContractNumber = "P4-SITE-001",
                CompanyId = companyId,
                OnHireDate = new DateTime(2026, 5, 1)
            });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // A site without a contract is rejected even though its hours are valid.
        var missingContract = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            StartTime = openTime,
            EndTime = closeTime
        });

        // The same hours reversed (start after end) are rejected.
        var invalidTimes = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            ContractId = contractId,
            StartTime = closeTime,
            EndTime = openTime
        });

        // A valid site persists the full weekly schedule declared in SiteWeeklyHours.
        var create = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            ContractId = contractId,
            AddressLine1 = "Unit 1",
            City = "Athens",
            OperatingHours = SiteWeeklyHours
                .Select(day => new SiteOperatingHoursMutationRequest
                {
                    DayOfWeek = day.DayOfWeek,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime,
                    IsClosed = day.IsClosed
                })
                .ToList()
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();
        var siteId = created!.Item!.Id;
        var update = await client.PutAsJsonAsync($"/api/sites/{siteId}", new SiteMutationRequest
        {
            SiteName = updatedSiteName,
            CompanyId = companyId,
            AddressLine1 = "Unit 2",
            City = "Athens",
            StartTime = openTime,
            EndTime = closeTime
        });
        var archive = await client.PostAsync($"/api/sites/{siteId}/archive", null);
        var archived = await archive.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();

        Assert.Equal(HttpStatusCode.BadRequest, missingContract.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTimes.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains(created.Item.ContractList, contract => contract.Id == contractId);
        // The submitted schedule round-trips verbatim, including named days and closed days.
        Assert.Equal(
            SiteWeeklyHours,
            created.Item.OperatingHours
                .OrderBy(hours => hours.DayOfWeek)
                .Select(hours => new DaySchedule(hours.DayOfWeek, hours.DayName, hours.StartTime, hours.EndTime, hours.IsClosed)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(updatedSiteName, (await update.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>())?.Item?.SiteName);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.True(archived?.Item?.Archived);
        Assert.NotNull(archived?.Item?.Archive);
    }

    [Fact]
    // Function summary: Verifies a failed archive export leaves the site active rather than reporting a false success.
    public async Task SiteArchive_WhenExportFails_LeavesSiteActiveAndReturns503()
    {
        using var factory = new SpaTestApplicationFactory(archiveExportFails: true);
        var companyId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Archive Owner", Contracts = [] },
            new Contract
            {
                Id = contractId,
                ContractNumber = "P4-ARCH-001",
                CompanyId = companyId,
                OnHireDate = new DateTime(2026, 5, 1)
            });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var create = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = "Archive Failure Site",
            CompanyId = companyId,
            ContractId = contractId,
            AddressLine1 = "Unit 9",
            City = "Athens"
        });
        var siteId = (await create.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>())!.Item!.Id;

        var archive = await client.PostAsync($"/api/sites/{siteId}/archive", null);

        // The export threw, so the site must NOT be archived and the caller must be told the export is unavailable
        // - not handed a 200 for an archive that was never created.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, archive.StatusCode);
        var detail = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        Assert.False(detail?.Item?.Archived);
        Assert.Null(detail?.Item?.Archive);
    }

    [Fact]
    // Function summary: Verifies site admins can upload/delete customer logos and reporting can fetch them through the internal API.
    public async Task SiteCustomerLogo_UploadsStreamsAndDeletesThroughProtectedRoutes()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Logo Customer", Contracts = [] },
            new Site { Id = siteId, SiteName = "Logo Site", CreateDate = DateTime.UtcNow, Contracts = [] });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(PngBytes()), "logo", "customer-logo.png");
        var upload = await client.PostAsync($"/api/sites/{siteId}/customer-logo", form);
        var uploaded = await upload.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();
        var detail = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        var preview = await client.GetAsync($"/api/sites/{siteId}/customer-logo");
        using var internalRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/report-content/sites/{siteId}/customer-logo");
        internalRequest.Headers.TryAddWithoutValidation("X-RVT-Internal-Key", ReportContentKey);
        var internalFetch = await client.SendAsync(internalRequest);
        var delete = await client.DeleteAsync($"/api/sites/{siteId}/customer-logo");
        var afterDelete = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        using var afterDeleteRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/report-content/sites/{siteId}/customer-logo");
        afterDeleteRequest.Headers.TryAddWithoutValidation("X-RVT-Internal-Key", ReportContentKey);
        var missingAfterDelete = await client.SendAsync(afterDeleteRequest);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal($"/api/sites/{siteId}/customer-logo", uploaded!.Item!.CustomerLogoUrl);
        Assert.Equal($"/api/sites/{siteId}/customer-logo", detail!.Item!.CustomerLogoUrl);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, internalFetch.StatusCode);
        Assert.Equal("image/png", internalFetch.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Null(afterDelete!.Item!.CustomerLogoUrl);
        Assert.Equal(HttpStatusCode.NotFound, missingAfterDelete.StatusCode);
    }

    [Fact]
    // Function summary: Handles the site customer logo rejects non image payload workflow for this module.
    public async Task SiteCustomerLogo_RejectsNonImagePayload()
    {
        using var factory = new SpaTestApplicationFactory();
        var siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = Guid.NewGuid(), CompanyName = "Logo Customer", Contracts = [] },
            new Site { Id = siteId, SiteName = "Logo Site", CreateDate = DateTime.UtcNow, Contracts = [] });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // A payload with a .png name/extension but non-image bytes must be rejected by the
        // magic-byte check rather than stored (and later served back) as an image.
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<svg onload=alert(1)>not really an image</svg>")), "logo", "customer-logo.png");
        var upload = await client.PostAsync($"/api/sites/{siteId}/customer-logo", form);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }
    [Fact]
    // Function summary: Handles the company user site access is scoped and can update own notification settings workflow for this module.
    public async Task CompanyUserSiteAccess_IsScopedAndCanUpdateOwnNotificationSettings()
    {
        using var factory = new SpaTestApplicationFactory();
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var assignedSiteId = Guid.NewGuid();
        var otherSiteId = Guid.NewGuid();
        var assignedContractId = Guid.NewGuid();
        var otherContractId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();
        var siteUserId = Guid.NewGuid();
        var companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: companyId);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Scoped Company", Contracts = [] },
            new Company { Id = otherCompanyId, CompanyName = "Other Company", Contracts = [] },
            new Site { Id = assignedSiteId, SiteName = "Assigned Site", CreateDate = DateTime.UtcNow, Contracts = [] },
            new Site { Id = otherSiteId, SiteName = "Other Site", CreateDate = DateTime.UtcNow, Contracts = [] },
            new Contract { Id = assignedContractId, ContractNumber = "P4-SCOPE-001", CompanyId = companyId, SiteiD = assignedSiteId, OnHireDate = DateTime.UtcNow.Date },
            new Contract { Id = otherContractId, ContractNumber = "P4-SCOPE-002", CompanyId = otherCompanyId, SiteiD = otherSiteId, OnHireDate = DateTime.UtcNow.Date },
            TestData.SiteUser(siteId: assignedSiteId, userId: Guid.Parse(companyUser.Id), id: siteUserId, startDate: DateTime.UtcNow, siteContact: true),
            new NotificationSettings { SiteUserId = siteUserId, Email = true, SMS = false },
            TestData.Monitor(MonitorTypeEnum.Dust, id: monitorId, fleetNr: "F-100", serialId: "S-100"),
            new Deployment
            {
                Id = Guid.NewGuid(),
                ContractId = assignedContractId,
                MonitorId = monitorId,
                StartDate = DateTime.UtcNow.AddDays(-1)
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                MonitorId = monitorId,
                NotificationTime = DateTime.UtcNow,
                AlertType = AlertTypeEnum.Alert,
                AlertField = "PM10",
                LimitOn = 10,
                Level = 12
            });
        var client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);
        var list = await client.GetFromJsonAsync<QuerySitesResponse>("/api/sites?includeArchived=true");
        var assigned = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{assignedSiteId}");
        var unassigned = await client.GetAsync($"/api/sites/{otherSiteId}");
        var settings = await client.GetFromJsonAsync<SiteNotificationSettingsResponse>($"/api/sites/{assignedSiteId}/notification-settings");
        var updateSettings = await client.PutAsJsonAsync($"/api/sites/{assignedSiteId}/notification-settings/{siteUserId}", new SiteNotificationSettingMutationRequest
        {
            Email = false,
            Sms = true,
            StartTime = "09:00",
            EndTime = "17:00"
        });
        var updatedSettings = await updateSettings.Content.ReadFromJsonAsync<EntityResponse<SiteNotificationSettingItem>>();
        Assert.True(list!.IsScopedToCurrentUser);
        Assert.Single(list.Results);
        Assert.Equal(assignedSiteId, list.Results.Single().Id);
        Assert.Equal(assignedSiteId, assigned!.Item!.Id);
        Assert.Equal(1, assigned.Item.MonitorCount);
        Assert.Equal(1, assigned.Item.OpenNotificationCount);
        Assert.Single(assigned.Item.OpenNotifications);
        Assert.Equal(HttpStatusCode.NotFound, unassigned.StatusCode);
        Assert.Single(settings!.Settings);
        Assert.Equal(HttpStatusCode.OK, updateSettings.StatusCode);
        Assert.True(updatedSettings?.Item?.Sms);
        Assert.Equal("09:00", updatedSettings?.Item?.StartTime);
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

    // Function summary: Provides a tiny valid PNG used by customer-logo upload tests.
    private static byte[] PngBytes()
    {
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lwGfVwAAAABJRU5ErkJggg==");
    }
}
