using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a message sent by a manager to the staff team.
    /// Staff members can mark messages as read and reply via <see cref="MessageReplyModel"/>.
    /// </summary>
    public class StaffMessageModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Body { get; set; } = string.Empty;

        public string SentBy { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public ICollection<MessageReplyModel> Replies { get; set; } = new List<MessageReplyModel>();
    }
}
