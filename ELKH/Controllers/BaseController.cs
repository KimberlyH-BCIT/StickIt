using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
    protected readonly UserManager<IdentityUser> _userManager;

    public BaseController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Inject cart count — only applies to registered customers.
        // Admin/staff accounts have no RegisteredUserModel entry, so the lookup
        // may return null; in that case we simply skip setting the cart count.
        if (User.Identity?.IsAuthenticated == true)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser?.Email is not null)
            {
                var registered = _db.RegisteredUsers.FirstOrDefault(u => u.Email == identityUser.Email);
                ViewBag.CartCount = registered is not null
                    ? _db.Carts
                        .Where(c => c.FkRegisteredUserId == registered.PkRegisteredUserId)
                        .Sum(c => (int?)c.Quantity) ?? 0
                    : 0;
            }
        }
        await next();
    }
}