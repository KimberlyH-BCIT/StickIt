using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class ProductImage
    {
        [Key]
        public int PkProductImageId { get; set; }

        [DisplayName("Product Image Link")]
        public required string ProductImageURL { get; set; }

        //Relationship with Product
        public int FkProductId { get; set; }
        public required Product Product { get; set; } 
    }
}
