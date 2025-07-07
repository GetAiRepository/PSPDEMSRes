using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IStoreIndentRepository
    {
        Task<StoreIndent?> GetByIdWithItemsAsync(int id);
        Task<IEnumerable<StoreIndent>> ListAsync(string currentUser = null);
        Task<IEnumerable<StoreIndent>> ListByTypeAsync(string indentType, string currentUser = null);
        Task<IEnumerable<StoreIndent>> ListByStatusAsync(string status, string currentUser = null);
        Task<StoreIndent?> GetByIdAsync(int id);
        Task AddAsync(StoreIndent entity);
        Task UpdateAsync(StoreIndent entity);
        Task DeleteAsync(int id);

        // Medicine-related methods
        Task<IEnumerable<MedMaster>> GetMedicinesAsync();
        Task<MedMaster?> GetMedicineByIdAsync(int medItemId);

        // StoreIndentItem methods
        Task AddItemAsync(StoreIndentItem item);
        Task UpdateItemAsync(StoreIndentItem item);
        Task DeleteItemAsync(int indentItemId);
        Task<StoreIndentItem?> GetItemByIdAsync(int indentItemId);
        Task<IEnumerable<StoreIndentItem>> GetItemsByIndentIdAsync(int indentId);

        // Business logic methods
        Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null);
        Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null);

        // NEW: Report methods
        Task<IEnumerable<StoreIndentReportDto>> GetStoreIndentReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<IEnumerable<StoreInventoryReportDto>> GetStoreInventoryReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }

    // DTOs for Store Reports
    public class StoreIndentReportDto
    {
        public int IndentId { get; set; }
        public DateTime IndentDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
    }

    public class StoreInventoryReportDto
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