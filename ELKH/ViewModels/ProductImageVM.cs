namespace ELKH.ViewModels;

/// <summary>
/// View model for product image management providing image upload, display,
/// and manipulation functionality for product catalog administration.
/// </summary>
public class ProductImageVM
{
    public int ImageId { get; set; }
    public string FileName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public byte[] ImageData { get; set; } = [];
    public IFormFile? ProductImage { get; set; }
    public int FkProductId { get; set; }
}
