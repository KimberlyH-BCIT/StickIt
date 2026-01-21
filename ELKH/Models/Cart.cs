using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    public class Cart
    {
        [Key]
        public int PkCartId { get; set; }

        //Relationship with RegisteredUser table
        public int FkRegisteredUserId { get; set; }
        public RegisteredUser RegisteredUser { get; set; } = new RegisteredUser();

        //Relationship with CartItem
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
