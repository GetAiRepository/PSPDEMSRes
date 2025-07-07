using System.Text.Json;

namespace EMS.WebApp.Data
{
    public class AuditTrailViewModel
    {
        public int AuditId { get; set; }
        public string TableName { get; set; } = null!;
        public string Operation { get; set; } = null!;
        public string PrimaryKey { get; set; } = null!;
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string UserId { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string? Reason { get; set; }
        public string? IpAddress { get; set; }
        public string? Action { get; set; }
        public string? AffectedColumns { get; set; }

        // Computed properties for display
        public Dictionary<string, object?>? OldValuesDict =>
            string.IsNullOrEmpty(OldValues) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(OldValues);

        public Dictionary<string, object?>? NewValuesDict =>
            string.IsNullOrEmpty(NewValues) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(NewValues);

        public List<string>? AffectedColumnsList =>
            string.IsNullOrEmpty(AffectedColumns) ? null : JsonSerializer.Deserialize<List<string>>(AffectedColumns);
    }

    public class UserActionLogViewModel
    {
        public int LogId { get; set; }
        public string UserId { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Controller { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public string? IpAddress { get; set; }
    }

    public class AuditSummaryViewModel
    {
        public int TotalAuditRecords { get; set; }
        public int TotalUserActions { get; set; }
        public int TotalInserts { get; set; }
        public int TotalUpdates { get; set; }
        public int TotalDeletes { get; set; }
        public int UniqueUsers { get; set; }
        public string? MostActiveTable { get; set; }
        public int FailedActions { get; set; }
    }

    public class AuditFilterViewModel
    {
        public string? TableName { get; set; }
        public string? UserId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Operation { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
