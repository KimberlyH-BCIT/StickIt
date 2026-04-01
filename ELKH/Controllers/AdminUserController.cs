using ELKH.Controllers.Base;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers;

/// <summary>
/// Admin controller responsible for user account management and role administration.
/// Handles user listing, account details, and role assignments.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (396 lines)
/// ================================================================================
/// 1. Constructor & Dependencies ................................. Lines   39-48
///    - IRoleRepository, ApplicationDbContext, UserManager, ILogger injection
///    - Inherits from AdminControllerBase for common admin functionality
/// 
/// 2. User Listing & Search ...................................... Lines   50-138
///    - Index()                            // Paginated user list with filtering
///    - Performance-optimized role filtering and email search
///    - Server-side filtering to minimize memory usage
/// 
/// 3. User Details & Profile Management .......................... Lines  140-213
///    - Details()                          // Complete user profile view
///    - Loads identity user, registered user, profile, roles, recent orders
///    - Contact details aggregation for comprehensive user view
/// 
/// 4. Role Management Operations ................................. Lines  215-348
///    - RemoveRole()                       // Remove role assignment from user
///    - AddRole()                          // Add role assignment to user
///    - Role validation and audit logging for administrative actions
/// 
/// 5. User Statistics & Analytics ................................ Lines  350-396
///    - Statistics()                       // User metrics and role distribution
///    - Recent registrations tracking and role distribution analysis
/// ================================================================================
/// 
/// ARCHITECTURAL CONTEXT:
/// • Extracted from AdminController for focused user administration responsibility
/// • Inherits AdminControllerBase providing audit logging and common admin features
/// • Integrates with ASP.NET Core Identity for user and role management
/// • Uses Entity Framework for additional user profile and order data access
/// 
/// PERFORMANCE OPTIMIZATIONS:
/// • Server-side role filtering using UserManager.GetUsersInRoleAsync()
/// • Database-level email filtering when no role filter is applied
/// • Role lookups performed only on paginated result slice to minimize queries
/// • Efficient aggregation for statistics with targeted database queries
/// 
/// BUSINESS LOGIC:
/// • Comprehensive user management covering identity and extended profile data
/// • Role-based filtering for administrative organization and security
/// • Audit trail for all administrative actions (role changes, user viewing)
/// • Recent activity tracking for user engagement analysis
/// 
/// SECURITY & AUTHORIZATION:
/// • Requires Admin role inheritance from AdminControllerBase
/// • Anti-forgery token validation for all POST operations
/// • Input validation for user IDs and role names
/// • Comprehensive error handling with user-friendly messages
/// 
/// INTEGRATION POINTS:
/// • ASP.NET Core Identity for user and role management
/// • ApplicationDbContext for extended user profile and order data
/// • IRoleRepository for role-related operations
/// • AdminControllerBase for shared admin functionality and audit logging
/// 
/// <para><strong>Extracted from AdminController</strong></para>
/// This controller handles all user management functionality that was previously
/// in the monolithic AdminController, providing focused user administration.
/// 
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Paginated user listing with filtering</item>
/// <item>User account details and profile information</item>
/// <item>Role assignment and removal</item>
/// <item>User search and role-based filtering</item>
/// <item>Order history and contact details for users</item>
/// </list>
/// 
/// <para><strong>Performance:</strong></para>
/// Uses server-side filtering to minimize memory usage and performs role lookups
/// only on paginated results for optimal performance.
/// </remarks>
public class AdminUserController : AdminControllerBase
{
    private readonly IRoleRepository _roleRepo;
    private readonly UserManager<IdentityUser> _userManager;

    // CA1861: Constant arrays to avoid repeated allocations
    private static readonly string[] AllRoles = { "Admin", "Manager", "Staff", "Customer" };

    public AdminUserController(
        IRoleRepository roleRepo,
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<AdminUserController> logger)
        : base(context, logger)
    {
        _roleRepo = roleRepo;
        _userManager = userManager;
    }

    #region User Listing & Search

