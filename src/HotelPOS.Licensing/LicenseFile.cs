using System;
using System.Collections.Generic;
using System.Text.Json;
using HotelPOS.Common;

namespace HotelPOS.Licensing;

public class LicenseFile
{
    public string CustomerName { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public LicenseType LicenseType { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ExpireDate { get; set; }
    public int? MaxRooms { get; set; }
    public List<string> Features { get; set; } = new();
    
    // Digital Signature ในรูปแบบ Base64
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// แปลงข้อมูล License เป็นข้อความต่อกันสำหรับเซ็นดิจิทัล (แบบ Deterministic)
    /// </summary>
    public string GetSignableData()
    {
        var expireStr = ExpireDate?.ToString("yyyy-MM-dd") ?? "NULL";
        var maxRoomsStr = MaxRooms?.ToString() ?? "NULL";
        var featuresCopy = new List<string>(Features);
        featuresCopy.Sort(); // เรียงลำดับเพื่อให้คงที่แน่นอน
        var featuresStr = string.Join(",", featuresCopy);

        return $"{CustomerName}|{HardwareId}|{(int)LicenseType}|{IssueDate:yyyy-MM-dd}|{expireStr}|{maxRoomsStr}|{featuresStr}";
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static LicenseFile? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LicenseFile>(json);
        }
        catch
        {
            return null;
        }
    }
}
