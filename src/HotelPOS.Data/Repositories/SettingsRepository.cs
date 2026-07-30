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

    public async Task ResetDatabaseSequencesAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var tables = new[]
            {
                "rooms", "bookings", "room_types", "customers", 
                "products", "product_categories", "sales", "sale_items", 
                "payments", "invoice_documents", "folios", "audit_logs", 
                "users", "roles"
            };

            foreach (var table in tables)
            {
                var sql = $@"
                    INSERT OR REPLACE INTO sqlite_sequence (name, seq)
                    VALUES ('{table}', (SELECT COALESCE(MAX(id), 0) FROM {table}));";
                await connection.ExecuteAsync(sql, transaction: transaction);
            }

            transaction.Commit();
            _logger.Info(LogCategory.Database, "รีเซ็ตค่าลำดับคีย์หลัก (Auto-increment Sequences) ของทุกตารางเรียบร้อยแล้ว", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "รีเซ็ตค่าลำดับคีย์หลักไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task ZetZeroDatabaseAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            // เคลียร์ pool ของ SQLite connections เพื่อให้ Windows ปล่อยล็อกไฟล์ฐานข้อมูล
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var dbFile = _connectionFactory.DatabaseFilePath;
            var walFile = dbFile + "-wal";
            var shmFile = dbFile + "-shm";

            // ลบไฟล์ฐานข้อมูลและไฟล์ล็อกชั่วคราวทั้งหมด
            if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);
            if (System.IO.File.Exists(walFile)) System.IO.File.Delete(walFile);
            if (System.IO.File.Exists(shmFile)) System.IO.File.Delete(shmFile);

            // ล้างข้อมูลโฟลเดอร์ assets (โลโก้ / QR Code ของระบบ)
            var appDataFolder = System.IO.Path.GetDirectoryName(dbFile);
            if (!string.IsNullOrEmpty(appDataFolder))
            {
                var assetsDir = System.IO.Path.Combine(appDataFolder, "assets");
                if (System.IO.Directory.Exists(assetsDir))
                {
                    System.IO.Directory.Delete(assetsDir, true);
                }
            }

            // สร้างฐานข้อมูลและโครงสร้างตารางใหม่โดยอัตโนมัติเพื่อให้พร้อมใช้งานทันที
            var runner = new MigrationRunner(_connectionFactory, _logger);
            runner.EnsureDatabaseIsReady();

            _logger.Info(LogCategory.Database, "ล้างระบบฐานข้อมูลและสร้างฐานข้อมูลเริ่มต้นใหม่ (Set Zero) สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ล้างข้อมูลระบบเป็น 0 (Set Zero) ล้มเหลว", ex, correlationId);
            throw;
        }
    }
}
