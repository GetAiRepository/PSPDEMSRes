using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface ICompounderIndentRepository
    {
        Task<CompounderIndent?> GetByIdWithItemsAsync(int id);
        Task<IEnumerable<CompounderIndent>> ListAsync(string currentUser = null);
        Task<IEnumerable<CompounderIndent>> ListByTypeAsync(string indentType, string currentUser = null);
        Task<IEnumerable<CompounderIndent>> ListByStatusAsync(string status, string currentUser = null);
        Task<CompounderIndent?> GetByIdAsync(int id);
        Task AddAsync(CompounderIndent entity);
        Task UpdateAsync(CompounderIndent entity);
        Task DeleteAsync(int id);

        // Medicine-related methods
        Task<IEnumerable<MedMaster>> GetMedicinesAsync();
        Task<MedMaster?> GetMedicineByIdAsync(int medItemId);

        // CompounderIndentItem methods
        Task AddItemAsync(CompounderIndentItem item);
        Task UpdateItemAsync(CompounderIndentItem item);
        Task DeleteItemAsync(int indentItemId);
        Task<CompounderIndentItem?> GetItemByIdAsync(int indentItemId);
        Task<IEnumerable<CompounderIndentItem>> GetItemsByIndentIdAsync(int indentId);

        // Business logic methods
        Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null);
        Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null);

        // NEW: Report methods
        Task<IEnumerable<CompounderIndentReportDto>> GetCompounderIndentReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<IEnumerable<CompounderInventoryReportDto>> GetCompounderInventoryReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }

    // DTOs for Reports
    public class CompounderIndentReportDto
    {
        public int IndentId { get; set; }
        public DateTime IndentDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
    }

    public class CompounderInventoryReportDto
    {
        public int IndentId { get; set; }
        public DateTime RaisedDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int RaisedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerBy { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
    }
}