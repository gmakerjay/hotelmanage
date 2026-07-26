using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;

namespace HotelPOS.Licensing;

public static class TrialManager
{
    public static string RegistrySubKey = @"Software\HotelPOS";
    public static string RegistryValueName = "TData";
    public static string HiddenFileName = ".tdata";

    private const int TrialDaysLimit = 30;

    public static string GetDefaultDbPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HotelPOS", "hotelpos.db");

    public static string GetDefaultHiddenFileFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HotelPOS");

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
    /// </summary>
    public static (bool IsActive, int DaysRemaining) GetTrialStatus(string? dbPath = null, string? hiddenFileFolder = null)
    {
        DateTime startDate = GetOrInitializeTrialStartDate(dbPath, hiddenFileFolder);
        DateTime today = DateTime.Now.Date;

        int daysUsed = (today - startDate).Days;

        // หากตรวจพบว่าเวลาของเครื่องถูกย้อนกลับ (วันที่ปัจจุบันน้อยกว่าวันที่เริ่ม) ให้ถือว่าหมดอายุ (Tampering protection)
        if (daysUsed < 0)
        {
            return (false, 0);
        }

        int daysRemaining = Math.Max(0, TrialDaysLimit - daysUsed);
        bool isActive = daysRemaining > 0;

        return (isActive, daysRemaining);
    }

    #region Helpers for Obfuscation
    private static string Obfuscate(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");
        char[] arr = dateStr.ToCharArray();
        Array.Reverse(arr);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(arr)));
    }

    private static DateTime? Deobfuscate(string obfuscated)
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
}
