using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class CompanySearch
{
    public Guid Id { get; set; }

    public string CompanyName { get; set; } = null!;

    public int NrUsers { get; set; }

    public string? Sites { get; set; }

    public string? Contracts { get; set; }
}
