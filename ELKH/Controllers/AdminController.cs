using ELKH.Data;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRole_repo _roleRepo;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly Data.ApplicationDbContext _context;

        public AdminController(IRole_repo roleRepo, UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _roleRepo = roleRepo;
            _userManager = userManager;
            _context = context;
        }

        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }

        /*============================== List Of All Users ==============================*/
        public async Task<IActionResult> ListUsers(string search, string roleFilter, int page = 1)
        {
            int pageSize = 5;
            var users = _userManager.Users.ToList();
            var userList = new List<UserListVM>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListVM
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = roles.ToList()
                });
            }

            if (!string.IsNullOrEmpty(search))
            {
                userList = userList
                    .Where(u => u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || u.Roles.Any(r => r.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All")
            {
                userList = userList.Where(u => u.Roles.Contains(roleFilter)).ToList();
            }

            int totalUsers = userList.Count;
            var pagedUsers = userList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.Search      = search;
            ViewBag.RoleFilter  = roleFilter;

            return View(pagedUsers);
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

            var roles   = await _userManager.GetRolesAsync(user);
            var contact = await _context.ContactDetails.FirstOrDefaultAsync(c => c.UserId == user.Id);

            var vm = new AccountDetailsVM
            {
                User = new UserListVM
                {
                    Id    = user.Id,
                    Name  = user.UserName ?? "",
                    Email = user.Email ?? "",
                    Roles = roles.ToList()
                },
                Contact = contact == null ? null : new ContactDetailVM
                {
                    ContactId   = contact.PkContactId,
                    FirstName   = contact.FirstName,
                    LastName    = contact.LastName,
                    PhoneNumber = contact.PhoneNumber,
                    Street      = contact.Street,
                    City        = contact.City,
                    Province    = contact.Province,
                    PostCode    = contact.PostCode,
                    Country     = contact.Country,
                    IsDefault   = contact.IsDefault
                }
            };

            return View(vm);
        }

        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.RemoveFromRoleAsync(user, role);
            return RedirectToAction("AccountDetails", new { id = userId });
        }

        /*============================== Manage Sales ==============================*/
        public async Task<IActionResult> ManageSales()
        {
            var now = DateTime.Now;
            var weekStart = now.AddDays(-6).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = now.AddMonths(-11).Date;

            // ── Fetch into memory first so decimal Sum() works with SQLite ──
            var allTransactions = await _context.Transactions
                .Where(t => t.TransactionDate >= yearStart)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync();

            var weeklyTx = allTransactions.Where(t => t.TransactionDate.Date >= weekStart).ToList();
            var monthlyTx = allTransactions.Where(t => t.TransactionDate >= monthStart).ToList();

            // ── Summary cards ─────────────────────────────────────────────
            decimal weeklyGross = weeklyTx.Any() ? weeklyTx.Sum(t => t.Amount) : 0m;
            decimal monthlyGross = monthlyTx.Any() ? monthlyTx.Sum(t => t.Amount) : 0m;
            int weeklyOrders = weeklyTx.Count;
            int monthlyOrders = monthlyTx.Count;
            int totalOrders = await _context.Orders.CountAsync();

            // ── Weekly chart: last 7 days ─────────────────────────────────
            var weeklyLabels = new List<string>();
            var weeklySalesData = new List<decimal>();

            for (int d = 6; d >= 0; d--)
            {
                var day = now.AddDays(-d).Date;
                var dayTx = allTransactions.Where(t => t.TransactionDate.Date == day).ToList();
                weeklyLabels.Add(day.ToString("ddd dd"));
                weeklySalesData.Add(dayTx.Any() ? dayTx.Sum(t => t.Amount) : 0m);
            }

            // ── Monthly chart: last 12 months ─────────────────────────────
            var monthlyLabels = new List<string>();
            var monthlySalesData = new List<decimal>();

            for (int m = 11; m >= 0; m--)
            {
                var month = now.AddMonths(-m);
                var monthTx = allTransactions
                    .Where(t => t.TransactionDate.Year == month.Year
                             && t.TransactionDate.Month == month.Month)
                    .ToList();
                monthlyLabels.Add(month.ToString("MMM yyyy"));
                monthlySalesData.Add(monthTx.Any() ? monthTx.Sum(t => t.Amount) : 0m);
            }

            // ── Top 5 products ────────────────────────────────────────────
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Products)
                .Select(oi => new
                {
                    oi.FkProductId,
                    ProductName = oi.Products == null ? "Unknown" : oi.Products.Name,
                    ProductPrice = oi.Products == null ? 0m : oi.Products.Price,
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
                WeeklyGrossSales = weeklyGross,
                MonthlyGrossSales = monthlyGross,
                WeeklyTotalOrders = weeklyOrders,
                MonthlyTotalOrders = monthlyOrders,
                TotalOrdersAllTime = totalOrders,
                WeeklyLabels = weeklyLabels,
                WeeklySalesData = weeklySalesData,
                MonthlyLabels = monthlyLabels,
                MonthlySalesData = monthlySalesData,
                TopProducts = topProducts
            };

            return View(vm);
        }
    }
}


















































































