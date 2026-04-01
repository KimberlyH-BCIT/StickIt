using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{

    [Authorize(Roles = "Admin,Manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context     = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ================= DASHBOARD =================
        public async Task<IActionResult> Index()
        {
            var now      = DateTime.UtcNow;
            var weekAgo  = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            ViewBag.TotalProducts = await _context.Product.CountAsync(p => !p.IsDeleted);
            ViewBag.ActiveProducts = await _context.Product.CountAsync(p => p.IsActive && !p.IsDeleted);
            ViewBag.InactiveProducts = await _context.Product.CountAsync(p => !p.IsActive && !p.IsDeleted);
            ViewBag.DeletedCount = await _context.Product.CountAsync(p => p.IsDeleted);
            ViewBag.StockUpCount     = await _context.Product.CountAsync(p => p.StockQuantity > 20);
            ViewBag.StockDownCount   = await _context.Product.CountAsync(p => p.StockQuantity <= 20);
            ViewBag.LowStockCount    = await _context.Product.CountAsync(p => p.StockQuantity <= 5);
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

            var query = _context.Product
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Where(p => !p.IsDeleted) 
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Category != null && p.Category.CategoryName.Contains(search))
                );
            }

            if (stockFilter == "low")
                query = query.Where(p => p.StockQuantity <= 20);
            else if (stockFilter == "stocked")
                query = query.Where(p => p.StockQuantity >= 21);

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

            var rawProducts = await query
                .OrderByDescending(p => p.PkProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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

        // ================= TOGGLE ACTIVE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, string? stockFilter, string? activeFilter, string? search, int page = 1)
        {
            var p = await _context.Product.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{p.Name}' is now {(p.IsActive ? "Active" : "Inactive")}.";

            return RedirectToAction("ListOfProducts", new { search, stockFilter, activeFilter, page });
        }

        // ================= PRODUCT DETAILS =================
        public async Task<IActionResult> ProductDetails(int id)
        {
            var p = await _context.Product
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .Include(p => p.ProductRatings)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null) return NotFound();

            // Compute average rating
            double avgRating = 0;
            int ratingCount  = 0;
            if (p.ProductRatings != null && p.ProductRatings.Any())
            {
                var approved = p.ProductRatings.Where(r => r.Approved && !r.IsDeleted).ToList();
                ratingCount  = approved.Count;
                avgRating    = ratingCount > 0 ? approved.Average(r => r.Rating) : 0;
            }

            var vm = new ProductVM
            {
                ProductId       = p.PkProductId,
                ProductName     = p.Name,
                Description     = p.Description,
                Price           = p.Price,
                DiscountPercent = p.DiscountPercent,
                StockQuantity   = (int)p.StockQuantity,
                IsActive        = p.IsActive,
                CategoryId      = p.FkCategoryId,
                CategoryName    = p.Category?.CategoryName ?? "",
                Thumbnail       = p.ProductImage?.FirstOrDefault()?.ProductImageURL ?? "",
                AverageRating   = avgRating
            };

            // Pass ratings to view via ViewBag
            ViewBag.Ratings     = p.ProductRatings?
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
            var product = await _context.Product.FindAsync(id);

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
            var deleted = await _context.Product
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
            var product = await _context.Product.FindAsync(id);

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
    }
}
