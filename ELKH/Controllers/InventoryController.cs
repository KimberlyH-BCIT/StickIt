using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;

        public InventoryController(InventoryRepo inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }

        /// <summary>
        /// GET: /Inventory
        /// Lists all products with their current stock quantities and active status.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var products = await _inventoryRepo.GetAllProduct();

            var inventoryList = products.Select(p => new InventoryVM
            {
                PkProductId = p.PkProductId,
                ProductName = p.Name,
                Quantity    = p.StockQuantity ?? 0,
                IsActive    = p.IsActive
            }).ToList();

            return View(inventoryList);
        }

        /// <summary>
        /// POST: /Inventory/EditProductAmount
        /// Updates the stock quantity for a single product and redirects to the inventory listing.
        /// </summary>
        /// <param name="productId">Primary key of the product to update.</param>
        /// <param name="quantity">New stock quantity to set.</param>
        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantity)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantity);

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// GET: /Inventory/ProductImages/{productId}
        /// Displays all images currently associated with a product.
        /// </summary>
        public async Task<IActionResult> ProductImages(int productId)
        {
            var productImages = await _inventoryRepo.GetProductImages(productId);

            return View(productImages);
        }

        /// <summary>
        /// GET: /Inventory/AddImage/{productId}
        /// Renders the upload form pre-filled with the target product ID.
        /// </summary>
        public async Task<IActionResult> AddImage(int productId)
        {
            var vm = new ProductImageVM
            {
                FkProductId = productId
            };

            return View(vm);
        }

        /// <summary>
        /// POST: /Inventory/AddImage
        /// Saves an uploaded image file to <c>wwwroot/images</c> and records
        /// its URL in the database via <see cref="InventoryRepo.AddProductImage"/>.
        /// Re-displays the form with validation errors when the model is invalid.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddImage(ProductImageVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var success = await _inventoryRepo.AddProductImage(vm);

            if (success)
            {
                return RedirectToAction(nameof(ProductImages));
            }
            return View(vm);
        }
    }
}
