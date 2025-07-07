using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("audit_trails")]
    public class AuditTrail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuditId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Operation { get; set; } = null!; // INSERT, UPDATE, DELETE

        [Required]
        public string PrimaryKey { get; set; } = null!;

        public string? OldValues { get; set; } // JSON of old values

        public string? NewValues { get; set; } // JSON of new values

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = null!;

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(100)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(100)]
        public string? SessionId { get; set; }

        [MaxLength(200)]
        public string? Action { get; set; } // Controller action that triggered the change

        public string? AffectedColumns { get; set; } // JSON array of changed column names
    }
}