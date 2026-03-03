using Microsoft.AspNetCore.Mvc;
using ELKH.Services;
using System.Threading.Tasks;

namespace ELKH.Controllers
{
    /// <summary>
    /// Shopping cart management controller. All actions require an authenticated user.
    /// Delegates all persistence and business logic to IWishlistService.
    /// </summary>
    public class WishlistController : AuthenticatedControllerBase
    {
        private readonly IWishlistService _wishlistService;

        public WishlistController(IWishlistService wishlistService, IUserService userService)
            : base(userService)
        {
            _wishlistService = wishlistService;
        }

        // POST: /Wishlist/AddAjax - returns JSON
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAjax(int productId)
        {
            if (!TryGetCurrentUserEmail(out var userEmail))
                return Json(new { success = false, message = "Not authenticated" });

            var result = await _wishlistService.AddAsync(userEmail, productId);
            return Json(new { result.Success, result.Message, result.Count });
        }

        // POST: /Wishlist/RemoveAjax - returns JSON
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAjax(int productId)
        {
            if (!TryGetCurrentUserEmail(out var userEmail))
                return Json(new { success = false, message = "Not authenticated" });

            var result = await _wishlistService.RemoveAsync(userEmail, productId);
            return Json(new { result.Success, result.Message, result.Count });
        }

        // GET: /Wishlist
        public async Task<IActionResult> Index(string sort = "date_desc")
        {
            var authResult = RequireAuthenticatedUser(out var userEmail);
            if (authResult != null) return authResult;

            var items = await _wishlistService.GetItemsAsync(userEmail, sort);
            return View(items);
        }

        // POST: /Wishlist/Add
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

        // POST: /Wishlist/Remove
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
