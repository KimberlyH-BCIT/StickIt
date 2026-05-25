// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Configuration;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ELKH.Constants;

namespace ELKH.Areas.Identity.Pages.Account
{
    // TABLE OF CONTENTS
    // - Registration flow
    // - Input validation
    // - Account creation
    // - Profile seeding
    // - Email confirmation

    /// <summary>
    /// Razor Page model for new user registration with extended profile creation.
    /// Extends the scaffolded ASP.NET Core Identity registration flow to create
    /// four database records on successful sign-up:
    /// </summary>
    /// <remarks>
    /// <para><strong>Registration Workflow (Multi-Record Creation):</strong></para>
    /// <list type="number">
    /// <item><strong>IdentityUser:</strong> ASP.NET Core Identity account (email/password)</item>
    /// <item><strong>RegisteredUserModel:</strong> Links Identity user to application data</item>
    /// <item><strong>UserProfileModel:</strong> Stores first/last name for profile header display</item>
    /// <item><strong>ContactDetailModel:</strong> Default shipping address from registration form</item>
    /// </list>
    ///
    /// <para><strong>Security Features:</strong></para>
    /// <list type="bullet">
    /// <item>Rate limiting via <c>[EnableRateLimiting(Auth)]</c> prevents registration spam</item>
    /// <item>Google reCAPTCHA v2 integration (site key injected from configuration)</item>
    /// <item>Email confirmation requirement (configurable via RequireConfirmedAccount)</item>
    /// <item>Password strength validation (6-100 chars, complexity rules from Identity)</item>
    /// </list>
    ///
    /// <para><strong>Post-Registration Flow:</strong></para>
    /// If <c>RequireConfirmedAccount</c> is true: Redirect to RegisterConfirmation page.<br/>
    /// If false: Sign in immediately and redirect to Product catalog (Index).
    ///
    /// <para><strong>Role Assignment:</strong></para>
    /// All new registrants are automatically assigned the "Customer" role.
    /// </remarks>
    public class RegisterModel : PageModel
    {
        #region Properties & Dependencies

        // â”€â”€ ASP.NET Core Identity Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        // â”€â”€ Application Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly ApplicationDbContext _context;
        private readonly IContactDetailRepo _contactRepository;

        // â”€â”€ ReCaptcha Configuration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly ReCaptchaOptions _reCaptchaOptions;

        /// <summary>
        /// Google reCAPTCHA v2 site key for client-side rendering.
        /// Injected from configuration and passed to the view.
        /// </summary>
        public string ReCaptchaSiteKey { get; set; } = "";

        /// <summary>
        /// Initializes a new instance of <see cref="RegisterModel"/> with required dependencies.
        /// </summary>
        /// <param name="userManager">ASP.NET Core Identity UserManager for account creation.</param>
        /// <param name="roleManager">ASP.NET Core Identity RoleManager used to assign roles (e.g. "Customer").</param>
        /// <param name="userStore">User store abstraction for UserManager operations.</param>
        /// <param name="signInManager">Handles sign-in after successful registration (if email confirmation not required).</param>
        /// <param name="logger">Logger for registration events and errors.</param>
        /// <param name="emailSender">Email service for sending confirmation links.</param>
        /// <param name="context">Database context for creating RegisteredUserModel and UserProfileModel.</param>
        /// <param name="contactRepository">Repository for creating default shipping address.</param>
        /// <param name="reCaptchaOptions">Google reCAPTCHA configuration (site key for client-side integration).</param>
        public RegisterModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IContactDetailRepo contactRepository,
            IOptions<ReCaptchaOptions> reCaptchaOptions
        )
        {
            _userManager = userManager;
            _roleManager = roleManager;

            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _contactRepository = contactRepository;

            _reCaptchaOptions = reCaptchaOptions.Value;
            ReCaptchaSiteKey = _reCaptchaOptions.SiteKey;
        }

        // ==========================================================================
        // â•‘ Input Model & View Properties                                          â•‘
        // ==========================================================================

        /// <summary>
        /// Form input data bound from the registration view.
        /// Includes Identity fields (Email/Password) plus extended profile and address fields.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// URL to redirect to after successful registration.
        /// Preserves the original destination from query string.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// List of configured external authentication providers (Google, Microsoft, etc.).
        /// Displayed on the registration form for social login options.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        /// Input model for registration form data.
        /// Extends standard Identity fields (Email/Password) with profile and shipping address.
        /// </summary>
        /// <remarks>
        /// <para><strong>Field Groups:</strong></para>
        /// <list type="bullet">
        /// <item><strong>Identity Fields:</strong> Email, Password, ConfirmPassword (ASP.NET Core Identity requirements)</item>
        /// <item><strong>Profile Fields:</strong> FirstName, LastName (stored in UserProfileModel for UI display)</item>
        /// <item><strong>Contact Fields:</strong> PhoneNumber, Street, City, Province, PostCode, Country (stored in ContactDetailModel as default shipping address)</item>
        /// </list>
        /// All fields are required except Country (defaults to "Canada").
        /// </remarks>
        public class InputModel
        {
            // â”€â”€ Identity Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            /// <summary>User's email address (also used as username).</summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>Account password (6-100 characters with complexity requirements).</summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>Password confirmation (must match Password field).</summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            // â”€â”€ Profile Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            /// <summary>User's first name (displayed in profile header and account pages).</summary>            [Required]
            [MaxLength(100)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            // â”€â”€ Contact & Shipping Address Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            /// <summary>User's phone number (used for order notifications and shipping contact).</summary>
            [Required]
            [Phone]
            [DataType(DataType.PhoneNumber)]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            [Required]
            [MaxLength(200)]
            [Display(Name = "Street Address")]
            public string Street { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "City")]
            public string City { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "Province/State")]
            public string Province { get; set; }

