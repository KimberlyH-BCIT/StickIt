using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ELKH.Repositories;
using System.Linq;
using System.Threading.Tasks;

namespace ELKH.Controllers;

using ELKH.Services;

/// <summary>
/// Controller responsible for order management and order-related views.
///
/// Design notes:
/// - All data access is delegated to IOrderManagementRepo and IProductService.
/// - The controller is responsible only for HTTP concerns: authorization, result
///   shaping, and routing. No DbContext is injected here.
/// </summary>
public class OrderController : AuthenticatedControllerBase
{
    private readonly IOrderManagementRepo _orderManagementRepo;
    private readonly IProductService _productService;

    public OrderController(
        IOrderManagementRepo orderManagementRepo, 
        IProductService productService,
        IUserService userService) 
        : base(userService)
    {
        _orderManagementRepo = orderManagementRepo;
        _productService = productService;
    }

    // ---------------------------------------------------------------------
    // Admin endpoints
    // ---------------------------------------------------------------------
    // Admin listing of orders using the repository. The repository may contain
    // optimized queries, projections or join logic suitable for administrative views.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var orders = await _orderManagementRepo.GetAllOrdersAsync();
        return View(orders);
    }

    // ---------------------------------------------------------------------
    // User-facing order history endpoints
    // ---------------------------------------------------------------------
    // GET: /Order/MyHistory - current user's order history
    public async Task<IActionResult> MyHistory()
    {
        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var orders = await _orderManagementRepo.GetUserOrdersAsync(email);
        return View("History", orders);
    }

    // Lists all orders ordered by creation date (admin history view)
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> History()
    {
        var orders = await _orderManagementRepo.GetAllOrderModelsAsync();
        return View(orders);
    }

    // ---------------------------------------------------------------------
    // Details and detail retrieval
    // ---------------------------------------------------------------------
    // Details for a specific order by id.
    // OutputCache removed: order data is personal; cache must not be shared across users.
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var authResult = RequireAuthenticatedUser(out var email);
        if (authResult != null) return authResult;

        var order = await _orderManagementRepo.GetOrderWithDetailsAsync(id);
        if (order == null) return NotFound();

        // Only the order owner or an Admin may view order details.
        if (order.RegisteredUser?.Email != email && !User.IsInRole("Admin"))
            return Forbid();

        // Use batch product lookup instead of N individual queries
        var productIds = order.OrderItems.Select(oi => oi.FkProductId).ToList();
        var productDict = await _productService.GetByIdsAsync(productIds);

        var productVms = order.OrderItems
            .Select(oi => productDict.ContainsKey(oi.FkProductId) 
                ? productDict[oi.FkProductId] 
                : null)
            .ToList();

        ViewBag.ProductVMs = productVms;
        return View(order);
    }

    // Get order details by user email via repository. This method accepts an email
    // and returns repository-shaped details which may be used by admin tooling.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OrderDetails(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest();

        var details = await _orderManagementRepo.OrderDetailsAsync(email);
        return View(details);
    }
}
