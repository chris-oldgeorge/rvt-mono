// File summary: Covers canonical database naming rules used by the database refactor.
// Major updates:
// - 2026-06-25 pending Returned concrete CSV field lists for CA1859 analyzer cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added database naming convention guardrails for the canonical schema refactor.
// - 2026-06-08 pending Added opt-in EF Core canonical mapping convention tests for the database upgrade.
// - 2026-06-08 pending Added constraint/index registry naming checks for provider rename script hardening.
// - 2026-06-08 pending Added rollback script ordering checks after the Timescale clone rehearsal.
// - 2026-06-08 pending Added ASP.NET Identity exclusion guardrails for the database naming refactor.
// - 2026-06-09 pending Added canonical routine-name conversion checks for stored procedure porting.
// - 2026-06-09 pending Added provider-specific live context mapping checks after the development Postgres cutover.
// - 2026-06-09 pending Updated live context guardrails for canonical SQL Server and PostgreSQL schemas.
// - 2026-06-09 pending Added search-context canonical mapping checks after monitor detail runtime failures.
// - 2026-06-09 pending Added whole-search-model canonical guardrail for tables, views, and columns.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities;

namespace RvtPortal.Spa.Tests;

public sealed class DatabaseNamingConventionTests
{
    [Theory]
    [InlineData("site")]
    [InlineData("site_operating_hour")]
    [InlineData("help_article")]
    [InlineData("air_q_noise_8_hour_average")]
    // Function summary: Verifies lowercase snake_case identifiers are accepted by the database naming rule helper.
    public void CanonicalIdentifierAcceptsLowercaseSnakeCase(string identifier)
    {
        Assert.True(DatabaseNamingRules.IsCanonicalIdentifier(identifier));
    }

    [Theory]
    [InlineData("Sites")]
    [InlineData("siteOperatingHour")]
    [InlineData("site-operating-hour")]
    [InlineData("site operating hour")]
    [InlineData("user")]
    [InlineData("lock")]
    [InlineData("table")]
    // Function summary: Verifies mixed-case, non-snake-case, and banned identifiers are rejected.
    public void CanonicalIdentifierRejectsInvalidOrBannedNames(string identifier)
    {
        Assert.False(DatabaseNamingRules.IsCanonicalIdentifier(identifier));
    }

    [Theory]
    [InlineData("site", "id", "site_id")]
    [InlineData("help_article", "id", "help_article_id")]
    [InlineData("monitor", "serial_id", "monitor_serial_id")]
    // Function summary: Verifies foreign-key column names are derived from referenced table and field names.
    public void ForeignKeyNameCombinesReferencedTableAndField(string referencedTable, string referencedField, string expectedColumn)
    {
        Assert.Equal(expectedColumn, DatabaseNamingRules.BuildForeignKeyColumnName(referencedTable, referencedField));
    }

    [Theory]
    [InlineData("Sites", "sites")]
    [InlineData("SiteOperatingHours", "site_operating_hours")]
    [InlineData("AirQNoise8HourAverage", "air_q_noise_8_hour_average")]
    [InlineData("SiteiD", "site_id")]
    // Function summary: Verifies legacy identifiers can be converted into a lowercase snake_case candidate.
    public void ToSnakeCaseNormalizesLegacyIdentifiers(string currentName, string expectedCandidate)
    {
        Assert.Equal(expectedCandidate, DatabaseNamingRules.ToSnakeCase(currentName));
    }

    [Theory]
    [InlineData("MonitorStatusTimeCheck", "monitor_status_time_check")]
    [InlineData("MonitorStatusForMonth", "monitor_status_for_month")]
    [InlineData("PeakRecordBreachAndAlerts", "peak_record_breach_and_alerts")]
    // Function summary: Verifies legacy stored procedure names convert to canonical PostgreSQL routine names.
    public void ToCanonicalRoutineNameNormalizesLegacyRoutineIdentifiers(string currentName, string expectedName)
    {
        Assert.Equal(expectedName, DatabaseNamingRules.ToCanonicalRoutineName(currentName));
    }

    [Theory]
    [InlineData("site")]
    [InlineData("contract")]
    [InlineData("help_article")]
    [InlineData("monitor_status")]
    // Function summary: Verifies canonical relation names stay singular as part of the database naming standard.
    public void CanonicalRelationNameAcceptsSingularSnakeCase(string relationName)
    {
        Assert.True(DatabaseNamingRules.IsCanonicalRelationName(relationName));
    }

