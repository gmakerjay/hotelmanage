using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Data;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public AuditRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task AddLogAsync(AuditLogEntry entry)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO audit_logs (user_id, action, entity_name, entity_id, detail_json, created_at)
                VALUES (@UserId, @Action, @EntityName, @EntityId, @DetailJson, datetime('now', 'localtime'));";

            await connection.ExecuteAsync(sql, entry);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "บันทึก Audit Log ไม่สำเร็จ", ex);
        }
    }

    public async Task<IEnumerable<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, user_id AS UserId, action AS Action, 
                       entity_name AS EntityName, entity_id AS EntityId, 
                       detail_json AS DetailJson, created_at AS CreatedAt
                FROM audit_logs
                WHERE 1=1";

            var parameters = new DynamicParameters();
            if (startDate.HasValue)
            {
                sql += " AND created_at >= @Start";
                parameters.Add("Start", startDate.Value.ToString("yyyy-MM-dd 00:00:00"));
            }
            if (endDate.HasValue)
            {
                sql += " AND created_at <= @End";
                parameters.Add("End", endDate.Value.ToString("yyyy-MM-dd 23:59:59"));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += " AND (action LIKE @Search OR entity_name LIKE @Search OR detail_json LIKE @Search)";
                parameters.Add("Search", $"%{search.Trim()}%");
            }

            sql += " ORDER BY created_at DESC LIMIT 500";
            return await connection.QueryAsync<AuditLogEntry>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่าน Audit Log ไม่สำเร็จ", ex);
            return Array.Empty<AuditLogEntry>();
        }
    }
}
