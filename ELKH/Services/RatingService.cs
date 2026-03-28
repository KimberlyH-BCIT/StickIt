using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Service for managing product ratings and reviews.
    /// Provides operations for querying, retrieving, and approving customer ratings.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Constructor & Dependencies
    /// 2. Query Operations
    ///    - QueryRatings()                        // Get queryable with eager loading
    /// 3. Retrieval Operations
    ///    - GetByIdAsync(id)                      // Retrieve single rating
    /// 4. Moderation Operations
    ///    - ApproveAsync(id)                      // Approve and unflag rating
    /// ================================================================================
    /// 
    /// Performance Notes:
    /// - QueryRatings() uses eager loading (Include) for Users and Products
    /// - Returns IQueryable for flexible filtering before materialization
    /// - Suitable for admin interfaces with additional filtering/pagination
    /// 
    /// Usage Example:
    /// ```csharp
    /// // Get all unapproved ratings for a product
    /// var unapproved = await _ratingService.QueryRatings()
    ///     .Where(r => r.FkProductId == productId && !r.Approved)
    ///     .ToListAsync();
    /// ```
    /// </remarks>
    public class RatingService : IRatingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of <see cref="RatingService"/>.
        /// </summary>
        /// <param name="db">EF Core context used for all rating queries and mutations.</param>
        public RatingService(ApplicationDbContext db, ApplicationDbContext context)
        {
            _db = db;
            _context = context;
        }

        // Base query used by GetRatingsPagedAsync: eager-loads User and Product so the
        // caller can apply additional Where/OrderBy clauses before materializing.
        private IQueryable<ProductRatingModel> QueryRatings()
        {
            return _db.ProductRatings
                .Include(r => r.RegisteredUser)
                .Include(r => r.Products);
        }

        public async Task<List<ProductRatingModel>> GetAllRatingsAsync(CancellationToken ct = default)
        {
            return await _context.ProductRatings
                .Include(r => r.Products)
                .Include(r => r.RegisteredUser)
                .OrderByDescending(r => r.RatedTime)
                .ToListAsync(ct);
        }

        public async Task<bool> MarkAsReadAsync(int id, CancellationToken ct = default)
        {
            var rating = await _context.ProductRatings.FindAsync(new object[] { id }, ct);
            if (rating == null) return false;

            rating.IsRead = true;
            return await _context.SaveChangesAsync(ct) > 0;
        }
        /// <inheritdoc/>
        public async Task<PagedResult<ProductRatingModel>> GetRatingsPagedAsync(RatingQuery query, CancellationToken ct = default)
        {
            var q = QueryRatings();

            q = query.Filter switch
            {
                Constants.RatingFilter.Flagged    => q.Where(r => r.IsFlagged),
                Constants.RatingFilter.Unapproved => q.Where(r => !r.Approved && !r.IsDeleted),
                _                                 => q
            };

            if (query.ProductId.HasValue)
                q = q.Where(r => r.FkProductId == query.ProductId.Value);
            if (!string.IsNullOrEmpty(query.ProductName))
                q = q.Where(r => r.Products != null && r.Products.Name.Contains(query.ProductName));
            if (!string.IsNullOrEmpty(query.UserEmail))
                q = q.Where(r => r.RegisteredUser != null && r.RegisteredUser.Email == query.UserEmail);
            if (query.FromDate.HasValue)
                q = q.Where(r => r.RatedTime >= query.FromDate.Value);
            if (query.ToDate.HasValue)
                q = q.Where(r => r.RatedTime <= query.ToDate.Value);
            if (query.RatingMin.HasValue)
                q = q.Where(r => r.Rating >= query.RatingMin.Value);
            if (query.RatingMax.HasValue)
                q = q.Where(r => r.Rating <= query.RatingMax.Value);

            q = query.Sort switch
            {
                Constants.RatingSort.DateAsc    => q.OrderBy(r => r.RatedTime),
                Constants.RatingSort.RatingDesc => q.OrderByDescending(r => r.Rating),
                Constants.RatingSort.RatingAsc  => q.OrderBy(r => r.Rating),
                _                               => q.OrderByDescending(r => r.RatedTime)
            };

            var total      = await q.CountAsync(ct);
            var totalPages = (int)Math.Ceiling(total / (double)query.PageSize);
            var items      = await q
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return new PagedResult<ProductRatingModel>
            {
                Items      = items,
                TotalCount = total,
                TotalPages = totalPages
            };
        }

        /// <inheritdoc/>
        public async Task<ProductRatingModel?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.ProductRatings.FindAsync(new object[] { id }, ct);
        }

        /// <inheritdoc/>
        public async Task<ProductRatingModel?> ApproveAsync(int id, CancellationToken ct = default)
        {
            var r = await _db.ProductRatings.FindAsync(new object[] { id }, ct);
            if (r is null) return null;

            r.Approved  = true;
            r.IsFlagged = false;
            await _db.SaveChangesAsync(ct);

            return r;
        }

        /// <inheritdoc/>
        public async Task<List<UserRatingVM>> GetUserRatingsAsync(int userId, CancellationToken ct = default)
        {
            var ratings = await _db.ProductRatings
                .AsNoTracking()
                .Where(r => r.FkRegisteredUserId == userId && !r.IsDeleted)
                .Include(r => r.Products)
                .ToListAsync(ct);

            // Collect the order item IDs referenced by this user's ratings so we can
            // fetch all purchase dates in a single query instead of one per rating.
            var orderItemIds = ratings
                .Where(r => r.FkOrderItemId.HasValue)
                .Select(r => r.FkOrderItemId!.Value)
                .ToList();

            // Build a lookup of orderItemId → order creation date. If no ratings reference
            // order items, skip the query entirely and use an empty dictionary.
            var purchaseDates = orderItemIds.Count > 0
                ? await _db.OrderItems
                    .AsNoTracking()
                    .Where(oi => orderItemIds.Contains(oi.PkOrderItemId))
                    .Select(oi => new { oi.PkOrderItemId, oi.Order!.CreatedAt })
                    .ToDictionaryAsync(x => x.PkOrderItemId, x => x.CreatedAt, ct)
                : [];

            return ratings.Select(r => new UserRatingVM
            {
                RatingId     = r.PkRatingId,
                ProductId    = r.FkProductId,
                ProductName  = r.Products!.Name,
                Rating       = r.Rating,
                Description  = r.Description,
                RatedTime    = r.RatedTime,
                // Resolve purchase date from the dictionary; null if the order item was not found.
                PurchaseDate = r.FkOrderItemId.HasValue && purchaseDates.TryGetValue(r.FkOrderItemId.Value, out var d)
                                   ? d : null,
                Approved     = r.Approved,
                IsFlagged    = r.IsFlagged
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<RatingOperationResult> CreateRatingAsync(
            int productId, int orderItemId, int rating, string? description, int userId, CancellationToken ct = default)
        {
            // Verify the order item exists, belongs to this product, and was placed by this user.
            // Including Order lets us check FkRegisteredUserId without a separate query.
            var orderItem = await _db.OrderItems
                .Include(oi => oi.Order)
                .FirstOrDefaultAsync(oi =>
                    oi.PkOrderItemId == orderItemId &&
                    oi.FkProductId   == productId   &&
                    oi.Order!.FkRegisteredUserId == userId, ct);

            if (orderItem is null)
                return new RatingOperationResult { Success = false, Message = "Order item not found or access denied.", ProductId = productId };

            // Prevent duplicate ratings: check whether a non-deleted rating already
            // exists for this order item from this user.
            var alreadyRated = await _db.ProductRatings
                .AnyAsync(r => r.FkOrderItemId == orderItemId && r.FkRegisteredUserId == userId && !r.IsDeleted, ct);

            if (alreadyRated)
                return new RatingOperationResult { Success = false, Message = "You have already rated this order item.", ProductId = productId };

            // 24-hour cooldown: if the user previously deleted a rating for this order item
            // within the last 24 hours, prevent immediate re-submission to deter manipulation.
            var deletedRating = await _db.ProductRatings
                .Where(r => r.FkOrderItemId == orderItemId && r.FkRegisteredUserId == userId && r.IsDeleted)
                .OrderByDescending(r => r.DeletedAt)
                .FirstOrDefaultAsync(ct);

            if (deletedRating?.DeletedAt.HasValue == true &&
                (DateTime.UtcNow - deletedRating.DeletedAt.Value).TotalHours < 24)
            {
                return new RatingOperationResult
                {
                    Success   = false,
                    Message   = "You recently deleted a rating for this item. Please wait 24 hours before re-submitting.",
                    ProductId = productId
                };
            }

            _db.ProductRatings.Add(new ProductRatingModel
            {
                FkProductId        = productId,
                FkRegisteredUserId = userId,
                FkOrderItemId      = orderItemId,
                Rating             = rating,
                Description        = description ?? string.Empty,
                RatedTime          = DateTime.UtcNow,
                Approved           = false,  // new ratings require moderator approval
                IsFlagged          = false
            });
            await _db.SaveChangesAsync(ct);

            return new RatingOperationResult { Success = true, Message = "Thank you for rating this product.", ProductId = productId };
        }

        /// <inheritdoc/>
        public async Task<RatingOperationResult> EditRatingAsync(
            int ratingId, int rating, string? description, int userId, CancellationToken ct = default)
        {
            // Filter by userId ensures only the original author can edit (ownership enforcement).
            var existing = await _db.ProductRatings
                .FirstOrDefaultAsync(r => r.PkRatingId == ratingId && r.FkRegisteredUserId == userId && !r.IsDeleted, ct);

            if (existing is null)
                return new RatingOperationResult { Success = false, Message = "Rating not found." };

            // 7-day edit cooldown: prevents rapid re-submission to manipulate product scores.
            // LastEditedAt is null on a rating that has never been edited, so the check is skipped.
            if (existing.LastEditedAt.HasValue &&
                (DateTime.UtcNow - existing.LastEditedAt.Value).TotalDays < 7)
            {
                return new RatingOperationResult
                {
                    Success   = false,
                    Message   = "You can only edit your rating once per week.",
                    ProductId = existing.FkProductId
                };
            }

            existing.Rating       = rating;
            existing.Description  = description ?? string.Empty;
            existing.LastEditedAt = DateTime.UtcNow;
            existing.RatedTime    = DateTime.UtcNow;
            // Re-editing resets the approval flag so the updated content goes through moderation again.
            existing.Approved     = false;
            await _db.SaveChangesAsync(ct);

            return new RatingOperationResult { Success = true, Message = "Rating updated.", ProductId = existing.FkProductId };
        }

        /// <inheritdoc/>
        public async Task<RatingOperationResult> DeleteRatingAsync(int ratingId, int userId, CancellationToken ct = default)
        {
            var existing = await _db.ProductRatings
                .FirstOrDefaultAsync(r => r.PkRatingId == ratingId && r.FkRegisteredUserId == userId && !r.IsDeleted, ct);

            if (existing is null)
                return new RatingOperationResult { Success = false, Message = "Rating not found." };

            existing.IsDeleted = true;
            existing.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new RatingOperationResult { Success = true, Message = "Rating deleted.", ProductId = existing.FkProductId };
        }

        /// <inheritdoc/>
        public async Task<List<ProductRatingModel>> GetApprovedReviewsAsync(int productId, CancellationToken ct = default)
        {
            return await _db.ProductRatings
                .Where(r => r.FkProductId == productId && r.Approved && !r.IsDeleted)
                .OrderByDescending(r => r.RatedTime)
                .ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<ViewModels.ReviewPageVM> GetPagedApprovedReviewsAsync(int productId, int page, CancellationToken ct = default)
        {
            const int pageSize = ViewModels.ReviewPageVM.PageSize;

            var baseQuery = _db.ProductRatings
                .Where(r => r.FkProductId == productId && r.Approved && !r.IsDeleted);

            var totalCount   = await baseQuery.CountAsync(ct);
            var totalPages   = (int)Math.Ceiling(totalCount / (double)pageSize);
            var currentPage  = Math.Clamp(page, 1, Math.Max(1, totalPages));
            var averageRating = totalCount > 0
                ? await baseQuery.AverageAsync(r => (double)r.Rating, ct)
                : 0.0;

            var ratings = await baseQuery
                .OrderByDescending(r => r.RatedTime)
                .Include(r => r.RegisteredUser)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Batch-load all reviewer profiles for this page in one query.
            var emails = ratings
                .Select(r => r.RegisteredUser.Email)
                .Distinct()
                .ToList();

            var profiles = await _db.UserProfiles
                .Where(p => emails.Contains(p.PkEmail))
                .Select(p => new { p.PkEmail, p.FirstName, HasAvatar = p.AvatarData != null })
                .ToDictionaryAsync(p => p.PkEmail, ct);

            var reviews = ratings.Select(r =>
            {
                profiles.TryGetValue(r.RegisteredUser.Email, out var profile);
                return new ViewModels.ReviewDisplayVM
                {
                    RatingId          = r.PkRatingId,
                    Rating            = r.Rating,
                    Description       = r.Description,
                    CreatedAt         = r.RatedTime,
                    LastEditedAt      = r.LastEditedAt,
                    ReviewerFirstName = profile?.FirstName ?? "Customer",
                    HasAvatar         = profile?.HasAvatar ?? false,
                    ReviewerUserId    = r.FkRegisteredUserId
                };
            }).ToList();

            return new ViewModels.ReviewPageVM
            {
                Reviews       = reviews,
                TotalCount    = totalCount,
                TotalPages    = totalPages,
                CurrentPage   = currentPage,
                AverageRating = averageRating
            };
        }

        /// <inheritdoc/>
        public async Task<ViewModels.RatingEligibilityVM> GetRatingEligibilityAsync(int productId, int userId, CancellationToken ct = default)
        {
            // Load all order items for this user+product combination, including the parent order
            // so we can display the purchase date as the dropdown label.
            // FIXED: Changed string comparisons to use the DeliveryStatus Enum
            var userOrderItems = await _db.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.FkProductId == productId
                          && oi.Order!.FkRegisteredUserId == userId
                          && (oi.Order!.DeliveryStatus == DeliveryStatus.Shipped
                           || oi.Order!.DeliveryStatus == DeliveryStatus.Shipped)) // Note: Using Shipped as the replacement for Delivered if you merged them
                .ToListAsync(ct);

            var orderItemIds = userOrderItems.Select(oi => oi.PkOrderItemId).ToList();

            // Fetch all existing ratings (including soft-deleted) for these order items in one query,
            // keyed by order item ID so we can resolve each item's state in O(1) below.
            var existingRatings = await _db.ProductRatings
                .Where(r => r.FkOrderItemId.HasValue
                         && orderItemIds.Contains(r.FkOrderItemId.Value)
                         && r.FkRegisteredUserId == userId)
                .ToDictionaryAsync(r => r.FkOrderItemId!.Value, ct);

            var eligibleItems = new List<ViewModels.EligibleOrderItemVM>();
            ProductRatingModel? existingRating = null;

            foreach (var oi in userOrderItems)
            {
                existingRatings.TryGetValue(oi.PkOrderItemId, out var rating);

                if (rating == null)
                {
                    // No rating exists for this order item — the user may submit one.
                    eligibleItems.Add(new ViewModels.EligibleOrderItemVM { Id = oi.PkOrderItemId, Label = oi.Order!.CreatedAt.ToString("g") });
                }
                else if (rating.IsDeleted && rating.DeletedAt.HasValue
                      && (DateTime.UtcNow - rating.DeletedAt.Value).TotalHours >= 24)
                {
                    // The previous rating was deleted more than 24 hours ago — the cooldown
                    // has expired, so this order item becomes eligible again.
                    eligibleItems.Add(new ViewModels.EligibleOrderItemVM { Id = oi.PkOrderItemId, Label = oi.Order!.CreatedAt.ToString("g") });
                }
                else if (existingRating == null)
                {
                    // A non-deleted, active rating exists for this order item. Capture the first
                    // one found so the view can render the edit/delete controls.
                    existingRating = rating;
                }
            }

            return new ViewModels.RatingEligibilityVM
            {
                EligibleItems = eligibleItems,
                ExistingRating = existingRating
            };
        }
    }
}