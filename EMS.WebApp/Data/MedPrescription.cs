using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data.Migrations
{
    public class MedPrescription
    {
        [Key]
        public int PrescriptionId { get; set; }

        public int emp_uid { get; set; }
        public int exam_id { get; set; }
        public DateTime PrescriptionDate { get; set; }

        [StringLength(20)]
        public string? BloodPressure { get; set; }

        [StringLength(20)]
        public string? Pulse { get; set; }

        [StringLength(20)]
        public string? Temperature { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Remarks { get; set; }

        // ======= NEW APPROVAL FIELDS =======

        [StringLength(50)]
        public string ApprovalStatus { get; set; } = "Approved"; // Default to Approved for regular visits

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        // Navigation properties
        public virtual HrEmployee? HrEmployee { get; set; }
        public virtual MedExamHeader? MedExamHeader { get; set; }
        public virtual ICollection<MedPrescriptionDisease> PrescriptionDiseases { get; set; } = new List<MedPrescriptionDisease>();
        public virtual ICollection<MedPrescriptionMedicine> PrescriptionMedicines { get; set; } = new List<MedPrescriptionMedicine>();
    }
}