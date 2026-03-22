using ELKH.Models;

namespace ELKH.ViewModels;


public class CartItemVM
{
    public int CartItemId { get; set; }   // PkCartId
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    
    public decimal LineTotal { get; set; }  
}

public class CartVM
{
    public List<CartModel> CartItems { get; set; } = new();
    public List<CartItemVM> Items { get; set; } = new();
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public bool IsEmpty => !Items.Any();
    
}