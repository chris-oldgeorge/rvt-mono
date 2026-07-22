// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-07-08 pending Updated report-generation override to target the reporting adapter port.
// - 2026-07-09 pending Added report-recipient query and performance-index guardrails.
// - 2026-06-26 pending Covered archived report-rule site warnings and mutation validation.
// - 2026-06-26 pending Added regression coverage for editing report rules tied to archived sites.
// - 2026-06-24 pending Added report-service client handoff coverage for manual generation requests.
// - 2026-06-24 pending Added backend reporting upgrade coverage for paged query implementations, generation requests, and recipient search endpoints.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-07-05 pending Updated report-rule paging inspection to follow query logic into RVT.BusinessLogic.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Adapters.Reporting;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class ReportWorkflowTests
{
    private const string AdminEmail = "reports.admin@rvt.test";
    private const string Password = "P8sSw0rd9$";
    private const string ReportSiteName = "Report Site";
    private const string ArchivedReportSiteName = "Archived Report Site";

    [Fact]
    // Function summary: Handles the report rules create update delete validate and persist workflow for this module.
    public async Task ReportRules_CreateUpdateDelete_ValidateAndPersist()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedReportSiteAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var invalid = await client.PostAsJsonAsync("/api/report-rules", new ReportRuleMutationRequest
        {
            SiteId = ids.SiteId,
            Frequency = ReportFrequencyType.Weekly,
            ReportName = "Invalid weekly report"
        });
        // The create/update requests are the fixtures; read-backs assert against their own fields.
        var createRequest = new ReportRuleMutationRequest
        {
            SiteId = ids.SiteId,
            Frequency = ReportFrequencyType.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            ReportName = "Weekly compliance"
        };
        var create = await client.PostAsJsonAsync("/api/report-rules", createRequest);
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<ReportRuleDetailResponse>>();
        var list = await client.GetFromJsonAsync<QueryReportRulesResponse>("/api/report-rules?searchText=weekly&sort=siteName");
        var updateRequest = new ReportRuleMutationRequest
        {
            SiteId = ids.SiteId,
            Frequency = ReportFrequencyType.WeeklyAndMonthly,
            DayOfWeek = DayOfWeek.Friday,
            DayOfMonth = 28,
            ReportName = "Board pack"
        };
        var update = await client.PutAsJsonAsync($"/api/report-rules/{created!.Item!.Id}", updateRequest);
        var updated = await update.Content.ReadFromJsonAsync<EntityResponse<ReportRuleDetailResponse>>();
        var delete = await client.DeleteAsync($"/api/report-rules/{created.Item.Id}");
        var afterDelete = await client.GetFromJsonAsync<QueryReportRulesResponse>("/api/report-rules");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(createRequest.ReportName, created.Item.ReportName);
        Assert.Equal(ReportSiteName, created.Item.SiteName);
        Assert.Contains(list!.Results, rule => rule.Id == created.Item.Id);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(updateRequest.Frequency, updated?.Item?.Frequency);
        Assert.Equal(updateRequest.DayOfWeek, updated?.Item?.DayOfWeek);
        Assert.Equal(updateRequest.DayOfMonth, updated?.Item?.DayOfMonth);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.DoesNotContain(afterDelete!.Results, rule => rule.Id == created.Item.Id);
    }

    [Fact]
    // Function summary: Verifies daily report rules are exposed and accepted without weekly or monthly schedule fields.
    public async Task ReportRules_DailyFrequency_IsAvailableAndDoesNotRequireScheduleFields()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedReportSiteAsync(factory);
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var options = await client.GetFromJsonAsync<ReportRuleOptionsResponse>("/api/report-rules/options");
        var create = await client.PostAsJsonAsync("/api/report-rules", new ReportRuleMutationRequest
        {
            SiteId = ids.SiteId,
            Frequency = ReportFrequencyType.Daily,
            ReportName = "Daily compliance"
        });
        var created = await create.Content.ReadFromJsonAsync<EntityResponse<ReportRuleDetailResponse>>();

        Assert.Contains(options!.Frequencies, frequency => frequency.Value == "1" && frequency.Label == "Daily");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(ReportFrequencyType.Daily, created!.Item!.Frequency);
        Assert.Null(created.Item.DayOfWeek);
        Assert.Null(created.Item.DayOfMonth);
    }

    [Fact]
    // Function summary: Verifies edit details keep the current site option even after that site is archived.
    public async Task ReportRules_EditDetailIncludesArchivedCurrentSiteOption()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedArchivedReportSiteAsync(factory);
        var admin = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var reportRuleId = Guid.NewGuid();
        await factory.SeedSearchEntitiesAsync(new ReportRule
        {
            Id = reportRuleId,
            SiteId = ids.SiteId,
            UserId = Guid.Parse(admin.Id),
            Frequency = ReportFrequencyType.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            ReportName = "Archived site weekly rule"
        });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var response = await client.GetAsync($"/api/report-rules/{reportRuleId}");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var item = document.RootElement.GetProperty("item");
        var currentSite = item
            .GetProperty("sites")
            .EnumerateArray()
            .Single(site => site.GetProperty("value").GetString() == ids.SiteId.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ids.SiteId.ToString(), item.GetProperty("siteId").GetString());
        Assert.Equal(ArchivedReportSiteName, item.GetProperty("siteName").GetString());
        Assert.Equal(ArchivedReportSiteName, currentSite.GetProperty("label").GetString());
        Assert.True(currentSite.GetProperty("disabled").GetBoolean());
    }

    [Fact]
    // Function summary: Verifies report-rule mutations reject archived site targets with a clear validation message.
    public async Task ReportRules_CreateUpdateRejectArchivedSiteTargets()
    {
        using var factory = new SpaTestApplicationFactory();
        var archivedIds = await SeedArchivedReportSiteAsync(factory);
        var activeIds = await SeedReportSiteAsync(factory);
        var admin = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var reportRuleId = Guid.NewGuid();
        await factory.SeedSearchEntitiesAsync(new ReportRule
        {
            Id = reportRuleId,
            SiteId = activeIds.SiteId,
            UserId = Guid.Parse(admin.Id),
            Frequency = ReportFrequencyType.Daily,
            ReportName = "Active daily rule"
        });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var create = await client.PostAsJsonAsync("/api/report-rules", new ReportRuleMutationRequest
        {
            SiteId = archivedIds.SiteId,
            Frequency = ReportFrequencyType.Daily,
            ReportName = "Archived daily rule"
        });
        var update = await client.PutAsJsonAsync($"/api/report-rules/{reportRuleId}", new ReportRuleMutationRequest
        {
            SiteId = archivedIds.SiteId,
            Frequency = ReportFrequencyType.Daily,
            ReportName = "Archived update"
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        AssertValidationErrorContains(await ReadValidationProblemAsync(create), nameof(ReportRuleMutationRequest.SiteId), "archived");
        AssertValidationErrorContains(await ReadValidationProblemAsync(update), nameof(ReportRuleMutationRequest.SiteId), "archived");
    }


    [Fact]
    // Function summary: Handles the report rule users add and remove site assignments workflow for this module.
    public async Task ReportRuleUsers_AddAndRemoveSiteAssignments()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedReportSiteAsync(factory);
        var admin = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var companyUser = await factory.SeedUserAsync(
            "reports.company@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: ids.CompanyId,
            name: "Report Recipient");
        var reportRuleId = Guid.NewGuid();
        await factory.SeedDomainEntitiesAsync(TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(companyUser.Id), startDate: DateTime.UtcNow.AddDays(-1)));
        await factory.SeedSearchEntitiesAsync(new ReportRule
        {
            Id = reportRuleId,
            SiteId = ids.SiteId,
            UserId = Guid.Parse(admin.Id),
            Frequency = ReportFrequencyType.Monthly,
            DayOfMonth = 1,
            ReportName = "Monthly recipient list"
        });

        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        var initial = await client.GetFromJsonAsync<QueryReportUsersResponse>($"/api/report-rules/{reportRuleId}/available-users");
        var add = await client.PostAsJsonAsync($"/api/report-rules/{reportRuleId}/users", new ReportUserMutationRequest
        {
            UserId = Guid.Parse(companyUser.Id)
        });
        var added = await add.Content.ReadFromJsonAsync<EntityResponse<ReportUserAssignmentResponse>>();
        var remove = await client.DeleteAsync($"/api/report-rules/{reportRuleId}/users/{companyUser.Id}");
        var removed = await remove.Content.ReadFromJsonAsync<EntityResponse<ReportUserAssignmentResponse>>();

        Assert.Contains(initial!.Results, user => user.Id == companyUser.Id);
        Assert.Contains(initial.Results, user => user.Id == admin.Id);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        Assert.Contains(added!.Item!.AssignedUsers, user => user.Id == companyUser.Id);
        Assert.DoesNotContain(added.Item.AvailableUsers, user => user.Id == companyUser.Id);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.DoesNotContain(removed!.Item!.AssignedUsers, user => user.Id == companyUser.Id);
    }

    [Fact]
    // Function summary: Verifies reporting list endpoints keep filtering and paging in EF queries.
    public void ReportQueries_PageBeforeMaterializingRows()
    {
        var reportApplicationService = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../RvtPortal.Spa/Application/Reports/ReportApplicationService.cs"));
        var reportRuleService = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../RvtPortal.Spa/Application/ReportRules/ReportRuleApplicationService.cs"));
        var reportRuleRecipientReader = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../RvtPortal.Spa/Application/ReportRules/ReportRuleRecipientReader.cs"));

        AssertQueryCountsBeforeMaterializing(reportApplicationService, "public async Task<ReportQueryResult> QueryAsync");
        AssertQueryCountsBeforeMaterializing(reportRuleService, "public async Task<ApplicationResult<PagedResult<ReportRuleListModel>>> QueryAsync");
        AssertQueryCountsBeforeMaterializing(reportRuleRecipientReader, "public async Task<QueryReportUsersResponse?> QueryAssignmentUsersAsync");
        AssertMethodDoesNotContain(
            reportRuleRecipientReader,
            "public async Task<QueryReportUsersResponse?> QueryAssignmentUsersAsync",
            "BuildAssignmentResponseAsync(reportRuleId");
    }

    [Fact]
    // Function summary: Verifies report recipient performance indexes are present in both database providers and the registry.
    public void ReportRecipientPerformanceIndexes_AreDocumentedForPostgresAndSqlServer()
    {
        var postgresScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../database/postgres/performance_indexes_20260609.sql"));
        var sqlServerScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../database/sqlserver/performance_indexes_20260609.sql"));
        var registry = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../docs/database/database-constraint-index-name-registry.csv"));
        var expectedIndexNames = new[]
        {
            "ix_report_user_report_rule_id_user_id",
            "ix_report_rule_site_id_deleted",
            "ix_report_deleted_report_date",
            "ix_site_user_site_id_end_date_user_id"
        };

        foreach (var expectedIndexName in expectedIndexNames)
        {
            Assert.Contains(expectedIndexName, postgresScript, StringComparison.Ordinal);
            Assert.Contains(expectedIndexName, sqlServerScript, StringComparison.Ordinal);
            Assert.Contains(expectedIndexName, registry, StringComparison.Ordinal);
        }
    }

    [Fact]
    // Function summary: Verifies manual report generation requests are accepted after the reporting service client handles them.
    public async Task ReportGenerationRequests_ReturnReportingServiceAcceptedResponse()
    {
        // The fake reporting client returns this status/message, which the endpoint should surface verbatim.
        const string queuedStatus = "Queued";
        const string queuedMessage = "Manual generation queued.";

        using var factory = new SpaTestApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReportGenerationClient>();
                services.AddSingleton<IReportGenerationClient>(new FakeReportGenerationClient(queuedStatus, queuedMessage));
            });
        });
        var ids = await SeedReportSiteAsync(factory);
        var admin = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var reportRuleId = Guid.NewGuid();
        await factory.SeedSearchEntitiesAsync(new ReportRule
        {
            Id = reportRuleId,
            SiteId = ids.SiteId,
            UserId = Guid.Parse(admin.Id),
            Frequency = ReportFrequencyType.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            ReportName = "Weekly generation"
        });

        var client = CreateClient(app);
        await LoginAsync(client, AdminEmail, Password);
        var response = await client.PostAsJsonAsync($"/api/report-rules/{reportRuleId}/generation-requests", new ReportGenerationRequest
        {
            ReportDate = new DateTime(2026, 6, 24),
            SendToRecipients = true
        });
        var body = await response.Content.ReadFromJsonAsync<ReportGenerationRequestResponse>();
        var missing = await client.PostAsJsonAsync($"/api/report-rules/{Guid.NewGuid()}/generation-requests", new ReportGenerationRequest
        {
            ReportDate = new DateTime(2026, 6, 24),
            SendToRecipients = true
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(reportRuleId, body.ReportRuleId);
        Assert.Equal(queuedStatus, body.Status);
        Assert.Equal(queuedMessage, body.Message);
        Assert.True(body.RequestedAtUtc > DateTime.MinValue);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    // Function summary: Verifies report recipient queries page assigned and available users separately.
    public async Task ReportRuleUsers_QueryAssignedAndAvailableRecipients()
    {
        using var factory = new SpaTestApplicationFactory();
        var ids = await SeedReportSiteAsync(factory);
        var admin = await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var assignedUser = await factory.SeedUserAsync(
            "assigned.recipient@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: ids.CompanyId,
            name: "Assigned Recipient");
        var availableUser = await factory.SeedUserAsync(
            "available.recipient@rvt.test",
            Password,
            RoleNames.CompanyUser,
            companyId: ids.CompanyId,
            name: "Available Recipient");
        var reportRuleId = Guid.NewGuid();
        await factory.SeedDomainEntitiesAsync(
            TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(assignedUser.Id), startDate: DateTime.UtcNow.AddDays(-1)),
            TestData.SiteUser(siteId: ids.SiteId, userId: Guid.Parse(availableUser.Id), startDate: DateTime.UtcNow.AddDays(-1)));
        await factory.SeedSearchEntitiesAsync(
            new ReportRule
            {
                Id = reportRuleId,
                SiteId = ids.SiteId,
                UserId = Guid.Parse(admin.Id),
                Frequency = ReportFrequencyType.Monthly,
                DayOfMonth = 1,
                ReportName = "Recipient paging"
            },
            new ReportUser
            {
                ReportRuleId = reportRuleId,
                UserId = Guid.Parse(assignedUser.Id)
            });

        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);
        var assigned = await client.GetFromJsonAsync<QueryReportUsersResponse>(
            $"/api/report-rules/{reportRuleId}/assigned-users?searchText=assigned&page=1&pageSize=1&sort=email&sortDir=Descending");
        var available = await client.GetFromJsonAsync<QueryReportUsersResponse>(
            $"/api/report-rules/{reportRuleId}/available-users?searchText=available&page=1&pageSize=1&sort=name");

        Assert.Equal(reportRuleId, assigned!.ReportRuleId);
        Assert.Equal(ids.SiteId, assigned.SiteId);
        Assert.Equal(1, assigned.Total);
        Assert.Single(assigned.Results);
        Assert.Equal(assignedUser.Id, assigned.Results[0].Id);
        Assert.Equal(SortDirections.Descending, assigned.SortDir);
        Assert.Equal(reportRuleId, available!.ReportRuleId);
        Assert.Equal(1, available.Total);
        Assert.Single(available.Results);
        Assert.Equal(availableUser.Id, available.Results[0].Id);
    }

    [Fact]
    // Function summary: Handles the reports list contract rejects unsupported sort workflow for this module.
    public async Task Reports_ListContractRejectsUnsupportedSort()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var empty = await client.GetFromJsonAsync<QueryReportsResponse>("/api/reports?sort=reportDate&sortDir=Descending");
        var invalid = await client.GetAsync("/api/reports?sort=unknown");

        Assert.Empty(empty!.Results);
        Assert.Equal("reportDate", empty.Sort);
        Assert.Equal(SortDirections.Descending, empty.SortDir);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // Function summary: Initializes report site state required by the application.
    private static async Task<ReportWorkflowIds> SeedReportSiteAsync(SpaTestApplicationFactory factory)
    {
        var companyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Report Company", Contracts = [] },
            new Site { Id = siteId, SiteName = ReportSiteName, CreateDate = now.AddDays(-30), Contracts = [] },
            new Contract
            {
                Id = contractId,
                ContractNumber = "P7-CON-001",
                CompanyId = companyId,
                SiteiD = siteId,
                OnHireDate = now.Date
            });

        return new ReportWorkflowIds(companyId, siteId);
    }

    // Function summary: Initializes archived report-site state used by edit-detail regression tests.
    private static async Task<ReportWorkflowIds> SeedArchivedReportSiteAsync(SpaTestApplicationFactory factory)
    {
        var companyId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await factory.SeedDomainEntitiesAsync(
            new Company { Id = companyId, CompanyName = "Archived Report Company", Contracts = [] },
            new Site { Id = siteId, SiteName = ArchivedReportSiteName, CreateDate = now.AddDays(-30), Archived = true, Contracts = [] },
            new Contract
            {
                Id = contractId,
                ContractNumber = "P7-ARCH-001",
                CompanyId = companyId,
                SiteiD = siteId,
                OnHireDate = now.Date
            });

        return new ReportWorkflowIds(companyId, siteId);
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

    // Function summary: Verifies query implementations count before the first row materialization point.
    private static void AssertQueryCountsBeforeMaterializing(string source, string signature)
    {
        var methodSource = ExtractMethodSource(source, signature);

        var count = methodSource.IndexOf("CountAsync", StringComparison.Ordinal);
        var firstMaterialization = methodSource.IndexOf("ToListAsync", StringComparison.Ordinal);

        Assert.True(count >= 0, $"{signature} should use CountAsync before paging.");
        Assert.True(firstMaterialization >= 0, $"{signature} should materialize only the requested page.");
        Assert.True(count < firstMaterialization, $"{signature} materializes rows before CountAsync.");
    }

    // Function summary: Verifies a method body does not include a rejected source fragment.
    private static void AssertMethodDoesNotContain(string source, string signature, string rejectedFragment)
    {
        var methodSource = ExtractMethodSource(source, signature);
        Assert.DoesNotContain(rejectedFragment, methodSource, StringComparison.Ordinal);
    }

    // Function summary: Extracts a source method body for text-based architecture guardrails.
    private static string ExtractMethodSource(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find query method signature: {signature}");
        var nextMember = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        if (nextMember < 0)
        {
            nextMember = source.IndexOf("\n    public ", start + signature.Length, StringComparison.Ordinal);
        }

        return nextMember < 0 ? source[start..] : source[start..nextMember];
    }

    // Function summary: Reads validation-problem details returned by mutation endpoint tests.
    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        return problem;
    }

    // Function summary: Verifies a validation-problem field includes an expected message fragment.
    private static void AssertValidationErrorContains(ValidationProblemDetails problem, string key, string expected)
    {
        Assert.True(problem.Errors.TryGetValue(key, out var messages), $"Expected validation key '{key}'.");
        Assert.Contains(messages, message => message.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    // Function summary: Handles the report workflow ids workflow for this module.
    private sealed record ReportWorkflowIds(Guid CompanyId, Guid SiteId);

    private sealed class FakeReportGenerationClient : IReportGenerationClient
    {
        private readonly string status;
        private readonly string message;

        public FakeReportGenerationClient(string status, string message)
        {
            this.status = status;
            this.message = message;
        }

        public Task<ReportGenerationRequestResponse?> RequestGenerationAsync(Guid reportRuleId, ReportGenerationRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<ReportGenerationRequestResponse?>(new ReportGenerationRequestResponse
            {
                Id = Guid.NewGuid(),
                ReportRuleId = reportRuleId,
                Status = status,
                Message = message,
                RequestedAtUtc = DateTime.UtcNow
            });
        }
    }
}
