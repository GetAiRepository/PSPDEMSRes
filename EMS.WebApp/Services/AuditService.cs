using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EMS.WebApp.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogUserActionAsync(string action, string? controller = null, string? description = null,
            object? parameters = null, string result = "Success", string? errorMessage = null,
            TimeSpan? executionTime = null)
        {
            var context = _httpContextAccessor.HttpContext;

            var userActionLog = new UserActionLog
            {
                UserId = context?.User?.Identity?.Name ?? "SYSTEM",
                Action = action,
                Controller = controller,
                Description = description,
                Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
                Result = result,
                ErrorMessage = errorMessage,
                ExecutionTime = executionTime,
                IpAddress = context?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = context?.Request?.Headers["User-Agent"].FirstOrDefault(),
                SessionId = context?.Session?.Id
            };

            _context.UserActionLogs.Add(userActionLog);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditTrailViewModel>> GetAuditTrailAsync(string? tableName = null, string? userId = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50)
        {
            var query = _context.AuditTrails.AsQueryable();

            if (!string.IsNullOrEmpty(tableName))
                query = query.Where(a => a.TableName.Contains(tableName));

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId.Contains(userId));

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value.AddDays(1));

            var auditTrails = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditTrailViewModel
                {
                    AuditId = a.AuditId,
                    TableName = a.TableName,
                    Operation = a.Operation,
                    PrimaryKey = a.PrimaryKey,
                    UserId = a.UserId,
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    IpAddress = a.IpAddress,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    AffectedColumns = a.AffectedColumns,
                    Reason = a.Reason
                })
                .ToListAsync();

            return auditTrails;
        }

        public async Task<List<UserActionLogViewModel>> GetUserActionLogsAsync(string? userId = null, string? action = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50)
        {
            var query = _context.UserActionLogs.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(l => l.UserId.Contains(userId));

            if (!string.IsNullOrEmpty(action))
                query = query.Where(l => l.Action.Contains(action));

            if (fromDate.HasValue)
                query = query.Where(l => l.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(l => l.Timestamp <= toDate.Value.AddDays(1));

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new UserActionLogViewModel
                {
                    LogId = l.LogId,
                    UserId = l.UserId,
                    Action = l.Action,
                    Controller = l.Controller,
                    Description = l.Description,
                    Timestamp = l.Timestamp,
                    Result = l.Result,
                    ErrorMessage = l.ErrorMessage,
                    ExecutionTime = l.ExecutionTime,
                    IpAddress = l.IpAddress
                })
                .ToListAsync();

            return logs;
        }

        public async Task<AuditSummaryViewModel> GetAuditSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var auditQuery = _context.AuditTrails.AsQueryable();
            var actionQuery = _context.UserActionLogs.AsQueryable();

            if (fromDate.HasValue)
            {
                auditQuery = auditQuery.Where(a => a.Timestamp >= fromDate.Value);
                actionQuery = actionQuery.Where(a => a.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.AddDays(1);
                auditQuery = auditQuery.Where(a => a.Timestamp <= endDate);
                actionQuery = actionQuery.Where(a => a.Timestamp <= endDate);
            }

            var summary = new AuditSummaryViewModel
            {
                TotalAuditRecords = await auditQuery.CountAsync(),
                TotalUserActions = await actionQuery.CountAsync(),
                TotalInserts = await auditQuery.CountAsync(a => a.Operation == "CREATE"),
                TotalUpdates = await auditQuery.CountAsync(a => a.Operation == "UPDATE"),
                TotalDeletes = await auditQuery.CountAsync(a => a.Operation == "DELETE"),
                UniqueUsers = await auditQuery.Select(a => a.UserId).Distinct().CountAsync(),
                MostActiveTable = await auditQuery
                    .GroupBy(a => a.TableName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync(),
                FailedActions = await actionQuery.CountAsync(a => a.Result == "Failed")
            };

            return summary;
        }

        public async Task<List<AuditTrailViewModel>> GetEntityAuditHistoryAsync(string tableName, string primaryKey)
        {
            var auditHistory = await _context.AuditTrails
                .Where(a => a.TableName == tableName && a.PrimaryKey == primaryKey)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new AuditTrailViewModel
                {
                    AuditId = a.AuditId,
                    TableName = a.TableName,
                    Operation = a.Operation,
                    PrimaryKey = a.PrimaryKey,
                    UserId = a.UserId,
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    IpAddress = a.IpAddress,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    AffectedColumns = a.AffectedColumns,
                    Reason = a.Reason
                })
                .ToListAsync();

            return auditHistory;
        }
    }
}