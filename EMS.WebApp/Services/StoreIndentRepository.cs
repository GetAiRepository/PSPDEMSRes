using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class StoreIndentRepository : IStoreIndentRepository
    {
        private readonly ApplicationDbContext _db;

        public StoreIndentRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<StoreIndent>> ListAsync(string currentUser = null)
        {
            var query = _db.StoreIndents.AsQueryable();

            // Filter drafts to show only to their creators
            if (!string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.IndentType != "Draft Indent" || s.CreatedBy == currentUser);
            }
            else
            {
                // If no current user, exclude all drafts
                query = query.Where(s => s.IndentType != "Draft Indent");
            }

            return await query
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<StoreIndent>> ListByTypeAsync(string indentType, string currentUser = null)
        {
            var query = _db.StoreIndents.Where(s => s.IndentType == indentType);

            // Additional filtering for Draft Indent - only show to creator
            if (indentType == "Draft Indent" && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.CreatedBy == currentUser);
            }
            else if (indentType == "Draft Indent" && string.IsNullOrEmpty(currentUser))
            {
                // If no current user and requesting drafts, return empty
                return new List<StoreIndent>();
            }

            return await query
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        // New method for Store Inventory to get approved indents
        public async Task<IEnumerable<StoreIndent>> ListByStatusAsync(string status, string currentUser = null)
        {
            var query = _db.StoreIndents.Where(s => s.Status == status);

            return await query
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<StoreIndent?> GetByIdAsync(int id)
        {
            return await _db.StoreIndents.FindAsync(id);
        }

        public async Task<StoreIndent?> GetByIdWithItemsAsync(int id)
        {
            return await _db.StoreIndents
                .Include(s => s.StoreIndentItems)
                    .ThenInclude(i => i.MedMaster)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task AddAsync(StoreIndent entity)
        {
            _db.StoreIndents.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(StoreIndent entity)
        {
            _db.StoreIndents.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.StoreIndents
                .Include(s => s.StoreIndentItems)
                .FirstOrDefaultAsync(s => s.IndentId == id);

            if (entity != null)
            {
                _db.StoreIndents.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Medicine-related methods
        public async Task<IEnumerable<MedMaster>> GetMedicinesAsync()
        {
            return await _db.med_masters
                .Include(m => m.MedBase)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        public async Task<MedMaster?> GetMedicineByIdAsync(int medItemId)
        {
            return await _db.med_masters
                .Include(m => m.MedBase)
                .FirstOrDefaultAsync(m => m.MedItemId == medItemId);
        }

        // StoreIndentItem methods
        public async Task AddItemAsync(StoreIndentItem item)
        {
            _db.StoreIndentItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateItemAsync(StoreIndentItem item)
        {
            _db.StoreIndentItems.Update(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(int indentItemId)
        {
            var item = await _db.StoreIndentItems.FindAsync(indentItemId);
            if (item != null)
            {
                _db.StoreIndentItems.Remove(item);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<StoreIndentItem?> GetItemByIdAsync(int indentItemId)
        {
            return await _db.StoreIndentItems
                .Include(i => i.MedMaster)
                .FirstOrDefaultAsync(i => i.IndentItemId == indentItemId);
        }

        public async Task<IEnumerable<StoreIndentItem>> GetItemsByIndentIdAsync(int indentId)
        {
            return await _db.StoreIndentItems
                .Include(i => i.MedMaster)
                .Where(i => i.IndentId == indentId)
                .ToListAsync();
        }

        // Business logic methods
        public async Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null)
        {
            if (string.IsNullOrWhiteSpace(vendorCode))
                return false;

            var query = _db.StoreIndentItems.Where(i =>
                i.IndentId == indentId &&
                i.VendorCode.ToLower() == vendorCode.ToLower());

            if (excludeItemId.HasValue)
            {
                query = query.Where(i => i.IndentItemId != excludeItemId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null)
        {
            var query = _db.StoreIndentItems.Where(i =>
                i.IndentId == indentId &&
                i.MedItemId == medItemId);

            if (excludeItemId.HasValue)
            {
                query = query.Where(i => i.IndentItemId != excludeItemId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<StoreIndentReportDto>> GetStoreIndentReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _db.StoreIndents
                .Include(si => si.StoreIndentItems)
                    .ThenInclude(sii => sii.MedMaster)
                        .ThenInclude(mm => mm.MedBase)
                .Where(si => si.Status != "Draft"); // Exclude drafts

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(si => si.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(si => si.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(si => si.IndentDate).ToListAsync();

            var reportData = new List<StoreIndentReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.StoreIndentItems)
                {
                    reportData.Add(new StoreIndentReportDto
                    {
                        IndentId = indent.IndentId,
                        IndentDate = indent.IndentDate,
                        MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                        Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                        ManufacturerName = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                        Quantity = item.RaisedQuantity,
                        RaisedBy = indent.CreatedBy ?? "Unknown"
                    });
                }
            }

            return reportData;
        }

        public async Task<IEnumerable<StoreInventoryReportDto>> GetStoreInventoryReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _db.StoreIndents
                .Include(si => si.StoreIndentItems)
                    .ThenInclude(sii => sii.MedMaster)
                        .ThenInclude(mm => mm.MedBase)
                .Where(si => si.Status == "Approved"); // Only approved indents for inventory

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(si => si.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(si => si.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(si => si.IndentDate).ToListAsync();

            var reportData = new List<StoreInventoryReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.StoreIndentItems.Where(i => i.ReceivedQuantity > 0))
                {
                    reportData.Add(new StoreInventoryReportDto
                    {
                        IndentId = indent.IndentId,
                        RaisedDate = indent.IndentDate,
                        MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                        RaisedQuantity = item.RaisedQuantity,
                        ReceivedQuantity = item.ReceivedQuantity,
                        Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                        ManufacturerBy = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                        BatchNo = item.BatchNo ?? "000",
                        ManufactureDate = item.ExpiryDate?.AddYears(-2), // Assuming 2 years shelf life, you may need to add ManufactureDate field
                        ExpiryDate = item.ExpiryDate,
                        RaisedBy = indent.CreatedBy ?? "Unknown"
                    });
                }
            }

            return reportData;
        }
    }
}