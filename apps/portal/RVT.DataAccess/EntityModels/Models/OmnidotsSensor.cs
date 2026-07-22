using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsSensor
{
    public Guid Id { get; set; }

    public string SerialId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime Lastseen { get; set; }

    public int BatteryCharge { get; set; }

    public string ConnectedUsing { get; set; } = null!;

    public bool Online { get; set; }
}
