using System;
using System.Security.Cryptography;
using System.Text;
using HotelPOS.Common;

namespace HotelPOS.Licensing;

public static class LicenseValidator
{
    // Public Key ที่เป็น Base64 string ของ SubjectPublicKeyInfo (ที่ได้จากการสุ่มคู่คีย์อย่างเป็นทางการ)
    public const string PublicKeyBase64 = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxxgC/zkISnEEAtY7wSK57ZgF9Em4P6rWJEZukDdLr2Dl+DLYZbvAA0CuimUlQWMQl2ggOp4aOm0zhqQiZpw2pI9QbTrOp9w/y7zyo+zDGd7AhKr5LVoiZShTTxyrWWzKX1yPm6q8UpAsQQ2xZq5xvz2lPeK0fm1wJbs1284XAh+oBJrSk1MelTPhCPz1SJQnWTI3K5Ezu2j+1AjBRJ05L7ljKbrh6dTZllExMDb6h/QZ5JCHZTyIR6aj3XoLvd2K2g+9ReViBvElN60jv8Lz4B0EzOxwnfMUW3IVCy5LT6xDkRzeiQtWSQZK9UW2nIJByKZYyWsXqdGI//jR6MMpdQIDAQAB";

    /// <summary>
    /// ตรวจสอบความถูกต้องของ License (ความสมบูรณ์ของไฟล์, ลายเซ็นดิจิทัล, ฮาร์ดแวร์ไอดี, และวันหมดอายุ)
    /// </summary>
    public static LicenseStatus Validate(LicenseFile license, string currentHardwareId)
    {
        if (license == null)
            return LicenseStatus.Invalid;

        // 1. ตรวจสอบความถูกต้องของ Signature ดิจิทัล
        if (!VerifySignature(license))
            return LicenseStatus.Invalid;

        // 2. ตรวจสอบว่า Hardware ID ตรงกับเครื่องปัจจุบันหรือไม่
        if (license.HardwareId != currentHardwareId)
            return LicenseStatus.Invalid;

        // 3. ตรวจสอบวันหมดอายุ
        if (license.ExpireDate.HasValue && DateTime.Now.Date > license.ExpireDate.Value.Date)
            return LicenseStatus.Expired;

        return LicenseStatus.Active;
    }

    /// <summary>
    /// ยืนยันข้อมูลใน License ด้วยลายเซ็นดิจิทัลและ Public Key
    /// </summary>
    public static bool VerifySignature(LicenseFile license)
    {
        try
        {
            if (string.IsNullOrEmpty(license.Signature))
                return false;

            string signableData = license.GetSignableData();
            byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);
            byte[] signatureBytes = Convert.FromBase64String(license.Signature);

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
