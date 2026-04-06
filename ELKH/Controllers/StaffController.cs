using ELKH.Data;
using ELKH.Models;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    /// <summary>
    /// Staff and Admin controller for daily operational tasks including the KPI dashboard,
    /// product review moderation, store review moderation, and staff message inbox.
    /// </summary>
    [Authorize(Roles = "Admin,Staff")]
    public class StaffController : Controller
    {
        private readonly IRatingService _ratingService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IStoreReviewService _storeReviewService;

        public StaffController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IRatingService ratingService,
            IStoreReviewService storeReviewService)
        {
            _context = context;
            _userManager = userManager;
            _ratingService = ratingService;
            _storeReviewService = storeReviewService;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders.AsNoTracking().ToListAsync();

            ViewBag.TotalOrders = orders.Count;
            ViewBag.TodayOrders = orders.Count(o => o.CreatedAt.Date == DateTime.UtcNow.Date);

            // FIXED: Using Enums instead of strings for the counts
            ViewBag.CancelledOrders = orders.Count(o => o.OrderStatus == OrderStatus.Cancelled);
            ViewBag.PendingOrders = orders.Count(o => o.OrderStatus == OrderStatus.Pending);

            ViewBag.LowStockCount = await _context.Products
                .CountAsync(p => p.StockQuantity < 20);

            ViewBag.Messages = await _context.StaffMessages
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return View();
        }

        // ================= REVIEW ACTIONS =================

        [HttpGet]
        public async Task<IActionResult> Reviews(int page = 1)
        {
            var query = new RatingQuery
            {
                Page = page,
                PageSize = 20
            };

            var pagedResult = await _ratingService.GetRatingsPagedAsync(query);
            return View(pagedResult);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveReview(int id)
        {
            await _ratingService.ApproveAsync(id);
            return RedirectToAction(nameof(Reviews));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var rating = await _ratingService.GetByIdAsync(id);
            if (rating != null)
            {
                // Fixed: Using FkRegisteredUserId from your ProductRatingModel
                await _ratingService.DeleteRatingAsync(id, rating.FkRegisteredUserId);
            }

            return RedirectToAction(nameof(Reviews));
        }

        // ================= STORE REVIEW MODERATION =================

        [HttpGet]
        public async Task<IActionResult> StoreReviews()
        {
            var pending = await _storeReviewService.GetPendingReviewsAsync();
            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStoreReview(int id)
        {
            await _storeReviewService.ApproveAsync(id);
            TempData["Message"] = "success,Review approved and is now visible on the homepage.";
            return RedirectToAction(nameof(StoreReviews));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStoreReview(int id)
        {
            await _storeReviewService.AdminDeleteAsync(id);
            TempData["Message"] = "success,Review deleted.";
            return RedirectToAction(nameof(StoreReviews));
        }

        // ================= STAFF MESSAGES ACTIONS =================

        [HttpPost]
        public async Task<IActionResult> ReplyMessage(int MessageId, string ReplyText)
        {
            if (string.IsNullOrEmpty(ReplyText)) return RedirectToAction("Index");

            var reply = new MessageReplyModel
            {
                MessageId = MessageId,
                ReplyText = ReplyText,
                RepliedBy = User.Identity?.Name ?? "Unknown",
                RepliedAt = DateTime.UtcNow
            };

            _context.MessageReplies.Add(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MarkMessageAsRead(int id)
        {
            var message = await _context.StaffMessages.FindAsync(id);
            if (message != null)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
    }
}
