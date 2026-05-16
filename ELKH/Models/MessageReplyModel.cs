using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a reply sent by a staff member in response to a manager message.
    /// Belongs to a <see cref="StaffMessageModel"/> via <see cref="MessageId"/>.
    /// </summary>
    public class MessageReplyModel
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }
        public StaffMessageModel Message { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string ReplyText { get; set; } = string.Empty;

        public string RepliedBy { get; set; } = string.Empty;

        public DateTime RepliedAt { get; set; } = DateTime.UtcNow;
    }
}
