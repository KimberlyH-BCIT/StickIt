using ELKH.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ELKH.Controllers;

/// <summary>
/// Base controller for MVC controllers that serve customer-facing pages.
/// Injects the authenticated user's live cart-item count into <c>ViewBag.CartCount</c>
/// before every action executes so the navbar cart badge stays accurate without
/// requiring each controller action to query it individually.
/// </summary>
public class BaseController : Controller
{
    protected readonly ApplicationDbContext _db;

    public BaseController(ApplicationDbContext db)
    {
        _db = db;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Read email directly from claims — avoids an extra UserManager round-trip on every request.
            var email = User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue(ClaimTypes.Name);

            if (!string.IsNullOrEmpty(email))
            {
                var registered = await _db.RegisteredUsers
                    .FirstOrDefaultAsync(u => u.Email == email);

                ViewBag.CartCount = registered is not null
                    ? await _db.Carts
                        .Where(c => c.FkRegisteredUserId == registered.PkRegisteredUserId)
                        .SumAsync(c => (int?)c.Quantity) ?? 0
                    : 0;
            }
        }
        await next();
    }
}