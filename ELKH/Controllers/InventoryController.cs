using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
    //[Authorize(Roles = "Admin")]
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;
        
=========
        
>>>>>>>>> Temporary merge branch 2
        public InventoryController(InventoryRepo inventoryRepo)

            _inventoryRepo = inventoryRepo;
        //Pass
<<<<<<<<< Temporary merge branch 1
=========


        //Pass
>>>>>>>>> Temporary merge branch 2
        public async Task<IActionResult> Index()
        {
            var products = await _inventoryRepo.GetAllProduct();

            var inventoryList = products.Select(p => new InventoryVM
            {
                PkProductId = p.PkProductId,
                ProductName = p.Name,
                Quantity = p.StockQuantity,
        //Pass
            }).ToList();

            return View(inventoryList);
        }

<<<<<<<<< Temporary merge branch 1
=========
        //Pass
>>>>>>>>> Temporary merge branch 2
        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantityId);

            return RedirectToAction(nameof(Index));
        }

        //Pass
        public async Task<IActionResult> ProductImages(int Id)
        {

            ViewBag.ProductId = Id;
            // GetProductImages likely returns List<ImageModel>
            var productImages = await _inventoryRepo.GetProductImages(Id);

            if (productImages == null)
            {
                return NotFound();
            }

            // Avoid accessing properties on 'pi' that may not exist (e.g. if pi is a string).
            // Use the known productId for FkProductId. ProductImage is set to null because
            // an IFormFile is not available when reading images from the repository.
            var vmList = productImages.Select(pi => new ProductImageVM
            {
                ProductImage = null,
                FkProductId = productId
            }).ToList();

            return View(vmList);
        }

        // GET: show add-image form for a specific product
        public async Task<IActionResult> AddImage(int productId)
        {
            var vm = new ImageModel();
            ViewBag.ProductId = Id;

            return View(vm);
        }


        //Pass
        [HttpPost]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return View("AddImage");
            }

            var addImageRepo = await _inventoryRepo.UploadImage(productId, file);

            if (addImageRepo)
            {
                return RedirectToAction("ProductImages", new { id = productId });
            }
            return View("Index");
        }

      
    }
}
