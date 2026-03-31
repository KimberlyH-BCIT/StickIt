using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELKH.Models;
using ELKH.Services;

namespace ELKH.Controllers.Base;

/// <summary>
/// Base controller for all user-area controllers that require authentication.
/// Provides common user-specific functionality and utilities.
/// </summary>
/// <remarks>
/// This controller extracts common patterns from the original UserController
/// and provides shared functionality for feature-specific user controllers:
/// - UserProfileController
/// - UserAddressController  
/// - UserReviewController
/// 
/// <para><strong>Common Features:</strong></para>
/// <list type="bullet">
/// <item>User authentication validation</item>
/// <item>Current user retrieval utilities</item>
/// <item>Success/error message handling</item>
/// <item>Common dependencies injection</item>
/// </list>
/// </remarks>
[Authorize]
public abstract class UserControllerBase : AuthenticatedControllerBase
{
    protected new readonly IUserService UserService;

    protected UserControllerBase(
        ELKH.Data.ApplicationDbContext db,
        IUserService userService) : base(db, userService)
    {
        UserService = userService;
    }

    /// <summary>
    /// Gets the current authenticated user's registered user ID.
    /// </summary>
    /// <returns>The registered user ID or null if not found.</returns>
    protected new async Task<int?> GetCurrentUserIdAsync()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return null;

        var user = await GetCurrentUserAsync();
        return user?.PkRegisteredUserId;
    }

    /// <summary>
    /// Validates that the current user can access the specified user's data.
    /// </summary>
    /// <param name="targetUserId">The user ID being accessed.</param>
    /// <returns>True if access is allowed, false otherwise.</returns>
    protected async Task<bool> CanAccessUserDataAsync(int targetUserId)
    {
        var currentUserId = await GetCurrentUserIdAsync();
        return currentUserId == targetUserId;
    }
}

/// <summary>
/// Base controller for all admin-area controllers that require admin role.
/// Provides common admin-specific functionality and utilities.
/// </summary>
/// <remarks>
/// This controller extracts common patterns from the original AdminController
/// and provides shared functionality for feature-specific admin controllers:
/// - AdminUserController
/// - AdminAnalyticsController  
/// - AdminSystemController
/// 
/// <para><strong>Common Features:</strong></para>
/// <list type="bullet">
/// <item>Admin role authorization</item>
/// <item>Audit logging utilities</item>
/// <item>Common admin operations</item>
/// <item>Performance monitoring helpers</item>
/// </list>
/// </remarks>
[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : Controller
{
    protected readonly ELKH.Data.ApplicationDbContext Context;
    protected readonly ILogger Logger;

    protected AdminControllerBase(
        ELKH.Data.ApplicationDbContext context,
        ILogger logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Logs an administrative action for auditing purposes.
    /// </summary>
    /// <param name="action">The action being performed.</param>
    /// <param name="details">Additional details about the action.</param>
    protected async Task LogAdminActionAsync(string action, string details = "")
    {
        var adminEmail = User.Identity?.Name ?? "Unknown";
        
        try
        {
            var auditEntry = new AuditEntryModel
            {
                Actor = adminEmail,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                Reason = string.Empty
            };

            Context.AuditEntries.Add(auditEntry);
            await Context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to log admin action: {Action}", action);
        }
    }

    /// <summary>
    /// Sets a success message in TempData for display after redirect.
    /// </summary>
    /// <param name="message">The success message to display.</param>
    protected void SetSuccessMessage(string message)
    {
        TempData["Message"] = $"success,{message}";
    }

    /// <summary>
    /// Sets an error message in TempData for display after redirect.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    protected void SetErrorMessage(string message)
    {
        TempData["Message"] = $"error,{message}";
    }

    /// <summary>
    /// Sets a warning message in TempData for display after redirect.
    /// </summary>
    /// <param name="message">The warning message to display.</param>
    protected void SetWarningMessage(string message)
    {
        TempData["Message"] = $"warning,{message}";
    }
}