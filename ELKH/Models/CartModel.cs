using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    public class CartModel
    {
        [Key]
        public int PkCartId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal TotalPrice { get; set; }

        //Relationship with RegisteredUser table
        public int FkRegisteredUserId { get; set; }
        public RegisteredUserModel RegisteredUser { get; set; } = new RegisteredUserModel();

        //Relationship with Product
        public int FkProductID { get; set; }
        public ProductModel Product { get; set; } =  new ProductModel();
    }
}
