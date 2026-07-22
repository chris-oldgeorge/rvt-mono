// File summary: Defines Help/FAQ CMS sections used by the RVT Cloud help page.
// Major updates:
// - 2026-06-08 pending Added Help CMS section entity for the SPA help module.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RVT.Entities
{
    public class HelpSection : BaseEntity
    {
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(120)]
        public string Slug { get; set; } = string.Empty;

        public int SortOrder { get; set; }
        public bool IsPublished { get; set; } = true;
        public List<HelpArticle> Articles { get; set; } = [];
    }
}
