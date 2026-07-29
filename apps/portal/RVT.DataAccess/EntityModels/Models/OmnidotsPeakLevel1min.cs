using System.Diagnostics.CodeAnalysis;

namespace RVT.DataAccess.EntityModels.Models;

[SuppressMessage("Naming", "S101:Types should be named in PascalCase", Justification = "Legacy EF entity name mirrors the existing database view contract and is referenced by shared query mappings.")]
public partial class OmnidotsPeakLevel1min
{
    public string SerialId { get; set; } = null!;

    public DateTime SampleTime { get; set; }

    public double? Xvtop { get; set; }

    public double? Yvtop { get; set; }

    public double? Zvtop { get; set; }
}
