using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly IAuditService _auditService;
    private readonly IBackupService _backupService;

    public BackupServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bak-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bak-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();
        var auditRepo = new AuditRepository(_connectionFactory, _logger);
        _auditService = new AuditService(auditRepo, _logger);

        _backupService = new BackupService(_connectionFactory, _auditService, _logger);
    }

    [Fact]
    public async Task CreateBackup_สร้างไฟล์สำรองฐานข้อมูล_ไฟล์ต้องถูกสร้างสมบูรณ์()
    {
        string backupTarget = Path.Combine(Path.GetTempPath(), $"hotelpos_backup_{Guid.NewGuid():N}.db");

        try
        {
            string backupResultPath = await _backupService.CreateBackupAsync(backupTarget);

            Assert.True(File.Exists(backupResultPath));
            var fileInfo = new FileInfo(backupResultPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(backupTarget)) File.Delete(backupTarget);
        }
    }

    [Fact]
    public async Task RestoreBackup_คืนค่าฐานข้อมูลจากไฟล์สำรอง_สำเร็จสมบูรณ์()
    {
        string backupTarget = Path.Combine(Path.GetTempPath(), $"hotelpos_backup_restore_{Guid.NewGuid():N}.db");

        try
        {
            // 1. สร้างไฟล์ backup แรกเริ่ม
            await _backupService.CreateBackupAsync(backupTarget);

            // 2. คืนค่าไฟล์ backup กลับเข้ามา
            await _backupService.RestoreBackupAsync(backupTarget);

            Assert.True(File.Exists(_connectionFactory.DatabaseFilePath));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(backupTarget)) File.Delete(backupTarget);
        }
    }

    [Fact]
    public async Task RestoreBackup_ไฟล์ไม่ดำรงอยู่_ต้อง_Throw_FileNotFoundException()
    {
        string dummyPath = Path.Combine(Path.GetTempPath(), "non_existent_backup_file.db");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _backupService.RestoreBackupAsync(dummyPath));
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
