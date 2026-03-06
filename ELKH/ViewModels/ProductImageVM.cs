namespace ELKH.ViewModels
{
    public class ProductImageVM
    {
        public string FileName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public byte[] ImageData { get; set; } = null!;
    }
}
