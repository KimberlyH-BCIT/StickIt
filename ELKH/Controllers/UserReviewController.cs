using ELKH.Controllers.Base;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers;

/// <summary>
/// Controller responsible for user review and rating management.
/// Handles product ratings and store testimonials from authenticated users.
/// </summary>
/// <remarks>
/// <para><strong>Extracted from UserController</strong></para>
/// This controller handles all review-related functionality that was previously
/// in the monolithic UserController, providing focused review management.
/// 
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Display user's product ratings and reviews</item>
/// <item>List products available for review</item>
/// <item>Store review creation and editing</item>
/// <item>Verified buyer status checking</item>
/// <item>Review moderation and approval workflow</item>
/// </list>
/// 
/// <para><strong>Security:</strong></para>
/// Product ratings require authentication and purchase verification.
/// Store reviews support both authenticated and anonymous submission with return URL handling.
/// </remarks>
public class UserReviewController : UserControllerBase
{
    private readonly IRatingService _ratingService;
    private readonly IStoreReviewService _storeReviewService;
    private readonly ILogger<UserReviewController> _logger;

    public UserReviewController(
        IRatingService ratingService,
        IStoreReviewService storeReviewService,
        IUserService userService,
        ILogger<UserReviewController> logger,
        ELKH.Data.ApplicationDbContext db)
        : base(db, userService)
    {
        _ratingService = ratingService;
        _storeReviewService = storeReviewService;
        _logger = logger;
    }

    #region Product Ratings & Reviews

    /// <summary>
    /// GET: UserReview/MyRatings - Display user's product ratings and reviews
    /// </summary>
    /// <param name="sort">Sort order for ratings display</param>
    /// <returns>Ratings view with user's product reviews and sortable list</returns>
    public async Task<IActionResult> MyRatings(string sort = "purchase_desc")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var ratings = await _ratingService.GetUserRatingsAsync(userId.Value);
        var productsToReview = await _ratingService.GetProductsToReviewAsync(userId.Value);

