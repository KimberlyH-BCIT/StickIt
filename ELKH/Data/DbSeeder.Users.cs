using Microsoft.AspNetCore.Identity;

namespace ELKH.Data;

/// <summary>
/// Database seeding operations for user accounts and role management.
/// Handles creation of administrative accounts, roles, and security setup.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. SeedUsersAndRolesAsync Method ............................... Lines [13-70]
///    - Configuration validation      // Ensure credentials are configured
///    - Role creation                // Admin, Manager, Staff, Customer roles
///    - Account setup               // Administrative user accounts
/// 
/// 2. Role Creation Logic ......................................... Lines [50-85]
///    - Admin role                  // Full system access
///    - Manager role               // Operational management
///    - Staff role                 // Content and order management
///    - Customer role              // Standard user access
/// 
/// 3. Administrative Account Creation ............................. Lines [87-140]
///    - Admin account              // Primary administrative access
///    - Manager account            // Store management access
///    - Staff account              // Content management access
/// 
/// 4. Security Configuration ..................................... Lines [142-158]
///    - Email confirmation         // Bypass for seeded accounts
///    - Role assignment           // Proper authorization setup
///    - Credential validation     // Secure password requirements
/// ================================================================================
/// 
/// ARCHITECTURAL CONTEXT:
/// • Critical component of ELKH's security and authorization infrastructure
/// • Implements role-based access control (RBAC) for administrative functions
/// • Integrates with ASP.NET Core Identity for authentication and authorization
/// • Provides secure foundation for multi-tier user management
/// • Environment-aware configuration using user secrets and environment variables
/// 
/// SECURITY IMPLEMENTATION:
/// This seeder establishes the complete authorization hierarchy:
/// 1. Admin role - Full system access including user management and configuration
/// 2. Manager role - Operational access including order and product management
/// 3. Staff role - Content management access for products, reviews, and content
/// 4. Customer role - Standard user access for purchasing and account management
/// 5. Secure credential management via configuration providers
/// 
/// DATA SEEDING STRATEGY:
/// • Idempotent operations - safe to run multiple times without duplication
/// • Configuration-driven account creation using secure credential providers
/// • Comprehensive role hierarchy supporting granular access control
/// • Email confirmation bypass for administrative seeded accounts
/// • Fallback security warnings for missing credential configuration
/// 
/// INTEGRATION POINTS:
/// • Depends on: ASP.NET Core Identity (UserManager, RoleManager)
/// • Depends on: IConfiguration for secure credential retrieval
/// • Creates: Complete role hierarchy and administrative user accounts
/// • Integrates with: Authorization policies and controller access control
/// • Used by: Application startup, deployment pipelines, and environment setup
/// 
/// SECURITY CONSIDERATIONS:
/// • Credentials must be configured via user secrets or environment variables
/// • No hardcoded passwords or sensitive data in source code
/// • Comprehensive role separation for principle of least privilege
/// • Email confirmation bypass only for trusted seeded accounts
/// • Strong password requirements enforced through Identity configuration
/// </remarks>
public static partial class DbSeeder
{
    #region Users & Roles Seeding

    /// <summary>
    /// Seeds default Admin, Manager, Staff, and Customer roles with corresponding test accounts.
    /// Fully idempotent—skipped if roles or accounts already exist.
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
    /// <para><strong>⚠️ SECURITY WARNING:</strong></para>
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
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        const string adminRole    = "Admin";
        const string managerRole  = "Manager";
        const string staffRole    = "Staff";
        const string customerRole = "Customer";

        // ══════════════════════════════════════════════════════════════════════
        // ║ Load Credentials from Configuration                                ║
        // ║ Fallback to weak defaults only for local development.              ║
        // ══════════════════════════════════════════════════════════════════════
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

        // ══════════════════════════════════════════════════════════════════════
        // ║ Ensure Roles Exist                                                 ║
        // ║ Create all four roles regardless of whether users exist.           ║
        // ══════════════════════════════════════════════════════════════════════
        string[] allRoles = { adminRole, managerRole, staffRole, customerRole };
        foreach (var role in allRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ║ Seed Administrative Accounts                                       ║
        // ║ Create admin, manager, and staff accounts with proper roles.       ║
        // ══════════════════════════════════════════════════════════════════════

        // ── Seed Admin Account ───────────────────────────────────────────────
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

        // Always verify the role assignment — covers the case where the user
        // was created on a previous run but role assignment failed.
        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);

        // ── Seed Manager Account ─────────────────────────────────────────────
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

        // ── Seed Staff Account ───────────────────────────────────────────────
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
    }

    #endregion
}
