using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class ExpiredMedicineRepository : IExpiredMedicineRepository
    {
        private readonly ApplicationDbContext _db;

        public ExpiredMedicineRepository(ApplicationDbContext db) => _db = db;

        // Basic CRUD operations
        public async Task<ExpiredMedicine?> GetByIdAsync(int id)
        {
            return await _db.ExpiredMedicines.FindAsync(id);
        }

        public async Task<ExpiredMedicine?> GetByIdWithDetailsAsync(int id)
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.CompounderIndent)
                .FirstOrDefaultAsync(e => e.ExpiredMedicineId == id);
        }

        public async Task<IEnumerable<ExpiredMedicine>> ListAsync()
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .OrderByDescending(e => e.DetectedDate)
                .ThenBy(e => e.MedicineName)
                .ToListAsync();
        }

        public async Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync()
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => e.Status == "Pending Disposal")
                .OrderBy(e => e.ExpiryDate) // Order by expiry date instead of DaysOverdue
                .ThenBy(e => e.MedicineName)
                .ToListAsync();
        }

        public async Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync()
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => e.Status == "Issued to Biomedical Waste")
                .OrderByDescending(e => e.BiomedicalWasteIssuedDate)
                .ThenBy(e => e.MedicineName)
                .ToListAsync();
        }

        public async Task AddAsync(ExpiredMedicine entity)
        {
            _db.ExpiredMedicines.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ExpiredMedicine entity)
        {
            _db.ExpiredMedicines.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.ExpiredMedicines.FindAsync(id);
            if (entity != null)
            {
                _db.ExpiredMedicines.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Business logic methods
        public async Task<IEnumerable<ExpiredMedicine>> GetByStatusAsync(string status)
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => e.Status == status)
                .OrderByDescending(e => e.DetectedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetByPriorityLevelAsync(string priority)
        {
            var query = _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => e.Status == "Pending Disposal");

            var today = DateTime.Today;

            query = priority.ToLower() switch
            {
                "low" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) <= 30),
                "medium" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) > 30 &&
                                           EF.Functions.DateDiffDay(e.ExpiryDate, today) <= 90),
                "high" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) > 90),
                _ => query
            };

            return await query.OrderBy(e => e.ExpiryDate) // Use ExpiryDate instead of DaysOverdue
                             .ToListAsync();
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetCriticalExpiredMedicinesAsync()
        {
            var today = DateTime.Today;
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => e.Status == "Pending Disposal" &&
                           EF.Functions.DateDiffDay(e.ExpiryDate, today) > 90)
                .OrderBy(e => e.ExpiryDate) // Use ExpiryDate instead of DaysOverdue
                .ToListAsync();
        }

        public async Task<bool> IsAlreadyTrackedAsync(int compounderIndentItemId)
        {
            return await _db.ExpiredMedicines
                .AnyAsync(e => e.CompounderIndentItemId == compounderIndentItemId);
        }

        // Biomedical waste operations - FIXED: Removed remarks parameters and assignments
        public async Task IssueToBiomedicalWasteAsync(int expiredMedicineId, string issuedBy, string? remarks = null)
        {
            var expiredMedicine = await _db.ExpiredMedicines.FindAsync(expiredMedicineId);
            if (expiredMedicine != null && expiredMedicine.Status == "Pending Disposal")
            {
                expiredMedicine.Status = "Issued to Biomedical Waste";
                expiredMedicine.BiomedicalWasteIssuedDate = DateTime.Now;
                expiredMedicine.BiomedicalWasteIssuedBy = issuedBy;
                // REMOVED: expiredMedicine.Remarks = remarks; (property no longer exists)

                await _db.SaveChangesAsync();
            }
        }

        public async Task BulkIssueToBiomedicalWasteAsync(List<int> expiredMedicineIds, string issuedBy, string? remarks = null)
        {
            var expiredMedicines = await _db.ExpiredMedicines
                .Where(e => expiredMedicineIds.Contains(e.ExpiredMedicineId) &&
                           e.Status == "Pending Disposal")
                .ToListAsync();

            var issuedDate = DateTime.Now;

            foreach (var expiredMedicine in expiredMedicines)
            {
                expiredMedicine.Status = "Issued to Biomedical Waste";
                expiredMedicine.BiomedicalWasteIssuedDate = issuedDate;
                expiredMedicine.BiomedicalWasteIssuedBy = issuedBy;
                // REMOVED: expiredMedicine.Remarks = remarks; (property no longer exists)
            }

            await _db.SaveChangesAsync();
        }

        // Sync operations to detect newly expired medicines
        public async Task<List<ExpiredMedicine>> DetectNewExpiredMedicinesAsync(string detectedBy)
        {
            var today = DateTime.Today;
            var newExpiredMedicines = new List<ExpiredMedicine>();

            // Get all CompounderIndentItems that are expired but not yet tracked
            var expiredItems = await _db.CompounderIndentItems
                .Include(c => c.MedMaster)
                .Include(c => c.CompounderIndent)
                .Where(c => c.CompounderIndent.Status == "Approved" &&  // Only from approved indents
                           c.ReceivedQuantity > 0 &&                    // Only received medicines
                           c.ExpiryDate.HasValue &&                     // Must have expiry date
                           c.ExpiryDate.Value.Date < today)             // Expired
                .ToListAsync();

            foreach (var item in expiredItems)
            {
                // Check if not already tracked
                if (!await IsAlreadyTrackedAsync(item.IndentItemId))
                {
                    var expiredMedicine = new ExpiredMedicine
                    {
                        CompounderIndentItemId = item.IndentItemId,
                        MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                        CompanyName = item.MedMaster?.CompanyName ?? "Not Defined",
                        BatchNumber = item.BatchNo ?? "N/A",
                        VendorCode = item.VendorCode,
                        ExpiryDate = item.ExpiryDate!.Value,
                        QuantityExpired = item.ReceivedQuantity,
                        IndentId = item.IndentId,
                        IndentNumber = item.IndentId.ToString(),
                        UnitPrice = item.UnitPrice,
                        TotalValue = item.UnitPrice * item.ReceivedQuantity,
                        DetectedDate = DateTime.Now,
                        DetectedBy = detectedBy,
                        Status = "Pending Disposal",
                        TypeOfMedicine = "Select Type of Medicine" // DEFAULT VALUE - Users can edit inline later
                    };

                    newExpiredMedicines.Add(expiredMedicine);
                }
            }

            return newExpiredMedicines;
        }

        public async Task SyncExpiredMedicinesAsync(string detectedBy)
        {
            var newExpiredMedicines = await DetectNewExpiredMedicinesAsync(detectedBy);

            if (newExpiredMedicines.Any())
            {
                _db.ExpiredMedicines.AddRange(newExpiredMedicines);
                await _db.SaveChangesAsync();
            }
        }

        // Statistics and reporting
        public async Task<int> GetTotalExpiredCountAsync()
        {
            return await _db.ExpiredMedicines.CountAsync();
        }

        public async Task<int> GetPendingDisposalCountAsync()
        {
            return await _db.ExpiredMedicines.CountAsync(e => e.Status == "Pending Disposal");
        }

        public async Task<int> GetDisposedCountAsync()
        {
            return await _db.ExpiredMedicines.CountAsync(e => e.Status == "Issued to Biomedical Waste");
        }

        public async Task<decimal> GetTotalExpiredValueAsync()
        {
            return await _db.ExpiredMedicines
                .Where(e => e.TotalValue.HasValue)
                .SumAsync(e => e.TotalValue.Value);
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetExpiredMedicinesForPrintAsync(List<int> ids)
        {
            return await _db.ExpiredMedicines
                .Include(e => e.CompounderIndentItem)
                    .ThenInclude(c => c.MedMaster)
                .Where(e => ids.Contains(e.ExpiredMedicineId))
                .OrderBy(e => e.MedicineName)
                .ToListAsync();
        }

        // Inline editing method for medicine type
        public async Task UpdateMedicineTypeAsync(int expiredMedicineId, string typeOfMedicine)
        {
            var expiredMedicine = await _db.ExpiredMedicines.FindAsync(expiredMedicineId);
            if (expiredMedicine != null)
            {
                expiredMedicine.TypeOfMedicine = typeOfMedicine;
                await _db.SaveChangesAsync();
            }
        }
    }
}