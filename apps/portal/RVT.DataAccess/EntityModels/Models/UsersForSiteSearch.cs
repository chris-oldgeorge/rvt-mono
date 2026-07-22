using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class UsersForSiteSearch
{
    public string Id { get; set; } = null!;

    public Guid? CompanyId { get; set; }

    public bool IsDisabled { get; set; }

    public string? Name { get; set; }

    public string? UserName { get; set; }

    public string? CompanyRole { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public string? Role { get; set; }

    public string? CompanyName { get; set; }

    public int NrSites { get; set; }

    public Guid? SiteId { get; set; }

    public bool? SiteContact { get; set; }
}
