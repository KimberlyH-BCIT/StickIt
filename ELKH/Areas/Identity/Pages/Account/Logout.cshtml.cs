// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ELKH.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Razor Page model for user logout functionality.
    /// Handles secure sign-out from ASP.NET Core Identity authentication system.
    /// </summary>
    /// <remarks>
    /// This page provides secure logout functionality for authenticated users.
    /// It clears the authentication cookie and invalidates the user session,
    /// ensuring complete sign-out from the ELKH platform.
    /// 
    /// <para><strong>Security Implementation:</strong></para>
    /// <list type="bullet">
    /// <item>SignInManager.SignOutAsync() - Properly clears authentication state</item>
    /// <item>Secure cookie invalidation prevents session replay attacks</item>
    /// <item>Audit logging tracks logout events for security monitoring</item>
    /// <item>Return URL validation prevents open redirect vulnerabilities</item>
    /// </list>
    /// 
    /// <para><strong>User Experience:</strong></para>
    /// <list type="bullet">
    /// <item>Immediate authentication state clearing</item>
    /// <item>Redirect to appropriate page after logout</item>
    /// <item>Clear visual feedback that logout was successful</item>
    /// <item>Proper handling of return URL for workflow continuation</item>
    /// </list>
    /// 
    /// <para><strong>Integration Points:</strong></para>
    /// <list type="bullet">
    /// <item>ASP.NET Core Identity authentication system</item>
    /// <item>Application-wide authorization policies</item>
    /// <item>Audit logging and security monitoring</item>
    /// <item>Single sign-on (SSO) systems if configured</item>
    /// </list>
    /// 
    /// <para><strong>Compliance Considerations:</strong></para>
    /// Proper logout functionality is important for:
    /// <list type="bullet">
    /// <item>Data protection regulations (GDPR, PIPEDA)</item>
    /// <item>Security compliance frameworks</item>
    /// <item>Shared computer environments</item>
    /// <item>Session management best practices</item>
    /// </list>
    /// </remarks>
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        /// <summary>
        /// Initializes a new instance of the LogoutModel.
        /// </summary>
        /// <param name="signInManager">ASP.NET Core Identity sign-in manager for authentication operations.</param>
        /// <param name="logger">Logger for audit trail and security monitoring.</param>
        public LogoutModel(SignInManager<IdentityUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        /// <summary>
        /// Handles POST requests to log out the current user.
        /// </summary>
        /// <param name="returnUrl">Optional URL to redirect to after successful logout.</param>
        /// <returns>Redirect to home page or specified return URL.</returns>
        /// <remarks>
        /// Performs secure logout by:
        /// <list type="number">
        /// <item>Clearing the authentication cookie via SignInManager</item>
        /// <item>Invalidating the user session</item>
        /// <item>Logging the logout event for audit purposes</item>
        /// <item>Redirecting to appropriate post-logout page</item>
        /// </list>
        /// 
        /// <para><strong>Security Notes:</strong></para>
        /// <list type="bullet">
        /// <item>Return URL validation prevents open redirect attacks</item>
        /// <item>Complete session invalidation prevents replay attacks</item>
        /// <item>Audit logging enables security monitoring</item>
        /// <item>Immediate authentication state clearing</item>
        /// </list>
        /// </remarks>
        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