    [Theory]
    [InlineData("sites", false)]
    [InlineData("contracts", false)]
    [InlineData("help_articles", false)]
    [InlineData("sample_timestamp", true)]
    [InlineData("reading_text", true)]
    // Function summary: Verifies plural relation names and data-type-like column names are rejected where applicable.
    public void CanonicalRelationOrColumnNamesRejectCommonNamingAntiPatterns(string identifier, bool rejectAsColumn)
    {
        Assert.False(DatabaseNamingRules.IsCanonicalRelationName(identifier));
        if (rejectAsColumn)
        {
            Assert.False(DatabaseNamingRules.IsCanonicalColumnName(identifier));
        }
    }

    [Fact]
    // Function summary: Verifies the opt-in EF convention maps entity tables and scalar columns to canonical names.
    public void EfCanonicalMappingConvention_MapsTablesAndScalarColumns()
    {
        using var context = new CanonicalMappingProbeContext();

        var company = context.Model.FindEntityType(typeof(Company)) ?? throw new InvalidOperationException("Company entity missing.");
        var site = context.Model.FindEntityType(typeof(Site)) ?? throw new InvalidOperationException("Site entity missing.");

        Assert.Equal("company", company.GetTableName());
        Assert.Equal("company_name", company.FindProperty(nameof(Company.CompanyName))?.GetColumnName());
        Assert.Equal("site", site.GetTableName());
        Assert.Equal("site_name", site.FindProperty(nameof(Site.SiteName))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the opt-in EF convention maps single-column primary keys to id.
    public void EfCanonicalMappingConvention_MapsSingleColumnPrimaryKeysToId()
    {
        using var context = new CanonicalMappingProbeContext();

        var contract = context.Model.FindEntityType(typeof(Contract)) ?? throw new InvalidOperationException("Contract entity missing.");

        Assert.Equal("id", contract.FindProperty(nameof(Contract.Id))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the opt-in EF convention maps foreign-key columns to referenced relation id names.
    public void EfCanonicalMappingConvention_MapsForeignKeysToReferencedRelationId()
    {
        using var context = new CanonicalMappingProbeContext();

        var contract = context.Model.FindEntityType(typeof(Contract)) ?? throw new InvalidOperationException("Contract entity missing.");

        Assert.Equal("company_id", contract.FindProperty(nameof(Contract.CompanyId))?.GetColumnName());
        Assert.Equal("site_id", contract.FindProperty(nameof(Contract.SiteiD))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the opt-in EF convention leaves ASP.NET Identity entity mappings under framework-managed names.
    public void EfCanonicalMappingConvention_SkipsAspNetIdentityEntities()
    {
        using var context = new CanonicalMappingProbeContext();

        var role = context.Model.FindEntityType(typeof(IdentityRole)) ?? throw new InvalidOperationException("IdentityRole entity missing.");

        Assert.Equal("AspNetRoles", role.GetTableName());
        Assert.Equal("Id", role.FindProperty(nameof(IdentityRole.Id))?.GetColumnName());
        Assert.Equal("Name", role.FindProperty(nameof(IdentityRole.Name))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the live Postgres context maps to the physically migrated canonical development schema.
    public void RvtDbContext_UsesCanonicalNamesForPostgresProvider()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTDbContext>();
        optionsBuilder.UseRvtDatabaseProvider(new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.Postgres,
            ConnectionString = "Host=localhost;Database=rvt;Username=postgres;Password=postgres"
        });
        using var context = new RVTDbContext(optionsBuilder.Options);

        var company = context.Model.FindEntityType(typeof(Company)) ?? throw new InvalidOperationException("Company entity missing.");
        var monitor = context.Model.FindEntityType(typeof(RVT.Entities.Monitor)) ?? throw new InvalidOperationException("Monitor entity missing.");

        Assert.Equal("company", company.GetTableName());
        Assert.Equal("company_name", company.FindProperty(nameof(Company.CompanyName))?.GetColumnName());
        Assert.Equal("monitor", monitor.GetTableName());
        Assert.Equal("serial_id", monitor.FindProperty(nameof(RVT.Entities.Monitor.SerialId))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the live SQL Server context maps to the physically migrated canonical development schema.
    public void RvtDbContext_UsesCanonicalNamesForSqlServerProvider()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTDbContext>();
        optionsBuilder.UseRvtDatabaseProvider(new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=rvt;User Id=sa;Password=Password1!;TrustServerCertificate=True"
        });
        using var context = new RVTDbContext(optionsBuilder.Options);

        var company = context.Model.FindEntityType(typeof(Company)) ?? throw new InvalidOperationException("Company entity missing.");
        var monitor = context.Model.FindEntityType(typeof(RVT.Entities.Monitor)) ?? throw new InvalidOperationException("Monitor entity missing.");

        Assert.Equal("company", company.GetTableName());
        Assert.Equal("company_name", company.FindProperty(nameof(Company.CompanyName))?.GetColumnName());
        Assert.Equal("monitor", monitor.GetTableName());
        Assert.Equal("serial_id", monitor.FindProperty(nameof(RVT.Entities.Monitor.SerialId))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies the live search context maps scaffolded search/data models to canonical schema objects.
    public void RvtSearchContext_UsesCanonicalNamesForSearchAndMeasurementModels()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTSearchContext>();
        optionsBuilder.UseRvtDatabaseProvider(new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=rvt;User Id=sa;Password=Password1!;TrustServerCertificate=True"
        });
        using var context = new RVTSearchContext(optionsBuilder.Options);

        var sensor = context.Model.FindEntityType(typeof(RVT.DataAccess.EntityModels.Models.OmnidotsSensor)) ??
            throw new InvalidOperationException("OmnidotsSensor entity missing.");
        var peakLevel = context.Model.FindEntityType(typeof(RVT.DataAccess.EntityModels.Models.OmnidotsPeakLevel)) ??
            throw new InvalidOperationException("OmnidotsPeakLevel entity missing.");
        var monitorSearch = context.Model.FindEntityType(typeof(RVT.DataAccess.EntityModels.Models.MonitorSearch)) ??
            throw new InvalidOperationException("MonitorSearch entity missing.");

        Assert.Equal("omnidots_sensor", sensor.GetTableName());
        Assert.Equal("battery_charge", sensor.FindProperty(nameof(RVT.DataAccess.EntityModels.Models.OmnidotsSensor.BatteryCharge))?.GetColumnName());
        Assert.Equal("serial_id", sensor.FindProperty(nameof(RVT.DataAccess.EntityModels.Models.OmnidotsSensor.SerialId))?.GetColumnName());
        Assert.Equal("omnidots_peak_level", peakLevel.GetTableName());
        Assert.Equal("x_vtop", peakLevel.FindProperty(nameof(RVT.DataAccess.EntityModels.Models.OmnidotsPeakLevel.Xvtop))?.GetColumnName());
        Assert.Equal("monitor_search", monitorSearch.GetViewName());
        Assert.Equal("fleet_nr", monitorSearch.FindProperty(nameof(RVT.DataAccess.EntityModels.Models.MonitorSearch.FleetNr))?.GetColumnName());
    }

    [Fact]
    // Function summary: Verifies every live search-context store mapping resolves to canonical identifiers.
    public void RvtSearchContext_AllStoreMappingsUseCanonicalNames()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RVTSearchContext>();
        optionsBuilder.UseRvtDatabaseProvider(new RvtDatabaseOptions
        {
            Provider = RvtDatabaseProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=rvt;User Id=sa;Password=Password1!;TrustServerCertificate=True"
        });
        using var context = new RVTSearchContext(optionsBuilder.Options);

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            var viewName = entityType.GetViewName();
            var relationName = viewName ?? tableName;
            Assert.True(DatabaseNamingRules.IsCanonicalRelationName(relationName), $"{entityType.ClrType.Name} maps to non-canonical relation '{relationName}'.");

            var storeObject = viewName is not null
                ? StoreObjectIdentifier.View(viewName, entityType.GetViewSchema())
                : StoreObjectIdentifier.Table(tableName!, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                Assert.True(
                    DatabaseNamingRules.IsCanonicalColumnName(columnName),
                    $"{entityType.ClrType.Name}.{property.Name} maps to non-canonical column '{columnName}' on '{relationName}'.");
            }
        }
    }

    private sealed class CanonicalMappingProbeContext : DbContext
    {
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<Site> Sites => Set<Site>();
        public DbSet<IdentityRole> IdentityRoles => Set<IdentityRole>();

        // Function summary: Configures the in-memory test context used to inspect EF canonical naming metadata.
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase($"canonical-mapping-{Guid.NewGuid()}");
        }

        // Function summary: Applies the opt-in canonical mapping convention to a small real-entity model.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Company>()
                .HasMany(company => company.Contracts)
                .WithOne(contract => contract.Company)
                .HasForeignKey(contract => contract.CompanyId);

            modelBuilder.Entity<Site>()
                .HasMany(site => site.Contracts)
                .WithOne(contract => contract.Site)
                .HasForeignKey(contract => contract.SiteiD);

            modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles");

            modelBuilder.ApplyRvtCanonicalDatabaseNames();
        }
    }
}
