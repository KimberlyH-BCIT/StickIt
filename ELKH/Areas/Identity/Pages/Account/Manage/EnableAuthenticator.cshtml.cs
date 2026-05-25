// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ELKH.Areas.Identity.Pages.Account.Manage
{
    // TABLE OF CONTENTS
    // - Authenticator setup
    // - QR code generation
    // - Verification flow
    // - Recovery code generation


    /// <summary>
    /// Razor Page model for enabling two-factor authentication (2FA) using TOTP authenticator apps.
    /// Guides users through setting up Google Authenticator, Microsoft Authenticator, or similar apps.
    /// </summary>
    /// <remarks>
    /// <para><strong>2FA Setup Workflow:</strong></para>
    /// <list type="number">
    /// <item><strong>Generate Secret Key:</strong> Create or retrieve user's unique TOTP secret</item>
    /// <item><strong>Display QR Code:</strong> Generate TOTP URI for scanning (or show manual entry key)</item>
    /// <item><strong>User Scans:</strong> User scans QR code with authenticator app</item>
    /// <item><strong>Verify Code:</strong> User enters 6-digit code from app to confirm setup</item>
    /// <item><strong>Enable 2FA:</strong> If code valid, enable 2FA for account</item>
    /// <item><strong>Generate Recovery Codes:</strong> Provide 10 backup codes (one-time use for account recovery)</item>
    /// </list>
    ///
    /// <para><strong>TOTP Protocol (RFC 6238):</strong></para>
    /// Uses Time-based One-Time Password algorithm with:
    /// <list type="bullet">
    /// <item>6-digit codes (configurable in URI)</item>
    /// <item>30-second time window (default)</item>
    /// <item>Base32-encoded shared secret</item>
    /// <item>HMAC-SHA1 hashing</item>
    /// </list>
    ///
    /// <para><strong>Compatible Apps:</strong></para>
    /// Google Authenticator, Microsoft Authenticator, Authy, 1Password, LastPass Authenticator, etc.
    ///
    /// <para><strong>Security Notes:</strong></para>
    /// <list type="bullet">
    /// <item>Secret key must be kept secure (never log or expose)</item>
    /// <item>Recovery codes are one-time use only</item>
    /// <item>If user loses authenticator app, recovery codes are the only fallback</item>
    /// <item>QR code contains sensitive data - display securely</item>
    /// </list>
    /// </remarks>
    public class EnableAuthenticatorModel : PageModel
    {
        #region Properties & Dependencies

        // â”€â”€ ASP.NET Core Identity Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<EnableAuthenticatorModel> _logger;
        private readonly UrlEncoder _urlEncoder;

        /// <summary>
        /// TOTP URI format for generating QR codes compatible with authenticator apps.
        /// Format: otpauth://totp/{issuer}:{email}?secret={secret}&amp;issuer={issuer}&amp;digits=6
        /// </summary>
        /// <remarks>
        /// <para><strong>URI Components:</strong></para>
        /// <list type="bullet">
        /// <item>{0} = Issuer (application name)</item>
        /// <item>{1} = Account identifier (user email)</item>
        /// <item>{2} = Base32-encoded secret key</item>
        /// <item>digits=6 = 6-digit TOTP codes</item>
        /// </list>
        /// Compatible with Google Authenticator, Microsoft Authenticator, and RFC 6238 standard.
        /// </remarks>
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        /// <summary>
        /// Initializes a new instance of <see cref="EnableAuthenticatorModel"/> with required dependencies.
        /// </summary>
        /// <param name="userManager">User manager for accessing authenticator keys and enabling 2FA.</param>
        /// <param name="logger">Logger for 2FA setup events.</param>
        /// <param name="urlEncoder">URL encoder for generating safe TOTP URIs.</param>
        public EnableAuthenticatorModel(
            UserManager<IdentityUser> userManager,
            ILogger<EnableAuthenticatorModel> logger,
            UrlEncoder urlEncoder)
        {
            _userManager = userManager;
            _logger = logger;
            _urlEncoder = urlEncoder;
        }

        // ==============================================================================
        // â•‘ Page Properties                                                            â•‘
        // ==============================================================================

        /// <summary>
        /// User's authenticator secret key formatted in human-readable groups (e.g., "ABCD EFGH IJKL MNOP").
        /// Displayed for manual entry if user cannot scan QR code.
        /// </summary>
        public string SharedKey { get; set; }

        /// <summary>
        /// TOTP URI for generating QR code.
        /// Format: otpauth://totp/AppName:user@email.com?secret=KEY&amp;issuer=AppName&amp;digits=6
        /// </summary>
        public string AuthenticatorUri { get; set; }

        /// <summary>
        /// Array of 10 one-time recovery codes generated after successful 2FA setup.
        /// Stored in TempData to pass to ShowRecoveryCodes page.
        /// </summary>
        [TempData]
        public string[] RecoveryCodes { get; set; }

        /// <summary>
        /// Status message displayed to user after successful verification.
        /// Stored in TempData to survive redirects.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        /// Form input data bound from the verification view.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// Input model for TOTP verification code.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            /// 6-digit verification code from authenticator app (7 chars allows for spaces/hyphens).
            /// </summary>
            [Required]
            [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Verification Code")]
            public string Code { get; set; }
        }

        #endregion

        #region GET Handler

        /// <summary>
        /// Handles GET requests to display the 2FA setup page.
        /// Loads or generates the authenticator key and QR code URI.
        /// </summary>
        /// <returns>
        /// NotFound if user not authenticated.<br/>
        /// Page with SharedKey and AuthenticatorUri for display.
        /// </returns>
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Load existing key or generate new one, format for display
            await LoadSharedKeyAndQrCodeUriAsync(user);

            return Page();
        }

        #endregion

        #region POST Handler

        /// <summary>
        /// Handles verification code submission to enable 2FA.
        /// Validates TOTP code, enables 2FA, and generates recovery codes.
        /// </summary>
        /// <returns>
        /// On success: Redirect to ShowRecoveryCodes (if new codes) or TwoFactorAuthentication page.<br/>
        /// On invalid code: Redisplay form with error.<br/>
        /// On user not found: NotFound result.
        /// </returns>
        /// <remarks>
        /// <para><strong>Verification Process:</strong></para>
        /// <list type="number">
        /// <item>Strip spaces and hyphens from user input (users may format code)</item>
        /// <item>Verify code using UserManager.VerifyTwoFactorTokenAsync</item>
        /// <item>If valid: Enable 2FA for account</item>
        /// <item>Generate 10 recovery codes if none exist</item>
        /// <item>Redirect to appropriate page based on recovery code status</item>
        /// </list>
        ///
        /// <para><strong>Recovery Codes:</strong></para>
        /// One-time use backup codes (10 codes) for account recovery if authenticator app is unavailable.
        /// Each code can only be used once. Users should store them securely.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            // ==========================================================================
            // â•‘ PHASE 1: Verify TOTP Code                                             â•‘
            // ==========================================================================
            // Users may enter code with spaces or hyphens (e.g., "123 456" or "123-456")
            var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

            var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError("Input.Code", "Verification code is invalid.");
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            // ==========================================================================
            // â•‘ PHASE 2: Enable 2FA                                                   â•‘
            // ==========================================================================
            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var userId = await _userManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

            StatusMessage = "Your authenticator app has been verified.";

            // ==========================================================================
            // â•‘ PHASE 3: Generate Recovery Codes (if needed)                         â•‘
            // ==========================================================================
            // If user has no recovery codes, generate 10 new ones
            if (await _userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                RecoveryCodes = recoveryCodes.ToArray();
                return RedirectToPage("./ShowRecoveryCodes");
            }
            else
            {
                // User already has recovery codes - go to 2FA management page
                return RedirectToPage("./TwoFactorAuthentication");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Loads or generates the user's authenticator secret key and QR code URI.
        /// </summary>
        /// <param name="user">The user to load/generate key for.</param>
        /// <remarks>
        /// <para><strong>Key Generation Process:</strong></para>
        /// <list type="number">
        /// <item>Attempt to retrieve existing authenticator key</item>
        /// <item>If none exists: Generate new key via ResetAuthenticatorKeyAsync</item>
        /// <item>Format key in human-readable groups for manual entry</item>
        /// <item>Generate TOTP URI for QR code generation</item>
        /// </list>
        /// </remarks>
        private async Task LoadSharedKeyAndQrCodeUriAsync(IdentityUser user)
        {
            // â”€â”€ Load or Generate Authenticator Key â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                // No existing key - generate new one
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            // â”€â”€ Format Key and Generate URI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            SharedKey = FormatKey(unformattedKey);

            var email = await _userManager.GetEmailAsync(user);
            AuthenticatorUri = GenerateQrCodeUri(email, unformattedKey);
        }

        /// <summary>
        /// Formats the raw authenticator key into human-readable groups of 4 characters.
        /// Example: "ABCDEFGHIJKLMNOP" becomes "ABCD EFGH IJKL MNOP"
        /// </summary>
        /// <param name="unformattedKey">Raw base32-encoded key (no spaces).</param>
        /// <returns>Formatted key with spaces every 4 characters for easier manual entry.</returns>
        private string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Generates TOTP URI for QR code compatible with authenticator apps.
        /// </summary>
        /// <param name="email">User's email (displayed in authenticator app).</param>
        /// <param name="unformattedKey">Raw base32-encoded secret key.</param>
        /// <returns>
        /// TOTP URI in format: otpauth://totp/Issuer:email?secret=KEY&amp;issuer=Issuer&amp;digits=6
        /// </returns>
        /// <remarks>
        /// Compatible with RFC 6238 (TOTP) standard.
        /// QR code libraries can encode this URI directly for scanning.
        /// </remarks>
        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                AuthenticatorUriFormat,
                _urlEncoder.Encode("Microsoft.AspNetCore.Identity.UI"),
                _urlEncoder.Encode(email),
                unformattedKey);
        }

        #endregion
    }
}
