using System;
using System.Collections.Generic;
using RVT.Entities;

namespace RVT.DataAccess.EntityModels.Models;

public partial class CustomerDashboardNotificationDatum
{
    public int? Nr { get; set; }

    public AlertTypeEnum AlertType { get; set; } // This will be changed to if you refresh against DB. Need to be readded manually

    public string AlertState { get; set; } = null!;

    public Guid UserId { get; set; }
}
