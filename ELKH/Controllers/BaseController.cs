using ELKH.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ELKH.Controllers;

/// <summary>
/// Base controller for MVC controllers that serve customer-facing pages.
/// Injects the authenticated user's live cart-item count into <c>ViewBag.CartCount</c>
/// before every action executes so the navbar cart badge stays accurate without
/// requiring each controller action to query it individually.
/// </summary>
/// <remarks>
/// The cart count is resolved with a single JOIN query (Carts → RegisteredUsers filtered
/// by email) rather than two separate round-trips, and the result is cached in
/// <see cref="IMemoryCache"/> for 10 seconds per user to limit DB pressure during
/// rapid page navigation.
/// </remarks>
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
            var email = User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue(ClaimTypes.Name);

            if (!string.IsNullOrEmpty(email))
            {
                var cache    = HttpContext.RequestServices.GetService<IMemoryCache>();
                var cacheKey = $"cart_count_{email}";

                if (cache == null || !cache.TryGetValue(cacheKey, out int cartCount))
                {
                    // Single JOIN query: avoids fetching the RegisteredUser row separately.
                    cartCount = await _db.Carts
                        .Where(c => c.RegisteredUser!.Email == email)
                        .SumAsync(c => (int?)c.Quantity) ?? 0;

                    cache?.Set(cacheKey, cartCount, TimeSpan.FromSeconds(10));
                }

                ViewBag.CartCount = cartCount;
            }
        }
        await next();
    }
}