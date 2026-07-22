using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class ReportUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReportRuleId { get; set; }

    public Guid UserId { get; set; }
}
