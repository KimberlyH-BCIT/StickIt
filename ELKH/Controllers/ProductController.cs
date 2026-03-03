using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

/*
 ProductController
 Table of Contents
1. Fields & Constructor
2. Index / Details
3. Create (GET/POST)
4. Edit (GET/POST)
5. Delete (GET/POST)
6. Helpers (BuildCategoryOptions, MapToVM, MapToEntity)
*/

namespace ELKH.Controllers
{
    public class ProductController : Controller
    {
        #region Fields & Constructor
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }
        #endregion

        #region Index / Details
        // GET: Product/Index
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .ToListAsync();

            var viewProducts = products.Select(p => MapToVM(p));

            // Return the list of ProductVMs to the view
            return View(viewProducts);
        }

        // GET: Product/Details
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }

            ProductVM vm = MapToVM(product);

            return View(vm);
        }
        #endregion

        #region Create
        // GET: Product/Create
        public async Task<IActionResult> Create()
        {
            var options = await BuildCategoryOptionsAsync();
            ViewBag.FkCategoryId = options;
            ViewBag.CategoryId = options;
            return View(new ProductVM());
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                ViewBag.FkCategoryId = options;
                ViewBag.CategoryId = options;

                // Helpful validation message
                ModelState.AddModelError(string.Empty,
                "One or more required fields are missing or invalid. " +
                "Please review your input and try again.");

                return View(vm);
            }

            ProductModel product = MapToEntity(vm);
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["Message"] = "success, Product created successfully";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Edit
        // GET: Product/Edit
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction("Index");
            }

            var options = await BuildCategoryOptionsAsync(product.FkCategoryId);
            ViewBag.FkCategoryId = options;
            ViewBag.CategoryId = options;

            ProductVM vm = MapToVM(product);

            return View(vm);
        }

        // POST: Product/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                // Rebuild the category options for the dropdown
                var options = await BuildCategoryOptionsAsync(vm.CategoryId);
                ViewBag.FkCategoryId = options;
                ViewBag.CategoryId = options;

                // Helpful validation message
                ModelState.AddModelError(string.Empty,
                "One or more required fields are missing or invalid. " +
                "Please review your input and try again.");

                return View(vm);
            }

            var product = await _context.Products.FindAsync(vm.ProductId);
            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {vm.ProductId}";
                return RedirectToAction(nameof(Index));
            }

            // Update properties
            product.Name = vm.ProductName;
            product.Description = vm.Description;
            product.Price = vm.Price;
            product.StockQuantity = vm.StockQuantity;
            product.IsActive = vm.IsActive;
            product.FkCategoryId = vm.CategoryId;

            await _context.SaveChangesAsync();

            TempData["Message"] = "success, Product updated successfully";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Delete
        // GET: Product/Delete
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PkProductId == id);

            if (product is null)
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
                return RedirectToAction(nameof(Index));
            }
            
            ProductVM vm = MapToVM(product);

            return View(vm);
        }

        // POST: Product/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product is not null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Message"] = "success, Product deleted successfully";
            }
            else
            {
                TempData["Message"] = $"warning, Unable to find product ID: {id}";
            }
            
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Helpers
        private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync(int? selectedId = null)
        {
            var categories = await _context.Categories.ToListAsync();
            return categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.PkCategoryId.ToString(),
                    Text = c.CategoryName,
                    Selected = selectedId.HasValue && c.PkCategoryId == selectedId.Value
                }).ToList();
        }

        private ProductVM MapToVM(ProductModel p)
        {
            return new ProductVM
            {
                ProductId = p.PkProductId,
                ProductName = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                CategoryId = p.FkCategoryId,
                CategoryName = p.Category?.CategoryName ?? "Unknown"
            };
        }

        private ProductModel MapToEntity(ProductVM vm)
        {
            return new ProductModel
            {
                PkProductId = vm.ProductId,
                Name = vm.ProductName,
                Description = vm.Description,
                Price = vm.Price,
                StockQuantity = vm.StockQuantity,
                IsActive = vm.IsActive,
                FkCategoryId = vm.CategoryId
            };
        }
        #endregion
    }
}