using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRoleRepository _roleRepo;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IFuzzyReindexService _reindexService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        public AdminController(
            IRoleRepository roleRepo,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AdminController> logger,
            IFuzzyReindexService reindexService,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _roleRepo = roleRepo;
            _context = context;
            _cache = cache;
            _logger = logger;
            _reindexService = reindexService;
            _userManager = userManager;
            _roleManager = roleManager;
        }
        /// <summary>Renders the admin dashboard with top products and stock stats.</summary>
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            // Top 5 products for dashboard widget
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Select(oi => new
                {
                    oi.FkProductId,
                    ProductName = oi.Product == null ? "Unknown" : oi.Product.Name,
                    ProductPrice = oi.Product == null ? 0m : oi.Product.Price,
                    oi.Quantity
                })
                .ToListAsync();

            ViewBag.TopProducts = orderItems
                .GroupBy(oi => new { oi.FkProductId, oi.ProductName, oi.ProductPrice })
                .Select(g => new TopProductVM
                {
                    ProductName = g.Key.ProductName,
                    UnitsSold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * g.Key.ProductPrice)
                })
                .OrderByDescending(p => p.UnitsSold)
                .Take(5)
                .ToList();

            ViewBag.ViewAs = "Admin";

            return View(new SalesVM());
        }

        /*============================== List Of All Users ==============================*/
        public async Task<IActionResult> ListUsers(string search, string roleFilter, int page = 1)
        {
            const int pageSize = 5;

            // Load roles dynamically from DB — new roles appear here automatically
            ViewBag.Roles = new List<string> { "All" }
                .Concat(await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync())
                .ToList();

            bool hasRoleFilter = !string.IsNullOrEmpty(roleFilter) && roleFilter != "All";

            IList<IdentityUser> candidates;

            if (hasRoleFilter)
            {
                candidates = await _userManager.GetUsersInRoleAsync(roleFilter);

                if (!string.IsNullOrEmpty(search))
                {
                    candidates = candidates
                        .Where(u => u.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                        .ToList();
                }
            }
            else
            {
                IQueryable<IdentityUser> query = _userManager.Users;
                if (!string.IsNullOrEmpty(search))
                    query = query.Where(u => u.Email != null && u.Email.Contains(search));

                candidates = await query.ToListAsync();
            }

            int totalUsers = candidates.Count;

            var pageUsers = candidates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userList = new List<UserListVM>(pageUsers.Count);
            foreach (var user in pageUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListVM
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList()
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.Search = search;
            ViewBag.RoleFilter = roleFilter;


            var allUsers = _userManager.Users.ToList();
            ViewBag.TotalUsers = allUsers.Count;

            var roleCounts = new Dictionary<string, int>();

            var rolesFromDb = await _roleManager.Roles
                .AsNoTracking()
                .Select(r => r.Name!)
                .ToListAsync();

            foreach (var role in rolesFromDb)
            {
                roleCounts[role] = 0;
            }

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                foreach (var role in roles)
                {
                    if (roleCounts.ContainsKey(role))
                        roleCounts[role]++;
                }
            }

            ViewBag.RoleCounts = roleCounts;
            return View(userList);
        }

        /*============================== Account Details ==============================*/
        [HttpGet]
        public async Task<IActionResult> AccountDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var registeredUser = await _context.RegisteredUsers
                .FirstOrDefaultAsync(r => r.Email == user.Email);
            var contact = registeredUser is null
                ? null
                : await _context.ContactDetails
                    .FirstOrDefaultAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId);

            var vm = new AccountDetailsVM
            {
                User = new UserListVM
                {
                    Id = user.Id,
                    Name = user.UserName ?? "",
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                },
                Contact = contact == null ? null : new ContactDetailVM
                {
                    ContactId = contact.PkContactId,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    PhoneNumber = contact.PhoneNumber,
                    Street = contact.Street,
                    City = contact.City,
                    Province = contact.Province,
                    PostCode = contact.PostCode,
                    Country = contact.Country,
                    IsDefault = contact.IsDefault
                }
            };
            return View(vm);
        }
   
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.RemoveFromRoleAsync(user, role);

            return RedirectToAction("AccountDetails", new { id = userId });
        }

        /*============================== Manage Sales ==============================*/
        public async Task<IActionResult> ManageSales()
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = now.AddDays(-6).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = new DateTime(now.Year, 1, 1);
            var fiveYrStart = now.AddYears(-4).Date;

            // Load enough history for all buckets in one round-trip
            var allTransactions = await _context.Transactions
                .Where(t => t.TransactionDate >= fiveYrStart)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync();

            // ── Bucket helpers ────────────────────────────────────────
            var todayTx = allTransactions.Where(t => t.TransactionDate.Date == todayStart).ToList();
            var weeklyTx = allTransactions.Where(t => t.TransactionDate.Date >= weekStart).ToList();
            var monthlyTx = allTransactions.Where(t => t.TransactionDate >= monthStart).ToList();
            var yearlyTx = allTransactions.Where(t => t.TransactionDate >= yearStart).ToList();

            // ── Summary cards ─────────────────────────────────────────
            decimal dailyGross = todayTx.Any() ? todayTx.Sum(t => t.Amount) : 0m;
            decimal weeklyGross = weeklyTx.Any() ? weeklyTx.Sum(t => t.Amount) : 0m;
            decimal monthlyGross = monthlyTx.Any() ? monthlyTx.Sum(t => t.Amount) : 0m;
            decimal yearlyGross = yearlyTx.Any() ? yearlyTx.Sum(t => t.Amount) : 0m;

            // ── Daily chart: today by hour ────────────────────────────
            var dailyLabels = new List<string>();
            var dailySalesData = new List<decimal>();
            for (int h = 0; h < 24; h++)
            {
                var hour = todayStart.AddHours(h);
                dailyLabels.Add(hour.ToString("HH:00"));
                dailySalesData.Add(todayTx
                    .Where(t => t.TransactionDate.Hour == h)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            // ── Weekly chart: last 7 days ─────────────────────────────
            var weeklyLabels = new List<string>();
            var weeklySalesData = new List<decimal>();
            for (int d = 6; d >= 0; d--)
            {
                var day = now.AddDays(-d).Date;
                weeklyLabels.Add(day.ToString("ddd dd"));
                weeklySalesData.Add(allTransactions
                    .Where(t => t.TransactionDate.Date == day)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            // ── Monthly chart: last 12 months ────────────────────────
            var monthlyLabels = new List<string>();
            var monthlySalesData = new List<decimal>();
            for (int m = 11; m >= 0; m--)
            {
                var month = now.AddMonths(-m);
                monthlyLabels.Add(month.ToString("MMM yyyy"));
                monthlySalesData.Add(allTransactions
                    .Where(t => t.TransactionDate.Year == month.Year
                             && t.TransactionDate.Month == month.Month)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            // ── Yearly chart: last 5 years ────────────────────────────
            var yearlyLabels = new List<string>();
            var yearlySalesData = new List<decimal>();
            for (int y = 4; y >= 0; y--)
            {
                int yr = now.AddYears(-y).Year;
                yearlyLabels.Add(yr.ToString());
                yearlySalesData.Add(allTransactions
                    .Where(t => t.TransactionDate.Year == yr)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            // ── Top 5 products ────────────────────────────────────────
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Select(oi => new
                {
                    oi.FkProductId,
                    ProductName = oi.Product == null ? "Unknown" : oi.Product.Name,
                    ProductPrice = oi.Product == null ? 0m : oi.Product.Price,
                    oi.Quantity
                })
                .ToListAsync();

            var topProducts = orderItems
                .GroupBy(oi => new { oi.FkProductId, oi.ProductName, oi.ProductPrice })
                .Select(g => new TopProductVM
                {
                    ProductName = g.Key.ProductName,
                    UnitsSold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * g.Key.ProductPrice)
                })
                .OrderByDescending(p => p.UnitsSold)
                .Take(5)
                .ToList();

            var vm = new SalesVM
            {
                DailyGrossSales = dailyGross,
                DailyTotalOrders = todayTx.Count,
                DailyLabels = dailyLabels,
                DailySalesData = dailySalesData,

                WeeklyGrossSales = weeklyGross,
                WeeklyTotalOrders = weeklyTx.Count,
                WeeklyLabels = weeklyLabels,
                WeeklySalesData = weeklySalesData,

                MonthlyGrossSales = monthlyGross,
                MonthlyTotalOrders = monthlyTx.Count,
                MonthlyLabels = monthlyLabels,
                MonthlySalesData = monthlySalesData,

                YearlyGrossSales = yearlyGross,
                YearlyTotalOrders = yearlyTx.Count,
                YearlyLabels = yearlyLabels,
                YearlySalesData = yearlySalesData,

                TotalOrdersAllTime = await _context.Orders.CountAsync(),
                TopProducts = topProducts
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReindexFTS([FromBody] ReindexPayload? payload)
        {
            string reason = payload?.Reason ?? string.Empty;

            var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
                            SELECT PkProductId, Name, PkProductId FROM Products
                            WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);";

            await _context.Database.ExecuteSqlRawAsync(sql);

            try
            {
                var audit = new AuditEntryModel
                {
                    Action = "ReindexFTS",
                    Actor = User.Identity?.Name ?? "unknown",
                    Timestamp = DateTime.UtcNow,
                    AffectedKeysCount = 0,
                    Details = "Reindexed ProductFTS table",
                    Reason = reason
                };

                _context.Add(audit);
                await _context.SaveChangesAsync();
            }
            catch { }

            if (_reindexService != null)
                await _reindexService.ReindexOnce();

            return Ok(new { success = true });
        }

        [HttpGet]
        public IActionResult ReindexHealth()
        {
            if (_reindexService == null)
                return Json(new { success = false });

            var suggestionCount = _context.FuzzySuggestions.Count();

            return Json(new
            {
                success = true,
                lastRun = _reindexService.LastRun,
                lastDuration = _reindexService.LastDuration,
                suggestionCount,
                runCount = _reindexService.RunCount
            });
        }

        [HttpGet]
        public IActionResult CacheStats()
        {
            try
            {
                var count = _context.CachedFuzzyKeys.Count();
                var lastClear = _context.AuditEntries
                    .Where(a => a.Action == "ClearFuzzyCache")
                    .OrderByDescending(a => a.Timestamp)
                    .Select(a => a.Timestamp)
                    .FirstOrDefault();

                // Include background service metrics if available
                DateTime? lastRun = null;
                TimeSpan? lastDuration = null;
                try
                {
                    if (_reindexService != null)
                    {
                        lastRun = _reindexService.LastRun;
                        lastDuration = _reindexService.LastDuration;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read reindex service metrics");
                }

                return Json(new
                {
                    success = true,
                    keys = count,
                    lastClear = lastClear == default ? (DateTime?)null : lastClear,
                    lastRun,
                    lastDuration
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve reindex health status");
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearFuzzyCache([FromBody] ClearCachePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload?.Reason))
                return BadRequest(new { success = false });

            var reason = payload.Reason;

            // Step 2: Load persisted cache keys and clear them
            var keys = _context.CachedFuzzyKeys.ToList();
            var registryCount = 0;

            foreach (var k in keys)
                _cache.Remove(k.CacheKey);

                // Remove persisted registry
                try
                {
                    _context.CachedFuzzyKeys.RemoveRange(keys);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist CachedFuzzyKey removal for {Count} keys", keys.Count);
                }

                // Step 3: Log for monitoring
                _logger.LogInformation(
                 "Admin {Admin} cleared {Count} fuzzy cache entries",
                  User.Identity?.Name ?? "unknown",
                 registryCount);

                // Step 4: Persist audit entry for compliance
                try
                {
                    var audit = new AuditEntryModel
                    {
                        Action = "ClearFuzzyCache",
                        Actor = User.Identity?.Name ?? "unknown",
                        Timestamp = DateTime.UtcNow,
                        AffectedKeysCount = registryCount,
                        Details = string.Join(',', keys.Select(k => k.CacheKey)),
                        Reason = reason
                    };
                    _context.Add(audit);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist ClearFuzzyCache audit entry");
                }
            return Ok(new { success = true, cleared = keys.Count });
        }
    }

    public sealed class ReindexPayload
    {
        public string? Reason { get; set; }
    }

    public sealed class ClearCachePayload
    {
        public string? Reason { get; set; }
    }
}

