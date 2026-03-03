using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class ProductRatingModel
    {
        [Key]
        public int PkRatingId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Rating { get; set; } = 0;
        public DateTime RatedTime { get; set; } = DateTime.Now;

        //Product Relationship
        public int FkProductId { get; set; }
        public ProductModel Products { get; set; } = new ProductModel();

        //Registered User Relationship
        public int FkRegisteredUserId { get; set; }
        public RegisteredUserModel RegisteredUsers { get; set; } = new RegisteredUserModel();
    }
}
