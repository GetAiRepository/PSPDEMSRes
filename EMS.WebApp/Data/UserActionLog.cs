using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("user_action_logs")]
    public class UserActionLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = null!;

        [MaxLength(100)]
        public string? Controller { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public string? Parameters { get; set; } // JSON of action parameters

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(100)]
        public string? SessionId { get; set; }

        [MaxLength(50)]
        public string? Result { get; set; } // Success, Failed, Unauthorized

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public TimeSpan? ExecutionTime { get; set; }
    }
}