// File summary: Maps user application-service models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added user detail, options, and site-assignment mappers for controller cleanup.
// - 2026-07-08 pending Added user list mapper for controller-to-application-service cleanup.

using RvtPortal.Spa.Application.Users;

namespace RvtPortal.Spa.Api.Mappers;

public static class UserApiMapper
{
    // Function summary: Maps the paged user list application result to the existing API response contract.
    public static QueryUsersResponse ToQueryResponse(UserListResult result)
    {
        return new QueryUsersResponse
        {
            Results = result.Page.Results.Select(ToListItem).ToList(),
            Total = result.Page.Total,
            Page = result.Page.Page,
            PageSize = result.Page.PageSize,
            TotalPages = result.Page.TotalPages,
            HasPreviousPage = result.Page.HasPreviousPage,
            HasNextPage = result.Page.HasNextPage,
            SearchText = result.Page.SearchText,
            Sort = result.Page.Sort,
            SortDir = result.Page.SortDir,
            CompanyId = result.CompanyId,
            CompanyName = result.CompanyName
        };
    }

    // Function summary: Maps user edit options to the existing user detail response shape.
    public static UserDetailResponse ToOptionsResponse(UserAdministrationOptionsModel model)
    {
        return new UserDetailResponse
        {
            AvailableRoles = model.AvailableRoles.Select(ToOptionItem).ToList(),
            Companies = model.Companies.Select(ToOptionItem).ToList()
        };
    }

    // Function summary: Maps a user detail application model to the existing API response contract.
    public static UserDetailResponse ToDetailResponse(UserDetailModel model)
    {
        var response = new UserDetailResponse
        {
            AvailableRoles = model.AvailableRoles.Select(ToOptionItem).ToList(),
            Companies = model.Companies.Select(ToOptionItem).ToList()
        };
        CopyUserFields(model, response);
        return response;
    }

    // Function summary: Maps one site-assignment application model to the existing API response contract.
    public static SiteAssignmentResponse ToSiteAssignmentResponse(SiteAssignmentModel model)
    {
        return new SiteAssignmentResponse
        {
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            AvailableUsers = model.AvailableUsers.Select(ToListItem).ToList(),
            AssignedUsers = model.AssignedUsers.Select(ToSiteUserAssignmentItem).ToList()
        };
    }

    // Function summary: Maps one user application model to the existing user list DTO.
    public static UserListItem ToListItem(UserListModel model)
    {
        var item = new UserListItem();
        CopyUserFields(model, item);
        return item;
    }

    // Function summary: Maps one assigned site user model to the existing API assignment DTO.
    private static SiteUserAssignmentItem ToSiteUserAssignmentItem(SiteUserAssignmentModel model)
    {
        var item = new SiteUserAssignmentItem
        {
            SiteContact = model.SiteContact
        };
        CopyUserFields(model, item);
        return item;
    }

    // Function summary: Maps one user option model to the shared option contract.
    private static OptionItem ToOptionItem(UserOptionModel model)
    {
        return new OptionItem
        {
            Value = model.Value,
            Label = model.Label
        };
    }

    // Function summary: Copies shared user fields into list, detail, and assignment DTOs.
    private static void CopyUserFields(UserListModel model, UserListItem item)
    {
        item.Id = model.Id;
        item.CompanyId = model.CompanyId;
        item.CompanyName = model.CompanyName;
        item.IsDisabled = model.IsDisabled;
        item.Name = model.Name;
        item.Email = model.Email;
        item.PhoneNumber = model.PhoneNumber;
        item.CompanyRole = model.CompanyRole;
        item.Role = model.Role;
        item.SiteCount = model.SiteCount;
        item.EmailConfirmed = model.EmailConfirmed;
        item.CanView = model.CanView;
        item.CanEdit = model.CanEdit;
        item.CanDisable = model.CanDisable;
        item.CanEnable = model.CanEnable;
        item.CanDelete = model.CanDelete;
        item.CanSendConfirmation = model.CanSendConfirmation;
        item.CanSendPasswordReset = model.CanSendPasswordReset;
        item.CanManageNotificationSettings = model.CanManageNotificationSettings;
    }
}
