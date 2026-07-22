// File summary: Exposes API endpoints used by the React portal for company user api contracts workflows.
// Major updates:
// - 2026-07-09 pending Removed empty DTO constructors left by generated comments.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

namespace RvtPortal.Spa.Api;

public sealed class QueryCompaniesRequest : PagedQueryRequest
{
}

public class QueryCompaniesResponse : PagedResponse<CompanyListItem>
{
}

public class CompanyListItem
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = "";
    public int UserCount { get; set; }
    public string? Sites { get; set; }
    public string? Contracts { get; set; }
}

public class CompanyDetailResponse
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = "";
    public int UserCount { get; set; }
    public int SiteCount { get; set; }
    public int ContractCount { get; set; }
    public string? Sites { get; set; }
    public string? Contracts { get; set; }
}

public class CompanyMutationRequest
{
    public string CompanyName { get; set; } = "";
}

public class QueryUsersRequest : PagedQueryRequest
{
    public Guid? CompanyId { get; set; }
}

public class QueryUsersResponse : PagedResponse<UserListItem>
{
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
}

public class UserListItem
{
    public string Id { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public bool IsDisabled { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? CompanyRole { get; set; }
    public string Role { get; set; } = "";
    public int SiteCount { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanEdit { get; set; }
    public bool CanDisable { get; set; }
    public bool CanEnable { get; set; }
    public bool CanDelete { get; set; }
    public bool CanSendConfirmation { get; set; }
    public bool CanSendPasswordReset { get; set; }
    public bool CanManageNotificationSettings { get; set; }
}

public class UserDetailResponse : UserListItem
{
    public List<OptionItem> AvailableRoles { get; set; } = [];
    public List<OptionItem> Companies { get; set; } = [];
}

public class UserMutationRequest
{
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? MobilePhone { get; set; }
    public string Role { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyRole { get; set; }
}

public class QuerySiteUsersRequest : PagedQueryRequest
{
    public required Guid SiteId { get; set; }
}

public class SiteAssignmentResponse
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public List<UserListItem> AvailableUsers { get; set; } = [];
    public List<SiteUserAssignmentItem> AssignedUsers { get; set; } = [];
}

public class SiteUserAssignmentItem : UserListItem
{
    public bool SiteContact { get; set; }
}

public class SiteUserMutationRequest
{
    public required Guid SiteId { get; set; }
    public required Guid UserId { get; set; }
}
