// File summary: Maps monitor application-service models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added monitor read-service mappers for controller cleanup.

using RvtPortal.Spa.Application.Monitors;

namespace RvtPortal.Spa.Api.Mappers;

public static class MonitorApiMapper
{
    // Function summary: Maps a monitor inventory application result to the existing API response contract.
    public static QueryMonitorsResponse ToQueryResponse(MonitorInventoryResult result)
    {
        return new QueryMonitorsResponse
        {
            Results = result.Results,
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir,
            State = result.State,
            IsScopedToCurrentUser = result.IsScopedToCurrentUser,
            CanManage = result.CanManage,
            CanUseInstallerTools = result.CanUseInstallerTools
        };
    }

    // Function summary: Maps monitor option application models to the existing API response contract.
    public static MonitorOptionsResponse ToOptionsResponse(MonitorOptionsModel model)
    {
        return new MonitorOptionsResponse
        {
            MonitorTypes = model.MonitorTypes.Select(ToOptionItem).ToList(),
            Contracts = model.Contracts.Select(ToOptionItem).ToList(),
            Sites = model.Sites.Select(ToOptionItem).ToList()
        };
    }

    // Function summary: Maps monitor assignment application models to the existing API response contract.
    public static MonitorAssignmentContextResponse ToAssignmentResponse(MonitorAssignmentContextModel model)
    {
        return new MonitorAssignmentContextResponse
        {
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            ContractId = model.ContractId,
            ContractNumber = model.ContractNumber,
            Contracts = model.Contracts.Select(ToOptionItem).ToList(),
            AvailableMonitors = model.AvailableMonitors,
            AssignedMonitors = model.AssignedMonitors
        };
    }

    // Function summary: Maps an unattached monitor inventory result to the existing API response contract.
    public static QueryUnattachedMonitorsResponse ToUnattachedQueryResponse(MonitorUnattachedInventoryResult result)
    {
        return new QueryUnattachedMonitorsResponse
        {
            Results = result.Results,
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir,
            CanRemove = result.CanRemove
        };
    }

    // Function summary: Maps one monitor option model to the shared option contract.
    private static OptionItem ToOptionItem(MonitorOptionModel model)
    {
        return new OptionItem
        {
            Value = model.Value,
            Label = model.Label
        };
    }
}
