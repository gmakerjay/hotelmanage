using System;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace HotelPOS.Licensing;

public static class HardwareIdGenerator
{
    // Fallback ที่ไม่ซ้ำกัน ใช้เมื่อ WMI มีปัญหา
    private static string UniqueFallback(string source)
    {
        string data = $"{Environment.MachineName}|{Environment.UserName}|{source}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)))
            .Substring(0, 16).ToUpperInvariant();
    }

    public static string Generate()
    {
        string cpuId = GetCpuId().Trim();
        string diskSerial = GetDiskSerial().Trim();
        string macAddress = GetMacAddress().Trim();
        string boardSerial = GetBoardSerial().Trim();
        string biosSerial = GetBiosSerial().Trim();

        // รวมค่าทั้งหมดเพื่อระบุเอกลักษณ์เครื่อง
        string combined = $"CPU:{cpuId}|DISK:{diskSerial}|MAC:{macAddress}|BOARD:{boardSerial}|BIOS:{biosSerial}";

        // ทำ SHA256 Hash
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var val = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        catch
        {
            // Fallback กรณี WMI มีปัญหา
        }
        return UniqueFallback("CPU");
    }

    private static string GetDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var val = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        catch
        {
            // Fallback กรณี WMI มีปัญหา
        }
        return UniqueFallback("DISK");
    }

    private static string GetMacAddress()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    string mac = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(mac)) return mac;
                }
            }
        }
        catch
        {
            // Fallback
        }
        return UniqueFallback("MAC");
    }

    private static string GetBoardSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var val = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        catch
        {
            // Fallback
        }
        return UniqueFallback("BOARD");
    }

    private static string GetBiosSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var val = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        catch
        {
            // Fallback
        }
        return UniqueFallback("BIOS");
    }
}
