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
    private readonly ISettingsService? _settingsService;

    public BackupService(DbConnectionFactory connectionFactory, IAuditService auditService, IAppLogger logger, ISettingsService? settingsService = null)
    {
        _connectionFactory = connectionFactory;
        _auditService = auditService;
        _logger = logger;
        _settingsService = settingsService;
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
            string backupDir = "";
            if (_settingsService != null)
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                backupDir = settings.CustomBackupFolderPath ?? "";
            }

            if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
            {
                backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PSoftRestRentManager",
                    "Backups");
            }

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            targetFilePath = Path.Combine(backupDir, $"PSoftRestRent_Backup_{timeStamp}.db");
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

    public async Task<(bool IsOk, string Message)> CheckAndOptimizeDatabaseAsync()
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open();

            // 1. Enable Write-Ahead Logging for maximum concurrency & reliability
            using (var cmdPragma = conn.CreateCommand())
            {
                cmdPragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                cmdPragma.ExecuteNonQuery();
            }

            // 2. Check Database Integrity
            string integrityResult = "ok";
            using (var cmdCheck = conn.CreateCommand())
            {
                cmdCheck.CommandText = "PRAGMA integrity_check;";
                var res = cmdCheck.ExecuteScalar();
                if (res != null) integrityResult = res.ToString() ?? "ok";
            }

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Error(LogCategory.Database, $"พบข้อผิดพลาดในโครงสร้างฐานข้อมูล: {integrityResult}");
                return (false, $"พบข้อผิดพลาดในฐานข้อมูล: {integrityResult}");
            }

            // 3. Compact & Defragment Database via VACUUM
            using (var cmdVacuum = conn.CreateCommand())
            {
                cmdVacuum.CommandText = "VACUUM;";
                cmdVacuum.ExecuteNonQuery();
            }

            await _auditService.LogAsync("บำรุงรักษาฐานข้อมูล (DB Optimize & Integrity Check)", "Database", "PRAGMA WAL / VACUUM", "ตรวจสอบสมบูรณ์แบบ");
            _logger.Info(LogCategory.Database, "ตรวจสอบความสมบูรณ์และ Optimize ฐานข้อมูลเรียบร้อยแล้ว");
            return (true, "ฐานข้อมูลอยู่ในสภาพสมบูรณ์ 100% (Integrity OK, WAL Mode Active, Compacted)");
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ตรวจสอบความสมบูรณ์ฐานข้อมูลล้มเหลว", ex);
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    public async Task<string?> AutoPerformRollingBackupAsync(int maxKeepBackups = 30)
    {
        try
        {
            int retentionLimit = maxKeepBackups;
            string backupDir = "";
            if (_settingsService != null)
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                if (!settings.AutoBackupEnabled) return null;
                backupDir = settings.CustomBackupFolderPath ?? "";
                if (settings.AutoBackupMaxKeepFiles > 0)
                {
                    retentionLimit = settings.AutoBackupMaxKeepFiles;
                }
            }

            if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
            {
                backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PSoftRestRentManager",
                    "Backups");
            }

            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            // Clean up old backup files exceeding retentionLimit
            var existingFiles = new DirectoryInfo(backupDir)
                .GetFiles("PSoftRestRent_Backup_*.db")
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (existingFiles.Count >= retentionLimit)
            {
                foreach (var oldFile in existingFiles.Skip(retentionLimit - 1))
                {
                    try { oldFile.Delete(); } catch { }
                }
            }

            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var autoPath = Path.Combine(backupDir, $"PSoftRestRent_Backup_{timeStamp}.db");
            return await CreateBackupAsync(autoPath);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "Auto-backup ล้มเหลว", ex);
            return null;
        }
    }
}
