using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Common;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly IAppLogger _logger;

    public AuditService(IAuditRepository auditRepository, IAppLogger logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task LogAsync(string action, string? entityName = null, string? entityId = null, string? details = null, int? userId = null)
    {
        var entry = new AuditLogEntry
        {
            UserId = userId ?? 1,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            DetailJson = details
        };

        await _auditRepository.AddLogAsync(entry);
        _logger.Info(LogCategory.System, $"[Audit] {action} | {entityName} {entityId} | {details}");
    }

    public async Task<IEnumerable<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null)
    {
        return await _auditRepository.GetLogsAsync(startDate, endDate, search);
    }
}
