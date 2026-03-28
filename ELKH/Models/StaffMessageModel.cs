using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class StaffMessageModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Body { get; set; }

        public string SentBy { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public ICollection<MessageReplyModel> Replies { get; set; } = new List<MessageReplyModel>();
    }
}