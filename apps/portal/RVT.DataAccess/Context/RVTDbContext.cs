// File summary: Defines Entity Framework Core context configuration for RVT domain and search data.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added site operating-hours and Help CMS table mappings.
// - 2026-06-09 pending Enabled canonical EF mappings when the context runs against migrated PostgreSQL.
// - 2026-06-09 pending Extended canonical EF mappings to SQL Server after the local SQL Server cutover.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Added SQL Server/PostgreSQL provider support.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RVT.DataAccess.Configuration;

namespace RVT.DataAccess.Context
{
    public class RVTDbContext : DbContext
    {
        // Function summary: Initializes this type with the dependencies required by its workflow.
        public RVTDbContext(DbContextOptions<RVTDbContext> options)
            : base(options)
        {
        }

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public RVTDbContext(string connectionString)
            : this(connectionString, RvtDatabaseProvider.SqlServer)
        {
        }

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public RVTDbContext(string connectionString, RvtDatabaseProvider provider)
            : base(new DbContextOptionsBuilder<RVTDbContext>()
                .UseRvtDatabaseProvider(new RvtDatabaseOptions
                {
                    Provider = provider,
                    ConnectionString = connectionString
                })
                .Options)
        {
        }

        // The parameterless constructor and its OnConfiguring fallback are gone. The fallback built a provider
        // from an appsettings.json found relative to Environment.CurrentDirectory, so a context constructed
        // without options would silently connect to whatever database the process's working directory implied.
        // Every runtime context now comes from AddDbContext with explicit options; EF tooling uses
        // RVTDbContextDesignTimeFactory.

        public virtual DbSet<RVT.Entities.Company> Companies { get; set; }
        public virtual DbSet<RVT.Entities.Contract> Contracts { get; set; }
        public virtual DbSet<RVT.Entities.Site> Sites { get; set; }
        public virtual DbSet<RVT.Entities.Deployment> Deployments { get; set; }
        public virtual DbSet<RVT.Entities.SiteUsers> SiteUsers { get; set; }
        public virtual DbSet<RVT.Entities.Monitor> MonitorsList { get; set; }
        public virtual DbSet<RVT.Entities.Alertlevel> RvtAlertRules { get; set; }
        public virtual DbSet<RVT.Entities.NotificationSettings> NotificationSettings { get; set; }
        public virtual DbSet<RVT.Entities.Notification> Notifications { get; set; }
        public virtual DbSet<RVT.Entities.SiteArchived> SiteArchived { get; set; }
        public virtual DbSet<RVT.Entities.SiteOperatingHours> SiteOperatingHours { get; set; }
        public virtual DbSet<RVT.Entities.HelpSection> HelpSections { get; set; }
        public virtual DbSet<RVT.Entities.HelpArticle> HelpArticles { get; set; }
        public virtual DbSet<RVT.Entities.HelpAsset> HelpAssets { get; set; }

        // Function summary: Configures provider-neutral domain relationships and indexes.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RVT.Entities.SiteOperatingHours>(entity =>
            {
                entity.HasIndex(hours => new { hours.SiteId, hours.DayOfWeek }).IsUnique();
                entity.HasOne(hours => hours.Site)
                    .WithMany(site => site.OperatingHours)
                    .HasForeignKey(hours => hours.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RVT.Entities.HelpSection>(entity =>
            {
                entity.HasIndex(section => section.Slug).IsUnique();
            });

            modelBuilder.Entity<RVT.Entities.HelpArticle>(entity =>
            {
                entity.HasIndex(article => article.Slug).IsUnique();
                entity.HasOne(article => article.Section)
                    .WithMany(section => section.Articles)
                    .HasForeignKey(article => article.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RVT.Entities.HelpAsset>(entity =>
            {
                entity.HasOne(asset => asset.HelpArticle)
                    .WithMany(article => article.Assets)
                    .HasForeignKey(asset => asset.HelpArticleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.ApplyRvtCanonicalDatabaseNames();
        }
    }
}
