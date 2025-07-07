using EMS.WebApp.Data;
namespace EMS.WebApp.Services
{
    public interface IDoctorDiagnosisRepository
    {
        /// <summary>
        /// Gets employee by employee ID (emp_id)
        /// </summary>
        /// <param name="empId">Employee ID</param>
        /// <returns>Employee with related data</returns>
        Task<HrEmployee?> GetEmployeeByEmpIdAsync(string empId);

        /// <summary>
        /// Gets all medical conditions for health profile display
        /// </summary>
        /// <returns>List of medical conditions</returns>
        Task<List<RefMedCondition>> GetMedicalConditionsAsync();

        /// <summary>
        /// Gets employee's selected medical conditions for a specific exam date
        /// </summary>
        /// <param name="empUid">Employee UID</param>
        /// <param name="examDate">Examination date</param>
        /// <returns>List of selected condition IDs</returns>
        Task<List<int>> GetEmployeeSelectedConditionsAsync(int empUid, DateTime examDate);

        /// <summary>
        /// Gets all diseases for prescription selection
        /// </summary>
        /// <returns>List of diseases</returns>
        Task<List<MedDisease>> GetDiseasesAsync();

        /// <summary>
        /// Gets all medicines for prescription selection
        /// </summary>
        /// <returns>List of medicines with base information</returns>
        Task<List<MedMaster>> GetMedicinesAsync();

        /// <summary>
        /// Gets employee IDs matching the search term for autocomplete
        /// </summary>
        /// <param name="term">Search term</param>
        /// <returns>List of matching employee IDs</returns>
        Task<List<string>> SearchEmployeeIdsAsync(string term);

        /// <summary>
        /// Gets previous diagnoses for an employee
        /// </summary>
        /// <param name="empId">Employee ID</param>
        /// <returns>List of previous diagnoses</returns>
        Task<List<DiagnosisEntry>> GetEmployeeDiagnosesAsync(string empId);

        /// <summary>
        /// Saves prescription data
        /// </summary>
        /// <param name="empId">Employee ID</param>
        /// <param name="examDate">Examination date</param>
        /// <param name="selectedDiseases">Selected disease IDs</param>
        /// <param name="medicines">Prescribed medicines</param>
        /// <param name="vitalSigns">Vital signs data</param>
        /// <param name="createdBy">User who created the prescription</param>
        /// <param name="visitType">Type of visit (for logging purposes only)</param>
        /// <returns>Success status</returns>
        Task<bool> SavePrescriptionAsync(string empId, DateTime examDate,
            List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
            VitalSigns vitalSigns, string createdBy, string? visitType = null);

        /// <summary>
        /// Gets detailed prescription information including diseases and medicines
        /// </summary>
        /// <param name="prescriptionId">Prescription ID</param>
        /// <returns>Prescription details or null if not found</returns>
        Task<PrescriptionDetailsViewModel?> GetPrescriptionDetailsAsync(int prescriptionId);

        /// <summary>
        /// Gets medicines available from compounder indent with batch information and stock
        /// </summary>
        /// <returns>List of available medicines with batch and stock details</returns>
        Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync();

        /// <summary>
        /// Checks available stock for a specific medicine batch
        /// </summary>
        /// <param name="indentItemId">Compounder indent item ID</param>
        /// <returns>Available stock quantity</returns>
        Task<int> GetAvailableStockAsync(int indentItemId);

        /// <summary>
        /// Updates available stock after prescription
        /// </summary>
        /// <param name="indentItemId">Compounder indent item ID</param>
        /// <param name="quantityUsed">Quantity to subtract from stock</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed);

        // ======= NEW APPROVAL METHODS =======

        /// <summary>
        /// Gets the count of prescriptions pending approval
        /// </summary>
        /// <returns>Count of pending prescriptions</returns>
        Task<int> GetPendingApprovalCountAsync();

        /// <summary>
        /// Gets all prescriptions pending approval with detailed information
        /// </summary>
        /// <returns>List of pending approval view models</returns>
        Task<List<PendingApprovalViewModel>> GetPendingApprovalsAsync();

        /// <summary>
        /// Approves a prescription
        /// </summary>
        /// <param name="prescriptionId">Prescription ID to approve</param>
        /// <param name="approvedBy">User who approved the prescription</param>
        /// <returns>Success status</returns>
        Task<bool> ApprovePrescriptionAsync(int prescriptionId, string approvedBy);

        /// <summary>
        /// Rejects a prescription with reason
        /// </summary>
        /// <param name="prescriptionId">Prescription ID to reject</param>
        /// <param name="rejectionReason">Reason for rejection</param>
        /// <param name="rejectedBy">User who rejected the prescription</param>
        /// <returns>Success status</returns>
        Task<bool> RejectPrescriptionAsync(int prescriptionId, string rejectionReason, string rejectedBy);

        /// <summary>
        /// Approves multiple prescriptions in bulk
        /// </summary>
        /// <param name="prescriptionIds">List of prescription IDs to approve</param>
        /// <param name="approvedBy">User who approved the prescriptions</param>
        /// <returns>Number of successfully approved prescriptions</returns>
        Task<int> ApproveAllPrescriptionsAsync(List<int> prescriptionIds, string approvedBy);
    }

    public class VitalSigns
    {
        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
    }

    // NEW: Class to hold medicine stock information with batch details
    public class MedicineStockInfo
    {
        public int IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int AvailableStock { get; set; }
        public string DisplayName => $"{MedItemName} - {BatchNo}";
        public string ExpiryDateFormatted => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;
    }
}