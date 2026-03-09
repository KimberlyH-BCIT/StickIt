using ELKH.Models;

namespace ELKH.Repositories
{
    /// <summary>
    /// Contract for user session and activity log data access.
    /// Tracks login/logout lifecycle and explicit user-initiated activity events
    /// (profile updates, address changes) for the audit trail on the dashboard.
    /// </summary>
    public interface IRegisteredUserLogRepo
    {
        /// <summary>Returns all log entries across all users, most recent first.</summary>
        Task<IEnumerable<UserLogModel>> GetAllAsync();

        /// <summary>Returns a single log entry by primary key, or <see langword="null"/> if not found.</summary>
        Task<UserLogModel?> GetByIdAsync(int id);

        /// <summary>Returns all log entries for the given user email, most recent first.</summary>
        Task<IEnumerable<UserLogModel>> GetByEmailAsync(string email);

        /// <summary>Returns the open session (no <c>LogOutTime</c>) for the user, or <see langword="null"/> if none is active.</summary>
        Task<UserLogModel?> GetActiveLogAsync(string email);

        /// <summary>Creates a new login session entry with the current UTC time as <c>LogInTime</c>.</summary>
        Task<UserLogModel> StartLogAsync(string email);

        /// <summary>Closes an open session by setting its <c>LogOutTime</c> to the current UTC time.</summary>
        Task<bool> EndLogAsync(int pkLogId);

        /// <summary>
        /// Closes any sessions for the user that were never properly ended
        /// (e.g. browser closed, server restarted) by marking them as abandoned.
        /// Should be called at the start of a new login to keep the log consistent.
        /// </summary>
        Task<bool> CloseDanglingIfAnyAsync(string email);

        /// <summary>
        /// Records a discrete user-initiated event (e.g. <c>"ProfileUpdated"</c>, <c>"AddressAdded"</c>)
        /// as a non-session log entry so it appears alongside login history on the dashboard.
        /// </summary>
        Task LogActivityAsync(string email, string activityType, string detail);
    }
}
