// File summary: Defines RVT domain entities shared across data access, business logic, and API layers.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace RVT.Entities
{
    public class BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public BaseEntity()
        {

        }
    }
}
