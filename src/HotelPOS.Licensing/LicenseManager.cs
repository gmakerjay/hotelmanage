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
        string currentAppSerial = AppWatermarkManager.GetCurrentAppSerial();
        DateTime? lastVerifiedAt = GetLastVerifiedAtFromDb(dbPath);

        // 1. ตรวจสอบ USB Hardware Dongle (หากมี Flash Drive เสียบอยู่)
        var (dongleLicense, usbInfo, _) = UsbDongleManager.ScanForDongleKey();
        if (dongleLicense != null && usbInfo != null)
        {
            // Auto-install: ก๊อปปี้ app.watermark จาก USB มาวางข้าง .exe อัตโนมัติ (ถ้ายังไม่มี หรือ AppSerial ไม่ตรง)
            AutoInstallWatermarkFromUsb(usbInfo.DriveLetter);

            // อ่าน AppSerial ใหม่อีกครั้งหลัง auto-install
            currentAppSerial = AppWatermarkManager.GetCurrentAppSerial();

            LicenseStatus dongleStatus = LicenseValidator.ValidateDongle(
                dongleLicense, usbInfo.UsbHardwareId, currentAppSerial, lastVerifiedAt, licenseDirectory);

            if (dongleStatus == LicenseStatus.Active)
            {
                int dongleDaysRemaining = dongleLicense.ExpireDate.HasValue
                    ? Math.Max(0, (dongleLicense.ExpireDate.Value.Date - DateTime.Now.Date).Days)
                    : 99999;

                UpdateDbLicenseInfo(dbPath, dongleLicense.CustomerName, dongleLicense.HardwareId, dongleLicense.LicenseType,
                    dongleLicense.IssueDate, dongleLicense.ExpireDate, dongleLicense.MaxRooms,
                    JsonSerializer.Serialize(dongleLicense.Features), dongleStatus);

                return (dongleStatus, dongleLicense, dongleDaysRemaining);
            }
            else
            {
                // หากพบ USB Dongle แต่หมดอายุหรือสิทธิ์ไม่ถูกต้อง
                UpdateDbLicenseInfo(dbPath, dongleLicense.CustomerName, dongleLicense.HardwareId, dongleLicense.LicenseType,
                    dongleLicense.IssueDate, dongleLicense.ExpireDate, dongleLicense.MaxRooms,
                    JsonSerializer.Serialize(dongleLicense.Features), dongleStatus);

                return (dongleStatus, dongleLicense, 0);
            }
        }

        // 2. กรณีไม่มี USB Dongle เสียบอยู่ -> สลับเข้าโหมดทดลองใช้ 30 วัน (Trial Anti-Reset)
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

    #region Auto-Install Watermark from USB
    /// <summary>
    /// ก๊อปปี้ไฟล์ app.watermark จาก USB Flash Drive มาวางข้างโปรแกรม (.exe) อัตโนมัติ
    /// ทำให้ลูกค้าเพียงเสียบ USB Flash Drive ครั้งแรก โปรแกรมจัดการเองทั้งหมด
    /// </summary>
    private static void AutoInstallWatermarkFromUsb(string usbDriveLetter)
    {
        try
        {
            string usbWatermarkPath = Path.Combine(usbDriveLetter + "\\", AppWatermarkManager.WatermarkFileName);
            if (!File.Exists(usbWatermarkPath)) return;

            string localWatermarkPath = AppWatermarkManager.GetDefaultWatermarkFilePath();

            // ถ้ายังไม่มีไฟล์ app.watermark ข้าง .exe → ก๊อปปี้จาก USB มาทันที
            if (!File.Exists(localWatermarkPath))
            {
                File.Copy(usbWatermarkPath, localWatermarkPath, overwrite: false);
                return;
            }

            // ถ้ามีไฟล์อยู่แล้ว → เปรียบเทียบ AppSerial ถ้าไม่ตรงกับใน USB ให้อัปเดตทับ
            string currentLocal = AppWatermarkManager.GetCurrentAppSerial();
            var usbWatermark = AppWatermarkFile.FromJson(File.ReadAllText(usbWatermarkPath));
            if (usbWatermark != null && usbWatermark.AppSerial != currentLocal)
            {
                File.Copy(usbWatermarkPath, localWatermarkPath, overwrite: true);
            }
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

    private static DateTime? GetLastVerifiedAtFromDb(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath)) return null;

            using var conn = new SqliteConnection($"Data Source={dbPath};");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT last_verified_at FROM license_info ORDER BY id DESC LIMIT 1";
            var val = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(val) && DateTime.TryParse(val, out var dt))
            {
                return dt;
            }
        }
        catch { }
        return null;
    }
    #endregion
}