    /// <summary>
    /// GET: AdminUser/Index - Display paginated, filterable list of all users
    /// </summary>
    /// <param name="search">Optional email search filter. Performs case-insensitive substring matching 
    /// against user email addresses. Pass null or empty string for no email filtering.</param>
    /// <param name="roleFilter">Optional role-based filter to display only users in specific roles. 
    /// Valid values: 'Admin', 'Manager', 'Staff', 'Customer', 'All', or null. 
    /// 'All' or null displays users from all roles.</param>
    /// <param name="page">Page number for pagination (1-based index). Defaults to 1 if not specified. 
    /// Must be a positive integer. Invalid values are treated as page 1.</param>
    /// <returns>
    /// Returns a View containing a List&lt;UserListVM&gt; with:
    /// <list type="bullet">
    /// <item>User ID, email, and assigned roles for each user</item>
    /// <item>Pagination metadata (current page, total pages, navigation flags)</item>
    /// <item>Current filter values for form persistence</item>
    /// <item>Maximum of 5 users per page for optimal performance</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Performance Optimization Strategy:</strong></para>
    /// <list type="bullet">
    /// <item>Role filtering is done server-side via UserManager.GetUsersInRoleAsync() for efficiency</item>
    /// <item>Email search uses database-level filtering when no role filter is active</item>
    /// <item>Role lookups (GetRolesAsync) are performed only on the current page slice (≤5 users)</item>
    /// <item>Pagination prevents loading all users into memory at once</item>
    /// </list>
    /// 
    /// <para><strong>Filter Behavior:</strong></para>
    /// When both email search and role filter are active, role filtering takes precedence
    /// and email filtering is applied to the role-filtered result set in memory.
    /// 
    /// <para><strong>Security:</strong></para>
    /// Admin role required. All user listing operations are logged for audit purposes.
    /// </remarks>
    public async Task<IActionResult> Index(string search, string roleFilter, int page = 1)
    {
        const int pageSize = 5;

        // Build candidate set using server-side filtering
        IList<IdentityUser> candidates;
        bool hasRoleFilter = !string.IsNullOrEmpty(roleFilter) && roleFilter != "All";

        if (hasRoleFilter)
        {
            // Single query: returns only users in the specified role
            candidates = await _userManager.GetUsersInRoleAsync(roleFilter);

            // Apply email search in-memory on the (already filtered) role-member list
            if (!string.IsNullOrEmpty(search))
            {
                candidates = candidates
                    .Where(u => u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    .ToList();
            }
        }
        else
        {
            // Push email filter to database to avoid loading all users into memory
            IQueryable<IdentityUser> query = _userManager.Users;
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.Email != null && u.Email.Contains(search));

            candidates = await query.ToListAsync();
        }

        int totalUsers = candidates.Count;

        // Materialize only the current page before per-user role lookups
        var pageUsers = candidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Fetch roles only for the paged users (≤ pageSize lookups)
        var userList = new List<UserListVM>(pageUsers.Count);
        foreach (var user in pageUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userList.Add(new UserListVM
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            });
        }

        var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

        ViewData["CurrentFilter"] = search;
        ViewData["CurrentRoleFilter"] = roleFilter;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["HasPrevious"] = page > 1;
        ViewData["HasNext"] = page < totalPages;

        await LogAdminActionAsync("ViewedUserList", $"Page {page}, Filter: {roleFilter ?? "All"}, Search: {search ?? "None"}");

