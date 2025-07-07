using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public abstract class BaseAuditEntity
    {
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "SYSTEM";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? DeletedBy { get; set; }

        public DateTime? DeletedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}