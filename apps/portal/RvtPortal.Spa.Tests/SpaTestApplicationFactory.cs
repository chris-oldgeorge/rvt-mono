// File summary: Covers regression tests for API host, React migration parity, and provider configuration behavior.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-24 pending Added report-content shared-key test configuration.
// - 2026-06-25 pending Removed EF Core options configuration callbacks when replacing contexts with in-memory stores.

using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using RvtPortal.Spa.Adapters.Archive;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RVT.DataAccess.Configuration;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class SpaTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string environment;
    private readonly int authRatePermitLimit;
    private readonly bool archiveExportFails;
    // Function summary: Handles the new guid workflow for this module.
    private readonly string databaseName = $"RvtPortalSpaTests-{Guid.NewGuid()}";
    // Function summary: Handles the service collection workflow for this module.
    private readonly ServiceProvider databaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SpaTestApplicationFactory(string environment = "Testing", int authRatePermitLimit = 1000, bool archiveExportFails = false)
    {
        this.environment = environment;
        this.authRatePermitLimit = authRatePermitLimit;
        this.archiveExportFails = archiveExportFails;
    }

    // Function summary: Configures web host during application startup.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SkipPasswordResetEmail"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Testing",
                ["ReportContent:InternalApiKey"] = "test-report-content-key",
                // Keep the auth rate limiter effectively off for the shared suite so
                // tests that legitimately log in many times do not trip a 429. The
                // dedicated rate-limit regression test constructs the factory with a
                // low permit limit to exercise the 429 path.
                ["RateLimiting:Auth:PermitLimit"] = authRatePermitLimit.ToString(CultureInfo.InvariantCulture),
                // Allow the shared 10-character test password under the stricter
                // production default (12) without churning every test's credential.
                ["Identity:Password:RequiredLength"] = "8"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<RVTDbContext>>();
            services.RemoveAll<DbContextOptions<RVTSearchContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RVTDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RVTSearchContext>>();
            services.RemoveAll<IOptions<RvtDatabaseOptions>>();
            services.RemoveAll<IRvtDatabaseConnectionFactory>();
            services.RemoveAll<DbConnection>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName).UseInternalServiceProvider(databaseProvider));
            services.AddDbContext<RVTDbContext>(options =>
                options.UseInMemoryDatabase($"{databaseName}-domain").UseInternalServiceProvider(databaseProvider));
            services.AddDbContext<RVTSearchContext>(options =>
                options.UseInMemoryDatabase($"{databaseName}-search").UseInternalServiceProvider(databaseProvider));
            services.AddSingleton<IOptions<RvtDatabaseOptions>>(Options.Create(new RvtDatabaseOptions
            {
                Provider = RvtDatabaseProvider.SqlServer,
                ConnectionString = "Testing"
            }));
            services.AddSingleton<IRvtDatabaseConnectionFactory, RvtDatabaseConnectionFactory>();

            // The real site-archive export streams data and uploads to Azure blob storage, which is not present
            // in tests. Fake it so the archive endpoint exercises its own logic without an external dependency.
            // With archiveExportFails set, the fake throws so the export-failure path can be verified.
            services.RemoveAll<ISiteArchiveService>();
            if (archiveExportFails)
            {
                services.AddScoped<ISiteArchiveService, FailingSiteArchiveService>();
            }
            else
            {
                services.AddScoped<ISiteArchiveService, FakeSiteArchiveService>();
            }
        });
    }

    // A stand-in for the blob-backed export: it always succeeds and returns a deterministic archive URL.
    private sealed class FakeSiteArchiveService : ISiteArchiveService
    {
        public Task<string> Process(Guid siteId, CancellationToken cancellationToken)
        {
            return Task.FromResult($"https://tests.local/site-archives/{siteId:N}.zip");
        }
    }

    // Simulates the blob-backed export being unavailable, so the caller's failure handling can be exercised.
    private sealed class FailingSiteArchiveService : ISiteArchiveService
    {
        public Task<string> Process(Guid siteId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Blob storage is unavailable in this test.");
        }
    }

    // Function summary: Initializes user state required by the application.
    public async Task<ApplicationUser> SeedUserAsync(
        string email,
        string? password,
        string roleName,
        bool emailConfirmed = true,
        bool isDisabled = false,
        Guid? companyId = null,
        string? name = null)
    {
        using var scope = Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleAsync(roleManager, RoleNames.RVTMasterAdmin);
        await EnsureRoleAsync(roleManager, RoleNames.RVTAdmin);
        await EnsureRoleAsync(roleManager, RoleNames.RVTInstaller);
        await EnsureRoleAsync(roleManager, RoleNames.CompanyUser);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            IsDisabled = isDisabled,
            Name = name ?? email.Split('@')[0],
            CompanyId = companyId,
            CompanyRole = roleName == RoleNames.CompanyUser ? "Site contact" : null
        };

        var createResult = password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);
        EnsureSucceeded(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        EnsureSucceeded(roleResult);

        return user;
    }

    // Function summary: Handles the generate password reset token workflow for this module.
    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"User {email} was not found.");
        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    // Function summary: Handles the generate email confirmation token workflow for this module.
    public async Task<string> GenerateEmailConfirmationTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"User {email} was not found.");
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    // Function summary: Initializes domain companies state required by the application.
    public async Task SeedDomainCompaniesAsync(params Company[] companies)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        context.Companies.AddRange(companies);
        await context.SaveChangesAsync();
    }

    // Function summary: Initializes domain entities state required by the application.
    public async Task SeedDomainEntitiesAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    // Function summary: Initializes search entities state required by the application.
    public async Task SeedSearchEntitiesAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RVTSearchContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    // Function summary: Handles the ensure role workflow for this module.
    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(roleName)));
    }

    // Function summary: Handles the ensure succeeded workflow for this module.
    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
