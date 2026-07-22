// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{


    public class SiteUsers : BaseEntity
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public Guid UserId { get; set; }

        [ForeignKey("SiteId")]
        public Guid SiteId { get; set; }
        public bool SiteContact { get; set; }

        public virtual Site Site { get; set; } = null!;

    }
}
