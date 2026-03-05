using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ELKH.Controllers
{
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;
        public InventoryController(InventoryRepo inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }


        //Pass
        public async Task<IActionResult> Index()
        {
            var products = await _inventoryRepo.GetAllProduct();

            var inventoryList = products.Select(p => new InventoryVM
            {
                PkProductId = p.PkProductId,
                ProductName = p.Name,
                Quantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList();

            return View(inventoryList);
        }

        //Pass
        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantityId);

            return RedirectToAction(nameof(Index));
        }

        //Pass
        public async Task<IActionResult> ProductImages(int productId)
        {
            var productImages = await _inventoryRepo.GetProductImages(productId);

            var vm = new InventoryVM
            {
                PkProductId = productId,
                ProductImage = productImages
            };

            return View(vm);
        }

        //Pass
        public async Task<IActionResult> AddImage(int productId)
        {
            var vm = new ProductImageVM
            {
                FkProductId = productId
            };

            return View(vm);
        }


        //Test this after finish the image input setup
        [HttpPost]
        public async Task<IActionResult> AddImage(ProductImageVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var addImageRepo = await _inventoryRepo.AddProductImage(vm);

            if(addImageRepo)
            {
                return RedirectToAction(nameof(ProductImages));
            }
            return View(vm);
        }
    }
}
