using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosisViewModel
    {
        // ======= NEW VISIT TYPE FIELD =======
        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = "Regular Visitor";

        // Patient Information
        [Display(Name = "Treatment ID")]
        [Required(ErrorMessage = "Treatment ID is required")]
        public string TreatmentId { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        [Required(ErrorMessage = "Patient name is required")]
        public string PatientName { get; set; } = string.Empty;

        [Display(Name = "Age")]
        [Range(1, 120, ErrorMessage = "Age must be between 1 and 120")]
        public int Age { get; set; }

        [Display(Name = "P. Number")]
        public string PNumber { get; set; } = string.Empty;

        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;

        [Display(Name = "Other Details")]
        public string? OtherDetails { get; set; }

        // Diagnosis Information
        public int? PatientId { get; set; }
        public int? DiagnosisId { get; set; }

        [Display(Name = "Last Visit Date")]
        public DateTime? LastVisitDate { get; set; }

        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse Rate")]
        public string? PulseRate { get; set; }

        [Display(Name = "Sugar")]
        public string? Sugar { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        // Prescription Data
        public List<MedDisease> AvailableDiseases { get; set; } = new();
        public List<MedMaster> AvailableMedicines { get; set; } = new();

        public List<int> SelectedDiseaseIds { get; set; } = new();
        public List<OthersPrescriptionMedicine> PrescriptionMedicines { get; set; } = new();

        // ======= NEW VISIT TYPE OPTIONS =======
        public List<string> VisitTypeOptions { get; set; } = new()
        {
            "Regular Visitor",
            "First Aid or Emergency"
        };
    }

    public class OthersDiagnosisListViewModel
    {
        public int DiagnosisId { get; set; }
        public string TreatmentId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime VisitDate { get; set; }
        public string DiagnosedBy { get; set; } = string.Empty;

        // ======= NEW FIELDS =======
        public string VisitType { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
    }

    // ======= UPDATED: Enhanced OthersPrescriptionMedicine to include batch tracking =======
    public class OthersPrescriptionMedicine
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }

        // ======= NEW: Batch tracking properties =======
        public int? IndentItemId { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? AvailableStock { get; set; }

        // Helper properties for display
        public string DisplayName => !string.IsNullOrEmpty(BatchNo) ? $"{MedicineName} - {BatchNo}" : MedicineName;
        public string ExpiryInfo => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;
        public bool IsNearExpiry => DaysToExpiry <= 30 && DaysToExpiry >= 0;
        public bool IsExpired => DaysToExpiry < 0;
    }

    public class OthersDiagnosisDetailsViewModel
    {
        public int DiagnosisId { get; set; }
        public string TreatmentId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string PNumber { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? OtherDetails { get; set; }
        public DateTime VisitDate { get; set; }
        public DateTime? LastVisitDate { get; set; }
        public string? BloodPressure { get; set; }
        public string? PulseRate { get; set; }
        public string? Sugar { get; set; }
        public string? Remarks { get; set; }
        public string DiagnosedBy { get; set; } = string.Empty;

        // ======= NEW FIELDS =======
        public string VisitType { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? RejectionReason { get; set; }

        public List<OthersDiseaseDetails> Diseases { get; set; } = new();
        public List<OthersMedicineDetails> Medicines { get; set; } = new();
    }

    public class OthersDiseaseDetails
    {
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string? DiseaseDescription { get; set; }
    }

    public class OthersMedicineDetails
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string? CompanyName { get; set; }
    }

    // ======= NEW APPROVAL VIEW MODELS =======

    public class OthersPendingApprovalViewModel
    {
        public int DiagnosisId { get; set; }
        public string TreatmentId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime VisitDate { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string DiagnosedBy { get; set; } = string.Empty;
        public string? BloodPressure { get; set; }
        public string? PulseRate { get; set; }
        public string? Sugar { get; set; }
        public string ApprovalStatus { get; set; } = "Pending";
        public int MedicineCount { get; set; }

        public List<OthersDiseaseDetails> Diseases { get; set; } = new();
        public List<OthersMedicineDetails> Medicines { get; set; } = new();
    }

    // ======= NEW: Stock validation result for Others Diagnosis =======
    public class OthersStockValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
    }

    // ======= NEW: Medicine dropdown item with batch and stock information for Others Diagnosis =======
    public class OthersMedicineDropdownItem
    {
        public int IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int AvailableStock { get; set; }

        // Display properties
        public string DisplayText => $"{MedItemName} - {BatchNo}";
        public string StockInfo => $"Stock: {AvailableStock}";
        public string ExpiryInfo => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;

        // CSS classes for styling based on expiry
        public string ExpiryClass => DaysToExpiry switch
        {
            < 0 => "text-danger", // Expired
            <= 7 => "text-warning", // Expires within a week
            <= 30 => "text-info", // Expires within a month
            _ => "text-success" // Good
        };

        public string ExpiryLabel => DaysToExpiry switch
        {
            < 0 => "EXPIRED",
            <= 7 => $"Expires in {DaysToExpiry} days",
            <= 30 => $"Expires in {DaysToExpiry} days",
            _ => $"Expires: {ExpiryInfo}"
        };
    }
}