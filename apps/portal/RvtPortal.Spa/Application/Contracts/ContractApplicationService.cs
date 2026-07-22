// File summary: Provides contract list, options, detail, and lifecycle workflows for the portal API.
// Major updates:
// - 2026-07-09 pending Moved contract create/update/delete orchestration out of the API controller.
// - 2026-07-09 pending Moved contract list/detail/options composition out of the API controller.

using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.Application.Contracts;

public interface IContractApplicationService
{
    // Function summary: Returns a paged contract list with filters, search, sorting, and paging applied in EF.
    Task<ContractQueryResult> QueryAsync(ContractQuery request, CancellationToken cancellationToken);

    // Function summary: Returns contract edit options, optionally scoped to a company.
    Task<ContractOptionsResponse> OptionsAsync(Guid? companyId, CancellationToken cancellationToken);

    // Function summary: Returns contract detail by id, or null when absent.
    Task<ContractDetailResponse?> GetAsync(Guid contractId, CancellationToken cancellationToken);

    // Function summary: Creates a contract and returns its refreshed detail model.
    Task<ContractMutationWorkflowResult> CreateAsync(
        ContractMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Updates a contract and returns its refreshed detail model.
    Task<ContractMutationWorkflowResult> UpdateAsync(
        Guid contractId,
        ContractMutationRequest request,
        CancellationToken cancellationToken);

    // Function summary: Deletes a contract and returns the deleted contract number for response messaging.
    Task<ContractMutationWorkflowResult> DeleteAsync(Guid contractId, CancellationToken cancellationToken);
}

public sealed record ContractQuery(
    Guid? CompanyId,
    Guid? SiteId,
    string? SearchText,
    string? Sort,
    string SortDir,
    int Page,
    int PageSize);

public sealed class ContractQueryResult
{
    public string? InvalidSort { get; init; }
    public IReadOnlyCollection<string> AllowedSortFields { get; init; } = ContractApplicationService.SortFields.Keys.ToArray();
    public QueryContractsResponse? Response { get; init; }
}

public sealed class ContractMutationWorkflowResult
{
    public bool NotFound { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public ContractDetailResponse? Contract { get; init; }
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    // Function summary: Builds a workflow result from a command result and optional refreshed contract detail.
    public static ContractMutationWorkflowResult FromCommand(ContractCommandResult result, ContractDetailResponse? contract = null)
    {
        return new ContractMutationWorkflowResult
        {
            NotFound = result.NotFound,
            ContractId = result.ContractId,
            ContractNumber = result.ContractNumber,
            Contract = contract,
            Errors = result.Errors
        };
    }
}

public sealed class ContractApplicationService : IContractApplicationService
{
    internal static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["contractNumber"] = "contractNumber",
        ["siteName"] = "siteName",
        ["companyName"] = "companyName",
        ["onHireDate"] = "onHireDate",
        ["offHireDate"] = "offHireDate"
    };

    private readonly RVTDbContext domainContext;
    private readonly IMediator mediator;

    // Function summary: Initializes contract workflows with domain reads and transactional command dispatch dependencies.
    public ContractApplicationService(
        RVTDbContext domainContext,
        IMediator mediator)
    {
        this.domainContext = domainContext;
        this.mediator = mediator;
    }

    // Function summary: Returns a paged contract list with filters, search, sorting, and paging applied in EF.
    public async Task<ContractQueryResult> QueryAsync(ContractQuery request, CancellationToken cancellationToken)
    {
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "contractNumber" : request.Sort.Trim();
        if (!SortFields.ContainsKey(requestedSort))
        {
            return new ContractQueryResult
            {
                InvalidSort = requestedSort,
                AllowedSortFields = SortFields.Keys.ToArray()
            };
        }

        var query = domainContext.Contracts
            .AsNoTracking()
            .Include(contract => contract.Company)
            .Include(contract => contract.Site)
            .AsQueryable();
        if (request.CompanyId.HasValue)
        {
            query = query.Where(contract => contract.CompanyId == request.CompanyId.Value);
        }
        if (request.SiteId.HasValue)
        {
            query = query.Where(contract => contract.SiteiD == request.SiteId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var search = request.SearchText.Trim();
            query = query.Where(contract =>
                contract.ContractNumber.Contains(search) ||
                contract.Company.CompanyName.Contains(search) ||
                (contract.Site != null && contract.Site.SiteName.Contains(search)));
        }

        query = ApplySort(query, requestedSort, request.SortDir);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new ContractQueryResult
        {
            Response = new QueryContractsResponse
            {
                Results = items.Select(BuildListItem).ToList(),
                Total = total,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize),
                HasPreviousPage = request.Page > 1 && total > 0,
                HasNextPage = request.Page * request.PageSize < total,
                SearchText = request.SearchText ?? "",
                Sort = requestedSort,
                SortDir = request.SortDir
            }
        };
    }

