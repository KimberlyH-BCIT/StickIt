namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for displaying a user log entry - either a login/logout session
    /// or a user-initiated activity event (profile/address change).
    /// When <see cref="IsActivity"/> is true the session fields are not meaningful.
    /// </summary>
    public class UserLogVM
    {
        /// <summary>Timestamp when the user logged in (or when the activity occurred).</summary>
        public DateTime LogInTime { get; set; }

        /// <summary>Timestamp when the user logged out (null for activity entries).</summary>
        public DateTime? LogOutTime { get; set; }

        /// <summary>Whether the session was abandoned (always false for activity entries).</summary>
        public bool Abandoned { get; set; }

        /// <summary>
        /// Type of user-initiated activity (e.g. "ProfileUpdated", "AddressAdded").
        /// Null for regular login/logout session entries.
        /// </summary>
        public string? ActivityType { get; set; }

        /// <summary>
        /// Human-readable description of what changed.
        /// Null for regular login/logout session entries.
        /// </summary>
        public string? ActivityDetail { get; set; }

        /// <summary>True when this entry represents a profile/address change rather than a login session.</summary>
        public bool IsActivity => ActivityType is not null;
    }
}
