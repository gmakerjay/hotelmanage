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
    /// ตรวจสอบความถูกต้องของ License (ความสมบูรณ์ของไฟล์, ลายเซ็นดิจิทัล, ฮาร์ดแวร์ไอดี, วันหมดอายุ, การย้อนเวลา, และ Revocation List)
    /// </summary>
    public static LicenseStatus Validate(
        LicenseFile license, 
        string currentHardwareId, 
        DateTime? lastVerifiedAt = null, 
        string? revocationDirectory = null)
    {
        if (license == null)
            return LicenseStatus.Invalid;

        // ตรวจสอบ HardwareId ของเครื่อง สำหรับ Software License (ป้องกันการก๊อปปี้ license.dat ไปเครื่องอื่น)
        if (!string.IsNullOrEmpty(license.HardwareId) && license.HardwareId != currentHardwareId)
            return LicenseStatus.Invalid;

        string appSerial = AppWatermarkManager.GetCurrentAppSerial();
        return ValidateDongle(license, currentHardwareId, appSerial, lastVerifiedAt, revocationDirectory);
    }

    /// <summary>
    /// ตรวจสอบความถูกต้องของ USB Hardware Dongle (ตรวจสอบ Physical USB Serial, App Serial Watermark, Signature, Expiration, Revocation)
    /// </summary>
    public static LicenseStatus ValidateDongle(
        LicenseFile dongleLicense,
        string currentUsbHardwareId,
        string currentAppSerial,
        DateTime? lastVerifiedAt = null,
        string? revocationDirectory = null)
    {
        if (dongleLicense == null)
            return LicenseStatus.Invalid;

        // 1. ตรวจสอบ Revocation Blacklist
        if (RevocationManager.IsRevoked(currentUsbHardwareId, dongleLicense.CustomerName, revocationDirectory))
            return LicenseStatus.Revoked;

        // 2. ตรวจสอบ Digital Signature
        if (!VerifySignature(dongleLicense))
            return LicenseStatus.Invalid;

        // 3. ตรวจสอบ Physical USB Hardware Serial ระดับชิป (ป้องกันการก๊อปปี้ไป Flash Drive อีกอัน)
        if (!string.IsNullOrEmpty(dongleLicense.UsbHardwareId) && dongleLicense.UsbHardwareId != currentUsbHardwareId)
            return LicenseStatus.Invalid;

        // 3.1 Fail-Closed: หากอ่าน Physical USB Serial ไม่ได้เลย (WMI ล้มเหลว) ให้ถือว่า Invalid
        //     ป้องกันการ bypass โดยการใช้ USB drive ที่ไม่มี Serial Number ให้อ่านได้
        if (string.IsNullOrEmpty(currentUsbHardwareId))
            return LicenseStatus.Invalid;

        // 4. ตรวจสอบ App Serial Watermark ประจำตัวชุดโปรแกรม .exe (ป้องกันการนำ Dongle ไปใช้ข้ามชุดโปรแกรม)
        if (!string.IsNullOrEmpty(dongleLicense.AppSerial) && dongleLicense.AppSerial != currentAppSerial)
            return LicenseStatus.Invalid;

        // 5. ตรวจสอบการย้อนเวลาเครื่อง (Clock Rollback Detection)
        if (dongleLicense.ExpireDate.HasValue && lastVerifiedAt.HasValue)
        {
            if (DateTime.Now < lastVerifiedAt.Value)
            {
                return LicenseStatus.Invalid;
            }
        }

        // 6. ตรวจสอบวันหมดอายุ
        if (dongleLicense.ExpireDate.HasValue && DateTime.Now.Date > dongleLicense.ExpireDate.Value.Date)
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
