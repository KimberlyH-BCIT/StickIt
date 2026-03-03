using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for displaying and editing contact/shipping address information.
    /// Used in address forms and order checkout.
    /// </summary>
    public class ContactDetailVM
    {
        /// <summary>Contact detail unique identifier.</summary>
        public int ContactId { get; set; }

        /// <summary>First name of the contact/recipient.</summary>
        [Required]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>Last name of the contact/recipient.</summary>
        [Required]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Phone number for delivery or contact purposes.</summary>
        [Required]
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>Street address (line 1).</summary>
        [Required]
        [Display(Name = "Street Address")]
        [MaxLength(200)]
        public string Street { get; set; } = string.Empty;

        /// <summary>City or locality.</summary>
        [Required]
        [Display(Name = "City")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>Province, state, or region.</summary>
        [Required]
        [Display(Name = "Province/State")]
        [MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        /// <summary>Postal or ZIP code.</summary>
        [Required]
        [Display(Name = "Postal Code")]
        [MaxLength(20)]
        public string PostCode { get; set; } = string.Empty;

        /// <summary>Country (default: Canada).</summary>
        [Required]
        [Display(Name = "Country")]
        [MaxLength(100)]
        public string Country { get; set; } = "Canada";

        /// <summary>Whether this is the user's default address.</summary>
        [Display(Name = "Set as Default Address")]
        public bool IsDefault { get; set; } = false;
    }
}
