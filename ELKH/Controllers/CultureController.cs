using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using ELKH.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace ELKH.Controllers;

[AllowAnonymous]
public class CultureController : Controller
{
    private readonly ApplicationDbContext _db;

    public CultureController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string culture, string currency, string returnUrl = null)
    {
        // Set culture cookie if provided
        if (!string.IsNullOrEmpty(culture))
        {
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue, new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = System.DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });
        }

        // Set currency cookie if provided
        if (!string.IsNullOrEmpty(currency))
        {
            Response.Cookies.Append("appCurrency", currency, new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = System.DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });
        }

        // Persist preference in DB for authenticated user
        var email = User.Identity?.Name;
        if (!string.IsNullOrEmpty(email))
        {
            var user = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(culture)) user.PreferredCulture = culture;
                if (!string.IsNullOrEmpty(currency)) user.PreferredCurrency = currency;
                await _db.SaveChangesAsync();
            }
        }

        return LocalRedirect(returnUrl ?? "/");
    }
}
