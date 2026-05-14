// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace ELKH.Areas.Identity.Pages.Account
{

    /// <summary>
    /// Razor Page model for external authentication (OAuth) login flow.
    /// Handles sign-in via third-party providers like Google, Microsoft, Facebook, etc.
    /// </summary>
    /// <remarks>
    /// <para><strong>OAuth Flow (Multi-Stage Process):</strong></para>
    /// <list type="number">
    /// <item><strong>Initiation (OnPost):</strong> User clicks external provider button â†’ Redirect to provider's OAuth page</item>
    /// <item><strong>Callback (OnGetCallbackAsync):</strong> Provider redirects back with authentication result
    ///   <list type="bullet">
    ///   <item>If user has existing linked account: Sign in immediately</item>
    ///   <item>If new user: Show email confirmation form</item>
    ///   <item>If error from provider: Display error and redirect to Login</item>
    ///   </list>
    /// </item>
    /// <item><strong>Account Creation (OnPostConfirmationAsync):</strong> Create new IdentityUser and link to external login</item>
    /// <item><strong>Email Confirmation:</strong> Send confirmation link (if RequireConfirmedAccount is true)</item>
    /// <item><strong>Sign-In:</strong> Automatic sign-in after successful account creation</item>
    /// </list>
    ///
    /// <para><strong>Supported Providers:</strong></para>
    /// Configured in <c>Program.cs</c> via <c>AddGoogle()</c>, <c>AddMicrosoft()</c>, <c>AddFacebook()</c>, etc.
    /// Each provider requires client ID and secret from configuration.
    ///
    /// <para><strong>Security Notes:</strong></para>
    /// <list type="bullet">
    /// <item>AllowAnonymous attribute required for unauthenticated OAuth callbacks</item>
    /// <item>TwoFactor bypassed for external logins (provider already authenticated user)</item>
    /// <item>Email claim extracted from OAuth provider's identity token</item>
    /// <item>Account lockout supported (redirects to Lockout page if triggered)</item>
    /// </list>
    /// </remarks>
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        #region Properties & Dependencies

        // â”€â”€ ASP.NET Core Identity Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ExternalLoginModel"/> with required dependencies.
        /// </summary>
        /// <param name="signInManager">Handles external authentication and sign-in operations.</param>
        /// <param name="userManager">Manages IdentityUser creation and external login associations.</param>
        /// <param name="userStore">User store abstraction for UserManager operations.</param>
        /// <param name="logger">Logger for OAuth events and errors.</param>
        /// <param name="emailSender">Email service for sending confirmation links.</param>
        public ExternalLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
        }

        // ==============================================================================
        // â•‘ Page Properties                                                            â•‘
        // ==============================================================================

        /// <summary>
        /// Form input data bound from the email confirmation view.
        /// Collected when creating a new account from external login.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// Human-readable name of the external provider (e.g., "Google", "Microsoft").
        /// Displayed to the user during account creation.
        /// </summary>
        public string ProviderDisplayName { get; set; }

        /// <summary>
        /// URL to redirect to after successful authentication.
        /// Preserves the original destination from query string.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// Error message from external provider or internal OAuth processing.
        /// Stored in TempData to survive redirects.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Input model for collecting email address when creating account from external login.
        /// </summary>
        /// <remarks>
        /// Email is typically pre-filled from OAuth provider's email claim.
        /// User can modify it before confirming account creation.
        /// </remarks>
        public class InputModel
        {
            /// <summary>User's email address for the new account.</summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        #endregion

        #region Initial GET/POST Handlers

        /// <summary>
        /// Handles direct GET requests to this page.
        /// Redirects to Login page since external login must be initiated via POST.
        /// </summary>
        /// <returns>Redirect to Login page.</returns>
        public IActionResult OnGet() => RedirectToPage("./Login");

        /// <summary>
        /// Initiates the OAuth challenge by redirecting user to external provider's login page.
        /// </summary>
        /// <param name="provider">Provider name (e.g., "Google", "Microsoft", "Facebook").</param>
        /// <param name="returnUrl">URL to redirect to after successful authentication.</param>
        /// <returns>
        /// ChallengeResult that redirects to the external provider's OAuth authorization endpoint.
        /// </returns>
        /// <remarks>
        /// The callback URL is set to <c>OnGetCallbackAsync</c> on this same page.
        /// Provider will redirect back to that handler after user authenticates.
        /// </remarks>
        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // â”€â”€ Configure OAuth Challenge Properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Sets callback URL and provider-specific authentication properties.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        #endregion

        #region OAuth Callback Handler

        /// <summary>
        /// Callback handler invoked by external OAuth provider after user authentication.
        /// Processes the provider's response and either signs in existing user or prompts for account creation.
        /// </summary>
        /// <param name="returnUrl">Original destination URL to redirect to after sign-in.</param>
        /// <param name="remoteError">Error message from external provider (if authentication failed).</param>
        /// <returns>
        /// <list type="bullet">
        /// <item>If provider error: Redirect to Login with error message</item>
        /// <item>If existing user: Sign in and redirect to returnUrl</item>
        /// <item>If account locked out: Redirect to Lockout page</item>
        /// <item>If new user: Display email confirmation form</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para><strong>Decision Tree:</strong></para>
        /// <list type="number">
        /// <item>Check for remote errors from provider â†’ Redirect to Login if error</item>
        /// <item>Retrieve external login info (email claim, provider key) â†’ Error if missing</item>
        /// <item>Attempt external login sign-in:
        ///   <list type="bullet">
        ///   <item>Success: Log event and redirect to returnUrl</item>
        ///   <item>Locked out: Redirect to Lockout page</item>
        ///   <item>No existing account: Show email confirmation form</item>
        ///   </list>
        /// </item>
        /// </list>
        /// TwoFactor is bypassed (<c>bypassTwoFactor: true</c>) since external provider already authenticated the user.
        /// </remarks>
        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            // ==========================================================================
            // â•‘ PHASE 1: Error Handling                                                â•‘
            // ==========================================================================
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // ==========================================================================
            // â•‘ PHASE 2: Retrieve OAuth Data                                           â•‘
            // ==========================================================================
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // ==========================================================================
            // â•‘ PHASE 3: Attempt Sign-In (Existing User)                               â•‘
            // ==========================================================================
            // Try signing in with this external login provider if user already has linked account.
            // TwoFactor bypassed since external provider already authenticated the user.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                // User already has linked account - sign in successful
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                // Account lockout triggered - redirect to lockout page
                return RedirectToPage("./Lockout");
            }
            else
            {
                // ======================================================================
                // â•‘ PHASE 4: Prompt for Account Creation (New User)                   â•‘
                // ======================================================================
                // User doesn't have an account - show email confirmation form.
                // Pre-fill email from OAuth provider's email claim if available.
                ReturnUrl = returnUrl;
                ProviderDisplayName = info.ProviderDisplayName;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    Input = new InputModel
                    {
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    };
                }
                return Page();
            }
        }

        #endregion

        #region Account Creation Handler

        /// <summary>
        /// Handles account creation confirmation after user provides/confirms email address.
        /// Creates new IdentityUser and links it to the external login provider.
        /// </summary>
        /// <param name="returnUrl">URL to redirect to after successful account creation and sign-in.</param>
        /// <returns>
        /// On success: Sign in and redirect to returnUrl (or RegisterConfirmation if email confirmation required).<br/>
        /// On failure: Redisplay form with validation errors.
        /// </returns>
        /// <remarks>
        /// <para><strong>Account Creation Workflow:</strong></para>
        /// <list type="number">
        /// <item>Retrieve external login info from previous OAuth callback</item>
        /// <item>Create new IdentityUser with provided email</item>
        /// <item>Link external login to the new user account</item>
        /// <item>Generate and send email confirmation link</item>
        /// <item>Sign in user automatically (unless RequireConfirmedAccount is true)</item>
        /// </list>
        ///
        /// <para><strong>Note:</strong></para>
        /// This handler does NOT create RegisteredUserModel, UserProfileModel, or ContactDetailModel
        /// like the standard registration flow. External login users may need to complete
        /// their profile separately.
        /// </remarks>
        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            // ==========================================================================
            // â•‘ Retrieve External Login Info                                           â•‘
            // ==========================================================================
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                // ======================================================================
                // â•‘ Create New Identity User                                           â•‘
                // ======================================================================
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    // ==================================================================
                    // â•‘ Link External Login to New User                                â•‘
                    // ==================================================================
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        // ==============================================================
                        // â•‘ Send Email Confirmation                                    â•‘
                        // ==============================================================
                        var userId = await _userManager.GetUserIdAsync(user);
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = userId, code = code },
                            protocol: Request.Scheme);

                        await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                        // ==============================================================
                        // â•‘ Conditional Sign-In or Confirmation Redirect               â•‘
                        // ==============================================================
                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                        }

                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }

                // â”€â”€ Add Identity Validation Errors to ModelState â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Redisplay form with validation errors
            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Factory method for creating a new <see cref="IdentityUser"/> instance.
        /// Required by scaffolded Identity UI infrastructure.
        /// </summary>
        /// <returns>New IdentityUser instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if IdentityUser cannot be instantiated (abstract class or missing parameterless constructor).
        /// </exception>
        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        /// <summary>
        /// Retrieves the email store from the user store and validates email support.
        /// Required by scaffolded Identity UI infrastructure for email-based account creation.
        /// </summary>
        /// <returns>Email store interface for setting user email.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown if the configured user store does not support email operations.
        /// </exception>
        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }

        #endregion
    }
}
