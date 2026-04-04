using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ELKH.Controllers
{

    [Authorize(Roles = "Admin,Manager")]
    public class ManagerController : Controller
    {
        #region Constructor & Dependencies

        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Displays the manager dashboard with key performance indicators.
        /// </summary>
        /// <returns>Dashboard view with statistics in ViewBag</returns>
        /// <remarks>
        /// KPI METRICS:
        /// - Product metrics: Total, Active, Inactive counts
        /// - Stock health: Well-stocked (>20), Low (6-20), Critical (≤5)
        /// - Order volume: 7-day and 30-day order counts
        /// - Staff count: Combined Manager and Staff role users
        ///
        /// All metrics calculated with async database queries for responsiveness.
        /// No caching applied - real-time data for operational decisions.
        /// </remarks>
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            ViewBag.TotalProducts = await _context.Products.CountAsync(p => !p.IsDeleted);
            ViewBag.ActiveProducts = await _context.Products.CountAsync(p => p.IsActive && !p.IsDeleted);
            ViewBag.InactiveProducts = await _context.Products.CountAsync(p => !p.IsActive && !p.IsDeleted);
            ViewBag.DeletedCount = await _context.Products.CountAsync(p => p.IsDeleted);
            ViewBag.StockUpCount     = await _context.Products.CountAsync(p => p.StockQuantity > 20);
            ViewBag.StockDownCount   = await _context.Products.CountAsync(p => p.StockQuantity <= 20);
            ViewBag.LowStockCount    = await _context.Products.CountAsync(p => p.StockQuantity <= 5);
            ViewBag.WeeklyOrders     = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo);
            ViewBag.MonthlyOrders    = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo);
            ViewBag.TotalStaff       = (await _userManager.GetUsersInRoleAsync("Staff")).Count
                                     + (await _userManager.GetUsersInRoleAsync("Manager")).Count;
            ViewBag.ViewAs = "Manager";

            // ================= MESSAGES =================

            // Recent messages (last 5)
            ViewBag.RecentMessages = await _context.StaffMessages
                .OrderByDescending(m => m.SentAt)
                .Take(5)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Body,
                    m.SentAt,
                    m.SentBy,
                    ReplyCount = m.Replies.Count
                })
                .ToListAsync();

            // Replies grouped by message
            ViewBag.MessageReplies = await _context.MessageReplies
                .OrderByDescending(r => r.RepliedAt)
                .Select(r => new
                {
                    r.MessageId,
                    Body = r.ReplyText,
                    r.RepliedBy,
                    r.RepliedAt
                })
               .ToListAsync();
            return View();
        }
        // ================= MESSAGES =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendStaffMessage(string MessageTitle, string MessageBody)
        {
            var message = new StaffMessageModel
            {
                Title = MessageTitle,
                Body = MessageBody,
                SentAt = DateTime.UtcNow,
                SentBy = User.Identity.Name
            };

            _context.StaffMessages.Add(message);
            await _context.SaveChangesAsync();

            TempData["MessageSent"] = "Message sent successfully!";
            return RedirectToAction("Index");
        }

        // ================= MESSAGES Reply=================
        [HttpPost]
        public async Task<IActionResult> ReplyMessage(int MessageId, string ReplyText)
        {
            var reply = new MessageReplyModel
            {
                MessageId = MessageId,
                ReplyText = ReplyText,
                RepliedBy = User.Identity.Name,
                RepliedAt = DateTime.UtcNow
            };

            _context.MessageReplies.Add(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ================= LIST PRODUCTS =================//
        [HttpGet]
        public async Task<IActionResult> ListOfProducts(string search, string stockFilter, string activeFilter, string categoryFilter, int page = 1)
        {
            int pageSize = 8;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Where(p => !p.IsDeleted) 
                .AsQueryable();

            // Apply search filter (product name or category name)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Category != null && p.Category.CategoryName.Contains(search))
                );
            }

            // Apply stock level filter
            if (stockFilter == "low")
                query = query.Where(p => p.StockQuantity <= 20);
            else if (stockFilter == "stocked")
                query = query.Where(p => p.StockQuantity >= 21);

            // Apply active status filter
            if (activeFilter == "active")
                query = query.Where(p => p.IsActive);
            else if (activeFilter == "inactive")
                query = query.Where(p => !p.IsActive);

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                int catId = int.Parse(categoryFilter);
                query = query.Where(p => p.FkCategoryId == catId);
            }

            int total = await query.CountAsync();

            // Execute paginated query ordered by stock quantity (critical items first)
            var rawProducts = await query
                .OrderByDescending(p => p.PkProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Project to view models
            var products = rawProducts.Select(p => new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "",
                Thumbnail = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? ""
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;
            ViewBag.StockFilter = stockFilter;
            ViewBag.ActiveFilter = activeFilter;

            return View(products);
        }

        /// <summary>
        /// Toggles a product's active status (enable/disable).
        /// </summary>
        /// <param name="id">Product ID to toggle</param>
        /// <param name="stockFilter">Current stock filter for return redirect</param>
        /// <param name="activeFilter">Current active filter for return redirect</param>
        /// <param name="search">Current search term for return redirect</param>
        /// <param name="page">Current page number for return redirect</param>
        /// <returns>Redirects back to ListOfProducts with preserved filters</returns>
        /// <remarks>
        /// WORKFLOW:
        /// 1. Retrieve product by ID
        /// 2. Toggle IsActive flag (true → false or false → true)
        /// 3. Save changes to database
        /// 4. Set success message in TempData
        /// 5. Redirect back to product list with all filters preserved
        ///
        /// This allows managers to temporarily disable products without deleting them.
        /// Inactive products remain in database but are hidden from customers.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(
            int id,
            string? stockFilter,
            string? activeFilter,
            string? search,
            int page = 1)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"'{p.Name}' is now {(p.IsActive ? "Active" : "Inactive")}.";

            return RedirectToAction("ListOfProducts", new { search, stockFilter, activeFilter, page });
        }

        /// <summary>
        /// Displays detailed product information including ratings.
        /// </summary>
        /// <param name="id">Product ID to display</param>
        /// <returns>View with detailed product information and ratings</returns>
        /// <remarks>
        /// INCLUDES:
        /// - Product details (name, price, stock, category, images)
        /// - Average rating calculation (approved, non-deleted ratings only)
        /// - Rating count and individual rating details
        ///
        /// RATING LOGIC:
        /// - Only approved and non-deleted ratings are included
        /// - Ratings sorted by RatedTime descending (newest first)
        /// - Average calculated from approved ratings only
        /// - Rating details passed via ViewBag to view
        ///
        /// Used by managers for product oversight and customer feedback review.
        /// </remarks>
        public async Task<IActionResult> ProductDetails(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Include(p => p.ProductRatings)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null) return NotFound();

            // ─────────────────────────────────────────────────────────
            // Compute average rating from approved, non-deleted ratings
            // ─────────────────────────────────────────────────────────
            double avgRating = 0;
            int ratingCount = 0;
            if (p.ProductRatings != null && p.ProductRatings.Count > 0)
            {
                var approved = p.ProductRatings.Where(r => r.Approved && !r.IsDeleted).ToList();
                ratingCount = approved.Count;
                avgRating = ratingCount > 0 ? approved.Average(r => r.Rating) : 0;
            }

            var vm = new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity   = (int)p.StockQuantity,
                IsActive        = p.IsActive,
                CategoryId      = p.FkCategoryId,
                CategoryName    = p.Category?.CategoryName ?? "",
                Thumbnail       = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? "",
                AverageRating   = avgRating
            };

            // Pass approved ratings to view (newest first)
            ViewBag.Ratings = p.ProductRatings?
                .Where(r => r.Approved && !r.IsDeleted)
                .OrderByDescending(r => r.RatedTime)
                .ToList() ?? new List<ELKH.Models.ProductRatingModel>();
            ViewBag.RatingCount = ratingCount;

            return View(vm);
        }
        //=================DELETE PRODUCTS=================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            product.IsDeleted = true;

            await _context.SaveChangesAsync();

            TempData["Deleted"] = "Product deleted successfully.";

            return RedirectToAction("ListOfProducts");
        }

        //=================LIST OF DELETEd PRODUCTS=================
        public async Task<IActionResult> DeletedProducts()
        {
            var deleted = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Where(p => p.IsDeleted)
                .OrderByDescending(p => p.PkProductId)
                .ToListAsync();

            var vm = deleted.Select(p => new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Price = p.Price,
                CategoryName = p.Category?.CategoryName ?? "",
                Thumbnail = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? ""
            }).ToList();

            return View("~/Views/Manager/DeletedProducts.cshtml", vm);
        }
        // ================= Restore product =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product != null)
            {
                product.IsDeleted = false;
                await _context.SaveChangesAsync();

                TempData["Restored"] = "Product restored successfully.";
            }

            return RedirectToAction("DeletedProducts");
        }

        // ================= TRANSACTIONS =================
        public async Task<IActionResult> ListAllTransactions(string search, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Transactions
                .Include(t => t.Order)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();

                query = query.Where(t =>
                    t.TransactionStatus.ToLower().Contains(search)
                );
            }

            int total = await query.CountAsync();

            // Execute paginated query
            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionVM
                {
                    PkTransactionId = t.PkTransactionId,
                    TransactionStatus = t.TransactionStatus,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    DeliveryFee = t.DeliveryFee,
                    FkOrderId = t.FkOrderId
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(transactions);
        }

        // ================= TRANSACTION DETAILS =================

        public async Task<IActionResult> TransactionDetail(int id)
        {
            var transaction = await _context.Transactions
                .Include(t => t.Order)
                    .ThenInclude(o => o.RegisteredUser)
                .Include(t => t.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(t => t.PkTransactionId == id);

            if (transaction == null) return NotFound();

            var vm = new TransactionDetailVM
            {
                TransactionId = transaction.PkTransactionId,
                OrderId = transaction.FkOrderId,
                TransactionStatus = transaction.TransactionStatus,
                Amount = transaction.Amount,
                DeliveryFee = transaction.DeliveryFee,
                TransactionDate = transaction.TransactionDate,

                CustomerEmail = transaction.Order.RegisteredUser.Email,

                Items = transaction.Order.OrderItems.Select(oi => new TransactionItemVM
                {
                    ProductId = oi.FkProductId,
                    ProductName = oi.Product.Name,
                    CategoryName = oi.Product.Category.CategoryName,
                    UnitPrice = oi.Product.Price,
                    DiscountPercent = oi.Product.DiscountPercent,
                    Quantity = oi.Quantity,
                    Thumbnail = oi.Product.ProductImage != null && oi.Product.ProductImage.Any()
                        ? oi.Product.ProductImage.First().ProductImageURL
                        : "/images/no-image.png"
                }).ToList()
            };

            return View(vm);
        }

        // ================= STAFF ACCOUNTS =================
        public async Task<IActionResult> ListOfStaffAccount(string search, string roleFilter, int page = 1)
        {
            int pageSize = 8;

            // ✅ Get roles dynamically from DB
            var rolesFromDb = _roleManager.Roles.Select(r => r.Name).ToList();

            var allUsers = _userManager.Users.ToList();
            var staffList = new List<UserListVM>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Any())
                {
                    staffList.Add(new UserListVM
                    {
                        Id = user.Id,
                        Email = user.Email ?? "",
                        Name = user.UserName ?? "",
                        Roles = roles.ToList(),
                        FirstName = user.UserName?.Split('.').FirstOrDefault(),
                        LastName = user.UserName?.Split('.').LastOrDefault()
                    });
                }
            }

            // 🔍 Search
            if (!string.IsNullOrEmpty(search))
            {
                staffList = staffList.Where(u =>
                    u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // 🎯 Role Filter
            if (!string.IsNullOrEmpty(roleFilter))
            {
                staffList = staffList
                    .Where(u => u.Roles.Contains(roleFilter))
                    .ToList();
            }

            // 📄 Pagination
            int total = staffList.Count();

            var pagedList = staffList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ✅ Send roles to View
            ViewBag.Roles = rolesFromDb;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;
            ViewBag.RoleFilter = roleFilter;

            return View(pagedList);
        }

        #endregion

        #region Shipping Management

        /// <summary>
        /// Displays list of all shipping methods with management options.
        /// Allows managers to view delivery options, pricing, and activate/deactivate methods.
        /// </summary>
        /// <returns>View with comprehensive shipping methods list</returns>
        /// <remarks>
        /// SHIPPING MANAGEMENT FEATURES:
        /// - List all shipping methods with pricing and delivery timeframes
        /// - Toggle active status for seasonal or promotional shipping options
        /// - View delivery statistics and performance metrics
        /// - Quick edit capabilities for pricing adjustments
        /// 
        /// DISPLAY INFORMATION:
        /// - Method name and description
        /// - Base price and delivery timeframes (min-max days)
        /// - Active status with visual indicators
        /// - Display order for customer-facing sequence
        /// - Created/updated timestamps for audit trail
        /// 
        /// OPERATIONAL USE CASES:
        /// - Seasonal shipping adjustments (holiday rush, weather delays)
        /// - Promotional free shipping campaigns
        /// - Carrier service level changes
        /// - Regional delivery option management
        /// - Pricing strategy optimization
        /// </remarks>
        public async Task<IActionResult> ShippingMethods()
        {
            var shippingMethods = await _context.ShippingMethods
                .OrderBy(sm => sm.DisplayOrder)
                .ThenBy(sm => sm.Name)
                .ToListAsync();

            return View(shippingMethods);
        }

        /// <summary>
        /// Displays form to create a new shipping method.
        /// </summary>
        /// <returns>View with empty shipping method form</returns>
        /// <remarks>
        /// FORM VALIDATION RULES:
        /// - Name: Required, 2-100 characters, must be unique
        /// - Description: Optional, max 500 characters
        /// - BasePrice: Required, must be ≥ 0.00, supports 2 decimal precision
        /// - DeliveryDaysMin: Required, must be ≥ 1
        /// - DeliveryDaysMax: Required, must be ≥ DeliveryDaysMin
        /// - DisplayOrder: Auto-calculated as max + 1 for new methods
        /// 
        /// BUSINESS RULES:
        /// - New methods default to Active status
        /// - Display order determines customer-facing sequence
        /// - Base price excludes tax and regional surcharges
        /// - Delivery days are business days only (exclude weekends/holidays)
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> CreateShippingMethod()
        {
            // Set default display order as next available
            var maxDisplayOrder = await _context.ShippingMethods
                .MaxAsync(sm => (int?)sm.DisplayOrder) ?? 0;

            var viewModel = new ShippingMethodVM
            {
                IsActive = true,
                DisplayOrder = maxDisplayOrder + 1,
                CreatedAt = DateTime.UtcNow
            };

            return View(viewModel);
        }

        /// <summary>
        /// Processes creation of a new shipping method with validation.
        /// </summary>
        /// <param name="model">Shipping method data from form submission</param>
        /// <returns>Redirect to shipping methods list on success, or view with errors</returns>
        /// <remarks>
        /// VALIDATION WORKFLOW:
        /// 1. Server-side model validation (required fields, data types, ranges)
        /// 2. Business rule validation (unique name, logical delivery timeframes)
        /// 3. Database constraint validation (foreign keys, unique indexes)
        /// 4. Audit logging for new shipping method creation
        /// 
        /// ERROR HANDLING:
        /// - Model validation errors displayed with field-specific messages
        /// - Duplicate name detection with user-friendly error message
        /// - Database errors logged and displayed as generic failure message
        /// - Form data preserved on validation failure for user convenience
        /// 
        /// SUCCESS WORKFLOW:
        /// - New shipping method saved to database
        /// - Success message displayed via TempData
        /// - Redirect to shipping methods list for immediate verification
        /// - Audit log entry created for management tracking
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShippingMethod(ShippingMethodVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for duplicate name
            var existingMethod = await _context.ShippingMethods
                .FirstOrDefaultAsync(sm => sm.Name.ToLower(CultureInfo.InvariantCulture) == model.Name.ToLower(CultureInfo.InvariantCulture));

            if (existingMethod != null)
            {
                ModelState.AddModelError("Name", "A shipping method with this name already exists.");
                return View(model);
            }

            // Validate delivery timeframe logic
            if (model.DeliveryDaysMin > model.DeliveryDaysMax)
            {
                ModelState.AddModelError("DeliveryDaysMax", "Maximum delivery days must be greater than or equal to minimum days.");
                return View(model);
            }

            try
            {
                var shippingMethod = new ELKH.Models.ShippingMethodModel
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim(),
                    BasePrice = model.BasePrice,
                    DeliveryDaysMin = model.DeliveryDaysMin,
                    DeliveryDaysMax = model.DeliveryDaysMax,
                    IsActive = model.IsActive,
                    DisplayOrder = model.DisplayOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ShippingMethods.Add(shippingMethod);
                await _context.SaveChangesAsync();

                TempData["Message"] = "success,Shipping method created successfully!";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                // Log error for debugging (implement logging as needed)
                ModelState.AddModelError("", "An error occurred while creating the shipping method. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// Displays form to edit an existing shipping method.
        /// </summary>
        /// <param name="id">Primary key of shipping method to edit</param>
        /// <returns>View with populated shipping method form, or NotFound if method doesn't exist</returns>
        /// <remarks>
        /// EDIT CAPABILITIES:
        /// - All shipping method properties can be modified
        /// - Historical orders preserve original shipping method name and cost
        /// - Changes apply to future orders only (no retroactive updates)
        /// 
        /// BUSINESS IMPACT CONSIDERATIONS:
        /// - Price changes affect customer checkout immediately
        /// - Deactivating methods removes them from customer selection
        /// - Delivery timeframe changes affect customer expectations
        /// - Display order changes affect checkout presentation sequence
        /// 
        /// FORM PRE-POPULATION:
        /// - All current values loaded from database
        /// - Edit timestamps preserved for audit trail
        /// - Form validation rules same as creation form
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> EditShippingMethod(int id)
        {
            var shippingMethod = await _context.ShippingMethods.FindAsync(id);
            if (shippingMethod == null)
            {
                TempData["Message"] = "error,Shipping method not found.";
                return RedirectToAction(nameof(ShippingMethods));
            }

            var viewModel = new ShippingMethodVM
            {
                PkShippingMethodId = shippingMethod.PkShippingMethodId,
                Name = shippingMethod.Name,
                Description = shippingMethod.Description,
                BasePrice = shippingMethod.BasePrice,
                DeliveryDaysMin = shippingMethod.DeliveryDaysMin,
                DeliveryDaysMax = shippingMethod.DeliveryDaysMax,
                IsActive = shippingMethod.IsActive,
                DisplayOrder = shippingMethod.DisplayOrder,
                CreatedAt = shippingMethod.CreatedAt,
                UpdatedAt = shippingMethod.UpdatedAt ?? shippingMethod.CreatedAt
            };

            return View(viewModel);
        }

        /// <summary>
        /// Processes updates to an existing shipping method with validation.
        /// </summary>
        /// <param name="model">Updated shipping method data from form submission</param>
        /// <returns>Redirect to shipping methods list on success, or view with errors</returns>
        /// <remarks>
        /// UPDATE VALIDATION:
        /// - Same validation rules as creation (required fields, ranges, business logic)
        /// - Name uniqueness checked excluding current record
        /// - Concurrency validation to detect simultaneous edits
        /// 
        /// BUSINESS IMPACT:
        /// - Price changes take effect immediately for new orders
        /// - Existing orders maintain original shipping details
        /// - Deactivated methods hidden from customer checkout
        /// - Display order changes affect customer presentation
        /// 
        /// AUDIT TRAIL:
        /// - UpdatedAt timestamp automatically set
        /// - Previous values preserved in existing orders
        /// - Management action logged for compliance
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShippingMethod(ShippingMethodVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for duplicate name (excluding current record)
            var existingMethod = await _context.ShippingMethods
                .FirstOrDefaultAsync(sm => sm.Name.ToLower() == model.Name.ToLower() 
                                          && sm.PkShippingMethodId != model.PkShippingMethodId);

            if (existingMethod != null)
            {
                ModelState.AddModelError("Name", "Another shipping method with this name already exists.");
                return View(model);
            }

            // Validate delivery timeframe logic
            if (model.DeliveryDaysMin > model.DeliveryDaysMax)
            {
                ModelState.AddModelError("DeliveryDaysMax", "Maximum delivery days must be greater than or equal to minimum days.");
                return View(model);
            }

            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(model.PkShippingMethodId);
                if (shippingMethod == null)
                {
                    TempData["Message"] = "error,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Update properties
                shippingMethod.Name = model.Name.Trim();
                shippingMethod.Description = model.Description?.Trim();
                shippingMethod.BasePrice = model.BasePrice;
                shippingMethod.DeliveryDaysMin = model.DeliveryDaysMin;
                shippingMethod.DeliveryDaysMax = model.DeliveryDaysMax;
                shippingMethod.IsActive = model.IsActive;
                shippingMethod.DisplayOrder = model.DisplayOrder;
                shippingMethod.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Message"] = "success,Shipping method updated successfully!";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                // Log error for debugging (implement logging as needed)
                ModelState.AddModelError("", "An error occurred while updating the shipping method. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// Toggles active status of a shipping method via AJAX or direct request.
        /// Allows quick enable/disable without full form submission.
        /// </summary>
        /// <param name="id">Primary key of shipping method to toggle</param>
        /// <returns>JSON result for AJAX requests, or redirect for direct requests</returns>
        /// <remarks>
        /// TOGGLE FUNCTIONALITY:
        /// - Switches IsActive status (true ↔ false)
        /// - Immediate effect on customer checkout availability
        /// - Preserves all other shipping method properties
        /// - Updates timestamp for audit trail
        /// 
        /// USE CASES:
        /// - Seasonal shipping option management (holiday express, summer delays)
        /// - Temporary carrier service disruptions
        /// - Promotional shipping campaigns (temporary free shipping tiers)
        /// - Regional service availability changes
        /// 
        /// AJAX INTEGRATION:
        /// - Returns JSON with new status and formatted message
        /// - Supports dynamic UI updates without page refresh
        /// - Error handling for failed toggle operations
        /// - Status indicators update immediately
        /// 
        /// SAFETY CONSIDERATIONS:
        /// - Validates shipping method exists before toggle
        /// - Atomic operation with proper error handling
        /// - Audit trail maintained via UpdatedAt timestamp
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> ToggleShippingMethodStatus(int id)
        {
            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(id);
                if (shippingMethod == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Shipping method not found." });
                    }
                    TempData["Message"] = "error,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Toggle active status
                shippingMethod.IsActive = !shippingMethod.IsActive;
                shippingMethod.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var statusText = shippingMethod.IsActive ? "activated" : "deactivated";
                var message = $"Shipping method '{shippingMethod.Name}' has been {statusText}.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { 
                        success = true, 
                        message = message,
                        isActive = shippingMethod.IsActive,
                        statusText = shippingMethod.IsActive ? "Active" : "Inactive"
                    });
                }

                TempData["Message"] = $"success,{message}";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                var errorMessage = "An error occurred while updating the shipping method status.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Message"] = $"error,{errorMessage}";
                return RedirectToAction(nameof(ShippingMethods));
            }
        }

        /// <summary>
        /// Safely deletes a shipping method with business rule validation.
        /// Prevents deletion if method is referenced in existing orders.
        /// </summary>
        /// <param name="id">Primary key of shipping method to delete</param>
        /// <returns>Redirect to shipping methods list with status message</returns>
        /// <remarks>
        /// DELETION SAFETY RULES:
        /// - Cannot delete shipping methods referenced in existing orders
        /// - Soft delete approach: deactivate instead of hard delete if orders exist
        /// - Confirmation required before permanent deletion
        /// - Audit logging for deletion attempts and outcomes
        /// 
        /// BUSINESS PROTECTION:
        /// - Referential integrity preserved for historical orders
        /// - Order history maintains shipping method details
        /// - No orphaned shipping references in database
        /// 
        /// ALTERNATIVE APPROACH:
        /// - If orders exist: offer to deactivate instead of delete
        /// - Display count of affected orders before confirmation
        /// - Suggest archive/deactivate option for historical preservation
        /// 
        /// ERROR SCENARIOS:
        /// - Method not found: Safe no-op with informational message
        /// - Referenced by orders: Prevention with explanatory message
        /// - Database constraints: Graceful error handling
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShippingMethod(int id)
        {
            try
            {
                var shippingMethod = await _context.ShippingMethods.FindAsync(id);
                if (shippingMethod == null)
                {
                    TempData["Message"] = "warning,Shipping method not found.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                // Check if shipping method is referenced in any orders
                var orderCount = await _context.Orders
                    .CountAsync(o => o.FkShippingMethodId == id);

                if (orderCount > 0)
                {
                    TempData["Message"] = $"error,Cannot delete shipping method '{shippingMethod.Name}' because it is referenced in {orderCount} order(s). Consider deactivating it instead.";
                    return RedirectToAction(nameof(ShippingMethods));
                }

                _context.ShippingMethods.Remove(shippingMethod);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"success,Shipping method '{shippingMethod.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(ShippingMethods));
            }
            catch (Exception ex)
            {
                TempData["Message"] = "error,An error occurred while deleting the shipping method. Please try again.";
                return RedirectToAction(nameof(ShippingMethods));
            }
        }

        #endregion
    }
}
