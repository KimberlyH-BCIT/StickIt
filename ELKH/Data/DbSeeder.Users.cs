using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/// <summary>
/// Database seeding operations for user accounts and role management.
/// Handles creation of administrative accounts, roles, and security setup.
/// </summary>
public static partial class DbSeeder
{
    #region Users & Roles Seeding

    /// <summary>
    /// Seeds default Admin, Manager, Staff, and Customer roles with corresponding test accounts.
    /// Fully idempotent-skipped if roles or accounts already exist.
    /// </summary>
    /// <param name="userManager">ASP.NET Core Identity UserManager for account creation.</param>
    /// <param name="roleManager">ASP.NET Core Identity RoleManager for role creation.</param>
    /// <param name="configuration">
    /// Application configuration for retrieving credentials. Reads the following keys:
    /// <list type="bullet">
    /// <item><c>Seed:AdminEmail</c> and <c>Seed:AdminPass</c></item>
    /// <item><c>Seed:ManagerEmail</c> and <c>Seed:ManagerPass</c></item>
    /// <item><c>Seed:StaffEmail</c> and <c>Seed:StaffPass</c></item>
    /// </list>
    /// </param>
    /// <remarks>
    /// <para><strong>âš ï¸ SECURITY WARNING:</strong></para>
    /// Credentials must be configured via <c>dotnet user-secrets</c> in development
    /// or environment variables/Azure Key Vault in production. The fallback defaults
    /// (admin@stickit.dev / Admin@2025!) are intentionally weak and suitable ONLY for
    /// local development. Never deploy with default credentials.
    ///
    /// <para><strong>Idempotency Strategy:</strong></para>
    /// <list type="number">
    /// <item>Create roles if they don't exist (always safe to call)</item>
    /// <item>Create user accounts if they don't exist</item>
    /// <item>Always verify and fix role assignments (handles partial failures)</item>
    /// </list>
    ///
    /// <para><strong>Role Hierarchy:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Admin</strong>: Full system access (user management, sales reports, cache control)</item>
    /// <item><strong>Manager</strong>: Order management, transaction viewing, inventory oversight</item>
    /// <item><strong>Staff</strong>: Order fulfillment and customer support</item>
    /// <item><strong>Customer</strong>: Shopping and account management (assigned during registration)</item>
    /// </list>
    /// </remarks>
    public static async Task SeedUsersAndRolesAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        string wwwRootPath)
    {
        const string adminRole    = "Admin";
        const string managerRole  = "Manager";
        const string staffRole    = "Staff";
        const string customerRole = "Customer";

        // ======================================================================
        // â•‘ Load Credentials from Configuration                                â•‘
        // â•‘ Fallback to weak defaults only for local development.              â•‘
        // ======================================================================
        // Read from user-secrets / environment variables.
        // IsNullOrWhiteSpace guards against empty-string values in appsettings.json,
        // which would bypass the ?? operator and create credential-less accounts.
        var adminEmail = configuration["Seed:AdminEmail"];
        if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = "admin@stickit.dev";
        var adminPass  = configuration["Seed:AdminPass"];
        if (string.IsNullOrWhiteSpace(adminPass))  adminPass  = "Admin@2025!";

        var managerEmail = configuration["Seed:ManagerEmail"];
        if (string.IsNullOrWhiteSpace(managerEmail)) managerEmail = "manager@stickit.dev";
        var managerPass  = configuration["Seed:ManagerPass"];
        if (string.IsNullOrWhiteSpace(managerPass))  managerPass  = "Manager@2025!";

        var staffEmail = configuration["Seed:StaffEmail"];
        if (string.IsNullOrWhiteSpace(staffEmail)) staffEmail = "staff@stickit.dev";
        var staffPass  = configuration["Seed:StaffPass"];
        if (string.IsNullOrWhiteSpace(staffPass))  staffPass  = "Staff@2025!";

        // ======================================================================
        // â•‘ Ensure Roles Exist                                                 â•‘
        // â•‘ Create all four roles regardless of whether users exist.           â•‘
        // ======================================================================
        string[] allRoles = { adminRole, managerRole, staffRole, customerRole };
        foreach (var role in allRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ======================================================================
        // â•‘ Seed Administrative Accounts                                       â•‘
        // â•‘ Create admin, manager, and staff accounts with proper roles.       â•‘
        // ======================================================================

        // â”€â”€ Seed Admin Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPass);
            if (!result.Succeeded) return;
        }

        // Always verify the role assignment - covers the case where the user
        // was created on a previous run but role assignment failed.
        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);

        // Create app-level records so admin can use all features (cart, wishlist, profile, etc.)
        await EnsureAppUserRecordsAsync(db, admin, adminEmail, "Admin", "User", wwwRootPath);

        // â”€â”€ Seed Manager Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var manager = await userManager.FindByEmailAsync(managerEmail);

        if (manager is null)
        {
            manager = new IdentityUser
            {
                UserName       = managerEmail,
                Email          = managerEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(manager, managerPass);
            if (result.Succeeded && !await userManager.IsInRoleAsync(manager, managerRole))
                await userManager.AddToRoleAsync(manager, managerRole);
        }
        else if (!await userManager.IsInRoleAsync(manager, managerRole))
        {
            await userManager.AddToRoleAsync(manager, managerRole);
        }

        // Create app-level records so manager can use all features
        await EnsureAppUserRecordsAsync(db, manager, managerEmail, "Manager", "User", wwwRootPath);

        // â”€â”€ Seed Staff Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var staff = await userManager.FindByEmailAsync(staffEmail);

        if (staff is null)
        {
            staff = new IdentityUser
            {
                UserName       = staffEmail,
                Email          = staffEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(staff, staffPass);
            if (result.Succeeded && !await userManager.IsInRoleAsync(staff, staffRole))
                await userManager.AddToRoleAsync(staff, staffRole);
        }
        else if (!await userManager.IsInRoleAsync(staff, staffRole))
        {
            await userManager.AddToRoleAsync(staff, staffRole);
        }

        // Create app-level records so staff can use all features
        await EnsureAppUserRecordsAsync(db, staff, staffEmail, "Staff", "User", wwwRootPath);
    }

    /// <summary>
    /// Ensures a RegisteredUserModel, UserProfileModel, and ContactDetailModel exist
    /// for the given Identity user. Idempotent â€” skips creation if records already exist.
    /// </summary>
    private static async Task EnsureAppUserRecordsAsync(
        ApplicationDbContext db,
        IdentityUser identityUser,
        string email,
        string firstName,
        string lastName,
        string wwwRootPath)
    {
        // RegisteredUserModel
        var registeredUser = await db.RegisteredUsers
            .FirstOrDefaultAsync(r => r.Email == email);

        if (registeredUser is null)
        {
            registeredUser = new RegisteredUserModel { Email = email };
            db.RegisteredUsers.Add(registeredUser);
            await db.SaveChangesAsync();
        }

        // UserProfileModel
        if (!await db.UserProfiles.AnyAsync(p => p.PkEmail == email))
        {
            var avatarPath = Path.Combine(wwwRootPath, "images", "placeholder.png");
            byte[]? avatarBytes = File.Exists(avatarPath)
                ? await File.ReadAllBytesAsync(avatarPath)
                : null;

            db.UserProfiles.Add(new UserProfileModel
            {
                PkEmail        = email,
                FirstName      = firstName,
                LastName       = lastName,
                AvatarData     = avatarBytes,
                AvatarMimeType = avatarBytes is not null ? "image/png" : null
            });
        }

        // ContactDetailModel (default shipping address)
        if (!await db.ContactDetails.AnyAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId))
        {
            db.ContactDetails.Add(new ContactDetailModel
            {
                FirstName          = firstName,
                LastName           = lastName,
                PhoneNumber        = "(416) 555-0100",
                Street             = "100 Queen St W",
                City               = "Toronto",
                Province           = "Ontario",
                PostCode           = "M5H 2N2",
                Country            = "Canada",
                IsDefault          = true,
                FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                UserId             = identityUser.Id
            });
        }

        await db.SaveChangesAsync();
    }

    #endregion
}
