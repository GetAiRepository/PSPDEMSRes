using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosis : BaseAuditEntity
    {
        [Key]
        public int DiagnosisId { get; set; }

        public int PatientId { get; set; }

        public DateTime VisitDate { get; set; }

        public DateTime? LastVisitDate { get; set; }

        [StringLength(20)]
        public string? BloodPressure { get; set; }

        [StringLength(20)]
        public string? PulseRate { get; set; }

        [StringLength(20)]
        public string? Sugar { get; set; }

        [StringLength(1000)]
        public string? Remarks { get; set; }

        [StringLength(100)]
        public string DiagnosedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // ======= NEW VISIT TYPE AND APPROVAL FIELDS =======

        [Column("visit_type")]  // Add this if column name differs
        [StringLength(50)]
        public string VisitType { get; set; } = "Regular Visitor";

        [Column("approval_status")]  // Add this if column name differs
        [StringLength(50)]
        public string ApprovalStatus { get; set; } = "Approved";

        [Column("approved_by")]  // Add this if column name differs
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("approved_date")]  // Add this if column name differs
        public DateTime? ApprovedDate { get; set; }

        [Column("rejection_reason")]  // Add this if column name differs
        [StringLength(500)]
        public string? RejectionReason { get; set; }

        // Navigation properties
        public virtual OtherPatient? Patient { get; set; }
        public virtual ICollection<OthersDiagnosisDisease> DiagnosisDiseases { get; set; } = new List<OthersDiagnosisDisease>();
        public virtual ICollection<OthersDiagnosisMedicine> DiagnosisMedicines { get; set; } = new List<OthersDiagnosisMedicine>();
    }
}