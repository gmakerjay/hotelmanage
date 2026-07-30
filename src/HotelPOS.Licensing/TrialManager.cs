using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;

namespace HotelPOS.Licensing;

public static class TrialManager
{
    public static string RegistrySubKey = @"Software\PSoftRestRentManager";
    public static string RegistryValueName = "TData";
    public static string HiddenFileName = ".tdata";

    private const int TrialDaysLimit = 30;

    // Settings keys สำหรับ Dongle pause/resume (deprecated — เก็บไว้ compat)
    private const string KeyDaysConsumed = "trial_days_consumed";
    private const string KeyDongleLastSeen = "dongle_last_seen_at";
    private const string KeyTrialLastActive = "trial_last_active_date";

    public static string GetDefaultDbPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSoftRestRentManager", "restrent.db");

    public static string GetDefaultHiddenFileFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSoftRestRentManager");

    /// <summary>
    /// ตรวจสอบและดึงข้อมูลวันที่เริ่มใช้งาน Trial (ซิงค์ข้อมูลจาก Registry, ไฟล์ซ่อน และฐานข้อมูล SQLite)
    /// </summary>
    public static DateTime GetOrInitializeTrialStartDate(string? dbPath = null, string? hiddenFileFolder = null)
    {
        dbPath ??= GetDefaultDbPath();
        hiddenFileFolder ??= GetDefaultHiddenFileFolder();

        DateTime? registryDate = ReadFromRegistry();
        DateTime? hiddenFileDate = ReadFromHiddenFile(hiddenFileFolder);
        DateTime? dbDate = ReadFromDatabase(dbPath);

        DateTime resolvedDate;

        if (!registryDate.HasValue && !hiddenFileDate.HasValue && !dbDate.HasValue)
        {
            // รันครั้งแรกจริงๆ: กำหนดค่าเป็นวันที่ปัจจุบัน
            resolvedDate = DateTime.Now.Date;
            WriteToRegistry(resolvedDate);
            WriteToHiddenFile(hiddenFileFolder, resolvedDate);
            WriteToDatabase(dbPath, resolvedDate);
        }
        else
        {
            // หากมีค่าบางตัวหายไป หรือไม่ตรงกัน ให้เลือกวันที่เก่าที่สุด (ป้องกันการแก้ไขเพื่อต่ออายุ)
            var earliestDate = DateTime.MaxValue;

            if (registryDate.HasValue && registryDate.Value < earliestDate) earliestDate = registryDate.Value;
            if (hiddenFileDate.HasValue && hiddenFileDate.Value < earliestDate) earliestDate = hiddenFileDate.Value;
            if (dbDate.HasValue && dbDate.Value < earliestDate) earliestDate = dbDate.Value;

            resolvedDate = earliestDate;

            // ซิงค์ข้อมูลทั้งหมดให้ตรงกับค่าที่เลือก (ซ่อมแซมส่วนที่หายหรือถูกแก้)
            if (registryDate != resolvedDate) WriteToRegistry(resolvedDate);
            if (hiddenFileDate != resolvedDate) WriteToHiddenFile(hiddenFileFolder, resolvedDate);
            if (dbDate != resolvedDate) WriteToDatabase(dbPath, resolvedDate);
        }

        return resolvedDate;
    }

    /// <summary>
    /// ตรวจสอบสถานะการใช้งานระบบทดลอง และวันใช้งานที่เหลือ
    /// คำนวณตามวันปฏิทิน (Calendar Days) นับจากวันที่เริ่มใช้งานครั้งแรก
    /// โดยนับต่อเนื่องทุกวันรวมวันที่ไม่ได้เปิดโปรแกรม 
    /// และนับรวมวันที่เสียบ USB Dongle ด้วย (ไม่มีการ Pause)
    /// </summary>
    public static (bool IsActive, int DaysRemaining) GetTrialStatus(string? dbPath = null, string? hiddenFileFolder = null)
    {
        dbPath ??= GetDefaultDbPath();
        DateTime startDate = GetOrInitializeTrialStartDate(dbPath, hiddenFileFolder);
        DateTime today = DateTime.Now.Date;

        // ตรวจสอบ clock rollback (การย้อนเวลาเครื่อง)
        int calendarDays = (today - startDate).Days;
        if (calendarDays < 0)
        {
            return (false, 0);
        }

        // คำนวณวันคงเหลือตาม Calendar Days (30 - วันที่ผ่านไปตั้งแต่เริ่ม)
        // ไม่ต้องสนใจ Dongle Pause/Resume — Trial นับตามปฏิทินล้วน
        int daysRemaining = Math.Max(0, TrialDaysLimit - calendarDays);
        bool isActive = daysRemaining > 0;

        return (isActive, daysRemaining);
    }

