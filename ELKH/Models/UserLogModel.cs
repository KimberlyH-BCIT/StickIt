using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a user login/logout session for auditing and analytics.
    /// </summary>
    public class UserLogModel
    {
        /// <summary>
        /// Unique identifier for the user log entry (primary key).
        /// </summary>
        [Key]
        public int PkLogId { get; set; }

        /// <summary>
        /// Email address of the user (foreign key).
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string FkEmail { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the user logged in.
        /// </summary>
        [Required]
        public DateTime LogInTime { get; set; }

        /// <summary>
        /// Timestamp when the user logged out (if available).
        /// </summary>
        public DateTime? LogOutTime { get; set; }

        /// <summary>
        /// Whether the session was abandoned (user closed browser without logging out).
        /// </summary>
        public bool Abandoned { get; set; }

        /// <summary>
        /// Optional type label for a user-initiated activity event
        /// (e.g. "ProfileUpdated", "AddressAdded"). Null on regular login/logout entries.
        /// </summary>
        [MaxLength(100)]
        public string? ActivityType { get; set; }

        /// <summary>
        /// Optional human-readable description of the activity. Null on login/logout entries.
        /// </summary>
        [MaxLength(500)]
        public string? ActivityDetail { get; set; }
    }
}
