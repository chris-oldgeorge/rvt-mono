// File summary: Defines shared data contracts used by repository projections and EF model interfaces.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using RVT.DataAccess.EntityModels.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.DataAccess.EntityModels.Interface
{
    public interface IMyAtmDustLevel
    {
        public string SerialId { get; set; }

        public DateTime SampleTime { get; set; }

        public double? Pm1 { get; set; }

        public double? Pm25 { get; set; }

        public double? Pm10 { get; set; }

        public double? PmTotal { get; set; }
    }
    public class MyAtmDustLevelForInterface : MyAtmDustLevel, IMyAtmDustLevel { }
    public class MyAtmDustLevel8hourAvgForInterface : MyAtmDustLevel8hourAvg, IMyAtmDustLevel { }

}
