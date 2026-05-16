namespace ELKH.ViewModels;

/// <summary>
/// Represents a cart item view model containing product details and quantity information
/// for display in shopping cart interfaces. Provides line-total calculation for pricing display.
/// </summary>
public record CartItemVM
{
    public int CartItemId { get; set; }   // PkCartId
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>
/// Shopping cart view model containing collection of cart items with computed totals.
/// Provides subtotal calculation, tax handling, shipping costs, and empty state checking
/// for complete cart display and checkout processing.
/// </summary>
public class CartVM
{
    public List<CartModel> CartItems { get; set; } = [];
    public List<CartItemVM> Items { get; set; } = [];
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public bool IsEmpty => Items.Count == 0;
}
