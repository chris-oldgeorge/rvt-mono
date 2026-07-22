// File summary: Defines ASP.NET Identity and seed-data infrastructure for the portal host.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-07-14 pending Pinned the seeded role identifiers so the Identity model stops changing on every build.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RvtPortal.Spa.Data;

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    // These identifiers are pinned, and they are the ones the cutover database already carries.
    //
    // They used to be left to IdentityRole's parameterless constructor, which assigns Id and ConcurrencyStamp
    // from Guid.NewGuid(). Seed data is baked into the model, so the model came out different on every build and
    // EF refused to scaffold a migration for the context at all (PendingModelChangesWarning: "the model for
    // context 'ApplicationDbContext' changes each time it is built"). It also meant the seed could never have
    // matched a real database: every run proposed the same four roles under brand-new primary keys.
    //
    // Changing these values is a breaking change - every AspNetUserRoles row references them.
    private const string MasterAdminRoleId = "826C0115-E5D5-41E3-91F9-9A8909C066AE";
    private const string AdminRoleId = "C2FE90A5-1547-4099-AD3B-4085DB533AC8";
    private const string InstallerRoleId = "325E890E-3DA2-484B-B67B-A0132CFD9F16";
    private const string CompanyUserRoleId = "734A7113-20AC-4924-A317-E27768AF1428";

    // Function summary: Seeds the four fixed portal roles under their established identifiers.
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.HasData(
            Role(MasterAdminRoleId, RoleNames.RVTMasterAdmin),
            Role(AdminRoleId, RoleNames.RVTAdmin),
            Role(InstallerRoleId, RoleNames.RVTInstaller),
            Role(CompanyUserRoleId, RoleNames.CompanyUser));
    }

    // Function summary: Builds a role whose identifier and concurrency stamp are fixed rather than generated.
    private static IdentityRole Role(string id, string name)
    {
        return new IdentityRole
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),

            // The cutover database stores no stamp on these rows, and generating one here would reintroduce the
            // per-build churn this configuration exists to remove.
            ConcurrencyStamp = null
        };
    }
}
