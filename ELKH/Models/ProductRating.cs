using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class ProductRating
    {
        [Key]
        public int PkRatingId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Rating { get; set; } = 0;
        public DateTime RatedTime { get; set; } = DateTime.Now;

        //Product Relationship
        public int FkProductId { get; set; }
        public Product Products { get; set; } = new Product();

        //Registered User Relationship
        public int FkRegisteredUserId { get; set; }
        public RegisteredUser RegisteredUsers { get; set; } = new RegisteredUser();
    }
}
