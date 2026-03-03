using System.Threading.Tasks;

namespace ELKH.Services
{
    /// <summary>
    /// Result returned by moderation operations to indicate success status and messages.
    /// </summary>
    public class ModerationResult
    {
        /// <summary>True if operation succeeded, false otherwise</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable message describing the result or error</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contract for content moderation operations.
    /// Handles flagging and reviewing user-generated content like product ratings.
    /// </summary>
    public interface IModerationService
    {
        /// <summary>
        /// Flags a product rating for moderator review and sends notification emails.
        /// </summary>
        /// <param name="ratingId">ID of the rating to flag</param>
        /// <param name="note">Optional note explaining why the rating was flagged</param>
        /// <param name="moderator">Username/email of the moderator performing the action</param>
        /// <returns>
        /// ModerationResult indicating success/failure and any relevant messages
        /// </returns>
        /// <remarks>
        /// Rate Limiting:
        /// - Per-moderator rate limit applied (configured in CacheOptions)
        /// - Default: 5 second window between flag operations
        /// - Returns failure if rate limit exceeded
        /// 
        /// Operation Steps:
        /// 1. Validates rate limit for moderator
        /// 2. Updates rating: IsFlagged = true, ModeratorNote = note
        /// 3. Sends email notification to configured admins
        /// 4. Email includes product details and moderation console links
        /// 
        /// Security:
        /// - Uses secure ModerationRoutes.WithBase() for email links
        /// - Validates BaseUrl from trusted configuration only
        /// - Email failures logged but don't fail the flag operation
        /// </remarks>
        Task<ModerationResult> FlagAsync(int ratingId, string note, string moderator);
    }
}
