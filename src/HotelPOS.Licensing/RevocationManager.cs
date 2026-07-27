using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelPOS.Licensing;

public class RevocationListFile
{
    [JsonPropertyName("issued_at")]
    public DateTime IssuedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("revoked_hardware_ids")]
    public List<string> RevokedHardwareIds { get; set; } = new();

    [JsonPropertyName("revoked_customer_names")]
    public List<string> RevokedCustomerNames { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    public string GetSignableData()
    {
        string ids = string.Join(",", RevokedHardwareIds ?? new());
        string names = string.Join(",", RevokedCustomerNames ?? new());
        return $"ISSUED:{IssuedAt:yyyy-MM-dd}|IDS:{ids}|NAMES:{names}";
    }

    public static RevocationListFile? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RevocationListFile>(json);
        }
        catch
        {
            return null;
        }
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}

public static class RevocationManager
{
    public static string RevocationFileName = "revoked.dat";

    public static string GetDefaultRevocationFilePath(string? directory = null)
    {
        directory ??= LicenseManager.GetDefaultLicenseDirectory();
        return Path.Combine(directory, RevocationFileName);
    }

    /// <summary>
    /// ตรวจสอบว่า Hardware ID หรือชื่อลูกค้าติดรายการ Revocation/Blacklist หรือไม่
    /// </summary>
    public static bool IsRevoked(string hardwareId, string? customerName = null, string? revocationDirectory = null)
    {
        string filePath = GetDefaultRevocationFilePath(revocationDirectory);
        if (!File.Exists(filePath)) return false;

        try
        {
            string json = File.ReadAllText(filePath).Trim();
            var list = RevocationListFile.FromJson(json);
            if (list == null) return false;

            // ยืนยัน Signature ด้วย Public Key
            if (!VerifyRevocationSignature(list)) return false;

            if (list.RevokedHardwareIds.Contains(hardwareId))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(customerName) && list.RevokedCustomerNames.Contains(customerName))
            {
                return true;
            }
        }
        catch
        {
            // หากอ่านล้มเหลว ยึดความปลอดภัยปกติ
        }

        return false;
    }

    public static bool VerifyRevocationSignature(RevocationListFile list)
    {
        try
        {
            if (string.IsNullOrEmpty(list.Signature)) return false;

            string signableData = list.GetSignableData();
            byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);
            byte[] signatureBytes = Convert.FromBase64String(list.Signature);

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
