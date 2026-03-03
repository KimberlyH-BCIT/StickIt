using Microsoft.AspNetCore.Mvc;
using ELKH.Data;
using ELKH.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using ELKH.ViewModels;

namespace ELKH.Controllers;

public class OrderController:Controller
{
    private readonly ApplicationDbContext _context;
    private readonly OrderManagementRepo _orderManagementRepo;

    public OrderController(ApplicationDbContext context)
    {
        _context = context;
        _orderManagementRepo = new OrderManagementRepo(context);
    }

    public async Task<IActionResult> History()
    {
        var orders = await _context.Orders
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(new OrderHistoryVM { Orders = orders });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var order = await _context.Orders
            .Include(o => o.RegisteredUser)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Products)
            .FirstOrDefaultAsync(o => o.PkOrderId == id);

        if (order == null) return NotFound();

        var vm = order.OrderItems.Select(oi => new OrderDetailsViewModel
        {
            OrderId = order.PkOrderId,
            UserEmail = order.RegisteredUser?.Email ?? "",
            DeliveryStatus = order.DeliveryStatus,
            ProductName = oi.Products?.Name ?? "",
            Quantity = oi.Quantity,
            UnitPrice = oi.Products?.Price ?? 0m
        }).ToList();

        return View(vm);
    }
    
    // Get order details by user email
    public IActionResult OrderDetails(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest();

        var details = _orderManagementRepo.OrderDetails(email).ToList();
        return View(details);
    }
}
