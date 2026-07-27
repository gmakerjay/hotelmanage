using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelPOS.Licensing;

public class AppWatermarkFile
{
    [JsonPropertyName("app_serial")]
    public string AppSerial { get; set; } = "DEFAULT-APP-SERIAL";

    [JsonPropertyName("issued_to")]
    public string IssuedTo { get; set; } = "General Customer";

    [JsonPropertyName("issued_date")]
    public DateTime IssuedDate { get; set; } = DateTime.Today;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    public string GetSignableData()
    {
        return $"APP:{AppSerial}|ISSUED_TO:{IssuedTo}|DATE:{IssuedDate:yyyy-MM-dd}";
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static AppWatermarkFile? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AppWatermarkFile>(json);
        }
        catch
        {
            return null;
        }
    }
}

public static class AppWatermarkManager
{
    public const string WatermarkFileName = "app.watermark";

    public static string GetDefaultWatermarkFilePath(string? baseDirectory = null)
    {
        baseDirectory ??= AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDirectory, WatermarkFileName);
    }

    /// <summary>
    /// ดึงค่า AppSerial ของโปรแกรม (.exe) ปัจจุบัน หากมีลายน้ำที่เซ็นถูกต้องจะส่งคืนค่าตามนั้น หากไม่มีจะส่งคืน DEFAULT-APP-SERIAL
    /// </summary>
    public static string GetCurrentAppSerial(string? baseDirectory = null)
    {
        string filePath = GetDefaultWatermarkFilePath(baseDirectory);
        if (!File.Exists(filePath)) return "DEFAULT-APP-SERIAL";

        try
        {
            string json = File.ReadAllText(filePath).Trim();
            var file = AppWatermarkFile.FromJson(json);
            if (file != null && VerifyWatermarkSignature(file))
            {
                return file.AppSerial;
            }
        }
        catch { }

        return "DEFAULT-APP-SERIAL";
    }

    public static bool VerifyWatermarkSignature(AppWatermarkFile file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.Signature)) return false;

            string signableData = file.GetSignableData();
            byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);
            byte[] signatureBytes = Convert.FromBase64String(file.Signature);

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(LicenseValidator.PublicKeyBase64), out _);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
