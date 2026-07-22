// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public class Deployment : BaseEntity
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
        [StringLength(256)]
        public string? Location { get; set; }
        [StringLength(256)]
        public string? What3words { get; set; }
        public string? PictureLink { get; set; }


        [ForeignKey("ContractId")]
        public Guid ContractId { get; set; }
        public virtual Contract Contract { get; set; } = null!;

        [ForeignKey("MonitorId")]
        public Guid MonitorId { get; set; }
        public virtual Monitor Monitor { get; set; } = null!;

    }
}
