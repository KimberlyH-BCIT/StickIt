using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class UserLogModel
    {
        [Key]
        public int PkLogId { get; set; }

        [Required]
        [MaxLength(256)]
        public string FkEmail { get; set; } = string.Empty;

        [Required]
        public DateTime LogInTime { get; set; }

        public DateTime? LogOutTime { get; set; }

        public bool Abandoned { get; set; }
    }
}
