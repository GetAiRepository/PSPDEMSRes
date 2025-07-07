using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IExpiredMedicineRepository
    {
        // Basic CRUD operations
        Task<ExpiredMedicine?> GetByIdAsync(int id);
        Task<ExpiredMedicine?> GetByIdWithDetailsAsync(int id);
        Task<IEnumerable<ExpiredMedicine>> ListAsync();
        Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync();
        Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync();
        Task AddAsync(ExpiredMedicine entity);
        Task UpdateAsync(ExpiredMedicine entity);
        Task DeleteAsync(int id);

        // Business logic methods
        Task<IEnumerable<ExpiredMedicine>> GetByStatusAsync(string status);
        Task<IEnumerable<ExpiredMedicine>> GetByPriorityLevelAsync(string priority);
        Task<IEnumerable<ExpiredMedicine>> GetCriticalExpiredMedicinesAsync();
        Task<bool> IsAlreadyTrackedAsync(int compounderIndentItemId);

        // Biomedical waste operations
        Task IssueToBiomedicalWasteAsync(int expiredMedicineId, string issuedBy, string? remarks = null);
        Task BulkIssueToBiomedicalWasteAsync(List<int> expiredMedicineIds, string issuedBy, string? remarks = null);

        // Sync operations to detect newly expired medicines
        Task<List<ExpiredMedicine>> DetectNewExpiredMedicinesAsync(string detectedBy);
        Task SyncExpiredMedicinesAsync(string detectedBy);

        // Statistics and reporting
        Task<int> GetTotalExpiredCountAsync();
        Task<int> GetPendingDisposalCountAsync();
        Task<int> GetDisposedCountAsync();
        Task<decimal> GetTotalExpiredValueAsync();
        Task<IEnumerable<ExpiredMedicine>> GetExpiredMedicinesForPrintAsync(List<int> ids);

        // Inline editing methods
        Task UpdateMedicineTypeAsync(int expiredMedicineId, string typeOfMedicine);
    }
}