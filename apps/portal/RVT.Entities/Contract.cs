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
    public class Contract : BaseEntity
    {
        [StringLength(20)]
        public string ContractNumber { get; set; } = null!;
        public DateTime OnHireDate { get; set; }
        public DateTime? OffHireDate { get; set; }

        [ForeignKey("CompanyId")]
        public Guid CompanyId { get; set; }
        public virtual Company Company { get; set; } = null!;

        [ForeignKey("SiteiD")]
        public Guid? SiteiD { get; set; }
        public virtual Site? Site { get; set; }
    }
}
