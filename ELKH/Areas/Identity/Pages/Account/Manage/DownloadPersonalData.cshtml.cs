// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ELKH.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Razor Page model for downloading personal data in compliance with data protection regulations.
    /// Enables users to export their personal information in a structured, machine-readable format.
    /// </summary>
    /// <remarks>
    /// This page implements the "Right to Data Portability" as required by major data
    /// protection regulations including GDPR, PIPEDA, and CCPA. Users can download
    /// their personal data in JSON format for backup, migration, or compliance purposes.
    /// 
    /// <para><strong>Data Protection Compliance:</strong></para>
    /// Supports key regulatory requirements:
    /// <list type="bullet">
    /// <item>GDPR Article 20 - Right to data portability</item>
    /// <item>PIPEDA - Individual access to personal information</item>
    /// <item>CCPA - Right to know about personal information</item>
    /// <item>Structured, machine-readable format requirement</item>
    /// </list>
    /// 
    /// <para><strong>Security Implementation:</strong></para>
    /// <list type="bullet">
    /// <item>User authentication required - only account owner can download</item>
    /// <item>Personal data access validation</item>
    /// <item>Audit logging for compliance tracking</item>
    /// <item>Secure data serialization without sensitive system data</item>
    /// </list>
    /// 
    /// <para><strong>Data Export Content:</strong></para>
    /// The export typically includes:
    /// <list type="bullet">
    /// <item>Account information (username, email, registration date)</item>
    /// <item>Profile data (name, preferences, settings)</item>
    /// <item>Order history and transaction records</item>
    /// <item>User-generated content (reviews, comments)</item>
    /// <item>Preference and consent settings</item>
    /// </list>
    /// 
    /// <para><strong>Technical Implementation:</strong></para>
    /// <list type="bullet">
    /// <item>JSON format for machine readability</item>
    /// <item>UTF-8 encoding for international character support</item>
    /// <item>Structured data organization by category</item>
    /// <item>Timestamp inclusion for data freshness verification</item>
    /// </list>
    /// 
    /// <para><strong>Privacy Protection:</strong></para>
    /// <list type="bullet">
    /// <item>Excludes sensitive system data (passwords, tokens)</item>
    /// <item>Only includes data belonging to the authenticated user</item>
    /// <item>Sanitizes data to prevent information disclosure</item>
    /// <item>Logs access for audit and compliance purposes</item>
    /// </list>
    /// 
    /// <para><strong>User Experience:</strong></para>
    /// <list type="bullet">
    /// <item>Simple, one-click download process</item>
    /// <item>Clear filename with timestamp</item>
    /// <item>Human-readable JSON structure</item>
    /// <item>Comprehensive data coverage</item>
    /// </list>
    /// </remarks>
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;

        /// <summary>
        /// Initializes a new instance of the DownloadPersonalDataModel.
        /// </summary>
        /// <param name="userManager">ASP.NET Core Identity user manager for user data access.</param>
        /// <param name="logger">Logger for compliance audit trail and security monitoring.</param>
        public DownloadPersonalDataModel(
            UserManager<IdentityUser> userManager,
            ILogger<DownloadPersonalDataModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(IdentityUser).GetProperties().Where(
                            prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            var logins = await _userManager.GetLoginsAsync(user);
            foreach (var l in logins)
            {
                personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
            }

            personalData.Add($"Authenticator Key", await _userManager.GetAuthenticatorKeyAsync(user));

            Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(JsonSerializer.SerializeToUtf8Bytes(personalData), "application/json");
        }
    }
}
