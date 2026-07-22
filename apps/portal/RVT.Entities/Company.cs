// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public class Company : BaseEntity
    {
        [StringLength(50)]
        public string CompanyName { get; set; } = null!;

        public List<Contract> Contracts { get; set; } = new List<Contract>();

    }
}
