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
using static ELKH.Extensions.RateLimitPolicies;

namespace ELKH.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Razor Page model for new user registration.
    /// Extends the scaffolded Identity registration flow to create three additional
    /// records on successful sign-up:
    /// <list type="bullet">
    ///   <item><see cref="RegisteredUserModel"/> — links the Identity user to application data.</item>
    ///   <item><see cref="UserProfileModel"/> — stores first/last name for the profile header.</item>
    ///   <item><see cref="ContactDetailModel"/> — stores the shipping address supplied during registration.</item>
    /// </list>
    /// </summary>
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IContactDetailRepo _contactRepository;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IContactDetailRepo contactRepository)
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
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [MaxLength(100)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

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


        /// <summary>Populates external login providers and stores the return URL for the view.</summary>
        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        /// <summary>
        /// Handles the registration form submission.
        /// On success, performs these steps in order:
        /// 1. Creates the ASP.NET Core Identity user (<see cref="IdentityUser"/>).
        /// 2. Persists a <see cref="RegisteredUserModel"/> to link the identity to app data.
        /// 3. Creates a <see cref="UserProfileModel"/> so the name shows on first login.
        /// 4. Creates the default <see cref="ContactDetailModel"/> from the address fields.
        /// 5. Sends an email-confirmation link; redirects to confirmation page or signs in directly
        ///    depending on <c>RequireConfirmedAccount</c> setting.
        /// </summary>
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELKH.Extensions.RateLimitPolicies.Auth)]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    //Save the registered user into database with additional information
                    var registeredUser = new RegisteredUserModel()
                    {
                        Email = Input.Email
                    };
                    _context.RegisteredUsers.Add(registeredUser);

                    // Create the user profile immediately so the header greeting shows
                    // the first name rather than the email address from the very first login.
                    var profile = new UserProfileModel
                    {
                        PkEmail   = Input.Email,
                        FirstName = Input.FirstName,
                        LastName  = Input.LastName
                    };
                    _context.UserProfiles.Add(profile);

                    await _context.SaveChangesAsync();

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

                    // Assign the Customer role to every new registrant.
                    const string customerRole = "Customer";
                    if (!await _roleManager.RoleExistsAsync(customerRole))
                        await _roleManager.CreateAsync(new IdentityRole(customerRole));
                    await _userManager.AddToRoleAsync(user, customerRole);

                    _logger.LogInformation("User created a new account with password.");

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

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

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

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
