using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class MessageReplyModel
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }
        public StaffMessageModel Message { get; set; }

        [Required]
        [MaxLength(500)]
        public string ReplyText { get; set; }

        public string RepliedBy { get; set; }

        public DateTime RepliedAt { get; set; } = DateTime.UtcNow;
    }
}