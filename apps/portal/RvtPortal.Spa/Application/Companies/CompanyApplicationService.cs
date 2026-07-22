// File summary: Provides company list, detail, and lifecycle workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved company create/update/delete orchestration out of the API controller.
// - 2026-07-09 pending Moved company list/detail composition out of the API controller.

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic;
using RVT.DataAccess.Context;
using RVT.Entities;
using RVT.Entities.Querying;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Companies;

public interface ICompanyApplicationService
{
    // Function summary: Returns a paged company list using the existing company search contract.
    Task<CompanyQueryResult> Query(CompanyQuery request, CancellationToken cancellationToken = default);

    // Function summary: Returns company detail by id, or null when absent.
    Task<CompanyDetailResponse?> GetAsync(Guid companyId, CancellationToken cancellationToken);

    // Function summary: Creates a company and returns its refreshed detail model.
    Task<CompanyMutationWorkflowResult> CreateAsync(
        CompanyMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Updates a company and returns its refreshed detail model.
    Task<CompanyMutationWorkflowResult> UpdateAsync(
        Guid companyId,
        CompanyMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Deletes a company and returns the deleted company name for response messaging.
    Task<CompanyMutationWorkflowResult> DeleteAsync(Guid companyId, CancellationToken cancellationToken);
}

public sealed record CompanyQuery(
    string? SearchText,
    string? Sort,
    string SortDir,
    int Page,
    int PageSize);

public sealed class CompanyQueryResult
{
    public string? InvalidSort { get; init; }
    public IReadOnlyCollection<string> AllowedSortFields { get; init; } = CompanyApplicationService.AllowedSortFields;
    public string? ErrorMessage { get; init; }
    public QueryCompaniesResponse? Response { get; init; }
}

public sealed class CompanyMutationWorkflowResult
{
    public bool NotFound { get; init; }
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public CompanyDetailResponse? Company { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a workflow result from a command result and optional refreshed company detail.
    public static CompanyMutationWorkflowResult FromCommand(CompanyCommandResult result, CompanyDetailResponse? company = null)
    {
        return new CompanyMutationWorkflowResult
        {
            NotFound = result.NotFound,
            CompanyId = result.CompanyId,
            CompanyName = result.CompanyName,
            Company = company,
            Errors = result.Errors
        };
    }
}

public sealed class CompanyApplicationService : ICompanyApplicationService
{
    private const string CompanyNameSort = "CompanyName";

    private static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["companyName"] = CompanyNameSort,
        [CompanyNameSort] = CompanyNameSort,
        ["userCount"] = "NrUsers",
        ["NrUsers"] = "NrUsers",
        ["sites"] = "Sites",
        ["Sites"] = "Sites",
        ["contracts"] = "Contracts",
        ["Contracts"] = "Contracts"
    };

    internal static readonly IReadOnlyCollection<string> AllowedSortFields = SortFields.Keys
        .Where(key => key[0] == char.ToLowerInvariant(key[0]))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private readonly ICompanyService companyService;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;

    // Function summary: Initializes company workflows with search, identity, domain data, and transactional command dispatch dependencies.
    public CompanyApplicationService(
        ICompanyService companyService,
        UserManager<ApplicationUser> userManager,
        RVTDbContext domainContext,
        IMediator mediator)
    {
        this.companyService = companyService;
        this.userManager = userManager;
        this.domainContext = domainContext;
        this.mediator = mediator;
    }

    // Function summary: Returns a paged company list using the existing company search contract.
    public async Task<CompanyQueryResult> Query(CompanyQuery request, CancellationToken cancellationToken = default)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? CompanyNameSort : request.Sort.Trim();
        if (!SortFields.TryGetValue(requestedSort, out var serviceSort))
        {
            return new CompanyQueryResult
            {
                InvalidSort = requestedSort,
                AllowedSortFields = AllowedSortFields
            };
        }

        var sortDir = request.SortDir.Equals(SortDirections.Descending, StringComparison.OrdinalIgnoreCase)
            ? OrderByDirectionEnum.Descending
            : OrderByDirectionEnum.Ascending;
        var result = await companyService.Search(
            request.SearchText ?? "",
            request.Page,
            sortDir,
            serviceSort,
            request.PageSize,
            cancellationToken);
        if (!result.WasSuccessful)
        {
            return new CompanyQueryResult { ErrorMessage = result.ErrorMessage };
        }

        var totalPages = result.RecordCount == 0 ? 0 : (int)Math.Ceiling(result.RecordCount / (double)request.PageSize);
        return new CompanyQueryResult
        {
            Response = new QueryCompaniesResponse
            {
                Results = result.Value.Select(company => new CompanyListItem
                {
                    Id = company.Id,
                    CompanyName = company.CompanyName,
                    UserCount = company.NrUsers,
                    Sites = company.Sites,
                    Contracts = company.Contracts
                }).ToList(),
                Total = result.RecordCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages,
                HasPreviousPage = request.Page > 1 && totalPages > 0,
                HasNextPage = request.Page < totalPages,
                SearchText = request.SearchText ?? "",
                Sort = requestedSort,
                SortDir = request.SortDir
            }
        };
    }

    // Function summary: Returns company detail by id, or null when absent.
    public async Task<CompanyDetailResponse?> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await companyService.ReadOneAsync(companyId);
        return company == null ? null : await BuildDetailAsync(company, cancellationToken);
    }

