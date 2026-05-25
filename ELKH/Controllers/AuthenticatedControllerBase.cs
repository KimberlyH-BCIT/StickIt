using ELKH.Data;
using ELKH.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Base controller for authenticated endpoints. Inherits cart-count injection
    /// from <see cref="BaseController"/> and adds user-identity helper methods.
    /// </summary>
    [Authorize]
    public abstract class AuthenticatedControllerBase : BaseController
    {
        private readonly IUserService _userService;

        protected IUserService UserService => _userService;

        protected AuthenticatedControllerBase(ApplicationDbContext db, IUserService userService)
            : base(db)
        {
            _userService = userService;
        }

        /// <summary>
        /// Get the current user's email from the authentication context.
        /// Returns null if the user is not authenticated.
        /// </summary>
        protected string? GetCurrentUserEmail()
        {
            return User.Identity?.Name;
        }

        /// <summary>
        /// Get the current authenticated user's email, or return a Challenge result.
        /// Use this when you need to ensure the user is authenticated.
        /// </summary>
        /// <param name="email">The authenticated user's email if successful</param>
        /// <returns>True if authenticated, false otherwise</returns>
        protected bool TryGetCurrentUserEmail(out string email)
        {
            email = User.Identity?.Name ?? string.Empty;
            return !string.IsNullOrEmpty(email);
        }

        /// <summary>
        /// Get the current authenticated user model from the UserService.
        /// Returns null if not authenticated or user not found in database.
        /// Uses caching via UserService for performance.
        /// </summary>
        protected async Task<Models.RegisteredUserModel?> GetCurrentUserAsync()
        {
            var email = GetCurrentUserEmail();
            if (string.IsNullOrEmpty(email))
                return null;

            return await UserService.GetByEmailAsync(email);
        }

        /// <summary>
        /// Get the current user's ID, or return null if not authenticated.
        /// </summary>
        protected async Task<int?> GetCurrentUserIdAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.PkRegisteredUserId;
        }

        /// <summary>
        /// Ensure the user is authenticated and return their email.
        /// If not authenticated, sets an error message and returns Challenge.
        /// </summary>
        protected IActionResult? RequireAuthenticatedUser(out string userEmail)
        {
            if (!TryGetCurrentUserEmail(out userEmail))
            {
                SetErrorMessage("You must be logged in to perform this action.");
                return Challenge();
            }
            return null;
        }

        /// <summary>
        /// Set a success message in TempData following the application's convention.
        /// Format: "success, {message}"
        /// </summary>
        protected void SetSuccessMessage(string message)
        {
            TempData["Message"] = $"success, {message}";
        }

        /// <summary>
        /// Set an error message in TempData following the application's convention.
        /// Format: "danger, {message}" - matches the Bootstrap alert-danger class.
        /// </summary>
        protected void SetErrorMessage(string message)
        {
            TempData["Message"] = $"danger, {message}";
        }

        /// <summary>
        /// Set a warning message in TempData following the application's convention.
        /// Format: "warning, {message}"
        /// </summary>
        protected void SetWarningMessage(string message)
        {
            TempData["Message"] = $"warning, {message}";
        }

        /// <summary>
        /// Redirect to the previous page if Referer header exists, otherwise redirect to specified action.
        /// </summary>
        protected IActionResult RedirectToRefererOrAction(string actionName, string? controllerName = null)
        {
            if (Request.Headers.TryGetValue("Referer", out var refererValue))
            {
                return Redirect(refererValue.ToString());
            }

            return controllerName != null
                ? RedirectToAction(actionName, controllerName)
                : RedirectToAction(actionName);
        }
    }
}
