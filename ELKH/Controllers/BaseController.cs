using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ELKH.Controllers;


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
        // Inject cart count
        if (User.Identity?.IsAuthenticated == true)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser?.Email is not null)
            {
                var registered = _db.RegisteredUsers.FirstOrDefault(u => u.Email == identityUser.Email);
                if (registered is not null)
                {
                    ViewBag.CartCount = _db.Carts
                        .Where(c => c.FkRegisteredUserId == registered.PkRegisteredUserId)
                        .Sum(c => (int?)c.Quantity) ?? 0;
                }
            }
        }
        await next();
    }
}