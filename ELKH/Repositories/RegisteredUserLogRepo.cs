using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for user login/logout log management.
    /// Inherits common CRUD operations from RepositoryBase and adds custom log tracking methods.
    /// </summary>
    public class RegisteredUserLogRepo : RepositoryBase<UserLogModel, int>, IRegisteredUserLogRepo
    {
        public RegisteredUserLogRepo(ApplicationDbContext context, ILogger<RegisteredUserLogRepo> logger) 
            : base(context, logger)
        {
        }

        // GetById() inherited from base
        public override async Task<IEnumerable<UserLogModel>> GetAllAsync()
            => await Context.UserLogs
                            .OrderByDescending(l => l.LogInTime)
                            .ToListAsync();

        /// <summary>
        /// Get all logs for a specific user email.
        /// </summary>
        public async Task<IEnumerable<UserLogModel>> GetByEmailAsync(string email)
            => await Context.UserLogs
                            .Where(l => l.FkEmail == email)
                            .OrderByDescending(l => l.LogInTime)
                            .ToListAsync();

        /// <summary>
        /// Get the active (not logged out) session for a user.
        /// </summary>
        public async Task<UserLogModel?> GetActiveLogAsync(string email)
            => await Context.UserLogs
                            .Where(l => l.FkEmail == email && l.LogOutTime == null)
                            .OrderByDescending(l => l.LogInTime)
                            .FirstOrDefaultAsync();

        /// <summary>
        /// Create a new login log entry.
        /// </summary>
        public async Task<UserLogModel> StartLogAsync(string email)
        {
            var log = new UserLogModel
            {
                FkEmail = email,
                LogInTime = DateTime.UtcNow,
                LogOutTime = null,
                Abandoned = false
            };

            Context.UserLogs.Add(log);
            await Context.SaveChangesAsync();
            Logger.LogInformation("Started login session for {Email}", email);

            return log;
        }

        /// <summary>
        /// Mark a session as logged out.
        /// </summary>
        public async Task<bool> EndLogAsync(int pkLogId)
        {
            var log = await GetByIdAsync(pkLogId);
            if (log is null || log.LogOutTime is not null)
                return false;

            log.LogOutTime = DateTime.UtcNow;
            try
            {
                await Context.SaveChangesAsync();
                Logger.LogInformation("Ended login session {LogId}", pkLogId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error ending login session {LogId}", pkLogId);
                return false;
            }
        }

        /// <summary>
        /// Close any dangling open sessions and mark them as abandoned.
        /// Policy: If the browser/app closed without logging out, the next successful login 
        /// will close the previous open log, set LogOutTime to current UTC time and set Abandoned = true.
        /// </summary>
        public async Task<bool> CloseDanglingIfAnyAsync(string email)
        {
            var active = await GetActiveLogAsync(email);
            if (active is null)
                return false;

            active.LogOutTime = DateTime.UtcNow;
            active.Abandoned = true;
            try
            {
                await Context.SaveChangesAsync();
                Logger.LogWarning("Closed dangling session for {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error closing dangling session for {Email}", email);
                return false;
            }
        }

        /// <summary>
        /// Record a user-initiated profile or address change event.
        /// Creates a non-session log entry with the given activity type and detail.
        /// </summary>
        /// <param name="email">Email of the user who performed the action.</param>
        /// <param name="activityType">Short label (e.g. "ProfileUpdated", "AddressAdded").</param>
        /// <param name="detail">Human-readable description of what changed.</param>
        public async Task LogActivityAsync(string email, string activityType, string detail)
        {
            if (string.IsNullOrEmpty(email)) return;

            var entry = new UserLogModel
            {
                FkEmail        = email,
                LogInTime      = DateTime.UtcNow,
                ActivityType   = activityType,
                ActivityDetail = detail
            };

            Context.UserLogs.Add(entry);
            await Context.SaveChangesAsync();
            Logger.LogInformation("Activity logged for {Email}: [{Type}] {Detail}", email, activityType, detail);
        }
    }
}