    // Function summary: Returns contract edit options, optionally scoped to a company.
    public async Task<ContractOptionsResponse> OptionsAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        var companies = await domainContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.CompanyName)
            .Select(company => new OptionItem
            {
                Value = company.Id.ToString(),
                Label = company.CompanyName
            })
            .ToListAsync(cancellationToken);
        var siteQuery = domainContext.Sites
            .AsNoTracking()
            .Include(site => site.Contracts)
            .Where(site => !site.Archived);
        if (companyId.HasValue)
        {
            siteQuery = siteQuery.Where(site => !site.Contracts.Any() || site.Contracts.Any(contract => contract.CompanyId == companyId.Value));
        }
        var sites = await siteQuery
            .OrderBy(site => site.SiteName)
            .Select(site => new OptionItem
            {
                Value = site.Id.ToString(),
                Label = site.SiteName
            })
            .ToListAsync(cancellationToken);

        return new ContractOptionsResponse
        {
            Companies = companies,
            Sites = sites
        };
    }

    // Function summary: Returns contract detail by id, or null when absent.
    public async Task<ContractDetailResponse?> GetAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await domainContext.Contracts
            .AsNoTracking()
            .Include(item => item.Company)
            .Include(item => item.Site)
            .SingleOrDefaultAsync(item => item.Id == contractId, cancellationToken);
        return contract == null ? null : await BuildDetailAsync(contract, cancellationToken);
    }

    // Function summary: Creates a contract through the transactional command pipeline and reloads its detail.
    public async Task<ContractMutationWorkflowResult> CreateAsync(
        ContractMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateContractCommand(request), cancellationToken);
        var contract = result.ContractId.HasValue && result.Errors.Count == 0
            ? await GetAsync(result.ContractId.Value, cancellationToken)
            : null;
        return ContractMutationWorkflowResult.FromCommand(result, contract);
    }

    // Function summary: Updates a contract through the transactional command pipeline and reloads its detail.
    public async Task<ContractMutationWorkflowResult> UpdateAsync(
        Guid contractId,
        ContractMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateContractCommand(contractId, request), cancellationToken);
        var contract = !result.NotFound && result.Errors.Count == 0
            ? await GetAsync(contractId, cancellationToken)
            : null;
        return ContractMutationWorkflowResult.FromCommand(result, contract);
    }

    // Function summary: Deletes a contract through the transactional command pipeline.
    public async Task<ContractMutationWorkflowResult> DeleteAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteContractCommand(contractId), cancellationToken);
        return ContractMutationWorkflowResult.FromCommand(result);
    }

    // Function summary: Builds a contract detail response from a loaded contract entity.
    private async Task<ContractDetailResponse> BuildDetailAsync(Contract contract, CancellationToken cancellationToken)
    {
        var item = BuildListItem(contract);
        var options = await OptionsAsync(contract.CompanyId, cancellationToken);
        return new ContractDetailResponse
        {
            Id = item.Id,
            ContractNumber = item.ContractNumber,
            OnHireDate = item.OnHireDate,
            OffHireDate = item.OffHireDate,
            CompanyId = item.CompanyId,
            CompanyName = item.CompanyName,
            SiteId = item.SiteId,
            SiteName = item.SiteName,
            Companies = options.Companies,
            Sites = options.Sites
        };
    }

    // Function summary: Applies the requested contract sort.
    private static IQueryable<Contract> ApplySort(IQueryable<Contract> query, string sort, string sortDir)
    {
        var descending = sortDir == SortDirections.Descending;
        return sort.ToLowerInvariant() switch
        {
            "sitename" => descending ? query.OrderByDescending(contract => contract.Site!.SiteName) : query.OrderBy(contract => contract.Site!.SiteName),
            "companyname" => descending ? query.OrderByDescending(contract => contract.Company.CompanyName) : query.OrderBy(contract => contract.Company.CompanyName),
            "onhiredate" => descending ? query.OrderByDescending(contract => contract.OnHireDate) : query.OrderBy(contract => contract.OnHireDate),
            "offhiredate" => descending ? query.OrderByDescending(contract => contract.OffHireDate) : query.OrderBy(contract => contract.OffHireDate),
            _ => descending ? query.OrderByDescending(contract => contract.ContractNumber) : query.OrderBy(contract => contract.ContractNumber)
        };
    }

    // Function summary: Maps a contract entity to the existing list API contract.
    private static ContractListItem BuildListItem(Contract contract)
    {
        return new ContractListItem
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            OnHireDate = contract.OnHireDate,
            OffHireDate = contract.OffHireDate,
            CompanyId = contract.CompanyId,
            CompanyName = contract.Company?.CompanyName,
            SiteId = contract.SiteiD,
            SiteName = contract.Site?.SiteName
        };
    }
}
