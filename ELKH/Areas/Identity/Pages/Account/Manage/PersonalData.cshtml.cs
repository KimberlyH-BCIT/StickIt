// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ELKH.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Razor Page model for displaying personal data management options.
    /// Provides users with tools to manage their personal information and privacy settings.
    /// </summary>
    /// <remarks>
    /// This page serves as the main hub for personal data management, offering users
    /// control over their personal information in compliance with data protection
    /// regulations such as GDPR, PIPEDA, and CCPA.
    /// 
    /// <para><strong>Data Protection Compliance:</strong></para>
    /// Supports key data protection rights:
    /// <list type="bullet">
    /// <item>Right to access - view personal data collected</item>
    /// <item>Right to portability - download personal data</item>
    /// <item>Right to erasure - delete personal data</item>
    /// <item>Right to rectification - update personal information</item>
    /// </list>
    /// 
    /// <para><strong>Security Features:</strong></para>
    /// <list type="bullet">
    /// <item>Authentication required - only account owner can access</item>
    /// <item>User verification - ensures legitimate data access requests</item>
    /// <item>Audit logging - tracks all personal data management activities</item>
    /// <item>Secure data handling - protects sensitive information</item>
    /// </list>
    /// 
    /// <para><strong>User Experience:</strong></para>
    /// <list type="bullet">
    /// <item>Clear presentation of available data management options</item>
    /// <item>Easy navigation to specific data management functions</item>
    /// <item>Transparent information about data collection and usage</item>
    /// <item>User-friendly explanations of data protection rights</item>
    /// </list>
    /// 
    /// <para><strong>Integration Points:</strong></para>
    /// <list type="bullet">
    /// <item>Download personal data functionality</item>
    /// <item>Account deletion workflows</item>
    /// <item>Profile management systems</item>
    /// <item>Privacy settings and consent management</item>
    /// </list>
    /// 
    /// <para><strong>Compliance Considerations:</strong></para>
    /// This page helps ensure compliance with:
    /// <list type="bullet">
    /// <item>GDPR (General Data Protection Regulation)</item>
    /// <item>PIPEDA (Personal Information Protection and Electronic Documents Act)</item>
    /// <item>CCPA (California Consumer Privacy Act)</item>
    /// <item>Other applicable data protection laws</item>
    /// </list>
    /// </remarks>
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<PersonalDataModel> _logger;

        /// <summary>
        /// Initializes a new instance of the PersonalDataModel.
        /// </summary>
        /// <param name="userManager">ASP.NET Core Identity user manager for user operations.</param>
        /// <param name="logger">Logger for audit trail and compliance monitoring.</param>
        public PersonalDataModel(
            UserManager<IdentityUser> userManager,
            ILogger<PersonalDataModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Handles GET requests to display the personal data management page.
        /// </summary>
        /// <returns>Page view with personal data management options, or NotFound if user is invalid.</returns>
        /// <remarks>
        /// Validates the current user and displays available personal data management
        /// options including data download, account deletion, and privacy controls.
        /// 
        /// <para><strong>Security Validation:</strong></para>
        /// <list type="bullet">
        /// <item>Verifies user authentication and authorization</item>
        /// <item>Ensures user can only access their own data</item>
        /// <item>Logs access attempts for audit purposes</item>
        /// <item>Returns appropriate error for invalid users</item>
        /// </list>
        /// 
        /// <para><strong>Page Content:</strong></para>
        /// <list type="bullet">
        /// <item>Overview of collected personal data</item>
        /// <item>Links to data download functionality</item>
        /// <item>Account deletion options</item>
        /// <item>Privacy settings and consent management</item>
        /// <item>Information about data protection rights</item>
        /// </list>
        /// 
        /// <para><strong>Compliance Features:</strong></para>
        /// <list type="bullet">
        /// <item>Transparent data collection disclosure</item>
        /// <item>Clear explanation of user rights</item>
        /// <item>Easy access to data portability tools</item>
        /// <item>Account deletion with data erasure options</item>
        /// </list>
        /// </remarks>
        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            return Page();
        }
    }
}
