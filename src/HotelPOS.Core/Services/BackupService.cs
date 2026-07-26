using System;
using System.IO;
using System.Threading.Tasks;
using HotelPOS.Common;
using HotelPOS.Data;
using HotelPOS.Logging;
using Microsoft.Data.Sqlite;

namespace HotelPOS.Core.Services;

public class BackupService : IBackupService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAuditService _auditService;
    private readonly IAppLogger _logger;

    public BackupService(DbConnectionFactory connectionFactory, IAuditService auditService, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _auditService = auditService;
        _logger = logger;
    }

    public string GetDatabasePath() => _connectionFactory.DatabaseFilePath;

    public async Task<string> CreateBackupAsync(string? targetFilePath = null)
    {
        var dbPath = _connectionFactory.DatabaseFilePath;
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("ไม่พบไฟล์ฐานข้อมูลเพื่อทำการสำรอง", dbPath);
        }

        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HotelPOS",
                "Backups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            targetFilePath = Path.Combine(backupDir, $"HotelPOS_Backup_{timeStamp}.db");
        }

        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // ใช้วิธี SQLite Online Backup API เพื่อให้สำรองข้อมูลได้แม้ขณะมี Connection เปิดอยู่
        using (var sourceConn = _connectionFactory.CreateConnection())
        using (var destConn = new SqliteConnection($"Data Source={targetFilePath}"))
        {
            sourceConn.Open();
            destConn.Open();
            sourceConn.BackupDatabase(destConn);
        }

        await _auditService.LogAsync("สำรองข้อมูล (Backup DB)", "Database", targetFilePath, $"ขนาดไฟล์: {new FileInfo(targetFilePath).Length / 1024} KB");
        _logger.Info(LogCategory.Database, $"สำรองฐานข้อมูลสำเร็จที่ {targetFilePath}");

        return targetFilePath;
    }

    public async Task RestoreBackupAsync(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("ไม่พบไฟล์สำรองข้อมูลที่เลือก", sourceFilePath);
        }

        var activeDbPath = _connectionFactory.DatabaseFilePath;

        // เคลียร์ connection pool ล็อกไฟล์ เพื่อเตรียมเขียนทับ
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // สำรองไฟล์ปัจจุบันไว้ก่อนเผื่อมีปัญหา
        var tempBackup = activeDbPath + ".tmp_before_restore";
        if (File.Exists(activeDbPath))
        {
            File.Copy(activeDbPath, tempBackup, overwrite: true);
        }

        try
        {
            File.Copy(sourceFilePath, activeDbPath, overwrite: true);
            if (File.Exists(tempBackup))
            {
                File.Delete(tempBackup);
            }

            await _auditService.LogAsync("คืนค่าฐานข้อมูล (Restore DB)", "Database", sourceFilePath, "คืนค่าสมบูรณ์");
            _logger.Info(LogCategory.Database, $"คืนค่าฐานข้อมูลสำเร็จจากไฟล์ {sourceFilePath}");
        }
        catch (Exception ex)
        {
            // ย้อนคืนไฟล์เดิมหากเกิดข้อผิดพลาด
            if (File.Exists(tempBackup))
            {
                File.Copy(tempBackup, activeDbPath, overwrite: true);
                File.Delete(tempBackup);
            }
            _logger.Error(LogCategory.Database, "คืนค่าฐานข้อมูลล้มเหลว ย้อนคืนไฟล์เดิมเรียบร้อย", ex);
            throw new InvalidOperationException($"เกิดข้อผิดพลาดขณะคืนค่าฐานข้อมูล: {ex.Message}", ex);
        }
    }
}
