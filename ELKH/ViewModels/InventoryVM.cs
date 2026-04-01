namespace ELKH.ViewModels
{
    /// <summary>
    /// Lightweight projection used for the Inventory index table.
    /// </summary>
    public class InventoryVM
    {
        public int PkProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsActive { get; set; }

        // Added so the Index table can show the product price.
        public decimal Price { get; set; }
    }
}