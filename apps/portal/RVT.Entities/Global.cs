// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{

    public class Paging
    {
        public bool paged { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
    }


}
