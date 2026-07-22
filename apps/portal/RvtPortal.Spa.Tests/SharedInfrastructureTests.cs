// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-07-09 pending Added lookup freshness coverage for database-backed async lookup queries.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.BusinessLogic;
using RvtPortal.Spa.Application.Companies;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public class SharedInfrastructureTests
{
    private const string AdminEmail = "shared.admin@rvt.test";
    private const string InstallerEmail = "shared.installer@rvt.test";
    private const string Password = "P8sSw0rd9$";

    [Fact]
    // Function summary: Handles the companies query uses shared paging sorting and search contract workflow for this module.
    public async Task Companies_Query_UsesSharedPagingSortingAndSearchContract()
    {
        // All three names contain "a", so the "a" search returns every seeded company; sorted
        // descending and paged 2-per-page, the first page is Bravo then Alpine.
        const string searchText = "a";
        const int pageSize = 2;
        var alpha = new CompanySearch { Id = Guid.NewGuid(), CompanyName = "Alpha Monitoring", NrUsers = 1, Sites = "Site A", Contracts = "A-001" };
        var bravo = new CompanySearch { Id = Guid.NewGuid(), CompanyName = "Bravo Monitoring", NrUsers = 4, Sites = "Site B", Contracts = "B-001" };
        var alpine = new CompanySearch { Id = Guid.NewGuid(), CompanyName = "Alpine Sensors", NrUsers = 2, Sites = null, Contracts = null };
        var seededCompanies = new[] { alpha, bravo, alpine };
        string[] expectedFirstPageOrder = [bravo.CompanyName, alpine.CompanyName];

        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = WithCompanyService(factory, seededCompanies);
        var client = CreateClient(app);
        await LoginAsync(client, AdminEmail, Password);

        var firstPage = await client.GetFromJsonAsync<QueryCompaniesResponse>(
            $"/api/companies?searchText={searchText}&page=1&pageSize={pageSize}&sort=companyName&sortDir=Descending");
        var emptyPage = await client.GetFromJsonAsync<QueryCompaniesResponse>(
            $"/api/companies?searchText={searchText}&page=3&pageSize={pageSize}&sort=companyName&sortDir=Ascending");

        Assert.NotNull(firstPage);
        Assert.Equal(seededCompanies.Length, firstPage!.Total);
        Assert.Equal(pageSize, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.True(firstPage.HasNextPage);
        Assert.False(firstPage.HasPreviousPage);
        Assert.Equal(searchText, firstPage.SearchText);
        Assert.Equal("companyName", firstPage.Sort);
        Assert.Equal(SortDirections.Descending, firstPage.SortDir);
        Assert.Equal(expectedFirstPageOrder, firstPage.Results.Select(company => company.CompanyName));

        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage!.Results);
        Assert.Equal(3, emptyPage.Page);
        Assert.False(emptyPage.HasNextPage);
        Assert.True(emptyPage.HasPreviousPage);
    }

    [Fact]
    // Function summary: Handles the companies query returns problem details for invalid sort field workflow for this module.
    public async Task Companies_Query_ReturnsProblemDetails_ForInvalidSortField()
    {
        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        using var app = WithCompanyService(factory);
        var client = CreateClient(app);
        await LoginAsync(client, AdminEmail, Password);

        var response = await client.GetAsync("/api/companies?sort=notAField");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(ApiDiagnostics.CorrelationIdHeader));
        Assert.Equal("Invalid sort field", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("allowedSortFields", out var allowedSortFields));
        Assert.Contains("companyName", allowedSortFields.EnumerateArray().Select(item => item.GetString()));
        Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    // Function summary: Handles the lookups require admin and return shared lookup shape workflow for this module.
    public async Task Lookups_RequireAdminAndReturnSharedLookupShape()
    {
        // Only the "RVT Alpha" company matches the "rvt" query; "Quiet Site" is seeded to prove the
        // lookup filters rather than returning everything.
        const string query = "rvt";
        const int take = 5;
        const string matchingCompany = "RVT Alpha";
        const string nonMatchingCompany = "Quiet Site";

        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedUserAsync(InstallerEmail, Password, RoleNames.RVTInstaller);
        await factory.SeedDomainCompaniesAsync(
            new Company { Id = Guid.NewGuid(), CompanyName = matchingCompany, Contracts = [] },
            new Company { Id = Guid.NewGuid(), CompanyName = nonMatchingCompany, Contracts = [] });
        var anonymousClient = CreateClient(factory);
        var installerClient = CreateClient(factory);
        var adminClient = CreateClient(factory);

        var anonymous = await anonymousClient.GetAsync($"/api/lookups/companies?query={query}");
        await LoginAsync(installerClient, InstallerEmail, Password);
        var installerLookup = await installerClient.GetAsync($"/api/lookups/companies?query={query}&take={take}");
        await LoginAsync(adminClient, AdminEmail, Password);
        var lookup = await adminClient.GetFromJsonAsync<SearchLookupResponse>($"/api/lookups/companies?query={query}&take={take}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, installerLookup.StatusCode);
        Assert.NotNull(lookup);
        Assert.Equal("companies", lookup!.Kind);
        Assert.Equal(query, lookup.Query);
        Assert.Equal(take, lookup.Take);
        Assert.Equal(new[] { matchingCompany }, lookup.Results);
    }

    [Fact]
    // Function summary: Verifies lookup searches read current database values instead of serving stale full-table cache entries.
    public async Task Lookups_QueryDatabaseAfterEarlierLookupRequest()
    {
        // "RVT Alpha" is seeded first but does not match "late"; the later-added "Late Arrival" does,
        // proving the lookup reads current data rather than a stale full-table cache.
        const string query = "late";
        const string lateCompany = "Late Arrival";

        using var factory = new SpaTestApplicationFactory();
        await factory.SeedUserAsync(AdminEmail, Password, RoleNames.RVTAdmin);
        await factory.SeedDomainCompaniesAsync(
            new Company { Id = Guid.NewGuid(), CompanyName = "RVT Alpha", Contracts = [] });
        var client = CreateClient(factory);
        await LoginAsync(client, AdminEmail, Password);

        var firstLookup = await client.GetFromJsonAsync<SearchLookupResponse>($"/api/lookups/companies?query={query}&take=5");
        await factory.SeedDomainCompaniesAsync(
            new Company { Id = Guid.NewGuid(), CompanyName = lateCompany, Contracts = [] });
        var secondLookup = await client.GetFromJsonAsync<SearchLookupResponse>($"/api/lookups/companies?query={query}&take=5");

        Assert.NotNull(firstLookup);
        Assert.Empty(firstLookup!.Results);
        Assert.NotNull(secondLookup);
        Assert.Equal(new[] { lateCompany }, secondLookup!.Results);
    }

    [Fact]
    // Function summary: Handles the diagnostic download returns file headers for download helper smoke workflow for this module.
    public async Task DiagnosticDownload_ReturnsFileHeaders_ForDownloadHelperSmoke()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/health/diagnostics/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("rvt-portal-spa-diagnostics.txt", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("RVT Portal SPA diagnostics\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    // Function summary: Handles the API exceptions return friendly problem details with correlation ID workflow for this module.
    public async Task ApiExceptions_ReturnFriendlyProblemDetails_WithCorrelationId()
    {
        using var factory = new SpaTestApplicationFactory();
        var client = CreateClient(factory);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/diagnostics/fault");
        request.Headers.Add(ApiDiagnostics.CorrelationIdHeader, "shared-correlation");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("shared-correlation", response.Headers.GetValues(ApiDiagnostics.CorrelationIdHeader).Single());
        Assert.Equal("An unexpected API error occurred.", document.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Diagnostic API fault", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("shared-correlation", document.RootElement.GetProperty("correlationId").GetString());
    }

    // Function summary: Handles the with company service workflow for this module.
    private static WebApplicationFactory<Program> WithCompanyService(SpaTestApplicationFactory factory, params CompanySearch[] companies)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICompanyService>();
                services.AddSingleton<ICompanyService>(new FakeCompanyService(companies));
            });
        });
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

    private sealed class FakeCompanyService : ICompanyService
    {
        private readonly IReadOnlyList<CompanySearch> companies;

        // Function summary: Handles the fake company service workflow for this module.
        public FakeCompanyService(IReadOnlyList<CompanySearch> companies)
        {
            this.companies = companies;
        }

        // Function summary: Handles the search workflow for this module.
        public Task<SearchQueryResult<CompanySearch>> Search(string companyName, int? page, OrderByDirectionEnum sortdir, string sort, int pageSize, CancellationToken cancellationToken = default)
        {
            var filtered = companies
                .Where(company => company.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));
            filtered = sort switch
            {
                "NrUsers" => sortdir == OrderByDirectionEnum.Descending
                    ? filtered.OrderByDescending(company => company.NrUsers)
                    : filtered.OrderBy(company => company.NrUsers),
                "Sites" => sortdir == OrderByDirectionEnum.Descending
                    ? filtered.OrderByDescending(company => company.Sites)
                    : filtered.OrderBy(company => company.Sites),
                "Contracts" => sortdir == OrderByDirectionEnum.Descending
                    ? filtered.OrderByDescending(company => company.Contracts)
                    : filtered.OrderBy(company => company.Contracts),
                _ => sortdir == OrderByDirectionEnum.Descending
                    ? filtered.OrderByDescending(company => company.CompanyName)
                    : filtered.OrderBy(company => company.CompanyName)
            };

            var pageValue = Math.Max(page ?? 1, 1);
            var items = filtered.ToList();
            return Task.FromResult(new SearchQueryResult<CompanySearch>(
                true,
                "",
                items.Skip((pageValue - 1) * pageSize).Take(pageSize).ToList(),
                items.Count,
                ""));
        }

        // Function summary: Registers this member for the current workflow.
        public Task<bool> AddAsync(Company company) => throw new NotSupportedException();
        // Function summary: Handles the company exist workflow for this module.
        public Task<bool> CompanyExist(string CompanyName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        // Function summary: Removes this member data for the current workflow.
        public Task DeleteAsync(Guid Id) => throw new NotSupportedException();
        // Function summary: Retrieves all data for callers.
        public Task<IList<Company>> ReadAllAsync() => throw new NotSupportedException();
        // Function summary: Retrieves one data for callers.
        public Task<Company> ReadOneAsync(Guid Id) => throw new NotSupportedException();
        // Function summary: Retrieves one with contracts data for callers.
        public Task<Company> ReadOneWithContractsAsync(Guid Id) => throw new NotSupportedException();
        // Function summary: Updates this member data for the current workflow.
        public Task UpdateAsync(Company company) => throw new NotSupportedException();
    }
}
