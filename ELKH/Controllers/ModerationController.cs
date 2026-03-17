using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ELKH.Services;
using ELKH.ViewModels;

namespace ELKH.Controllers
{
    /// <summary>
    /// Handles administrative moderation actions for product reviews,
    /// including paginated listing, approval, and flagging of user-submitted ratings.
    /// Accessible only to users in the <c>Admin</c> role.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ModerationController : Controller
    {
        private readonly IRatingService _ratingService;
        private readonly IModerationService _moderationService;
        private readonly ILogger<ModerationController> _logger;

        public ModerationController(IRatingService ratingService, IModerationService moderationService, ILogger<ModerationController> logger)
        {
            _ratingService = ratingService;
            _moderationService = moderationService;
            _logger = logger;
        }

        /// <summary>
        /// Displays a paginated list of product reviews for moderation.
        /// </summary>
        /// <param name="query">Filter and pagination parameters bound from the query string.</param>
        /// <returns>The moderation index view populated with the paged review results.</returns>
        // GET: /Admin/Moderation
        public async Task<IActionResult> Index([FromQuery] RatingQuery query)
        {
            var result = await _ratingService.GetRatingsPagedAsync(query);
            return View(new ModerationIndexVM
            {
                Items      = result.Items,
                Query      = query,
                TotalPages = result.TotalPages
            });
        }

        /// <summary>
        /// Approves a pending product review, making it publicly visible.
        /// Supports both standard form submissions and AJAX requests.
        /// </summary>
        /// <param name="id">The unique identifier of the rating to approve.</param>
        /// <returns>
        /// A JSON response with <c>success: true</c> for AJAX requests,
        /// or a redirect to <see cref="Index"/> with a <c>TempData</c> confirmation message for standard requests.
        /// Returns <see cref="NotFoundResult"/> if no rating with the given <paramref name="id"/> exists.
        /// </returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var r = await _ratingService.ApproveAsync(id);
            if (r is null) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Review approved", id });

            TempData["Message"] = "success, Review approved";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Flags a product review for further review, optionally attaching a moderator note.
        /// Supports both standard form submissions and AJAX requests.
        /// </summary>
        /// <param name="id">The unique identifier of the rating to flag.</param>
        /// <param name="note">An optional note describing the reason for flagging.</param>
        /// <returns>
        /// A JSON response indicating success or failure for AJAX requests,
        /// or a redirect to <see cref="Index"/> with a <c>TempData</c> status message for standard requests.
        /// </returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Flag(int id, string note)
        {
            var moderator = User.Identity?.Name ?? "anon";
            var result = await _moderationService.FlagAsync(id, note ?? string.Empty, moderator);
            if (!result.Success)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = result.Message });
                TempData["Message"] = $"warning, {result.Message}";
                return RedirectToAction(nameof(Index));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Review flagged", id });

            TempData["Message"] = "warning, Review flagged";
            return RedirectToAction(nameof(Index));
        }
    }
}
