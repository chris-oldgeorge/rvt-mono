// File summary: Defines Help/FAQ CMS article content and publishing metadata.
// Major updates:
// - 2026-06-08 pending Added Help CMS article entity for FAQ, documents, videos, and definitions.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RVT.Entities
{
    public class HelpArticle : BaseEntity
    {
        public Guid SectionId { get; set; }

        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(160)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(512)]
        public string? Summary { get; set; }

        public string Body { get; set; } = string.Empty;

        [StringLength(40)]
        public string ContentType { get; set; } = "FAQ";

        public bool IsPublished { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public HelpSection? Section { get; set; }
        public List<HelpAsset> Assets { get; set; } = [];
    }
}
