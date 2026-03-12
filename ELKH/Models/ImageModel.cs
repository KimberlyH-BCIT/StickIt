using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models;

public partial class ImageModel
{
    [Key]
    public int ImageId { get; set; }

    public string FileName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public byte[] ImageData { get; set; } = null!;

    //Foreign key to Product (no navigation to keep ImageStoreContext isolated)
    public int FkProductId { get; set; }
    
    public string? ProductImageURL { get; set; }
}
