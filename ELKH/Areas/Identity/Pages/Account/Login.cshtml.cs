// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ELKH.Configuration;
using ELKH.Models;
using ELKH.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ELKH.Constants;

namespace ELKH.Areas.Identity.Pages.Account
{
    // TABLE OF CONTENTS
    // - Login form display
    // - Login submission
    // - Two-factor redirect
    // - Lockout handling
    // - External login provider display

    /// <summary>
    /// Razor Page model for user login with role-based redirects and security features.
    /// Handles both local (email/password) and external (OAuth) authentication.
    /// </summary>
    /// <remarks>
    /// <para><strong>Login Workflow:</strong></para>
    /// <list type="number">
    /// <item>User submits email/password + completes reCAPTCHA</item>
    /// <item>Validate reCAPTCHA token (prevents bot attacks)</item>
    /// <item>Attempt password sign-in via ASP.NET Core Identity</item>
    /// <item>On success: Determine user role and redirect accordingly:
    ///   <list type="bullet">
    ///   <item>Admin â†’ /Admin/Index (Admin dashboard)</item>
    ///   <item>Manager â†’ /Manager/Index (Manager dashboard)</item>
    ///   <item>Staff â†’ /Home/Index (Home page)</item>
    ///   <item>Customer â†’ /User/Index (Customer dashboard)</item>
    ///   </list>
    /// </item>
    /// <item>On 2FA required: Redirect to LoginWith2fa page</item>
    /// <item>On lockout: Redirect to Lockout page</item>
    /// <item>On failure: Display error message</item>
    /// </list>
    ///
    /// <para><strong>Security Features:</strong></para>
    /// <list type="bullet">
    /// <item>Google reCAPTCHA v2 validation (prevents automated login attacks)</item>
    /// <item>Rate limiting via <c>[EnableRateLimiting(Auth)]</c> policy</item>
    /// <item>Two-factor authentication support (redirects to 2FA page if enabled)</item>
    /// <item>Account lockout support (configurable, currently disabled: lockoutOnFailure: false)</item>
    /// <item>External cookie clearing on GET to ensure clean login state</item>
    /// </list>
    ///
    /// <para><strong>External Login Integration:</strong></para>
    /// Displays configured external authentication providers (Google, Microsoft, etc.)
    /// alongside the standard email/password form.
    /// </remarks>
    public class LoginModel : PageModel
    {
        #region Properties & Dependencies

        // â”€â”€ ASP.NET Core Identity Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        // â”€â”€ ReCaptcha Validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly IReCaptchaService _reCaptcha;
        private readonly ReCaptchaOptions _reCaptchaOptions;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Google reCAPTCHA v2 site key for client-side rendering.
        /// Injected from configuration and passed to the view.
        /// </summary>
        public string ReCaptchaSiteKey { get; set; } = "";

        /// <summary>
        /// Initializes a new instance of <see cref="LoginModel"/> with required dependencies.
        /// </summary>
        /// <param name="signInManager">Handles password sign-in and external authentication.</param>
        /// <param name="logger">Logger for login events and security diagnostics.</param>
        /// <param name="reCaptcha">Service for validating reCAPTCHA tokens.</param>
        /// <param name="reCaptchaOptions">Google reCAPTCHA configuration (site key for client).</param>
        /// <param name="environment">Hosting environment used to alter runtime behavior (Development/Production).</param>
        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            ILogger<LoginModel> logger,
            IReCaptchaService reCaptcha,
            IOptions<ReCaptchaOptions> reCaptchaOptions,
            IWebHostEnvironment environment)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = signInManager.UserManager;

