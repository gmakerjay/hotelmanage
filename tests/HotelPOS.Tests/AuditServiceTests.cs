using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class AuditServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly IAuditRepository _auditRepo;
    private readonly IAuditService _auditService;

    public AuditServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-audit-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-audit-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();
        _auditRepo = new AuditRepository(_connectionFactory, _logger);
        _auditService = new AuditService(_auditRepo, _logger);
    }

    [Fact]
    public async Task LogAsync_และ_GetLogs_บันทึกและดึงประวัติการกระทำถูกต้อง()
    {
        await _auditService.LogAsync("CHECK_IN", "rooms", "101", "เช็คอินห้อง 101 ลูกค้า: สมชาย", userId: 1);
        await _auditService.LogAsync("POS_SALE", "sales", "SL-001", "ขายของมินิบาร์ ยอด 150 บาท", userId: 1);

        var logs = (await _auditService.GetLogsAsync()).ToList();

        Assert.True(logs.Count >= 2);
        var checkInLog = logs.FirstOrDefault(l => l.Action == "CHECK_IN");
        Assert.NotNull(checkInLog);
        Assert.Equal("rooms", checkInLog.EntityName);
        Assert.Equal("101", checkInLog.EntityId);
    }

    [Fact]
    public async Task GetLogs_ค้นหาด้วยคำค้น_กรองประวัติถูกต้อง()
    {
        await _auditService.LogAsync("CHANGE_RATE", "settings", "electric_rate", "ปรับค่าไฟเป็น 8 บาท");
        await _auditService.LogAsync("DELETE_ROOM", "rooms", "102", "ลบห้อง 102");

        var results = (await _auditService.GetLogsAsync(search: "CHANGE_RATE")).ToList();

        Assert.Single(results);
        Assert.Equal("CHANGE_RATE", results[0].Action);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger) disposableLogger.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
                if (Directory.Exists(_tempLogPath)) Directory.Delete(_tempLogPath, recursive: true);
                break;
            }
            catch (IOException) { Thread.Sleep(100); }
        }
    }
}