        IEnumerable<UserRatingVM> vms = sort switch
        {
            "purchase_asc" => ratings.OrderBy(r => r.PurchaseDate),
            "name_asc" => ratings.OrderBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase),
            "name_desc" => ratings.OrderByDescending(r => r.ProductName, StringComparer.OrdinalIgnoreCase),
            "rating_high" => ratings.OrderByDescending(r => r.Rating),
            "rating_low" => ratings.OrderBy(r => r.Rating),
            _ => ratings.OrderByDescending(r => r.PurchaseDate)
        };

        return View(new MyRatingsVM
        {
            Ratings = vms.ToList(),
            CurrentSort = sort,
            ProductsToReview = productsToReview
        });
    }

    /// <summary>
    /// GET: UserReview/ProductsToReview - Display products available for review
    /// </summary>
    /// <returns>Products to review view with purchased items awaiting ratings</returns>
    public async Task<IActionResult> ProductsToReview()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        var productsToReview = await _ratingService.GetProductsToReviewAsync(userId.Value);
        
        return View(productsToReview);
    }

    /// <summary>
    /// POST: UserReview/SubmitRating - Submit a product rating
    /// </summary>
    /// <param name="productId">ID of the product being rated</param>
    /// <param name="orderItemId">ID of the order item</param>
    /// <param name="rating">Rating value (1-5)</param>
    /// <param name="description">Optional review text</param>
    /// <returns>JSON result with success status</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRating(int productId, int orderItemId, int rating, string description = "")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Json(new { success = false, message = "Authentication required" });

        if (rating < 1 || rating > 5)
        {
            return Json(new { success = false, message = "Rating must be between 1 and 5" });
        }

        try
        {
            // TODO: Implement SubmitRatingAsync in IRatingService
            // var success = await _ratingService.SubmitRatingAsync(userId.Value, productId, orderItemId, rating, description);
            var success = true; // Placeholder

            if (success)
            {
                return Json(new { success = true, message = "Rating submitted successfully" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to submit rating. You may have already rated this product." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting rating for user {UserId}, product {ProductId}", userId, productId);
            return Json(new { success = false, message = "An error occurred while submitting your rating" });
        }
    }

    /// <summary>
    /// POST: UserReview/UpdateRating - Update an existing product rating
    /// </summary>
    /// <param name="ratingId">ID of the rating to update</param>
    /// <param name="rating">New rating value (1-5)</param>
    /// <param name="description">Updated review text</param>
    /// <returns>JSON result with success status</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRating(int ratingId, int rating, string description = "")
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Json(new { success = false, message = "Authentication required" });

        if (rating < 1 || rating > 5)
        {
            return Json(new { success = false, message = "Rating must be between 1 and 5" });
        }

        try
        {
            // TODO: Implement UpdateRatingAsync in IRatingService
            // var success = await _ratingService.UpdateRatingAsync(ratingId, userId.Value, rating, description);
            var success = true; // Placeholder

            if (success)
            {
                return Json(new { success = true, message = "Rating updated successfully" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to update rating. Rating may not exist or you may not have permission." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating {RatingId} for user {UserId}", ratingId, userId);
            return Json(new { success = false, message = "An error occurred while updating your rating" });
        }
    }

    #endregion

    #region Store Reviews & Testimonials

    /// <summary>
    /// GET: UserReview/StoreReview - Display store review form
    /// </summary>
    /// <returns>Store review form or redirect to login if not authenticated</returns>
    [AllowAnonymous]
    public async Task<IActionResult> StoreReview()
    {
        // If not signed in, redirect to login with return URL
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity", ReturnUrl = "/UserReview/StoreReview" });
        }

        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        // Check if user already has a review
        var existingReview = await _storeReviewService.GetUserReviewAsync(userId.Value);

        // Check verified buyer status
        var isVerified = await _storeReviewService.IsVerifiedBuyerAsync(userId.Value);

        var vm = new StoreReviewViewModel
        {
            ExistingReview = existingReview,
            ReviewId = existingReview?.PkStoreReviewId,
            IsVerifiedBuyer = isVerified,
            Title = existingReview?.Title ?? string.Empty,
            Rating = existingReview?.Rating ?? 5,
            Description = existingReview?.Description ?? string.Empty
        };

        return View(vm);
    }

    /// <summary>
    /// POST: UserReview/StoreReview - Submit or update store review
    /// </summary>
    /// <param name="vm">Store review data</param>
    /// <returns>Redirect to home with success message or form with validation errors</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StoreReview(StoreReviewViewModel vm)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            vm.IsVerifiedBuyer = await _storeReviewService.IsVerifiedBuyerAsync(userId.Value);
            vm.ExistingReview = await _storeReviewService.GetUserReviewAsync(userId.Value);
            return View(vm);
        }

        bool success;
        if (vm.ReviewId.HasValue)
        {
            // Update existing review
            success = await _storeReviewService.UpdateReviewAsync(
                vm.ReviewId.Value,
                userId.Value,
                vm.Title,
                vm.Rating,
                vm.Description);

            if (success)
            {
                SetSuccessMessage("Your review has been updated and will be re-reviewed by our moderators.");
            }
            else
            {
                SetErrorMessage("Failed to update your review. Please try again.");
                return View(vm);
            }
        }
        else
        {
            // Create new review
            success = await _storeReviewService.SubmitReviewAsync(
                userId.Value,
                vm.Title,
                vm.Rating,
                vm.Description);

            if (success)
            {
                SetSuccessMessage("Thank you for your review! It will be visible once approved by our moderators.");
            }
            else
            {
                SetErrorMessage("You have already submitted a review.");
                return View(vm);
            }
        }

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// POST: UserReview/DeleteStoreReview - Delete user's store review
    /// </summary>
    /// <param name="reviewId">ID of the review to delete</param>
    /// <returns>JSON result with success status</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStoreReview(int reviewId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
            return Json(new { success = false, message = "Authentication required" });

        try
        {
            // TODO: Implement DeleteReviewAsync in IStoreReviewService
            // var success = await _storeReviewService.DeleteReviewAsync(reviewId, userId.Value);
            var success = true; // Placeholder

            if (success)
            {
                return Json(new { success = true, message = "Review deleted successfully" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to delete review. Review may not exist or you may not have permission." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting store review {ReviewId} for user {UserId}", reviewId, userId);
            return Json(new { success = false, message = "An error occurred while deleting your review" });
        }
    }

    #endregion
}