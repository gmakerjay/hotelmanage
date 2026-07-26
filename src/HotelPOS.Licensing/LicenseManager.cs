using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using HotelPOS.Common;

namespace HotelPOS.Licensing;

public static class LicenseManager
{
    public static string LicenseRegistryValueName = "LData";
    public static string LicenseFileName = "license.dat";

    public static string GetDefaultLicenseDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HotelPOS");

    public static string GetDefaultLicenseFilePath() =>
        Path.Combine(GetDefaultLicenseDirectory(), LicenseFileName);

    /// <summary>
    /// ตรวจสอบลิขสิทธิ์ทั้งหมดของโปรแกรม (พิจารณาทั้งไฟล์ License และ Trial 30 วัน)
    /// </summary>
    public static (LicenseStatus Status, LicenseFile? License, int DaysRemaining) CheckLicense(
        string? dbPath = null, 
        string? licenseDirectory = null)
    {
        dbPath ??= TrialManager.GetDefaultDbPath();
        licenseDirectory ??= GetDefaultLicenseDirectory();

        string licenseFilePath = Path.Combine(licenseDirectory, LicenseFileName);
        string currentHardwareId = HardwareIdGenerator.Generate();

        // 1. อ่านข้อมูล License (ลองอ่านจากไฟล์ และสำรองใน Registry)
        string? licenseJson = ReadLicenseContent(licenseFilePath);
        LicenseFile? license = null;

        if (!string.IsNullOrEmpty(licenseJson))
        {
            license = LicenseFile.FromJson(licenseJson);
        }

        // 2. หากพบไฟล์ License และต้องการซิงค์ลง Registry หรือกลับกัน
        SyncLicenseStorage(licenseFilePath, licenseJson);

        // 3. ตรวจสอบ License
        if (license != null)
        {
            LicenseStatus status = LicenseValidator.Validate(license, currentHardwareId);

            if (status == LicenseStatus.Active)
            {
                int daysRemaining = 99999; // ถาวร
                if (license.ExpireDate.HasValue)
                {
                    daysRemaining = Math.Max(0, (license.ExpireDate.Value.Date - DateTime.Now.Date).Days);
                }

                UpdateDbLicenseInfo(dbPath, license.CustomerName, license.HardwareId, license.LicenseType, 
                    license.IssueDate, license.ExpireDate, license.MaxRooms, 
                    JsonSerializer.Serialize(license.Features), status);

                return (status, license, daysRemaining);
            }
            else
            {
                // หากพบ License แต่หมดอายุหรือสิทธิ์ไม่ถูกต้อง
                UpdateDbLicenseInfo(dbPath, license.CustomerName, license.HardwareId, license.LicenseType, 
                    license.IssueDate, license.ExpireDate, license.MaxRooms, 
                    JsonSerializer.Serialize(license.Features), status);

                return (status, license, 0);
            }
        }

        // 4. กรณีไม่มี License เลย -> สลับเข้าโหมดทดลองใช้ 30 วัน (Trial)
        var trialStatus = TrialManager.GetTrialStatus(dbPath, licenseDirectory);
        LicenseStatus trialLicenseStatus = trialStatus.IsActive ? LicenseStatus.Active : LicenseStatus.Expired;

        // ดึงวันที่เริ่มใช้งานมาคำนวณหาวันสิ้นสุด
        DateTime trialStartDate = TrialManager.GetOrInitializeTrialStartDate(dbPath, licenseDirectory);
        DateTime trialEndDate = trialStartDate.AddDays(30);

        UpdateDbLicenseInfo(dbPath, "Trial Customer", currentHardwareId, LicenseType.Trial, 
            trialStartDate, trialEndDate, 100, "[\"BOOKING\",\"POS\",\"REPORT\"]", trialLicenseStatus);

        var trialLicense = new LicenseFile
        {
            CustomerName = "ทดลองใช้งาน 30 วัน",
            HardwareId = currentHardwareId,
            LicenseType = LicenseType.Trial,
            IssueDate = trialStartDate,
            ExpireDate = trialEndDate,
            MaxRooms = 100,
            Features = new List<string> { "BOOKING", "POS", "REPORT" }
        };

        return (trialLicenseStatus, trialLicense, trialStatus.DaysRemaining);
    }

