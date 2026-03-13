using ELKH.Data;
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

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context     = context;
            _userManager = userManager;
        }

        // ================= DASHBOARD =================
        public async Task<IActionResult> Index()
        {
            var now      = DateTime.UtcNow;
            var weekAgo  = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            ViewBag.TotalProducts    = await _context.Products.CountAsync();
            ViewBag.ActiveProducts   = await _context.Products.CountAsync(p => p.IsActive);
            ViewBag.InactiveProducts = await _context.Products.CountAsync(p => !p.IsActive);
            ViewBag.StockUpCount     = await _context.Products.CountAsync(p => p.StockQuantity > 20);
            ViewBag.StockDownCount   = await _context.Products.CountAsync(p => p.StockQuantity <= 20);
            ViewBag.LowStockCount    = await _context.Products.CountAsync(p => p.StockQuantity <= 5);
            ViewBag.WeeklyOrders     = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo);
            ViewBag.MonthlyOrders    = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo);
            ViewBag.TotalStaff       = (await _userManager.GetUsersInRoleAsync("Staff")).Count
                                     + (await _userManager.GetUsersInRoleAsync("Manager")).Count;

            return View();
        }

        // ================= LIST PRODUCTS =================//
        [HttpGet]
        public async Task<IActionResult> ListOfProducts(string search, string stockFilter, string activeFilter, int page = 1)
        {
            int pageSize = 8;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImage)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Category != null && p.Category.CategoryName.Contains(search))
                );
            }

            if (stockFilter == "low")
                query = query.Where(p => p.StockQuantity <= 5);
            else if (stockFilter == "medium")
                query = query.Where(p => p.StockQuantity > 5 && p.StockQuantity <= 20);
            else if (stockFilter == "stocked")
                query = query.Where(p => p.StockQuantity > 20);

            if (activeFilter == "active")
                query = query.Where(p => p.IsActive);
            else if (activeFilter == "inactive")
                query = query.Where(p => !p.IsActive);

            int total = await query.CountAsync();

            var rawProducts = await query
                .OrderBy(p => p.StockQuantity)
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
                StockQuantity = (int)p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "",
                Thumbnail = p.ProductImage.Select(i => i.ProductImageURL).FirstOrDefault() ?? ""
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
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{p.Name}' is now {(p.IsActive ? "Active" : "Inactive")}.";

            return RedirectToAction("ListOfProducts", new { search, stockFilter, activeFilter, page });
        }

        // ================= PRODUCT DETAILS =================
        public async Task<IActionResult> ProductDetails(int id)
        {
            var p = await _context.Products
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


        // ================= TRANSACTIONS =================
        public async Task<IActionResult> ListAllTransactions(string search, int page = 1)
        {
            int pageSize = 10;
            var query    = _context.Transactions.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.TransactionStatus.Contains(search));

            int total = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionVM
                {
                    PkTransactionId   = t.PkTransactionId,
                    TransactionStatus = t.TransactionStatus,
                    Amount            = t.Amount,
                    TransactionDate   = t.TransactionDate,
                    DeliberyFee       = t.DeliveryFee,
                    FkOrderId         = t.FkOrderId
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search      = search;

            return View(transactions);
        }

        // ================= STAFF ACCOUNTS =================
        public async Task<IActionResult> ListOfStaffAccount(string search)
        {
            var staffRoles = new[] { "Manager", "Staff", "Admin" };
            var allUsers   = _userManager.Users.ToList();
            var staffList  = new List<UserListVM>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Any(r => staffRoles.Contains(r)))
                    staffList.Add(new UserListVM
                    {
                        Id    = user.Id,
                        Email = user.Email ?? "",
                        Name  = user.UserName ?? "",
                        Roles = roles.ToList()
                    });
            }

            if (!string.IsNullOrEmpty(search))
                staffList = staffList
                    .Where(u => u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || u.Roles.Any(r => r.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            ViewBag.Search = search;
            return View(staffList);
        }
    }
}
