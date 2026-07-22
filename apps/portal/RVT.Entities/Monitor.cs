// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-06-08 pending Added monitor archive metadata for admin removal workflows.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public enum MonitorTypeEnum
    {
        Dust = 0,
        Noise = 1,
        Vibration = 2
    }


    public class Monitor : BaseEntity
    {
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public Monitor()
        {
            Alertlevels = new List<Alertlevel>();
        }
        [StringLength(32)]
        public string? FleetNr { get; set; }
        [StringLength(32)]
        public string SerialId { get; set; } = null!;
        [StringLength(64)]
        public string Manufacturer { get; set; } = null!;
        [StringLength(64)]
        public string Model { get; set; } = null!;
        [StringLength(32)]
        public string FirmwareVersion { get; set; } = null!;

        public MonitorTypeEnum TypeOfMonitor { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CalibrationDate { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? CalibrationDue { get; set; }

        //Not used by web app
        public int? LocationId { get; set; }
        [StringLength(128)]
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        [StringLength(128)]
        public string? LocationAddress { get; set; }
        [StringLength(32)]
        public string? TimeZone { get; set; }
        public int? CustomerId { get; set; }
        [StringLength(64)]
        public string? CustomerDisplayName { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime ListedAtTime { get; set; }
        public bool Archived { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ArchivedAt { get; set; }
        [StringLength(256)]
        public string? ArchivedBy { get; set; }
        [StringLength(512)]
        public string? ArchiveReason { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastDataTime1Min { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastDataTime15Min { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastDataTime1Hour { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastDataTime24Hour { get; set; }

        //References
        public List<Alertlevel> Alertlevels { get; set; }


    }
}