            _reCaptcha = reCaptcha;
            _reCaptchaOptions = reCaptchaOptions.Value;
            _environment = environment;
            ReCaptchaSiteKey = _reCaptchaOptions.SiteKey;
        }

        // ==============================================================================
        // â•‘ Page Properties                                                            â•‘
        // ==============================================================================

        /// <summary>
        /// Form input data bound from the login view.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// List of configured external authentication providers (Google, Microsoft, etc.).
        /// Displayed as social login buttons on the login form.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        /// URL to redirect to after successful login.
        /// Preserves the original destination from query string.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// Error message from external sources (e.g., external login failures).
        /// Stored in TempData to survive redirects.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Input model for login form data.
        /// </summary>
        public class InputModel
        {
            /// <summary>User's email address (used as username).</summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>User's password.</summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>Persistent authentication cookie flag ("Remember me" checkbox).</summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        #endregion

        #region GET Handler

        /// <summary>
        /// Handles GET requests to the login page.
        /// Clears external authentication cookies and loads available external providers.
        /// </summary>
        /// <param name="returnUrl">URL to redirect to after successful login.</param>
        /// <remarks>
        /// <para><strong>Preparation Steps:</strong></para>
        /// <list type="number">
        /// <item>Display any error messages from TempData (e.g., external login failures)</item>
        /// <item>Clear existing external authentication cookies to ensure clean state</item>
        /// <item>Load configured external authentication providers for display</item>
        /// <item>Log reCAPTCHA configuration status for debugging</item>
        /// </list>
        /// </remarks>
        public async Task OnGetAsync(string returnUrl = null)
        {
            // â”€â”€ Display Error Messages â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Show errors from TempData (e.g., external login failures)
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // â”€â”€ Clear External Auth Cookies â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Ensures clean login state by removing any lingering external auth cookies
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // â”€â”€ Load External Providers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;

            // â”€â”€ Debug Logging â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Verify reCAPTCHA site key is loaded for troubleshooting
            _logger.LogInformation("ReCAPTCHA SiteKey loaded: {SiteKey}",
                string.IsNullOrEmpty(ReCaptchaSiteKey) ? "(empty)" : ReCaptchaSiteKey);
        }

        #endregion

        #region POST Handler

        /// <summary>
        /// Handles login form submission with reCAPTCHA validation and role-based redirects.
        /// Protected by rate limiting to prevent brute-force attacks.
        /// </summary>
        /// <param name="returnUrl">URL to redirect to after successful login.</param>
        /// <returns>
        /// On success: Role-based redirect (Admin/Manager/Staff/Customer dashboard).<br/>
        /// On 2FA required: Redirect to LoginWith2fa page.<br/>
        /// On lockout: Redirect to Lockout page.<br/>
        /// On failure: Redisplay form with error message.
        /// </returns>
        /// <remarks>
        /// <para><strong>Authentication Flow:</strong></para>
        /// <list type="number">
        /// <item><strong>reCAPTCHA Validation:</strong> Verify token from form submission</item>
        /// <item><strong>Password Sign-In:</strong> Attempt authentication via ASP.NET Core Identity</item>
        /// <item><strong>Role-Based Redirect:</strong>
        ///   <list type="bullet">
        ///   <item>Admin: /Admin/Index (admin dashboard with user management)</item>
        ///   <item>Manager: /Manager/Index (manager dashboard with orders/inventory)</item>
        ///   <item>Staff: /Home/Index (home page)</item>
        ///   <item>Customer: /User/Index (customer dashboard with orders/wishlist)</item>
        ///   </list>
        /// </item>
        /// <item><strong>Special Cases:</strong>
        ///   <list type="bullet">
        ///   <item>RequiresTwoFactor: Redirect to 2FA verification page</item>
        ///   <item>IsLockedOut: Redirect to lockout notification page</item>
        ///   <item>Invalid credentials: Show error, allow retry</item>
        ///   </list>
        /// </item>
        /// </list>
        ///
        /// <para><strong>Security Notes:</strong></para>
        /// <list type="bullet">
        /// <item>Account lockout is enabled outside Development and remains paired with rate limiting</item>
        /// <item>Rate limiting prevents rapid login attempts</item>
        /// <item>reCAPTCHA prevents automated bot attacks</item>
        /// </list>
        /// </remarks>
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Constants.RateLimitPolicies.Auth)]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            // ==========================================================================
            // â•‘ PHASE 1: reCAPTCHA Validation                                          â•‘
            // ==========================================================================
            var token = Request.Form["g-recaptcha-response"].ToString();
            var ok = await _reCaptcha.VerifyAsync(token, HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!ok)
            {
                ModelState.AddModelError("ReCaptcha", "Please complete the reCAPTCHA.");
                return Page();
            }

            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // ======================================================================
                // â•‘ PHASE 2: Password Sign-In Attempt                                    â•‘
                // ======================================================================
                var lockoutOnFailure = !_environment.IsDevelopment();
                var result = await _signInManager.PasswordSignInAsync(
                    Input.Email,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: lockoutOnFailure);

                if (result.Succeeded)
                {
                    // ==================================================================
                    // â•‘ PHASE 3: Role-Based Redirect                                      â•‘
                    // ==================================================================
                    // Check user roles in priority order: Admin > Manager > Staff > Customer
                    var user = await _userManager.FindByEmailAsync(Input.Email);

                    // â”€â”€ Admin Role Check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Admin", new { area = "" });
                    }

                    // â”€â”€ Manager Role Check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    if (await _userManager.IsInRoleAsync(user, "Manager"))
                    {
                        return RedirectToAction("Index", "Manager", new { area = "" });
                    }

                    // â”€â”€ Staff Role Check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    if (await _userManager.IsInRoleAsync(user, "Staff"))
                    {
                        return RedirectToAction("Index", "Home", new { area = "" });
                    }

                    // â”€â”€ Customer Role Check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    if (await _userManager.IsInRoleAsync(user, "Customer"))
                    {
                        _logger.LogInformation("User {Email} logged in as Customer.", Input.Email);
                        return RedirectToAction("Index", "User", new { area = "" });
                    }

                    await _signInManager.SignOutAsync();
                    _logger.LogWarning("User {Email} logged in successfully but has no recognized application role.", Input.Email);
                    ModelState.AddModelError(string.Empty, "Your account does not have an assigned role. Please contact support.");
                    return Page();
                }

                // ======================================================================
                // â•‘ Special Cases: 2FA and Lockout                                       â•‘
                // ======================================================================
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // Redisplay form with validation errors
            return Page();
        }

        #endregion
    }
}
