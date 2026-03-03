using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents an audit log entry for tracking administrative or system actions.
    /// </summary>
    public class AuditEntryModel
    {
        /// <summary>
        /// Unique identifier for the audit entry (primary key).
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Name of the action performed (e.g., "ReindexFTS", "ClearFuzzyCache").
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Username or identifier of the actor who performed the action.
        /// </summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action occurred (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Reason provided for the action (if any).
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Number of keys or records affected by the action.
        /// </summary>
        public int AffectedKeysCount { get; set; }

        /// <summary>
        /// Additional details or context for the action.
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }
}
