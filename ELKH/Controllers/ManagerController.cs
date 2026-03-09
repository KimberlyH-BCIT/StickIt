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
    /// <summary>
    /// Manager console controller — routes accessible to both Admin and Manager roles.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Inventory / Product management
    ///    - Index()                   // Dashboard landing page
    ///    - ListOfProducts()          // Product catalogue list
    ///    - AddNewProduct()           // New product form
    ///    - ProductDetails(id)        // Single-product detail view
    ///    - UpdateProductDetails(id)  // Edit product form
    ///    - DeleteProduct(id)         // Delete confirmation
    /// 2. Staff management
    ///    - ListOfStaffAccount()      // Staff account listing
    /// 3. Financials
    ///    - ListAllTransactions()     // Transaction listing
    /// ================================================================================
    ///
    /// All actions are currently view-only stubs that delegate rendering to their
    /// corresponding Razor views. Business logic will be wired in a future iteration
    /// once the service layer contracts are finalised.
    /// </remarks>
    [Authorize(Roles = "Admin,Manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ManagerController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ================= DASHBOARD =================
        public IActionResult Index()
        {
            return View();
        }

        // ================= PRODUCTS =================
        public async Task<IActionResult> ListOfProducts(string search, int page = 1)
        {
            int pageSize = 8;

            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || 
                                         p.Category.CategoryName.Contains(search));
            }

            int total = await query.CountAsync();

            var products = await query
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(p => new ProductVM
                            {
                                ProductId = p.PkProductId,
                                ProductName = p.Name,
                                Description = p.Description,
                                Price = p.Price,
                                StockQuantity = p.StockQuantity,
                                IsActive = p.IsActive,
                                CategoryId = p.FkCategoryId,
                                CategoryName = p.Category.CategoryName
                            })
                            .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(products);
        }

        // ================= PRODUCTS DETAILS=================
        public async Task<IActionResult> ProductDetails(int id)
        {
            var p = await _context.Products
                            .Include(p => p.Category)
                            .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null)
            {
                return NotFound();
            }

            return View(new ProductVM
                            {
                                ProductId = p.PkProductId,
                                ProductName = p.Name,
                                Description = p.Description,
                                Price = p.Price,
                                StockQuantity = p.StockQuantity,
                                IsActive = p.IsActive,
                                CategoryId = p.FkCategoryId,
                                CategoryName = p.Category.CategoryName
                            });
        }

        // ================= ADD NEW PRODUCT=================

        [HttpGet]
        public IActionResult AddNewProduct()
        {
            return View(new ProductVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewProduct(ProductVM model)
        {
            // Find or create category by name
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryName == model.CategoryName);

            if (category == null)
            {
                category = new ELKH.Models.CategoryModel { CategoryName = model.CategoryName };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            ModelState.Remove("CategoryId");
            model.CategoryId = category.PkCategoryId;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.Products.Add(new ELKH.Models.ProductModel
                                {
                                    Name = model.ProductName,
                                    Description = model.Description,
                                    Price = model.Price,
                                    StockQuantity = model.StockQuantity,
                                    IsActive = model.IsActive,
                                    FkCategoryId = category.PkCategoryId
                                });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Product added successfully.";
            return RedirectToAction("ListOfProducts");
        }

        // ================= UPDATE PRODUCT=================
        [HttpGet]
        public async Task<IActionResult> UpdateProductDetails(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null)
            {
                return NotFound();
            }

            return View(new ProductVM
                        {
                            ProductId = p.PkProductId,
                            ProductName = p.Name,
                            Description = p.Description,
                            Price = p.Price,
                            StockQuantity = p.StockQuantity,
                            IsActive = p.IsActive,
                            CategoryId = p.FkCategoryId,
                            CategoryName = p.Category.CategoryName
                        });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductDetails(ProductVM model)
        {
            var p = await _context.Products.FindAsync(model.ProductId);
            if (p == null)
            {
                return NotFound();
            }

            // Find or create category by name
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryName == model.CategoryName);

            if (category == null)
            {
                category = new ELKH.Models.CategoryModel { CategoryName = model.CategoryName };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            ModelState.Remove("CategoryId");
            model.CategoryId = category.PkCategoryId;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            p.Name = model.ProductName;
            p.Description = model.Description;
            p.Price = model.Price;
            p.StockQuantity = model.StockQuantity;
            p.IsActive = model.IsActive;
            p.FkCategoryId = category.PkCategoryId;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Product updated successfully.";
            return RedirectToAction("ListOfProducts");
        }

        // ================= DELETE PRODUCT=================
        [HttpGet]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (p == null) return NotFound();

            return View(new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category.CategoryName
            });
        }

        [HttpPost, ActionName("DeleteProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductConfirmed(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null)
            {
                return NotFound();
            }

            _context.Products.Remove(p);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Product deleted successfully.";
            return RedirectToAction("ListOfProducts");
        }

        // ================= TRANSACTIONS =================
        public async Task<IActionResult> ListAllTransactions(string search, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Transactions.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(t => t.TransactionStatus.Contains(search));
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
                                    DeliberyFee = t.DeliberyFee,
                                    FkOrderId = t.FkOrderId
                                })
                                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(transactions);
        }


        // ================= STAFF ACCOUNTS =================
        public async Task<IActionResult> ListOfStaffAccount(string search)
        {
            var staffRoles = new[] { "Manager", "Staff", "Admin" };
            var allUsers = _userManager.Users.ToList();
            var staffList = new List<UserListVM>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Any(r => staffRoles.Contains(r)))
                {
                    staffList.Add(new UserListVM
                    {
                        Id = user.Id,
                        Email = user.Email ?? "",
                        Name = user.UserName ?? "",
                        Roles = roles.ToList()
                    });
                }
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

        
    