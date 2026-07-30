// File summary: Provides the Portal test host with an isolated throwaway PostgreSQL schema and explicit local host filtering.
// Major updates:
// - 2026-07-30 pending Replatformed from EF InMemory onto the real Postgres integration database (throwaway schemas).
// - 2026-07-26 pending Removed the obsolete provider-selection setting from test database options.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.
// - 2026-06-24 pending Added report-content shared-key test configuration.
// - 2026-07-25 pending Replaced WebApplicationFactory's wildcard host default with the portal's explicit local test hosts.

using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Adapters.Archive;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.Tests.Support;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// Boots the real Spa host against a throwaway PostgreSQL schema on the shared integration database, so the
/// production wiring runs unchanged: the three EF contexts share one scoped Npgsql connection, the Unit of
/// Work opens real transactions, the site-write raw SQL and ExecuteUpdate paths execute, and startup schema
/// validation reads a live information schema. Requires <c>RVT__POSTGRES_INTEGRATION_CONNECTION</c>; gate
/// every test that constructs this factory with <see cref="RequiresPostgresFactAttribute"/> (or the Theory
/// variant) so suites stay runnable on machines without the container.
/// </summary>
public sealed class SpaTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string environment;
    private readonly int authRatePermitLimit;
    private readonly bool archiveExportFails;
    private readonly string _schemaName = $"spa_host_{Guid.NewGuid():N}";
    private readonly Lock _schemaGate = new();
    private string? _scopedConnectionString;

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
        // Derived factories (WithWebHostBuilder) re-run this method on the same instance, so the schema is
        // created exactly once and shared by every host the factory produces.
        string connectionString = EnsureSchema();

        builder.UseEnvironment(environment);
        builder.UseSetting("EmailConfiguration:SENDGRID_API_KEY", "test-sendgrid-api-key");
        builder.UseSetting("EmailConfiguration:Sending_Email_Address", "portal-tests@example.test");
        builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
        // The database keys must be UseSetting, not ConfigureAppConfiguration: AddRvtDatabaseProvider snapshots
        // the configuration while Program's ConfigureServices runs, and only host settings are merged that
        // early - ConfigureAppConfiguration values arrive after registration and would leave the host on the
        // Testing placeholder connection string.
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        // Nothing deploys stored routines into the throwaway schema, so point routine execution at it
        // too: a routine call fails loudly instead of silently reading the shared public schema.
        builder.UseSetting("Database:PostgresRoutineSchema", _schemaName);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SkipPasswordResetEmail"] = "true",
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

    // Function summary: Creates the factory's throwaway schema exactly once, tolerating derived-host re-entry.
    private string EnsureSchema()
    {
        lock (_schemaGate)
        {
            return _scopedConnectionString ??= SpaTestDatabase.CreateSchema(_schemaName);
        }
    }

    // Function summary: Drops the throwaway schema after the hosts this factory produced are gone.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DropSchema();
        }
    }

    // Function summary: Drops the throwaway schema on the async disposal path as well.
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DropSchema();
    }

    // Function summary: Drops the schema at most once across the sync and async disposal paths.
    private void DropSchema()
    {
        lock (_schemaGate)
        {
            if (_scopedConnectionString is null)
            {
                return;
            }

            SpaTestDatabase.DropSchema(_schemaName, _scopedConnectionString);
            _scopedConnectionString = null;
        }
    }

    // A stand-in for the blob-backed export: it always succeeds and returns a deterministic archive URL.
    private sealed class FakeSiteArchiveService : ISiteArchiveService
    {
        public Task<string> Process(Guid siteId, CancellationToken cancellationToken)
        {
            return Task.FromResult($"https://tests.local/site-archives/{siteId:N}.zip");
        }

        public Task DeleteSupersededAsync(
            Guid siteId,
            string durableArchiveUrl,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    // Simulates the blob-backed export being unavailable, so the caller's failure handling can be exercised.
    private sealed class FailingSiteArchiveService : ISiteArchiveService
    {
        public Task<string> Process(Guid siteId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Blob storage is unavailable in this test.");
        }

        public Task DeleteSupersededAsync(
            Guid siteId,
            string durableArchiveUrl,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Blob storage is unavailable in this test.");
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
        using IServiceScope scope = Services.CreateScope();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleAsync(roleManager, RoleNames.RVTMasterAdmin);
        await EnsureRoleAsync(roleManager, RoleNames.RVTAdmin);
        await EnsureRoleAsync(roleManager, RoleNames.RVTInstaller);
        await EnsureRoleAsync(roleManager, RoleNames.CompanyUser);

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            IsDisabled = isDisabled,
            Name = name ?? email.Split('@')[0],
            CompanyId = companyId,
            CompanyRole = roleName == RoleNames.CompanyUser ? "Site contact" : null
        };

        IdentityResult createResult = password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);
        EnsureSucceeded(createResult);

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, roleName);
        EnsureSucceeded(roleResult);

        return user;
    }

    // Function summary: Handles the generate password reset token workflow for this module.
    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"User {email} was not found.");
        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    // Function summary: Handles the generate email confirmation token workflow for this module.
    public async Task<string> GenerateEmailConfirmationTokenAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"User {email} was not found.");
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    // Function summary: Initializes domain companies state required by the application.
    public async Task SeedDomainCompaniesAsync(params Company[] companies)
    {
        using IServiceScope scope = Services.CreateScope();
        RVTDbContext context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        context.Companies.AddRange(companies);
        await context.SaveChangesAsync();
    }

    // Function summary: Initializes domain entities state required by the application.
    public async Task SeedDomainEntitiesAsync(params object[] entities)
    {
        using IServiceScope scope = Services.CreateScope();
        RVTDbContext context = scope.ServiceProvider.GetRequiredService<RVTDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    // Function summary: Initializes search entities state required by the application.
    public async Task SeedSearchEntitiesAsync(params object[] entities)
    {
        using IServiceScope scope = Services.CreateScope();
        RVTSearchContext context = scope.ServiceProvider.GetRequiredService<RVTSearchContext>();
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
