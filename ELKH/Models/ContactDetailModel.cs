using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a contact or shipping address for a registered user.
    /// Stores recipient name, phone, address, and links to orders and transactions.
    /// </summary>
    public class ContactDetailModel
    {
        /// <summary>
        /// Unique identifier for the contact detail (primary key).
        /// </summary>
        [Key]
        public int PkContactId { get; set; }

        /// <summary>
        /// First name of the contact/recipient.
        /// </summary>
        [Display(Name ="First Name")]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Last name of the contact/recipient.
        /// </summary>
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Phone number for delivery or contact purposes.
        /// </summary>
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Street address (line 1).
        /// </summary>
        public string Street { get; set; } = string.Empty;

        /// <summary>
        /// City or locality.
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// Province, state, or region.
        /// </summary>
        public string Province { get; set; } = string.Empty;

        /// <summary>
        /// Postal or ZIP code.
        /// </summary>
        [Display(Name ="Postcode")]
        public string PostCode { get; set; } = string.Empty;

        /// <summary>
        /// Country (default: Canada).
        /// </summary>
        public string Country { get; set; } = "Canada";

        /// <summary>
        /// Whether this is the user's default address for shipping/billing.
        /// </summary>
        [Display(Name ="Is Default Address")]
        public bool IsDefault { get; set; } = true;

// Relationship with RegisteredUser
public int? FkRegisteredUserId { get; set; }
public RegisteredUserModel? RegisteredUser { get; set; }

// Relationship with Transaction
public ICollection<TransactionModel>? Transactions { get; set; }

// Relationship with Order
public ICollection<OrderModel> Orders { get; set; } = new List<OrderModel>();

public string UserId { get; internal set; }
    }
}
