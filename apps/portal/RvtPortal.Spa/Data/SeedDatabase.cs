// File summary: Defines ASP.NET Identity and seed-data infrastructure for the portal host.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using Microsoft.AspNetCore.Identity;

namespace RvtPortal.Spa.Data;

public static class SeedDatabase
{
    private const string DefaultMasterAdminEmail = "master@rvtGroup.com";
    private const string MasterAdminSeedSettingName = "RVT_PORTAL_SEED_MASTER_ADMIN";

    // Function summary: Initializes initialize state required by the application.
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedDatabase));
        try
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var seedCredential = configuration[MasterAdminSeedSettingName];

            await EnsureRoleAsync(roleManager, RoleNames.RVTMasterAdmin);
            await EnsureRoleAsync(roleManager, RoleNames.RVTAdmin);
            await EnsureRoleAsync(roleManager, RoleNames.RVTInstaller);
            await EnsureRoleAsync(roleManager, RoleNames.CompanyUser);

            var masterAdmin = await userManager.FindByNameAsync(DefaultMasterAdminEmail)
                ?? await userManager.FindByEmailAsync(DefaultMasterAdminEmail);
            if (masterAdmin == null)
            {
                masterAdmin = new ApplicationUser
                {
                    UserName = DefaultMasterAdminEmail,
                    Email = DefaultMasterAdminEmail,
                    IsDisabled = false,
                    EmailConfirmed = true
                };
                if (string.IsNullOrWhiteSpace(seedCredential))
                {
                    logger.LogWarning(
                        "Default master admin user was not created because {SettingName} is not configured.",
                        MasterAdminSeedSettingName);
                    return;
                }

                var createResult = await userManager.CreateAsync(masterAdmin, seedCredential);
                if (!createResult.Succeeded)
                {
                    LogIdentityErrors(logger, createResult, "Could not create default master admin user", DefaultMasterAdminEmail);
                    return;
                }
            }
            else
            {
                await EnsureMasterAdminCanSignInAsync(userManager, logger, masterAdmin, seedCredential);
            }

            if (!await userManager.IsInRoleAsync(masterAdmin, RoleNames.RVTMasterAdmin))
            {
                var roleResult = await userManager.AddToRoleAsync(masterAdmin, RoleNames.RVTMasterAdmin);
                if (!roleResult.Succeeded)
                {
                    logger.LogWarning("Could not assign {Role} to default master admin user {UserName}: {Errors}",
                        RoleNames.RVTMasterAdmin,
                        DefaultMasterAdminEmail,
                        string.Join("; ", roleResult.Errors.Select(error => error.Description)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not initialize default SPA portal admin user.");
        }
    }

    // Function summary: Handles the ensure master admin can sign in workflow for this module.
    private static async Task EnsureMasterAdminCanSignInAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser masterAdmin,
        string? seedCredential)
    {
        await SynchronizeMasterAdminProfileAsync(userManager, logger, masterAdmin);
        await ClearMasterAdminLockoutAsync(userManager, logger, masterAdmin);
        await ResetMasterAdminAccessFailuresAsync(userManager, logger, masterAdmin);
        await EnsureMasterAdminCredentialAsync(userManager, logger, masterAdmin, seedCredential);
    }

    // Function summary: Handles the synchronize master admin profile workflow for this module.
    private static async Task SynchronizeMasterAdminProfileAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser masterAdmin)
    {
        var shouldUpdateUser = false;
        if (!string.Equals(masterAdmin.UserName, DefaultMasterAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            masterAdmin.UserName = DefaultMasterAdminEmail;
            shouldUpdateUser = true;
        }
        if (!string.Equals(masterAdmin.Email, DefaultMasterAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            masterAdmin.Email = DefaultMasterAdminEmail;
            shouldUpdateUser = true;
        }
        if (!masterAdmin.EmailConfirmed)
        {
            masterAdmin.EmailConfirmed = true;
            shouldUpdateUser = true;
        }
        if (masterAdmin.IsDisabled)
        {
            masterAdmin.IsDisabled = false;
            shouldUpdateUser = true;
        }
        if (shouldUpdateUser)
        {
            var updateResult = await userManager.UpdateAsync(masterAdmin);
            if (!updateResult.Succeeded)
            {
                LogIdentityErrors(logger, updateResult, "Could not update default master admin user", DefaultMasterAdminEmail);
            }
        }
    }

    // Function summary: Handles the clear master admin lockout workflow for this module.
    private static async Task ClearMasterAdminLockoutAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser masterAdmin)
    {
        if (masterAdmin.LockoutEnd.HasValue)
        {
            var lockoutResult = await userManager.SetLockoutEndDateAsync(masterAdmin, null);
            if (!lockoutResult.Succeeded)
            {
                LogIdentityErrors(logger, lockoutResult, "Could not clear default master admin lockout", DefaultMasterAdminEmail);
            }
        }
    }

    // Function summary: Handles the reset master admin access failures workflow for this module.
    private static async Task ResetMasterAdminAccessFailuresAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser masterAdmin)
    {
        if (masterAdmin.AccessFailedCount > 0)
        {
            var resetAccessResult = await userManager.ResetAccessFailedCountAsync(masterAdmin);
            if (!resetAccessResult.Succeeded)
            {
                LogIdentityErrors(logger, resetAccessResult, "Could not reset failed login count for default master admin user", DefaultMasterAdminEmail);
            }
        }
    }

    // Function summary: Handles the ensure master admin credential workflow for this module.
    private static async Task EnsureMasterAdminCredentialAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser masterAdmin,
        string? seedCredential)
    {
        if (string.IsNullOrWhiteSpace(seedCredential))
        {
            return;
        }

        if (!await userManager.CheckPasswordAsync(masterAdmin, seedCredential))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(masterAdmin);
            var passwordResult = await userManager.ResetPasswordAsync(masterAdmin, resetToken, seedCredential);
            if (!passwordResult.Succeeded)
            {
                LogIdentityErrors(logger, passwordResult, "Could not reset default master admin credential", DefaultMasterAdminEmail);
            }
        }
    }

    // Function summary: Handles the ensure role workflow for this module.
    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }
        var result = await roleManager.CreateAsync(new IdentityRole
        {
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        });
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create role '{roleName}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    // Function summary: Handles the log identity errors workflow for this module.
    private static void LogIdentityErrors(ILogger logger, IdentityResult result, string message, string userName)
    {
        logger.LogWarning("{Message}: {UserName}: {Errors}",
            message,
            userName,
            string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