    /// <summary>
    /// บันทึกว่าพบ USB Dongle เสียบอยู่ → หยุดนับ Trial ทันที
    /// เรียก method นี้ทุกครั้งที่ CheckLicense พบ Dongle
    /// </summary>
    public static void RecordDonglePresent(string? dbPath = null)
    {
        dbPath ??= GetDefaultDbPath();
        try
        {
            EnsureSettingsTable(dbPath);
            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO settings (key, value, description, updated_at) 
                VALUES (@key, @value, 'วันที่พบ Dongle ครั้งล่าสุด (หยุดนับ Trial)', datetime('now', 'localtime'))";
            cmd.Parameters.AddWithValue("@key", KeyDongleLastSeen);
            cmd.Parameters.AddWithValue("@value", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();

            // บันทึก timestamp ล่าสุดที่พบ Dongle (สำหรับ Audit/Logging เท่านั้น)
            // หมายเหตุ: Trial ไม่ได้หยุดนับเมื่อเสียบ Dongle แล้ว (นับตามปฏิทินล้วน)
        }
        catch { }
    }

    /// <summary>
    /// บันทึกว่า USB Dongle ถูกถอดออก → เริ่มนับ Trial ต่อจากจุดที่เหลือ
    /// </summary>
    public static void RecordDongleRemoved(string? dbPath = null)
    {
        // ไม่ต้องทำอะไรพิเศษ — GetTrialStatus จะเริ่มนับอัตโนมัติเมื่อไม่พบ Dongle
        // method นี้เป็น placeholder สำหรับ logging/notification ในอนาคต
    }

    #region Helpers for Obfuscation (AES-256 + HMAC เพื่อความปลอดภัยที่แข็งแกร่งกว่า Base64 Reverse แบบเดิม)

    // Static salt ผสมกับ machine info เพื่อสร้าง key ที่ผูกกับเครื่อง
    private static readonly byte[] _staticSalt = Encoding.UTF8.GetBytes("PSoft-RestRent-TrialSalt-2026!!");

    private static byte[] DeriveKey()
    {
        string machineInfo = $"{Environment.MachineName}|{Environment.UserName}|PSoft-T";
        return SHA256.HashData(Encoding.UTF8.GetBytes(machineInfo + Convert.ToBase64String(_staticSalt)));
    }

    private static string Obfuscate(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");
        byte[] key = DeriveKey();
        byte[] iv = new byte[16];
        Array.Copy(key, 0, iv, 0, 16); // ใช้ครึ่งแรกของ key เป็น IV (คงที่ต่อเครื่อง)

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] encrypted;
        using (var encryptor = aes.CreateEncryptor())
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(dateStr);
            encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        // เพิ่ม HMAC เพื่อตรวจสอบความสมบูรณ์ (integrity)
        byte[] hmac = HMACSHA256.HashData(key, encrypted);
        byte[] result = new byte[encrypted.Length + hmac.Length];
        Array.Copy(encrypted, 0, result, 0, encrypted.Length);
        Array.Copy(hmac, 0, result, encrypted.Length, hmac.Length);

        return Convert.ToBase64String(result);
    }

