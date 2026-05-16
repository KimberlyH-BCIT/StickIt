using System.Security.Claims;
using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers;


/// <summary>
/// Base controller for MVC controllers that serve customer-facing pages.
/// Injects the authenticated user's live cart-item count into <c>ViewBag.CartCount</c>
/// before every action executes so the navbar cart badge stays accurate without
/// requiring each controller action to query it individually.
/// </summary>
/// <remarks>
/// The count is injected via <see cref="OnActionExecutionAsync"/> (an action filter override)
/// rather than in each action method, which is the recommended ASP.NET Core pattern for
/// cross-cutting pre-action work.
///
/// Admin and staff accounts do not have a <see cref="ELKH.Models.RegisteredUserModel"/> row,
/// so a null result from the user lookup is handled gracefully by setting the count to 0.
/// </remarks>
public class BaseController : Controller
{
    protected readonly ApplicationDbContext _db;
    protected readonly UserManager<IdentityUser>? _userManager;

    public BaseController(ApplicationDbContext db)
    {
        _db = db;
    }

    public BaseController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Inject cart count - only applies to registered customers.
        // Admin/staff accounts have no RegisteredUserModel entry, so the lookup
        // may return null; in that case we simply skip setting the cart count.
        if (User.Identity?.IsAuthenticated == true)
        {
            // Read email directly from claims - avoids an extra UserManager round-trip on every request.
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
