using System.Security.Claims;
using ELKH.Data;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ELKH.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ICartRepo _cartRepo;
    private readonly ApplicationDbContext _db;

    public CartController(ICartRepo cartRepo, ApplicationDbContext db)
    {
        _cartRepo = cartRepo;
        _db = db;
    }

    // GET: /Cart/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var registeredUserId = await GetOrCreateRegisteredUserIdAsync();
        if (registeredUserId == null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var cartItems = await _cartRepo.GetByUserIdAsync(registeredUserId.Value);

        var vm = new CartVM
        {
            Items = cartItems.Select(c => new CartItemVM
            {
                CartItemId = c.PkCartId,
                ProductId = c.FkProductID,
                ProductName = c.Product?.Name ?? string.Empty,
                ImageUrl = c.Product?.ProductImages?.FirstOrDefault()?.ProductImageURL,
                UnitPrice = c.Product?.Price ?? 0m,
                Quantity = c.Quantity,
                LineTotal = (c.Product?.Price ?? 0m) * c.Quantity
                
            }).ToList()
        };
        
        
        vm.Tax = vm.Subtotal * 0.12m;
        vm.ShippingCost = vm.Subtotal >= 50m ? 0m : 7.99m;
        vm.Total = vm.Subtotal + vm.Tax + vm.ShippingCost;

        return View(vm);
    }

    // POST: /Cart/Add
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        if (quantity < 1) quantity = 1;

        var registeredUserId = await GetOrCreateRegisteredUserIdAsync();
        if (registeredUserId == null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var existing = await _cartRepo.GetByUserAndProductAsync(registeredUserId.Value, productId);
        if (existing != null)
        {
            existing.Quantity += quantity;
            await _cartRepo.UpdateAsync(existing);
        }
        else
        {
            var cart = new CartModel
            {
                FkRegisteredUserId = registeredUserId.Value,
                FkProductID = productId,
                Quantity = quantity,
                TotalPrice = 0m
            };

            await _cartRepo.AddAsync(cart);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int cartId, int quantity)
    {
        if (quantity < 1) quantity = 1;

        var registeredUserId = await GetOrCreateRegisteredUserIdAsync();
        if (registeredUserId == null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        var cartItems = await _cartRepo.GetByUserIdAsync(registeredUserId.Value);
        var item = cartItems.FirstOrDefault(x => x.PkCartId == cartId);

        if (item != null)
        {
            item.Quantity = quantity;
            await _cartRepo.UpdateAsync(item);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartId)
    {
        await _cartRepo.RemoveAsync(cartId);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Clear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var registeredUserId = await GetOrCreateRegisteredUserIdAsync();
        if (registeredUserId == null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        await _cartRepo.ClearByUserIdAsync(registeredUserId.Value);
        return RedirectToAction(nameof(Index));
    }

    
    private async Task<int?> GetOrCreateRegisteredUserIdAsync()
    {
        // Map the current Identity user to your RegisteredUsers table via email.
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email)) return null;

        var registeredUser = await _db.RegisteredUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (registeredUser != null) return registeredUser.PkRegisteredUserId;

        // Create a RegisteredUser row on first use.
        registeredUser = new RegisteredUserModel { Email = email };
        _db.RegisteredUsers.Add(registeredUser);
        await _db.SaveChangesAsync();
        return registeredUser.PkRegisteredUserId;
    }
}
