using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Data.Repositories;

namespace HotelPOS.Core.Services;

public interface IAuditService
{
    Task LogAsync(string action, string? entityName = null, string? entityId = null, string? details = null, int? userId = null);
    Task<IEnumerable<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null);
}
