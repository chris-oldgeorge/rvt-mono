// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-06-25 pending Resolved legacy nullable (CS8618) warnings via null-forgiving initializers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using RVT.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RVT.Entities
{
    public class NotificationsSent : BaseEntity
    {
        public DateTime SendTime { get; set; }
        [StringLength(256)]
        public String Address { get; set; } = null!;
        [StringLength(256)]
        public String ErrorMessage { get; set; } = null!;

        [ForeignKey("NotificationId")]
        public Guid NotificationId { get; set; }
        public virtual Notification Notification { get; set; } = null!;
    }
}
