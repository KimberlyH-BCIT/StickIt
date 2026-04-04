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
using System.Globalization;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRoleRepo _roleRepo;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IFuzzyReindexService _reindexService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            IRoleRepo roleRepo,
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

        #region Dashboard

        public async Task<IActionResult> Index()
        {
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

            return View(new SalesVM());
        }

        #endregion

        #region Manage Sales (MAIN VERSION - KEPT)

        public async Task<IActionResult> ManageSales()
        {
            var now = DateTime.Now;

            var todayStart = now.Date;
            var weekStart = now.AddDays(-6).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = new DateTime(now.Year, 1, 1);
            var fiveYrStart = now.AddYears(-4).Date;

            var allTransactions = await _context.Transactions
                .Where(t => t.TransactionDate >= fiveYrStart)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync();

            var todayTx = allTransactions
                .Where(t => t.TransactionDate >= todayStart && t.TransactionDate < todayStart.AddDays(1))
                .ToList();

            var weeklyTx = allTransactions.Where(t => t.TransactionDate >= weekStart && t.TransactionDate <= now).ToList();
            var monthlyTx = allTransactions.Where(t => t.TransactionDate >= monthStart).ToList();
            var yearlyTx = allTransactions.Where(t => t.TransactionDate >= yearStart).ToList();

            decimal dailyGross = todayTx.Any() ? todayTx.Sum(t => t.Amount) : 0m;
            decimal weeklyGross = weeklyTx.Any() ? weeklyTx.Sum(t => t.Amount) : 0m;
            decimal monthlyGross = monthlyTx.Any() ? monthlyTx.Sum(t => t.Amount) : 0m;
            decimal yearlyGross = yearlyTx.Any() ? yearlyTx.Sum(t => t.Amount) : 0m;

            var dailyLabels = new List<string>();
            var dailySalesData = new List<decimal>();

            for (int h = 0; h < 24; h++)
            {
                var hourStart = todayStart.AddHours(h);
                var hourEnd = hourStart.AddHours(1);

                dailyLabels.Add(hourStart.ToString("HH:00"));
                dailySalesData.Add(todayTx
                    .Where(t => t.TransactionDate >= hourStart && t.TransactionDate < hourEnd)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            var weeklyLabels = new List<string>();
            var weeklySalesData = new List<decimal>();

            for (int d = 6; d >= 0; d--)
            {
                var dayStart = now.AddDays(-d).Date;
                var dayEnd = dayStart.AddDays(1);

                weeklyLabels.Add(dayStart.ToString("ddd dd"));
                weeklySalesData.Add(allTransactions
                    .Where(t => t.TransactionDate >= dayStart && t.TransactionDate < dayEnd)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

            var monthlyLabels = new List<string>();
            var monthlySalesData = new List<decimal>();

            for (int m = 11; m >= 0; m--)
            {
                var month = now.AddMonths(-m);

                monthlyLabels.Add(month.ToString("MMM yyyy"));
                monthlySalesData.Add(allTransactions
                    .Where(t => t.TransactionDate.Year == month.Year &&
                                t.TransactionDate.Month == month.Month)
                    .Sum(t => (decimal?)t.Amount) ?? 0m);
            }

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

        #endregion

        #region User Management

        public async Task<IActionResult> ListUsers(string search, string roleFilter, int page = 1)
        {
            const int pageSize = 5;

            ViewBag.Roles = new List<string> { "All" }
                .Concat(await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync())
                .ToList();

            IQueryable<IdentityUser> query = _userManager.Users;

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.Email != null && u.Email.Contains(search));

            var candidates = await query.ToListAsync();
            int totalUsers = candidates.Count;

            var pageUsers = candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var userList = new List<UserListVM>();

            foreach (var user in pageUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListVM
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);

            return View(userList);
        }

        public async Task<IActionResult> AccountDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return View(new AccountDetailsVM
            {
                User = new UserListVM
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.RemoveFromRoleAsync(user, role);

            return RedirectToAction("AccountDetails", new { id = userId });
        }

        #endregion
    }
}