    private static DateTime? Deobfuscate(string obfuscated)
    {
        try
        {
            byte[] raw = Convert.FromBase64String(obfuscated);
            
            // HMAC อยู่ 32 bytes สุดท้าย
            if (raw.Length <= 32) 
            {
                // อาจเป็นรูปแบบเก่า (Base64 Reverse) → ลอง fallback
                return DeobfuscateLegacy(obfuscated);
            }

            byte[] key = DeriveKey();
            byte[] iv = new byte[16];
            Array.Copy(key, 0, iv, 0, 16);

            int encLen = raw.Length - 32;
            byte[] encrypted = new byte[encLen];
            byte[] storedHmac = new byte[32];
            Array.Copy(raw, 0, encrypted, 0, encLen);
            Array.Copy(raw, encLen, storedHmac, 0, 32);

            // ตรวจสอบ HMAC ก่อน decrypt
            byte[] computedHmac = HMACSHA256.HashData(key, encrypted);
            if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
            {
                return null; // ข้อมูลถูกแก้ไข (tampered)
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            string dateStr = Encoding.UTF8.GetString(decrypted);

            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        catch
        {
            // ลอง fallback แบบเก่า (backward compatibility)
            return DeobfuscateLegacy(obfuscated);
        }
        return null;
    }

    /// <summary>
    /// อ่านรูปแบบเก่า (Base64 Reverse) สำหรับความเข้ากันได้ย้อนหลังกับข้อมูลที่บันทึกก่อนอัปเกรด
    /// </summary>
    private static DateTime? DeobfuscateLegacy(string obfuscated)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(obfuscated);
            string reversed = Encoding.UTF8.GetString(bytes);
            char[] arr = reversed.ToCharArray();
            Array.Reverse(arr);
            string dateStr = new string(arr);
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        catch
        {
            // ละเว้น
        }
        return null;
    }
    #endregion

    #region Registry Operations
    private static DateTime? ReadFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
            var val = key?.GetValue(RegistryValueName)?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                return Deobfuscate(val);
            }
        }
        catch
        {
            // ละเว้น
        }
        return null;
    }

    private static void WriteToRegistry(DateTime date)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
            key.SetValue(RegistryValueName, Obfuscate(date));
        }
        catch
        {
            // ละเว้น
        }
    }
    #endregion

    #region Hidden File Operations
    private static DateTime? ReadFromHiddenFile(string folderPath)
    {
        try
        {
            string filePath = Path.Combine(folderPath, HiddenFileName);
            if (File.Exists(filePath))
            {
                string obfuscated = File.ReadAllText(filePath).Trim();
                return Deobfuscate(obfuscated);
            }
        }
        catch
        {
            // ละเว้น
        }
        return null;
    }

    private static void WriteToHiddenFile(string folderPath, DateTime date)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, HiddenFileName);
            File.WriteAllText(filePath, Obfuscate(date));
            
            // ตั้งค่าไฟล์เป็น Hidden
            var fileInfo = new FileInfo(filePath);
            if ((fileInfo.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
            {
                fileInfo.Attributes |= FileAttributes.Hidden;
            }
        }
        catch
        {
            // ละเว้น
        }
    }
    #endregion

    #region Database Operations
    private static DateTime? ReadFromDatabase(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath)) return null;

            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = 'trial_start_date'";
            var val = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                return Deobfuscate(val);
            }
        }
        catch
        {
            // ละเว้น
        }
        return null;
    }

    private static void WriteToDatabase(string dbPath, DateTime date)
    {
        try
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            // ตรวจสอบว่ามีตาราง settings หรือไม่
            using (var cmdTable = conn.CreateCommand())
            {
                cmdTable.CommandText = "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT, description TEXT, updated_at TEXT)";
                cmdTable.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value, description, updated_at) VALUES ('trial_start_date', @value, 'วันที่เริ่มใช้งาน Trial 30 วัน', datetime('now', 'localtime'))";
                cmd.Parameters.AddWithValue("@value", Obfuscate(date));
                cmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // ละเว้น
        }
    }
    #endregion

    #region Dongle Pause/Resume Helpers
    private static int? ReadDaysConsumed(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath)) return null;
            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", KeyDaysConsumed);
            var val = cmd.ExecuteScalar()?.ToString();
            return int.TryParse(val, out int days) ? days : null;
        }
        catch { return null; }
    }

    private static void WriteDaysConsumed(string dbPath, int days)
    {
        try
        {
            EnsureSettingsTable(dbPath);
            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO settings (key, value, description, updated_at) 
                VALUES (@key, @value, 'จำนวนวันที่ใช้ Trial จริง (ไม่นับวันที่เสียบ Dongle)', datetime('now', 'localtime'))";
            cmd.Parameters.AddWithValue("@key", KeyDaysConsumed);
            cmd.Parameters.AddWithValue("@value", days.ToString());
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static DateTime? ReadTrialLastActiveDate(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath)) return null;
            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", KeyTrialLastActive);
            var val = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(val) && DateTime.TryParse(val, out var dt))
                return dt;
        }
        catch { }
        return null;
    }

    private static void WriteTrialLastActiveDate(string dbPath, DateTime date)
    {
        try
        {
            EnsureSettingsTable(dbPath);
            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO settings (key, value, description, updated_at) 
                VALUES (@key, @value, 'วันที่ Trial ทำงานล่าสุด (ไม่มี Dongle)', datetime('now', 'localtime'))";
            cmd.Parameters.AddWithValue("@key", KeyTrialLastActive);
            cmd.Parameters.AddWithValue("@value", date.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static void EnsureSettingsTable(string dbPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var connStr = $"Data Source={dbPath};";
            using var conn = new SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT, description TEXT, updated_at TEXT)";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }
    #endregion
}
