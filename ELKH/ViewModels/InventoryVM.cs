namespace ELKH.ViewModels;

/// <summary>
/// View model for inventory management providing product stock information,
/// availability status, and inventory tracking for administrative operations.
/// </summary>
public class InventoryVM
{
    public int PkProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public bool IsActive { get; set; }
    public List<string>? ProductImage { get; set; } = [];
}
