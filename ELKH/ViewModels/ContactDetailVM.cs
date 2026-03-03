using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class ContactDetailVM
    {
        public int ContactId { get; set; }

        [Required]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Street Address")]
        [MaxLength(200)]
        public string Street { get; set; } = string.Empty;

        [Required]
        [Display(Name = "City")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Province/State")]
        [MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Postal Code")]
        [MaxLength(20)]
        public string PostCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Country")]
        [MaxLength(100)]
        public string Country { get; set; } = "Canada";

        [Display(Name = "Set as Default Address")]
        public bool IsDefault { get; set; } = false;
    }
}
