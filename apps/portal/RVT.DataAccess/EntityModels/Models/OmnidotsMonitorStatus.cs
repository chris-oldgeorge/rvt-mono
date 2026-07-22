using System;
using System.Collections.Generic;

namespace RVT.DataAccess.EntityModels.Models;

public partial class OmnidotsMonitorStatus
{
    public Guid Id { get; set; }

    public string SerialId { get; set; } = null!;

    public int? MeasurementDuration { get; set; }

    public double? DataSaveLevel { get; set; }

    public bool VdvEnabled { get; set; }

    public string? VdvX { get; set; }

    public string? VdvY { get; set; }

    public string? VdvZ { get; set; }

    public int? VdvPeriod { get; set; }

    public double? TraceSaveLevel { get; set; }

    public double? TracePreTrigger { get; set; }

    public double? TracePostTrigger { get; set; }

    public double? AlarmValue { get; set; }

    public double? FlatLevel { get; set; }

    public bool DisableLed { get; set; }

    public int LogFlushInterval { get; set; }

    public string? GuideLine { get; set; }

    public string BuildingLevel { get; set; } = null!;

    public bool VectorEnabled { get; set; }

    public bool VtopEnabled { get; set; }

    public bool AtopEnabled { get; set; }
}
