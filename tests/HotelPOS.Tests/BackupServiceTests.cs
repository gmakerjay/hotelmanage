using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HotelPOS.Tests;

public class DummyAuditService : IAuditService
{
    public Task LogAsync(string action, string? entityName = null, string? entityId = null, string? details = null, int? userId = null)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<HotelPOS.Data.Repositories.AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null)
    {
        return Task.FromResult<IEnumerable<HotelPOS.Data.Repositories.AuditLogEntry>>(new List<HotelPOS.Data.Repositories.AuditLogEntry>());
    }

    public Task<(IEnumerable<HotelPOS.Data.Repositories.AuditLogEntry> Logs, int TotalCount)> GetLogsPaginatedAsync(DateTime? startDate = null, DateTime? endDate = null, string? search = null, int page = 1, int pageSize = 25)
    {
        return Task.FromResult<(IEnumerable<HotelPOS.Data.Repositories.AuditLogEntry>, int)>((new List<HotelPOS.Data.Repositories.AuditLogEntry>(), 0));
    }
}

public class DummyAppLogger : IAppLogger
{
    public void Trace(LogCategory category, string message, string? correlationId = null) { }
    public void Debug(LogCategory category, string message, string? correlationId = null) { }
    public void Info(LogCategory category, string message, string? correlationId = null) { }
    public void Warning(LogCategory category, string message, string? correlationId = null) { }
    public void Error(LogCategory category, string message, Exception? exception = null, string? correlationId = null) { }
    public void Fatal(LogCategory category, string message, Exception? exception = null, string? correlationId = null) { }
    public string NewCorrelationId() => Guid.NewGuid().ToString();
}

public class BackupServiceTests
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IAppLogger _logger;
    private readonly string _testDbPath;

    public BackupServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos_test_{Guid.NewGuid()}.db");
        _dbFactory = new DbConnectionFactory(_testDbPath);

        _auditService = new DummyAuditService();
        _logger = new DummyAppLogger();

        var migrationRunner = new MigrationRunner(_dbFactory, _logger);
        migrationRunner.EnsureDatabaseIsReady();
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateValidBackupFile()
    {
        var backupService = new BackupService(_dbFactory, _auditService, _logger);
        var targetBackupPath = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid()}.db");

        try
        {
            var resultPath = await backupService.CreateBackupAsync(targetBackupPath);

            Assert.True(File.Exists(resultPath));
            Assert.True(new FileInfo(resultPath).Length > 0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try { if (File.Exists(targetBackupPath)) File.Delete(targetBackupPath); } catch { }
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task CheckAndOptimizeDatabaseAsync_ShouldReturnIntegrityOk()
    {
        var backupService = new BackupService(_dbFactory, _auditService, _logger);

        try
        {
            var (isOk, message) = await backupService.CheckAndOptimizeDatabaseAsync();

            Assert.True(isOk);
            Assert.Contains("สมบูรณ์", message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
