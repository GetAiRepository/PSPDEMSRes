using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class CompounderIndentRepository : ICompounderIndentRepository
    {
        private readonly ApplicationDbContext _db;

        public CompounderIndentRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<CompounderIndent>> ListAsync(string currentUser = null)
        {
            var query = _db.CompounderIndents.AsQueryable();

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

        public async Task<IEnumerable<CompounderIndent>> ListByTypeAsync(string indentType, string currentUser = null)
        {
            var query = _db.CompounderIndents.Where(s => s.IndentType == indentType);

            // Additional filtering for Draft Indent - only show to creator
            if (indentType == "Draft Indent" && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.CreatedBy == currentUser);
            }
            else if (indentType == "Draft Indent" && string.IsNullOrEmpty(currentUser))
            {
                // If no current user and requesting drafts, return empty
                return new List<CompounderIndent>();
            }

            return await query
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        // New method for Compounder Inventory to get approved indents
        public async Task<IEnumerable<CompounderIndent>> ListByStatusAsync(string status, string currentUser = null)
        {
            var query = _db.CompounderIndents.Where(s => s.Status == status);

            return await query
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<CompounderIndent?> GetByIdAsync(int id)
        {
            return await _db.CompounderIndents.FindAsync(id);
        }

        public async Task<CompounderIndent?> GetByIdWithItemsAsync(int id)
        {
            return await _db.CompounderIndents
                .Include(s => s.CompounderIndentItems)
                    .ThenInclude(i => i.MedMaster)
                        .ThenInclude(m => m.MedBase)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task AddAsync(CompounderIndent entity)
        {
            _db.CompounderIndents.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(CompounderIndent entity)
        {
            _db.CompounderIndents.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.CompounderIndents
                .Include(s => s.CompounderIndentItems)
                .FirstOrDefaultAsync(s => s.IndentId == id);

            if (entity != null)
            {
                _db.CompounderIndents.Remove(entity);
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

        // CompounderIndentItem methods
        public async Task AddItemAsync(CompounderIndentItem item)
        {
            _db.CompounderIndentItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateItemAsync(CompounderIndentItem item)
        {
            _db.CompounderIndentItems.Update(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(int indentItemId)
        {
            var item = await _db.CompounderIndentItems.FindAsync(indentItemId);
            if (item != null)
            {
                _db.CompounderIndentItems.Remove(item);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<CompounderIndentItem?> GetItemByIdAsync(int indentItemId)
        {
            return await _db.CompounderIndentItems
                .Include(i => i.MedMaster)
                .FirstOrDefaultAsync(i => i.IndentItemId == indentItemId);
        }

        public async Task<IEnumerable<CompounderIndentItem>> GetItemsByIndentIdAsync(int indentId)
        {
            return await _db.CompounderIndentItems
                .Include(i => i.MedMaster)
                .Where(i => i.IndentId == indentId)
                .ToListAsync();
        }

        // Business logic methods
        public async Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null)
        {
            if (string.IsNullOrWhiteSpace(vendorCode))
                return false;

            var query = _db.CompounderIndentItems.Where(i =>
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
            var query = _db.CompounderIndentItems.Where(i =>
                i.IndentId == indentId &&
                i.MedItemId == medItemId);

            if (excludeItemId.HasValue)
            {
                query = query.Where(i => i.IndentItemId != excludeItemId.Value);
            }

            return await query.AnyAsync();
        }

        // NEW: Report Methods
        public async Task<IEnumerable<CompounderIndentReportDto>> GetCompounderIndentReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _db.CompounderIndents
                .Include(ci => ci.CompounderIndentItems)
                    .ThenInclude(cii => cii.MedMaster)
                        .ThenInclude(mm => mm.MedBase)
                .Where(ci => ci.Status != "Draft"); // Exclude drafts

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(ci => ci.IndentDate).ToListAsync();

            var reportData = new List<CompounderIndentReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.CompounderIndentItems)
                {
                    reportData.Add(new CompounderIndentReportDto
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

        public async Task<IEnumerable<CompounderInventoryReportDto>> GetCompounderInventoryReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _db.CompounderIndents
                .Include(ci => ci.CompounderIndentItems)
                    .ThenInclude(cii => cii.MedMaster)
                        .ThenInclude(mm => mm.MedBase)
                .Where(ci => ci.Status == "Approved"); // Only approved indents for inventory

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(ci => ci.IndentDate).ToListAsync();

            var reportData = new List<CompounderInventoryReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.CompounderIndentItems.Where(i => i.ReceivedQuantity > 0))
                {
                    reportData.Add(new CompounderInventoryReportDto
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