namespace ELKH.ViewModels;

/// <summary>
/// View model representing an individual order item with product details,
/// quantities, pricing, and line-total calculations for order display.
/// </summary>
public record OrderItemVM(int ProductId = 0, int Quantity = 0, string ProductName = "", decimal ProductPrice = 0M);