    /// <summary>
    /// ทำการลงทะเบียนใช้คีย์ลิขสิทธิ์ใหม่ (Activate)
    /// </summary>
    public static (bool Success, string Message) Activate(
        string licenseContent, 
        string? dbPath = null, 
        string? licenseDirectory = null)
    {
        dbPath ??= TrialManager.GetDefaultDbPath();
        licenseDirectory ??= GetDefaultLicenseDirectory();

        try
        {
            // 1. ตรวจสอบเบื้องต้นว่าเป็นรูปแบบ JSON ที่ถูกต้อง
            var license = LicenseFile.FromJson(licenseContent);
            if (license == null)
            {
                return (false, "รูปแบบไฟล์ License หรือข้อความไม่ถูกต้อง (ไม่สามารถแปลงข้อมูลได้)");
            }

            // 2. ตรวจสอบลายเซ็นและความสอดคล้องกับเครื่อง
            string currentHardwareId = HardwareIdGenerator.Generate();
            LicenseStatus status = LicenseValidator.Validate(license, currentHardwareId);

            if (status == LicenseStatus.Invalid)
            {
                return (false, "คีย์ลิขสิทธิ์ไม่ถูกต้อง หรือลายเซ็นไม่ตรงกับชุดข้อมูล หรือไม่ได้ผูกไว้กับเครื่องนี้");
            }
            if (status == LicenseStatus.Expired)
            {
                return (false, "คีย์ลิขสิทธิ์นี้หมดอายุการใช้งานแล้ว");
            }

            // 3. บันทึกลงไดเรกทอรี
            if (!Directory.Exists(licenseDirectory))
            {
                Directory.CreateDirectory(licenseDirectory);
            }
            string licenseFilePath = Path.Combine(licenseDirectory, LicenseFileName);
            File.WriteAllText(licenseFilePath, licenseContent);

            // 4. บันทึกลง Registry
            WriteToRegistry(licenseContent);

            // 5. บันทึกลงฐานข้อมูล SQLite
            UpdateDbLicenseInfo(dbPath, license.CustomerName, license.HardwareId, license.LicenseType, 
                license.IssueDate, license.ExpireDate, license.MaxRooms, 
                JsonSerializer.Serialize(license.Features), LicenseStatus.Active);

            return (true, "เปิดใช้งานโปรแกรมสำเร็จ (Activated Successfully)");
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาดในการเปิดใช้งาน: {ex.Message}");
        }
    }

    #region Helper Storage Sync
    private static string? ReadLicenseContent(string filePath)
    {
        // 1. ลองอ่านจากไฟล์ก่อน
        if (File.Exists(filePath))
        {
            try
            {
                return File.ReadAllText(filePath).Trim();
            }
            catch { }
        }

        // 2. ลองอ่านจาก Registry สำรอง
        return ReadFromRegistry();
    }

    private static void SyncLicenseStorage(string filePath, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;

        try
        {
            // ซิงค์ลงไฟล์ถ้าไฟล์ไม่มี
            if (!File.Exists(filePath))
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, content);
            }

            // ซิงค์ลง Registry ถ้าใน Registry ไม่มี
            string? regContent = ReadFromRegistry();
            if (string.IsNullOrEmpty(regContent))
            {
                WriteToRegistry(content);
            }
        }
        catch { }
    }

    private static string? ReadFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TrialManager.RegistrySubKey);
            return key?.GetValue(LicenseRegistryValueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void WriteToRegistry(string content)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(TrialManager.RegistrySubKey);
            key.SetValue(LicenseRegistryValueName, content);
        }
        catch { }
    }
    #endregion

    #region DB Sync Helper
    private static void UpdateDbLicenseInfo(
        string dbPath, 
        string customerName, 
        string hardwareId, 
        LicenseType type, 
        DateTime issueDate, 
        DateTime? expireDate, 
        int? maxRooms, 
        string featuresJson, 
        LicenseStatus status)
    {
        try
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var conn = new SqliteConnection($"Data Source={dbPath};");
            conn.Open();

            using (var cmdTable = conn.CreateCommand())
            {
                cmdTable.CommandText = @"
                    CREATE TABLE IF NOT EXISTS license_info (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        customer_name TEXT NOT NULL,
                        hardware_id TEXT NOT NULL,
                        license_type INTEGER NOT NULL,
                        issue_date TEXT NOT NULL,
                        expire_date TEXT,
                        max_rooms INTEGER,
                        features_json TEXT NOT NULL DEFAULT '[]',
                        status INTEGER NOT NULL DEFAULT 4,
                        last_verified_at TEXT
                    );";
                cmdTable.ExecuteNonQuery();
            }

            using (var cmdDel = conn.CreateCommand())
            {
                cmdDel.CommandText = "DELETE FROM license_info";
                cmdDel.ExecuteNonQuery();
            }

            using (var cmdIns = conn.CreateCommand())
            {
                cmdIns.CommandText = @"
                    INSERT INTO license_info (customer_name, hardware_id, license_type, issue_date, expire_date, max_rooms, features_json, status, last_verified_at)
                    VALUES (@customerName, @hardwareId, @licenseType, @issueDate, @expireDate, @maxRooms, @featuresJson, @status, datetime('now', 'localtime'))";
                
                cmdIns.Parameters.AddWithValue("@customerName", customerName);
                cmdIns.Parameters.AddWithValue("@hardwareId", hardwareId);
                cmdIns.Parameters.AddWithValue("@licenseType", (int)type);
                cmdIns.Parameters.AddWithValue("@issueDate", issueDate.ToString("yyyy-MM-dd"));
                cmdIns.Parameters.AddWithValue("@expireDate", expireDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
                cmdIns.Parameters.AddWithValue("@maxRooms", maxRooms.HasValue ? maxRooms.Value : (object)DBNull.Value);
                cmdIns.Parameters.AddWithValue("@featuresJson", featuresJson);
                cmdIns.Parameters.AddWithValue("@status", (int)status);
                cmdIns.ExecuteNonQuery();
            }
        }
        catch
        {
            // ละเว้น
        }
    }
    #endregion
}
