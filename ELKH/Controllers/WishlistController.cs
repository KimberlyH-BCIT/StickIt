using System.Threading.Tasks;
using ELKH.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Wishlist management controller. All actions require an authenticated user.
    /// Delegates all persistence and business logic to <see cref="IWishlistService"/>.
    /// Provides both AJAX endpoints (returning JSON) and full-page form endpoints
    /// for add/remove operations so both progressive-enhancement and no-JS flows work.
    /// </summary>
    public class WishlistController : AuthenticatedControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(IWishlistService wishlistService, IUserService userService, ILogger<WishlistController> logger, ELKH.Data.ApplicationDbContext db)
            : base(db, userService)
        {
            _wishlistService = wishlistService;
            _logger = logger;
        }

        /// <summary>
        /// POST: /Wishlist/AddAjax
        /// Adds a product to the wishlist and returns a JSON result consumed by <c>site.js</c>.
        /// Used by the optimistic-UI wishlist button - the page is not reloaded.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAjax(int productId)
        {
            if (!TryGetCurrentUserEmail(out var userEmail))
                return Json(new { success = false, message = "Not authenticated" });

            var result = await _wishlistService.AddAsync(userEmail, productId);
            return Json(new { result.Success, result.Message, result.Count });
        }

        /// <summary>
        /// POST: /Wishlist/RemoveAjax
        /// Removes a product from the wishlist and returns a JSON result consumed by <c>site.js</c>.
        /// Used by the optimistic-UI wishlist button - the page is not reloaded.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAjax(int productId)
        {
            if (!TryGetCurrentUserEmail(out var userEmail))
                return Json(new { success = false, message = "Not authenticated" });

            var result = await _wishlistService.RemoveAsync(userEmail, productId);
            return Json(new { result.Success, result.Message, result.Count });
        }

        /// <summary>
        /// GET: /Wishlist
        /// Displays the signed-in user's wishlist, optionally sorted.
        /// </summary>
        /// <param name="sort">Sort key passed to the service (default: <c>date_desc</c>).</param>
        public async Task<IActionResult> Index(string sort = "date_desc")
        {
            var authResult = RequireAuthenticatedUser(out var userEmail);
            if (authResult != null) return authResult;

            var items = await _wishlistService.GetItemsAsync(userEmail, sort);
            return View(items);
        }

        /// <summary>
        /// POST: /Wishlist/Add
        /// Full-page form fallback for adding a product. Sets a TempData message and
        /// redirects back to the referring page (or User/Index when no referrer is available).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId)
        {
            var authResult = RequireAuthenticatedUser(out var userEmail);
            if (authResult != null) return authResult;

            var result = await _wishlistService.AddAsync(userEmail, productId);
            if (result.Success)
                SetSuccessMessage("Product added to your wishlist");
            else if (result.AlreadyExists)
                SetWarningMessage("Product already in wishlist");
            else
                SetErrorMessage(result.Message);

            return RedirectToRefererOrAction("Index", "User");
        }

        /// <summary>
        /// POST: /Wishlist/Remove
        /// Full-page form fallback for removing a product. Sets a TempData message and
        /// redirects back to the wishlist index.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int productId)
        {
            var authResult = RequireAuthenticatedUser(out var userEmail);
            if (authResult != null) return authResult;

            var result = await _wishlistService.RemoveAsync(userEmail, productId);
            if (result.Success)
                SetSuccessMessage("Product removed from your wishlist");
            else
                SetErrorMessage(result.Message);

            return RedirectToRefererOrAction(nameof(Index));
        }
    }
}
