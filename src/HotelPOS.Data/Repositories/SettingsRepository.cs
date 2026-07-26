using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

/// <summary>
/// ตัวอย่าง Repository มาตรฐานที่ใช้เป็นแม่แบบสำหรับ Repository อื่นๆ ทั้งหมดในระบบ
/// (RoomRepository, BookingRepository, ProductRepository ฯลฯ ให้ทำตามรูปแบบนี้)
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public SettingsRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AppSetting>> GetAllAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT key AS Key, value AS Value, description AS Description, updated_at AS UpdatedAt FROM settings";
            return await connection.QueryAsync<AppSetting>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านค่า settings ทั้งหมดไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<AppSetting?> GetByKeyAsync(string key)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT key AS Key, value AS Value, description AS Description, updated_at AS UpdatedAt FROM settings WHERE key = @Key";
            return await connection.QuerySingleOrDefaultAsync<AppSetting>(sql, new { Key = key });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านค่า settings key='{key}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task UpsertAsync(string key, string? value)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO settings (key, value, updated_at)
                VALUES (@Key, @Value, datetime('now','localtime'))
                ON CONFLICT(key) DO UPDATE SET
                    value = excluded.value,
                    updated_at = excluded.updated_at;";
            await connection.ExecuteAsync(sql, new { Key = key, Value = value });
            _logger.Info(LogCategory.Database, $"บันทึกค่า settings key='{key}' สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกค่า settings key='{key}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
