namespace ELKH.ViewModels
{
    public class InventoryVM
    {
        public int PkProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsActive { get; set; }
        public List<string> ProductImage { get; set; } = new ();
    }
}
