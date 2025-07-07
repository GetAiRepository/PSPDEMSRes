using EMS.WebApp.Data;
namespace EMS.WebApp.Services
{
    public interface IAuditService
    {
        Task LogUserActionAsync(string action, string? controller = null, string? description = null,
            object? parameters = null, string result = "Success", string? errorMessage = null,
            TimeSpan? executionTime = null);

        Task<List<AuditTrailViewModel>> GetAuditTrailAsync(string? tableName = null, string? userId = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50);

        Task<List<UserActionLogViewModel>> GetUserActionLogsAsync(string? userId = null, string? action = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50);

        Task<AuditSummaryViewModel> GetAuditSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null);

        Task<List<AuditTrailViewModel>> GetEntityAuditHistoryAsync(string tableName, string primaryKey);
    }
}