using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class SiteSearch
{
    public Guid Id { get; set; }

    public string SiteName { get; set; } = null!;

    public bool Archived { get; set; }

    public DateTime CreateDate { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? Postcode { get; set; }

    public string? City { get; set; }

    public string? County { get; set; }

    public string? SiteAddress { get; set; }

    public string? Contracts { get; set; }

    public string? CompanyName { get; set; }

    public Guid? CompanyId { get; set; }

    public string? SiteContact { get; set; }
}