            [Required]
            [MaxLength(20)]
            [Display(Name = "Postal Code")]
            public string PostCode { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "Country")]
            public string Country { get; set; } = "Canada";
        }

        #endregion

        #region GET/POST Handlers

        /// <summary>
        /// Handles GET requests to the registration page.
        /// Populates external login providers and stores the return URL for the view.
        /// </summary>
        /// <param name="returnUrl">URL to redirect to after successful registration.</param>
        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        /// <summary>
        /// Handles POST request for user registration form submission.
        /// Creates multiple database records and sends email confirmation.
        /// Protected by rate limiting to prevent registration spam.
        /// </summary>
        /// <param name="returnUrl">URL to redirect to after successful registration.</param>
        /// <returns>
        /// On success: RedirectToPage("RegisterConfirmation") if email confirmation required,
        /// otherwise RedirectToAction("Index", "Product") after automatic sign-in.
        /// On failure: Returns page with validation errors.
        /// </returns>
        /// <remarks>
        /// <para><strong>Multi-Record Creation Workflow:</strong></para>
        /// <list type="number">
        /// <item><strong>IdentityUser:</strong> Create ASP.NET Core Identity account with email/password</item>
        /// <item><strong>RegisteredUserModel:</strong> Create app-level user record (links Identity to business data)</item>
        /// <item><strong>UserProfileModel:</strong> Create profile with first/last name (for UI greeting)</item>
        /// <item><strong>ContactDetailModel:</strong> Create default shipping address from form data</item>
        /// <item><strong>Email Confirmation:</strong> Generate token and send confirmation link</item>
        /// <item><strong>Sign-In or Redirect:</strong> Auto sign-in if confirmation not required, else redirect to confirmation page</item>
        /// </list>
        ///
        /// <para><strong>Rate Limiting:</strong></para>
        /// Protected by <c>[EnableRateLimiting(Auth)]</c> policy to prevent automated registration attacks.
        /// </remarks>
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Constants.RateLimitPolicies.Auth)]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            const string customerRoleName = "Customer";

            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                // ==================================================================
                // â•‘ PHASE 1: Create Identity Account                              â•‘
                // ==================================================================
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    // ==============================================================
                    // â•‘ PHASE 2: Create Application-Level User Records           â•‘
                    // ==============================================================

                    // 2a. RegisteredUserModel: Links Identity user to app business logic
                    var registeredUser = new RegisteredUserModel()
                    {
                        Email = Input.Email
                    };
                    _context.RegisteredUsers.Add(registeredUser);

                    // 2b. UserProfileModel: Stores first/last name for profile header greeting
                    // Created immediately so the header shows the name instead of email from first login.
                    var profile = new UserProfileModel
                    {
                        PkEmail = Input.Email,
                        FirstName = Input.FirstName,
                        LastName = Input.LastName
                    };
                    _context.UserProfiles.Add(profile);

                    await _context.SaveChangesAsync();

                    // 2c. ContactDetailModel: Default shipping address from registration form
                    // Marked as IsDefault so it's auto-selected during checkout.
                    var contact = new ContactDetailModel
                    {
                        FirstName = Input.FirstName,
                        LastName = Input.LastName,
                        PhoneNumber = Input.PhoneNumber,
                        Street = Input.Street,
                        City = Input.City,
                        Province = Input.Province,
                        PostCode = Input.PostCode,
                        Country = Input.Country,
                        IsDefault = true,
                        FkRegisteredUserId = registeredUser.PkRegisteredUserId
                    };
                    await _contactRepository.AddAndSaveAsync(contact);

                    // ==============================================================
                    // â•‘ PHASE 3: Role Assignment                                    â•‘
                    // ==============================================================
                    if (!await _roleManager.RoleExistsAsync(customerRoleName))
                    {
                        var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(customerRoleName));
                        if (!createRoleResult.Succeeded)
                        {
                            foreach (var error in createRoleResult.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }

                            await _userManager.DeleteAsync(user);
                            return Page();
                        }
                    }

                    var addToRoleResult = await _userManager.AddToRoleAsync(user, customerRoleName);
                    if (!addToRoleResult.Succeeded)
                    {
                        foreach (var error in addToRoleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        await _userManager.DeleteAsync(user);
                        return Page();
                    }

                    _logger.LogInformation("User created a new account with password.");

                    // ==============================================================
                    // â•‘ PHASE 4: Email Confirmation                               â•‘
                    // ==============================================================

                    // Generate email confirmation token and build callback URL
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    // â”€â”€ Conditional Sign-In or Redirect to Confirmation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    // If RequireConfirmedAccount=true: User must click email link before signing in.
                    // If false: Sign in immediately and redirect to product catalog.
                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        // Redirect new customers to the product catalog
                        return RedirectToAction("Index", "Product");
                    }
                }

                // â”€â”€ Add Identity Validation Errors to ModelState â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Redisplay form with validation errors
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
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        /// <summary>
        /// Retrieves the email store from the user store and validates email support.
        /// Required by scaffolded Identity UI infrastructure for email-based registration.
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
