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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Contracts;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.Tests.Support;
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
    // Function summary: Verifies contract create converts date-only values to UTC midnight before the timestamptz guard.
    public void CreateContract_StoresCalendarDatesAsUtcMidnight()
    {
        using RVTDbContext context = NpgsqlDomainContext();
        ContractMutationRequest request = new()
        {
            ContractNumber = "T5-CREATE-DATE",
            CompanyId = Guid.NewGuid(),
            OnHireDate = new DateTime(2026, 7, 1, 14, 30, 0, DateTimeKind.Unspecified),
            OffHireDate = new DateTime(2026, 7, 2, 23, 45, 0, DateTimeKind.Local)
        };

        Contract contract = ContractCommandWorkflow.CreateContract(request);
        context.Contracts.Add(contract);

        UtcTimestampGuardInterceptor.Guard(context);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), contract.OnHireDate);
        Assert.Equal(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc), contract.OffHireDate);
    }

    [Fact]
    // Function summary: Verifies contract update converts nullable date-only values to UTC midnight before the timestamptz guard.
    public void UpdateContract_StoresCalendarDatesAsUtcMidnight()
    {
        using RVTDbContext context = NpgsqlDomainContext();
        Contract contract = new()
        {
            Id = Guid.NewGuid(),
            ContractNumber = "T5-OLD-DATE",
            CompanyId = Guid.NewGuid(),
            OnHireDate = DateTime.UnixEpoch
        };
        context.Contracts.Attach(contract);
        ContractCommandWorkflow.ApplyContractMutation(contract, new ContractMutationRequest
        {
            ContractNumber = "T5-UPDATE-DATE",
            CompanyId = contract.CompanyId,
            OnHireDate = new DateTime(2026, 7, 1, 19, 0, 0, DateTimeKind.Local),
            OffHireDate = new DateTime(2026, 7, 3, 11, 0, 0, DateTimeKind.Unspecified)
        });

        UtcTimestampGuardInterceptor.Guard(context);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), contract.OnHireDate);
        Assert.Equal(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc), contract.OffHireDate);
    }

    [RequiresPostgresFact]
    // Function summary: Verifies the create command persists a date-only contract through the real PostgreSQL UTC guard.
    public async Task CreateContractCommand_PersistsCalendarDateAgainstRealPostgres()
    {
        string? connectionString = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable);
        DbContextOptions<RVTDbContext> options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(UtcTimestampGuardInterceptor.Instance)
            .Options;
        await using RVTDbContext context = new(options);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Company company = new() { Id = Guid.NewGuid(), CompanyName = "T5 Contract Date Company" };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        CreateContractCommandHandler handler = new(context);

        ContractCommandResult result = await handler.Handle(new CreateContractCommand(new ContractMutationRequest
        {
            ContractNumber = $"T5-{Guid.NewGuid():N}"[..20],
            CompanyId = company.Id,
            OnHireDate = new DateTime(2026, 7, 1)
        }), CancellationToken.None);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Contract persisted = await context.Contracts.SingleAsync(contract => contract.Id == result.ContractId);
        Assert.True(result.ShouldCommit);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), persisted.OnHireDate);
        await transaction.RollbackAsync();
    }

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

        using SpaTestApplicationFactory factory = new();
        Guid alphaId = Guid.NewGuid();
        Guid betaId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        Guid existingContractId = Guid.NewGuid();
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
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // Reusing the seeded contract's number is rejected as a duplicate.
        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = existingContractNumber,
            CompanyId = alphaId,
            OnHireDate = new DateTime(2026, 1, 2)
        });

        // Off-hire dated before on-hire is rejected.
        HttpResponseMessage invalidDates = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = "P4-002",
            CompanyId = alphaId,
            OnHireDate = new DateTime(2026, 2, 2),
            OffHireDate = new DateTime(2026, 2, 1)
        });

        // A site already belongs to Alpha, so binding it to a Beta contract is rejected.
        HttpResponseMessage conflictingSiteCompany = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = "P4-003",
            CompanyId = betaId,
            SiteId = siteId,
            OnHireDate = new DateTime(2026, 3, 1)
        });

        // A valid contract is created, renamed, listed by its shared "P4" prefix, then deleted.
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/contracts", new ContractMutationRequest
        {
            ContractNumber = createdContractNumber,
            CompanyId = betaId,
            OnHireDate = new DateTime(2026, 4, 1)
        });
        EntityResponse<ContractDetailResponse>? created = await create.Content.ReadFromJsonAsync<EntityResponse<ContractDetailResponse>>();
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/contracts/{created!.Item!.Id}", new ContractMutationRequest
        {
            ContractNumber = renamedContractNumber,
            CompanyId = betaId,
            OnHireDate = new DateTime(2026, 4, 2)
        });
        QueryContractsResponse? list = await client.GetFromJsonAsync<QueryContractsResponse>("/api/contracts?searchText=P4&sort=contractNumber");
        HttpResponseMessage delete = await client.DeleteAsync($"/api/contracts/{created.Item.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDates.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, conflictingSiteCompany.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(renamedContractNumber, (await update.Content.ReadFromJsonAsync<EntityResponse<ContractDetailResponse>>())?.Item?.ContractNumber);
        Assert.Contains(list!.Results, contract => contract.ContractNumber == existingContractNumber && contract.SiteName == siteName);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    // Function summary: Builds the PostgreSQL model without opening a connection so timestamp guards see actual provider types.
    private static RVTDbContext NpgsqlDomainContext()
    {
        DbContextOptions<RVTDbContext> options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;
        return new RVTDbContext(options);
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

        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
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
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // A site without a contract is rejected even though its hours are valid.
        HttpResponseMessage missingContract = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            StartTime = openTime,
            EndTime = closeTime
        });

        // The same hours reversed (start after end) are rejected.
        HttpResponseMessage invalidTimes = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            ContractId = contractId,
            StartTime = closeTime,
            EndTime = openTime
        });

        // A valid site persists the full weekly schedule declared in SiteWeeklyHours.
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = siteName,
            CompanyId = companyId,
            ContractId = contractId,
            AddressLine1 = "Unit 1",
            City = "Athens",
            OperatingHours = [.. SiteWeeklyHours
                .Select(day => new SiteOperatingHoursMutationRequest
                {
                    DayOfWeek = day.DayOfWeek,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime,
                    IsClosed = day.IsClosed
                })]
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        EntityResponse<SiteDetailResponse>? created = await create.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();
        Guid siteId = created!.Item!.Id;
        Assert.Equal($"/api/sites/{siteId}", create.Headers.Location?.AbsolutePath);
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sites/{siteId}", new SiteMutationRequest
        {
            SiteName = updatedSiteName,
            CompanyId = companyId,
            AddressLine1 = "Unit 2",
            City = "Athens",
            StartTime = openTime,
            EndTime = closeTime
        });
        HttpResponseMessage archive = await client.PostAsync($"/api/sites/{siteId}/archive", null);
        EntityResponse<SiteDetailResponse>? archived = await archive.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();

        Assert.Equal(HttpStatusCode.BadRequest, missingContract.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTimes.StatusCode);
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
    public async Task SiteUpdate_MalformedMissingSite_ReturnsMaskedNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        Guid missingSiteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{missingSiteId}",
            new SiteMutationRequest
            {
                SiteName = "",
                CompanyId = Guid.NewGuid(),
                EndTime = "not-a-time"
            });
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            $"Site '{missingSiteId}' was not found.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SiteUpdate_MalformedExistingSite_ReturnsExactValidationProblem()
    {
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Site
            {
                Id = siteId,
                SiteName = "Existing Malformed Update Site",
                CreateDate = DateTime.UtcNow,
                Contracts = []
            });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{siteId}",
            new SiteMutationRequest
            {
                SiteName = "Existing Malformed Update Site",
                CompanyId = Guid.NewGuid(),
                StartTime = "08:00",
                EndTime = "not-a-time"
            });
        Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, errors.Count);
        Assert.Equal(
            ["Time values must use HH:mm format."],
            errors[nameof(SiteMutationRequest.EndTime)]);
        Assert.Equal(
            ["You need to set both start and end time"],
            errors[nameof(SiteMutationRequest.StartTime)]);
    }

    [Fact]
    public async Task NotificationSetting_InvalidTimeMissingSite_ReturnsMaskedNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{Guid.NewGuid()}/notification-settings/{Guid.NewGuid()}",
            new SiteNotificationSettingMutationRequest
            {
                Email = true,
                Sms = false,
                StartTime = "08:00",
                EndTime = "not-a-time"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationSetting_InvalidTimeMissingTarget_ReturnsMaskedNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Site
            {
                Id = siteId,
                SiteName = "Missing Notification Target",
                CreateDate = DateTime.UtcNow,
                Contracts = []
            });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{siteId}/notification-settings/{Guid.NewGuid()}",
            new SiteNotificationSettingMutationRequest
            {
                Email = true,
                Sms = false,
                StartTime = "08:00",
                EndTime = "not-a-time"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationSetting_InvalidTimeExpiredAssignment_ReturnsMaskedNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        Guid siteUserId = Guid.NewGuid();
        ApplicationUser companyUser = await factory.SeedUserAsync(
            CompanyUserEmail,
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        await factory.SeedDomainEntitiesAsync(
            new Company
            {
                Id = companyId,
                CompanyName = "Expired Notification Company",
                Contracts = []
            },
            new Site
            {
                Id = siteId,
                SiteName = "Expired Notification Site",
                CreateDate = DateTime.UtcNow.AddDays(-30),
                Contracts = []
            },
            new SiteUsers
            {
                Id = siteUserId,
                SiteId = siteId,
                UserId = Guid.Parse(companyUser.Id),
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(-1)
            });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{siteId}/notification-settings/{siteUserId}",
            new SiteNotificationSettingMutationRequest
            {
                Email = true,
                Sms = false,
                StartTime = "08:00",
                EndTime = "not-a-time"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationSetting_InvalidTimeForeignTarget_ReturnsForbidden()
    {
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        Guid ownSiteUserId = Guid.NewGuid();
        Guid foreignSiteUserId = Guid.NewGuid();
        ApplicationUser companyUser = await factory.SeedUserAsync(
            CompanyUserEmail,
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        ApplicationUser foreignUser = await factory.SeedUserAsync(
            "contracts.foreign@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: companyId);
        await factory.SeedDomainEntitiesAsync(
            new Company
            {
                Id = companyId,
                CompanyName = "Foreign Notification Company",
                Contracts = []
            },
            new Site
            {
                Id = siteId,
                SiteName = "Foreign Notification Site",
                CreateDate = DateTime.UtcNow.AddDays(-30),
                Contracts = []
            },
            TestData.SiteUser(
                siteId: siteId,
                userId: Guid.Parse(companyUser.Id),
                id: ownSiteUserId,
                startDate: DateTime.UtcNow.AddDays(-1)),
            TestData.SiteUser(
                siteId: siteId,
                userId: Guid.Parse(foreignUser.Id),
                id: foreignSiteUserId,
                startDate: DateTime.UtcNow.AddDays(-1)));
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{siteId}/notification-settings/{foreignSiteUserId}",
            new SiteNotificationSettingMutationRequest
            {
                Email = true,
                Sms = false,
                StartTime = "08:00",
                EndTime = "not-a-time"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(nameof(SiteMutationRequest.EndTime), nameof(SiteMutationRequest.StartTime))]
    [InlineData(nameof(SiteMutationRequest.SatEndTime), nameof(SiteMutationRequest.SatStartTime))]
    [InlineData(nameof(SiteMutationRequest.SunEndTime), nameof(SiteMutationRequest.SunStartTime))]
    public async Task SiteValidation_MalformedLegacyEndTimeSerializesExactFieldKeys(
        string endField,
        string startField)
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        SiteMutationRequest request = new()
        {
            SiteName = "Malformed End Time Site",
            CompanyId = Guid.NewGuid(),
            ContractId = Guid.NewGuid()
        };
        switch (endField)
        {
            case nameof(SiteMutationRequest.EndTime):
                request.StartTime = "08:00";
                request.EndTime = "not-a-time";
                break;
            case nameof(SiteMutationRequest.SatEndTime):
                request.SatStartTime = "08:00";
                request.SatEndTime = "not-a-time";
                break;
            case nameof(SiteMutationRequest.SunEndTime):
                request.SunStartTime = "08:00";
                request.SunEndTime = "not-a-time";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(endField));
        }

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sites", request);
        Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, errors.Count);
        Assert.Equal(
            ["Time values must use HH:mm format."],
            errors[endField]);
        Assert.Equal(
            ["You need to set both start and end time"],
            errors[startField]);
    }

    [Fact]
    public async Task SiteValidation_ReversedLegacyWeekdayPairSerializesOneStartTimeError()
    {
        using SpaTestApplicationFactory factory = new();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/sites",
            new SiteMutationRequest
            {
                SiteName = "Reversed Weekday Site",
                CompanyId = Guid.NewGuid(),
                ContractId = Guid.NewGuid(),
                StartTime = "17:00",
                EndTime = "08:00"
            });
        Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        KeyValuePair<string, string[]> error = Assert.Single(errors);
        Assert.Equal(nameof(SiteMutationRequest.StartTime), error.Key);
        Assert.Equal(
            ["Start time needs to be before end time"],
            error.Value);
    }

    [Fact]
    public async Task NotificationSetting_MalformedEndTimeSerializesExactFieldKeys()
    {
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        Guid siteUserId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Site
            {
                Id = siteId,
                SiteName = "Notification Validation Site",
                CreateDate = DateTime.UtcNow,
                Contracts = []
            },
            new SiteUsers
            {
                Id = siteUserId,
                SiteId = siteId,
                UserId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.AddDays(-1)
            });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/sites/{siteId}/notification-settings/{siteUserId}",
            new SiteNotificationSettingMutationRequest
            {
                Email = true,
                Sms = false,
                StartTime = "08:00",
                EndTime = "not-a-time"
            });
        Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, errors.Count);
        Assert.Equal(
            ["Time values must use HH:mm format."],
            errors[nameof(SiteNotificationSettingMutationRequest.EndTime)]);
        Assert.Equal(
            ["You need to set both start and end time"],
            errors[nameof(SiteNotificationSettingMutationRequest.StartTime)]);
    }

    [Fact]
    // Function summary: Verifies a failed archive export leaves the site active rather than reporting a false success.
    public async Task SiteArchive_WhenExportFails_LeavesSiteActiveAndReturns503()
    {
        using SpaTestApplicationFactory factory = new(archiveExportFails: true);
        Guid companyId = Guid.NewGuid();
        Guid contractId = Guid.NewGuid();
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
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/sites", new SiteMutationRequest
        {
            SiteName = "Archive Failure Site",
            CompanyId = companyId,
            ContractId = contractId,
            AddressLine1 = "Unit 9",
            City = "Athens"
        });
        Guid siteId = (await create.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>())!.Item!.Id;

        HttpResponseMessage archive = await client.PostAsync($"/api/sites/{siteId}/archive", null);

        // The export threw, so the site must NOT be archived and the caller must be told the export is unavailable
        // - not handed a 200 for an archive that was never created.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, archive.StatusCode);
        EntityResponse<SiteDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        Assert.False(detail?.Item?.Archived);
        Assert.Null(detail?.Item?.Archive);
    }

    [Fact]
    // Function summary: Verifies site admins can upload/delete customer logos and reporting can fetch them through the internal API.
    public async Task SiteCustomerLogo_UploadsStreamsAndDeletesThroughProtectedRoutes()
    {
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Logo Customer", Contracts = [] },
            new Site { Id = siteId, SiteName = "Logo Site", CreateDate = DateTime.UtcNow, Contracts = [] });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        using MultipartFormDataContent form = new();
        form.Add(new ByteArrayContent(PngBytes()), "logo", "customer-logo.png");
        HttpResponseMessage upload = await client.PostAsync($"/api/sites/{siteId}/customer-logo", form);
        EntityResponse<SiteDetailResponse>? uploaded = await upload.Content.ReadFromJsonAsync<EntityResponse<SiteDetailResponse>>();
        EntityResponse<SiteDetailResponse>? detail = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        HttpResponseMessage preview = await client.GetAsync($"/api/sites/{siteId}/customer-logo");
        using HttpRequestMessage internalRequest = new(HttpMethod.Get, $"/api/report-content/sites/{siteId}/customer-logo");
        internalRequest.Headers.TryAddWithoutValidation("X-RVT-Internal-Key", ReportContentKey);
        HttpResponseMessage internalFetch = await client.SendAsync(internalRequest);
        HttpResponseMessage delete = await client.DeleteAsync($"/api/sites/{siteId}/customer-logo");
        EntityResponse<SiteDetailResponse>? afterDelete = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{siteId}");
        using HttpRequestMessage afterDeleteRequest = new(HttpMethod.Get, $"/api/report-content/sites/{siteId}/customer-logo");
        afterDeleteRequest.Headers.TryAddWithoutValidation("X-RVT-Internal-Key", ReportContentKey);
        HttpResponseMessage missingAfterDelete = await client.SendAsync(afterDeleteRequest);

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
        using SpaTestApplicationFactory factory = new();
        Guid siteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = Guid.NewGuid(), CompanyName = "Logo Customer", Contracts = [] },
            new Site { Id = siteId, SiteName = "Logo Site", CreateDate = DateTime.UtcNow, Contracts = [] });
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        // A payload with a .png name/extension but non-image bytes must be rejected by the
        // magic-byte check rather than stored (and later served back) as an image.
        using MultipartFormDataContent form = new();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<svg onload=alert(1)>not really an image</svg>")), "logo", "customer-logo.png");
        HttpResponseMessage upload = await client.PostAsync($"/api/sites/{siteId}/customer-logo", form);
        using JsonDocument problem = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal("Invalid customer logo", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Customer logos must be valid PNG, JPEG, or WebP images.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.False(problem.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    // Function summary: Preserves masked site-not-found ordering before missing-logo validation.
    public async Task SiteCustomerLogo_MissingSiteWithoutFile_ReturnsMaskedNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        Guid missingSiteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        using MultipartFormDataContent form = new();
        form.Add(new StringContent("ignored"), "unrelated");
        HttpResponseMessage upload = await client.PostAsync($"/api/sites/{missingSiteId}/customer-logo", form);
        using JsonDocument problem = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal("Site not found", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            $"Site '{missingSiteId}' was not found.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    // Function summary: Preserves the legacy site-not-found payload when deleting a logo for a missing site.
    public async Task SiteCustomerLogo_DeleteMissingSite_ReturnsLegacyNotFound()
    {
        using SpaTestApplicationFactory factory = new();
        Guid missingSiteId = Guid.NewGuid();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sites/{missingSiteId}/customer-logo");
        using JsonDocument problem = JsonDocument.Parse(await delete.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal("Site not found", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            $"Site '{missingSiteId}' was not found.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    // Function summary: Handles the company user site access is scoped and can update own notification settings workflow for this module.
    public async Task CompanyUserSiteAccess_IsScopedAndCanUpdateOwnNotificationSettings()
    {
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid otherCompanyId = Guid.NewGuid();
        Guid assignedSiteId = Guid.NewGuid();
        Guid otherSiteId = Guid.NewGuid();
        Guid assignedContractId = Guid.NewGuid();
        Guid otherContractId = Guid.NewGuid();
        Guid monitorId = Guid.NewGuid();
        Guid siteUserId = Guid.NewGuid();
        ApplicationUser companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: companyId);
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
        HttpClient client = CreateClient(factory);
        await LoginAsync(client, CompanyUserEmail, Password);
        QuerySitesResponse? list = await client.GetFromJsonAsync<QuerySitesResponse>("/api/sites?includeArchived=true");
        EntityResponse<SiteDetailResponse>? assigned = await client.GetFromJsonAsync<EntityResponse<SiteDetailResponse>>($"/api/sites/{assignedSiteId}");
        HttpResponseMessage unassigned = await client.GetAsync($"/api/sites/{otherSiteId}");
        SiteNotificationSettingsResponse? settings = await client.GetFromJsonAsync<SiteNotificationSettingsResponse>($"/api/sites/{assignedSiteId}/notification-settings");
        HttpResponseMessage updateSettings = await client.PutAsJsonAsync($"/api/sites/{assignedSiteId}/notification-settings/{siteUserId}", new SiteNotificationSettingMutationRequest
        {
            Email = false,
            Sms = true,
            StartTime = "09:00",
            EndTime = "17:00"
        });
        EntityResponse<SiteNotificationSettingItem>? updatedSettings = await updateSettings.Content.ReadFromJsonAsync<EntityResponse<SiteNotificationSettingItem>>();
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

    [Fact]
    // Function summary: Verifies only currently active site assignments grant company-user list and detail access.
    public async Task CompanyUserSiteAccess_RequiresActiveAssignmentWindow()
    {
        DateTimeOffset nowUtc = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        using SpaTestApplicationFactory factory = new();
        Guid companyId = Guid.NewGuid();
        Guid expiredSiteId = Guid.NewGuid();
        Guid futureSiteId = Guid.NewGuid();
        Guid activeSiteId = Guid.NewGuid();
        ApplicationUser companyUser = await factory.SeedUserAsync(CompanyUserEmail, Password, RoleNames.CompanyUser, companyId: companyId);
        Guid userId = Guid.Parse(companyUser.Id);
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Windowed Company", Contracts = [] },
            new Site { Id = expiredSiteId, SiteName = "Expired Assignment Site", CreateDate = nowUtc.UtcDateTime.AddDays(-30), Contracts = [] },
            new Site { Id = futureSiteId, SiteName = "Future Assignment Site", CreateDate = nowUtc.UtcDateTime.AddDays(-30), Contracts = [] },
            new Site { Id = activeSiteId, SiteName = "Active Assignment Site", CreateDate = nowUtc.UtcDateTime.AddDays(-30), Contracts = [] },
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = expiredSiteId,
                UserId = userId,
                StartDate = nowUtc.UtcDateTime.AddDays(-10),
                EndDate = nowUtc.UtcDateTime.AddTicks(-1)
            },
            TestData.SiteUser(siteId: futureSiteId, userId: userId, startDate: nowUtc.UtcDateTime.AddTicks(1)),
            new SiteUsers
            {
                Id = Guid.NewGuid(),
                SiteId = activeSiteId,
                UserId = userId,
                StartDate = nowUtc.UtcDateTime,
                EndDate = nowUtc.UtcDateTime
            });

        using WebApplicationFactory<Program> fixedTimeFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(nowUtc));
            });
        });
        HttpClient client = CreateClient(fixedTimeFactory);
        await LoginAsync(client, CompanyUserEmail, Password);

        HttpResponseMessage expiredDetail = await client.GetAsync($"/api/sites/{expiredSiteId}");
        HttpResponseMessage activeDetail = await client.GetAsync($"/api/sites/{activeSiteId}");
        QuerySitesResponse? list = await client.GetFromJsonAsync<QuerySitesResponse>("/api/sites?includeArchived=true");

        Assert.Equal(HttpStatusCode.NotFound, expiredDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activeDetail.StatusCode);
        Assert.Equal(activeSiteId, Assert.Single(list!.Results).Id);
        Assert.DoesNotContain(list.Results, site => site.Id == futureSiteId);
    }

    private static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(
        HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return problem.RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value
                    .EnumerateArray()
                    .Select(message => message.GetString()!)
                    .ToArray());
    }

    // Function summary: Creates client data for the current workflow.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
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

    // Function summary: Supplies a deterministic UTC clock for assignment-window authorization tests.
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
