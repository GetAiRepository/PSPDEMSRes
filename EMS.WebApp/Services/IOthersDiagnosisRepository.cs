using EMS.WebApp.Data;
using EMS.WebApp.Services; // For MedicineStockInfo

namespace EMS.WebApp.Services
{
    public interface IOthersDiagnosisRepository
    {
        /// <summary>
        /// Gets all diagnosis records for listing
        /// </summary>
        Task<List<OthersDiagnosisListViewModel>> GetAllDiagnosesAsync();

        /// <summary>
        /// Generates a new treatment ID
        /// </summary>
        Task<string> GenerateNewTreatmentIdAsync();

        /// <summary>
        /// Finds patient by treatment ID
        /// </summary>
        Task<OtherPatient?> GetPatientByTreatmentIdAsync(string treatmentId);

        /// <summary>
        /// Gets all diseases for prescription selection
        /// </summary>
        Task<List<MedDisease>> GetDiseasesAsync();

        /// <summary>
        /// Gets all medicines for prescription selection (fallback method)
        /// </summary>
        Task<List<MedMaster>> GetMedicinesAsync();

        /// <summary>
        /// Saves diagnosis data with visit type and approval logic
        /// </summary>
        Task<(bool Success, string ErrorMessage)> SaveDiagnosisAsync(OthersDiagnosisViewModel model, string createdBy);

        /// <summary>
        /// Gets detailed diagnosis information
        /// </summary>
        Task<OthersDiagnosisDetailsViewModel?> GetDiagnosisDetailsAsync(int diagnosisId);

        /// <summary>
        /// Deletes a diagnosis record
        /// </summary>
        Task<bool> DeleteDiagnosisAsync(int diagnosisId);

        /// <summary>
        /// Gets patient details with latest diagnosis
        /// </summary>
        Task<OthersDiagnosisViewModel?> GetPatientForEditAsync(string treatmentId);

        /// <summary>
        /// Gets raw medicine data for debugging
        /// </summary>
        Task<object> GetRawMedicineDataAsync(int diagnosisId);

        /// <summary>
        /// Gets medicines from compounder indent (deprecated - use GetMedicinesFromCompounderIndentAsync)
        /// </summary>
        Task<List<MedMaster>> GetCompounderMedicinesAsync();

        // ======= NEW BATCH TRACKING AND STOCK MANAGEMENT METHODS =======

        /// <summary>
        /// Gets medicines available from compounder indent with batch information and stock, sorted by expiry date
        /// </summary>
        Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync();

        /// <summary>
        /// Checks available stock for a specific medicine batch
        /// </summary>
        Task<int> GetAvailableStockAsync(int indentItemId);

        /// <summary>
        /// Updates available stock after prescription
        /// </summary>
        Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed);

        // ======= APPROVAL METHODS =======

        /// <summary>
        /// Gets count of pending approvals
        /// </summary>
        Task<int> GetPendingApprovalCountAsync();

        /// <summary>
        /// Gets pending approval diagnoses for doctor review
        /// </summary>
        Task<List<OthersPendingApprovalViewModel>> GetPendingApprovalsAsync();

        /// <summary>
        /// Approves a diagnosis
        /// </summary>
        Task<bool> ApproveDiagnosisAsync(int diagnosisId, string approvedBy);

        /// <summary>
        /// Rejects a diagnosis
        /// </summary>
        Task<bool> RejectDiagnosisAsync(int diagnosisId, string rejectionReason, string rejectedBy);

        /// <summary>
        /// Approves multiple diagnoses
        /// </summary>
        Task<int> ApproveAllDiagnosesAsync(List<int> diagnosisIds, string approvedBy);
    }
}