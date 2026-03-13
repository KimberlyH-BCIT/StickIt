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

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IRole_repo _roleRepo;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IFuzzyReindexService _reindexService;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(
            IRole_repo roleRepo,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<AdminController> logger,
            IFuzzyReindexService reindexService,
            UserManager<IdentityUser> userManager)
        {
            _roleRepo = roleRepo;
            _context = context;
            _cache = cache;
            _logger = logger;
            _reindexService = reindexService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            var vm = new SalesVM
            {
                WeeklyTotalOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= weekAgo),
                MonthlyTotalOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= monthAgo),
                StockUpCount = await _context.Products.CountAsync(p => p.StockQuantity > 100),
                StockDownCount = await _context.Products.CountAsync(p => p.StockQuantity <= 100),
            };

            return View(vm);
        }

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
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);

            return View(pagedUsers);
        }

        [HttpGet]
        public async Task<IActionResult> AccountDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var contact = await _context.ContactDetails.FirstOrDefaultAsync(c => c.UserId == user.Id);

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

            var suggestionCount = _context.Set<FuzzySuggestionModel>().Count();

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
            var count = _context.Set<CachedFuzzyKeyModel>().Count();

            return Json(new
            {
                success = true,
                keys = count,
                lastRun = _reindexService?.LastRun,
                lastDuration = _reindexService?.LastDuration
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearFuzzyCache([FromBody] ClearCachePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload?.Reason))
                return BadRequest(new { success = false });

            var keys = _context.Set<CachedFuzzyKeyModel>().ToList();

            foreach (var k in keys)
                _cache.Remove(k.CacheKey);

            _context.Set<CachedFuzzyKeyModel>().RemoveRange(keys);
            _context.SaveChanges();

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