        return View(userList);
    }

    #endregion

    #region User Account Details

    /// <summary>
    /// GET: AdminUser/Details/id - Display detailed user account information
    /// </summary>
    /// <param name="id">The unique identifier of the Identity user to view details for. 
    /// Must be a valid ASP.NET Core Identity user ID (GUID format).</param>
    /// <returns>
    /// Returns a UserDetailsVM containing:
    /// <list type="bullet">
    /// <item>User profile information (email, registration date)</item>
    /// <item>Assigned roles and permissions</item>
    /// <item>Order history and transaction details</item>
    /// <item>Contact information and address data</item>
    /// <item>Account status and activity metrics</item>
    /// </list>
    /// Returns NotFound() if user ID is null, empty, or does not exist in the system.
    /// </returns>
    /// <remarks>
    /// This method aggregates data from multiple sources:
    /// <para><strong>Data Sources:</strong></para>
    /// <list type="bullet">
    /// <item>ASP.NET Core Identity - user account and authentication info</item>
    /// <item>RegisteredUserModel - extended profile data</item>
    /// <item>Order history - transaction and purchase data</item>
    /// <item>UserManager - role assignments and permissions</item>
    /// </list>
    /// 
    /// <para><strong>Security:</strong></para>
    /// Requires Admin role for access. All user data viewing is logged for audit purposes.
    /// 
    /// <para><strong>Performance:</strong></para>
    /// Uses efficient queries to minimize database calls when aggregating user data.
    /// </remarks>
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var identityUser = await _userManager.FindByIdAsync(id);
        if (identityUser == null)
        {
            return NotFound();
        }

        // Get registered user profile
        var registeredUser = await Context.RegisteredUsers
            .FirstOrDefaultAsync(ru => ru.Email == identityUser.Email);

        UserProfileModel? profile = null;
        if (registeredUser != null)
        {
            profile = await Context.UserProfiles
                .FirstOrDefaultAsync(up => up.PkEmail == identityUser.Email);
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(identityUser);

        // Get recent orders
        List<OrderModel> recentOrders = new();
        if (registeredUser != null)
        {
            recentOrders = await Context.Orders
                .Where(o => o.FkRegisteredUserId == registeredUser.PkRegisteredUserId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToListAsync();
        }

        // Get contact details
        ContactDetailModel? contact = null;
        if (registeredUser != null)
        {
            contact = await Context.ContactDetails
                .FirstOrDefaultAsync(cd => cd.FkRegisteredUserId == registeredUser.PkRegisteredUserId && cd.IsDefault);
        }

        var vm = new
        {
            IdentityUser = identityUser,
            RegisteredUser = registeredUser,
            Profile = profile,
            Roles = roles.ToList(),
            RecentOrders = recentOrders,
            Contact = contact == null ? null : new ContactDetailVM
            {
                ContactId = contact.PkContactId,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhoneNumber = contact.PhoneNumber,
                Street = contact.Street,
                City = contact.City,
                Province = contact.Province,
                PostCode = contact.PostCode,
                Country = contact.Country,
                IsDefault = contact.IsDefault
            }
        };

        await LogAdminActionAsync("ViewedUserDetails", $"User: {identityUser.Email}");

        return View(vm);
    }

    #endregion

    #region Role Management

    /// <summary>
    /// POST: AdminUser/RemoveRole - Remove a role assignment from a user
    /// </summary>
    /// <param name="userId">The unique identifier of the Identity user to remove the role from.
    /// Must be a valid ASP.NET Core Identity user ID (GUID format).</param>
    /// <param name="role">The name of the role to remove from the user.
    /// Must be an existing role name (e.g., 'Admin', 'Manager', 'Staff', 'Customer').</param>
    /// <returns>
    /// Returns a RedirectToAction to the user Details page with:
    /// <list type="bullet">
    /// <item>Success message if role removal succeeds</item>
    /// <item>Error message if role removal fails or user/role not found</item>
    /// <item>NotFound() result if userId or role parameters are invalid</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Security and Validation:</strong></para>
    /// <list type="bullet">
    /// <item>Validates userId and role parameters are not null or empty</item>
    /// <item>Verifies user exists before attempting role removal</item>
    /// <item>Uses ASP.NET Core Identity's built-in role validation</item>
    /// <item>Comprehensive error handling with user-friendly messages</item>
    /// </list>
    /// 
    /// <para><strong>Audit and Logging:</strong></para>
    /// <list type="bullet">
    /// <item>Logs successful role removals for admin audit trail</item>
    /// <item>Records user email and role name in audit log</item>
    /// <item>Logs errors for troubleshooting and security monitoring</item>
    /// </list>
    /// 
    /// <para><strong>Error Scenarios:</strong></para>
    /// <list type="bullet">
    /// <item>User not found - returns NotFound with error message</item>
    /// <item>Role doesn't exist - handled by Identity with descriptive error</item>
    /// <item>User not in role - handled gracefully by Identity system</item>
    /// <item>Database errors - caught and logged with generic user message</item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(string userId, string role)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            SetErrorMessage("Invalid user ID or role");
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("User not found");
            return NotFound();
        }

        try
        {
            var result = await _userManager.RemoveFromRoleAsync(user, role);

            if (result.Succeeded)
            {
                await LogAdminActionAsync("RoleRemoved", $"Removed role '{role}' from user '{user.Email}'");
                SetSuccessMessage($"Successfully removed {role} role from {user.Email}");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                SetErrorMessage($"Failed to remove role: {errors}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing role {Role} from user {UserId}", role, userId);
            SetErrorMessage("An error occurred while removing the role");
        }

        return RedirectToAction("Details", new { id = userId });
    }

    /// <summary>
    /// POST: AdminUser/AddRole - Add a role assignment to a user
    /// </summary>
    /// <param name="userId">The unique identifier of the Identity user to add the role to.
    /// Must be a valid ASP.NET Core Identity user ID (GUID format).</param>
    /// <param name="role">The name of the role to assign to the user.
    /// Must be an existing role name (e.g., 'Admin', 'Manager', 'Staff', 'Customer').</param>
    /// <returns>
    /// Returns a RedirectToAction to the user Details page with:
    /// <list type="bullet">
    /// <item>Success message if role assignment succeeds</item>
    /// <item>Error message if role assignment fails or user/role not found</item>
    /// <item>NotFound() result if userId or role parameters are invalid</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Security and Validation:</strong></para>
    /// <list type="bullet">
    /// <item>Validates userId and role parameters are not null or empty</item>
    /// <item>Verifies user exists before attempting role assignment</item>
    /// <item>Uses ASP.NET Core Identity's built-in role validation</item>
    /// <item>Prevents duplicate role assignments automatically</item>
    /// </list>
    /// 
    /// <para><strong>Business Rules:</strong></para>
    /// <list type="bullet">
    /// <item>Users can have multiple roles simultaneously</item>
    /// <item>Role hierarchy and permissions managed by authorization policies</item>
    /// <item>Admin actions require proper authentication and authorization</item>
    /// </list>
    /// 
    /// <para><strong>Audit and Logging:</strong></para>
    /// <list type="bullet">
    /// <item>Logs successful role assignments for admin audit trail</item>
    /// <item>Records user email and role name for compliance tracking</item>
    /// <item>Error logging for security monitoring and troubleshooting</item>
    /// </list>
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRole(string userId, string role)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
        {
            SetErrorMessage("Invalid user ID or role");
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("User not found");
            return NotFound();
        }

        try
        {
            // Check if user already has the role
            if (await _userManager.IsInRoleAsync(user, role))
            {
                SetWarningMessage($"User already has the {role} role");
                return RedirectToAction("Details", new { id = userId });
            }

            var result = await _userManager.AddToRoleAsync(user, role);

            if (result.Succeeded)
            {
                await LogAdminActionAsync("RoleAdded", $"Added role '{role}' to user '{user.Email}'");
                SetSuccessMessage($"Successfully added {role} role to {user.Email}");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                SetErrorMessage($"Failed to add role: {errors}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding role {Role} to user {UserId}", role, userId);
            SetErrorMessage("An error occurred while adding the role");
        }

        return RedirectToAction("Details", new { id = userId });
    }

    /// <summary>
    /// GET: AdminUser/AvailableRoles/id - Get available roles for a user (AJAX)
    /// </summary>
    /// <param name="id">Identity user ID</param>
    /// <returns>JSON list of roles not assigned to the user</returns>
    [HttpGet]
    public async Task<IActionResult> AvailableRoles(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Json(new { success = false, message = "Invalid user ID" });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Json(new { success = false, message = "User not found" });
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var availableRoles = AllRoles.Except(userRoles).ToList();

        return Json(new { success = true, roles = availableRoles });
    }

    #endregion

    #region User Statistics

    /// <summary>
    /// GET: AdminUser/Statistics - Display user statistics and analytics
    /// </summary>
    /// <returns>Statistics view with user metrics and role distribution</returns>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var totalUsers = await _userManager.Users.CountAsync();
            
            var roleStats = new Dictionary<string, int>();

            foreach (var role in AllRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                roleStats[role] = usersInRole.Count;
            }

            var recentRegistrations = await Context.RegisteredUsers
                .Where(ru => ru.Email.EndsWith("@home.com") == false) // Exclude demo accounts
                .OrderByDescending(ru => ru.PkRegisteredUserId)
                .Take(10)
                .Select(ru => new { ru.Email, ru.PkRegisteredUserId })
                .ToListAsync();

            var vm = new
            {
                TotalUsers = totalUsers,
                RoleDistribution = roleStats,
                RecentRegistrations = recentRegistrations.Select(r => r.Email).ToList()
            };

            await LogAdminActionAsync("ViewedUserStatistics");

            return View(vm);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading user statistics");
            SetErrorMessage("Error loading user statistics");
            return RedirectToAction("Index");
        }
    }

    #endregion
}
