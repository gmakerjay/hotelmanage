using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HotelPOS.Data.Repositories;

public class AuditLogEntry
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IAuditRepository
{
    Task AddLogAsync(AuditLogEntry entry);
    Task<IEnumerable<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null);
}