    // Function summary: Creates a company through the transactional command pipeline and reloads its detail.
    public async Task<CompanyMutationWorkflowResult> CreateAsync(
        CompanyMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateCompanyCommand(request), cancellationToken);
        var company = result.CompanyId.HasValue && result.Errors.Count == 0
            ? await GetAsync(result.CompanyId.Value, cancellationToken)
            : null;
        return CompanyMutationWorkflowResult.FromCommand(result, company);
    }

    // Function summary: Updates a company through the transactional command pipeline and reloads its detail.
    public async Task<CompanyMutationWorkflowResult> UpdateAsync(
        Guid companyId,
        CompanyMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateCompanyCommand(companyId, request), cancellationToken);
        var company = !result.NotFound && result.Errors.Count == 0
            ? await GetAsync(companyId, cancellationToken)
            : null;
        return CompanyMutationWorkflowResult.FromCommand(result, company);
    }

    // Function summary: Deletes a company through the transactional command pipeline.
    public async Task<CompanyMutationWorkflowResult> DeleteAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteCompanyCommand(companyId), cancellationToken);
        return CompanyMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Builds the company detail counters and compact contract/site summaries.
    private async Task<CompanyDetailResponse> BuildDetailAsync(Company company, CancellationToken cancellationToken)
    {
        var contracts = await domainContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.CompanyId == company.Id)
            .ToListAsync(cancellationToken);
        var siteIds = contracts
            .Where(contract => contract.SiteiD.HasValue)
            .Select(contract => contract.SiteiD!.Value)
            .Distinct()
            .ToList();
        var sites = siteIds.Count == 0
            ? []
            : await domainContext.Sites
                .AsNoTracking()
                .Where(site => siteIds.Contains(site.Id))
                .OrderBy(site => site.SiteName)
                .Select(site => site.SiteName)
                .ToListAsync(cancellationToken);
        var userCount = await userManager.Users.CountAsync(user => user.CompanyId == company.Id, cancellationToken);

        return new CompanyDetailResponse
        {
            Id = company.Id,
            CompanyName = company.CompanyName,
            UserCount = userCount,
            ContractCount = contracts.Count,
            SiteCount = sites.Count,
            Contracts = JoinSummary(contracts.OrderBy(contract => contract.ContractNumber).Select(contract => contract.ContractNumber)),
            Sites = JoinSummary(sites)
        };
    }

    // Function summary: Builds a compact comma-separated summary from distinct non-empty values.
    private static string? JoinSummary(IEnumerable<string?> values)
    {
        var list = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        return list.Count == 0 ? null : string.Join(", ", list);
    }
}
