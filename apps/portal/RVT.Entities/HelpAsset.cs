// File summary: Defines linked document, video, and file assets for Help/FAQ CMS articles.
// Major updates:
// - 2026-06-08 pending Added Help CMS asset entity for internal folder and external media references.

using System;
using System.ComponentModel.DataAnnotations;

namespace RVT.Entities
{
    public class HelpAsset : BaseEntity
    {
        public Guid HelpArticleId { get; set; }

        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(40)]
        public string AssetType { get; set; } = "Document";

        [StringLength(512)]
        public string Url { get; set; } = string.Empty;

        [StringLength(512)]
        public string? InternalPath { get; set; }

        public int SortOrder { get; set; }
        public HelpArticle? HelpArticle { get; set; }
    }
}
