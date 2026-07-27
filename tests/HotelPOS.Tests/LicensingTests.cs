using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using HotelPOS.Common;
using HotelPOS.Licensing;
using Xunit;

namespace HotelPOS.Tests;

public class LicensingTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempFolder;
    
    // คีย์ส่วนตัวจำลองสำหรับใช้ทดสอบการออกลายเซ็น (สอดคล้องกับคีย์สาธารณะในฝั่งลูกค้าของโปรเจค)
    private const string PrivateKeyBase64 = "MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDHGAL/OQhKcQQC1jvBIrntmAX0Sbg/qtYkRm6QN0uvYOX4Mthlu8ADQK6KZSVBYxCXaCA6nho6bTOGpCJmnDakj1BtOs6n3D/LvPKj7MMZ3sCEqvktWiJlKFNPHKtZbMpfXI+bqrxSkCxBDbFmrnG/PaU94rR+bXAluzXbzhcCH6gEmtKTUx6VM+EI/PVIlCdZMjcrkTO7aP7UCMFEnTkvuWMpuuHp1NmWUTEwNvqH9BnkkIdlPIhHpqPdegu93YraD71F5WIG8SU3rSO/wvPgHQTM7HCd8xRbchULLktPrEORHN6JC1ZJBkr1RbacgkHIpljJaxep0Yj/+NHowyl1AgMBAAECggEBAIIY7bRrR0ClszJLXcap84cPZSypk41/C+muYIc6qulST1QtnXx1AFbfyG5FA+BDZM8bSpwjPg5Z12avEI+umoJT6AFIgUvtP37Z3FBD4YWhKnpG4wbAtGMXw8CZglqwHVnNOUZGfkMRVOm5kegAK/IEzVqwLrPCvZraR6p3dE98yseuQdKwy/KNuA0PbCOA8Md8Le+hng36DAAdcn8kHKksi9W8gBqS9qB5LKnla4kXNKeYPGDBKhjaCf45k2aJtnBHMd74/P1y+VkeJMlSjH8elx9rDbzkn+CvmSBY/BDLLlpuD2nftPSuZ8yWNp/krG5lufUdFsFa8kHoqJnH+W0CgYEA15L4D2ZFC7stFenwPLGjbh0SFtQZACzM48xMX3I2Ecuro+qONrdHgZ7Q0wm6b1W1dUkUeNSZ4wMiux/lhhaHYbBqMbjRpIagPGsN6+62KPOsK+L90OqPz5N49BYdF0NuBTQSif1xGP39cv7LX2JwUEYaoSs7lTYVGJ73yQMnU5MCgYEA7G3gIjYt7PTJBWNtVt1dUJ1TNdIz6B6UM/sMWw0t3qCMR4oQBJz7E8NmZLIUeS0TT7McDaaCymnn2/JKBdXWWu8dM8KGjm9tzq6CPPd5Lvt2aUWkijFfwtVg6SYSmwp786SfStsNjXKED7xiqU03GwT8nLf8TewgCB7lV6uBw9cCgYEAlVEVRQVfedqyReV+I2wfeVvlda5/iqF9YaPWmp3vWbArOSR0UO3uN5gbqLGqUweY4p418ePAm39GhTp4rsHYEBAz3jDX9Q/S2UaFpA/6WK8/aD6X9CckaXEKbHcMu1pXUH9a//1uYxM6hHZ7w5vZk6CbPVtGr/l/70fc9XybtsUCgYAtaJDyoStC5mSxZz45v7xLXlv760pS24SlUyM1XZugtX8bwlV/PVMvoYjJ8DXkbBbYaNMLgB6Al8STRr6WzlIkFuap6UOEmbwiRPv4j6MztdIxN9H5RLBasDazsL9EDchurAB4FQhOUV8x0oG0eIML6nJF+0Q3BxHD3YM4ylTa8wKBgB+UjpKeSIcMuG1Em5wbXbLbzNwxjuPX6TwyZKHmzOZZRfZq/4ppJaV66h8pngQC1ZZOBMDI/IWIKorM40hFqHGXmnp+Z7dFwsjoRWoC77/Y6plkb4qq/Od5ZnCVLbBN8uTK3hYdAUd2OfYQ/m6E5CNkSA/xUGGgm8Zhzlf58ezI";

    public LicensingTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-lic-test-{Guid.NewGuid():N}.db");
        _tempFolder = Path.Combine(Path.GetTempPath(), $"hotelpos-lic-folder-{Guid.NewGuid():N}");

        // สลับการชี้เป้าหมายข้อมูลสำหรับการรันเทส (ห้ามรบกวนข้อมูลจริงของเครื่องและฐานข้อมูลลูกค้า)
        TrialManager.RegistrySubKey = @"Software\HotelPOS\Tests";
        TrialManager.RegistryValueName = "TestTData";
        TrialManager.HiddenFileName = ".test-tdata";

        LicenseManager.LicenseRegistryValueName = "TestLData";
        LicenseManager.LicenseFileName = "test-license.dat";

        if (!Directory.Exists(_tempFolder))
        {
            Directory.CreateDirectory(_tempFolder);
        }
    }

    [Fact]
    public void HardwareId_ควรมีความยาวหกสิบสี่ตัวอักษรและไม่ว่าง()
    {
        var hwId = HardwareIdGenerator.Generate();
        Assert.NotNull(hwId);
        Assert.Equal(64, hwId.Length);
    }

    [Fact]
    public void LicenseValidation_คีย์ถูกต้องและผูกเครื่องตรง_ควรยืนยันผ่าน()
    {
        var hwId = HardwareIdGenerator.Generate();
        var license = new LicenseFile
        {
            CustomerName = "ทดสอบการออกลิขสิทธิ์",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(30),
            MaxRooms = 10,
            Features = new List<string> { "BOOKING", "POS" }
        };

        // เซ็นลายเซ็นดิจิทัลจำลองแบบเดียวกับ Admin Tool
        SignLicense(license);

        // ตรวจสอบ
        var result = LicenseValidator.Validate(license, hwId);
        Assert.Equal(LicenseStatus.Active, result);
    }

    [Fact]
    public void LicenseValidation_เนื้อหาถูกดัดแปลงมือ_ควรตรวจพบและแจ้งเตือน_Invalid()
    {
        var hwId = HardwareIdGenerator.Generate();
        var license = new LicenseFile
        {
            CustomerName = "ลูกค้าเดิม",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(30)
        };

        SignLicense(license);

        // จำลองการดัดแปลงเนื้อหา
        license.CustomerName = "ลูกค้าปลอมแปลง";

        var result = LicenseValidator.Validate(license, hwId);
        Assert.Equal(LicenseStatus.Invalid, result);
    }

    [Fact]
    public void LicenseValidation_นำไฟล์ไปก็อปปี้ใช้เครื่องอื่น_ควรคืนค่า_Invalid()
    {
        var hwIdMachineA = HardwareIdGenerator.Generate();
        var hwIdMachineB = "fake-machine-b-hardware-id-that-is-different-from-machine-a";

        var license = new LicenseFile
        {
            CustomerName = "ลูกค้าผู้ซื้อสิทธิ์",
            HardwareId = hwIdMachineA,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(365)
        };

        SignLicense(license);

        // ตรวจสอบโดยนำไปรันบนเครื่อง B
        var result = LicenseValidator.Validate(license, hwIdMachineB);
        Assert.Equal(LicenseStatus.Invalid, result);
    }

    [Fact]
    public void LicenseValidation_วันที่หมดอายุผ่านมาแล้ว_ควรได้สถานะ_Expired()
    {
        var hwId = HardwareIdGenerator.Generate();
        var license = new LicenseFile
        {
            CustomerName = "ลูกค้าหมดอายุการใช้งาน",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today.AddDays(-60),
            ExpireDate = DateTime.Today.AddDays(-1) // หมดอายุเมื่อวาน
        };

        SignLicense(license);

        var result = LicenseValidator.Validate(license, hwId);
        Assert.Equal(LicenseStatus.Expired, result);
    }

    [Fact]
    public void TrialManager_เช็คครั้งแรก_ควรได้วันใช้งานเหลือสามสิบวันเต็ม()
    {
        ClearTestRegistryAndFiles();

        var (isActive, daysRemaining) = TrialManager.GetTrialStatus(_tempDbPath, _tempFolder);
        
        Assert.True(isActive);
        Assert.Equal(30, daysRemaining);
    }

    [Fact]
    public void TrialManager_หากมีข้อมูลไม่ตรงกัน_ควรปรับซิงค์คืนค่าวันที่เริ่มเก่าที่สุด()
    {
        ClearTestRegistryAndFiles();

        // เขียนวันที่ต่างกันลงใน 3 แหล่ง (จำลองผู้ใช้พยายามแฮกเพื่อแก้เวลา)
        var date1 = DateTime.Today.AddDays(-10); // เก่าสุด (ความเป็นจริง)
        var date2 = DateTime.Today.AddDays(-5);
        var date3 = DateTime.Today;

        WriteRegistryDate(date2);
        WriteHiddenFileDate(date3);
        WriteDatabaseDate(date1);

        // โหลดมาประเมินผล
        var startDate = TrialManager.GetOrInitializeTrialStartDate(_tempDbPath, _tempFolder);
        
        // ต้องซิงค์กลับไปเป็นวันที่เก่าที่สุดเพื่อความปลอดภัย (date1)
        Assert.Equal(date1, startDate);

        // ประเมินผลวันเหลือ: 30 - 10 วันที่ใช้ไปแล้ว = 20 วันคงเหลือ
        var (isActive, daysRemaining) = TrialManager.GetTrialStatus(_tempDbPath, _tempFolder);
        Assert.Equal(20, daysRemaining);
    }

    [Fact]
    public void LicenseValidation_ย้อนเวลาเครื่องก่อนหน้า_lastVerifiedAt_ควรได้สถานะ_Invalid()
    {
        var hwId = HardwareIdGenerator.Generate();
        var license = new LicenseFile
        {
            CustomerName = "ลูกค้าแบบรายปี",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today.AddDays(-10),
            ExpireDate = DateTime.Today.AddDays(30)
        };

        SignLicense(license);

        // จำลอง lastVerifiedAt เป็นวันพรุ่งนี้ (พยายามหมุนเวลาคอมย้อนหลังกลับมาวันนี้)
        var futureVerifiedAt = DateTime.Now.AddDays(1);
        var result = LicenseValidator.Validate(license, hwId, futureVerifiedAt, _tempFolder);

        Assert.Equal(LicenseStatus.Invalid, result);
    }

    [Fact]
    public void LicenseValidation_อยู่ในรายการถอนสิทธิ์_Revoked_ควรได้สถานะ_Revoked()
    {
        var hwId = HardwareIdGenerator.Generate();
        var license = new LicenseFile
        {
            CustomerName = "ลูกค้าโดนระงับสิทธิ์",
            HardwareId = hwId,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(30)
        };

        SignLicense(license);

        // สร้างไฟล์ revoked.dat จำลองใน _tempFolder
        var revokedFile = new RevocationListFile
        {
            IssuedAt = DateTime.Now,
            RevokedHardwareIds = new List<string> { hwId }
        };

        string signableData = revokedFile.GetSignableData();
        byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(PrivateKeyBase64), out _);
        byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        revokedFile.Signature = Convert.ToBase64String(signatureBytes);

        File.WriteAllText(Path.Combine(_tempFolder, RevocationManager.RevocationFileName), revokedFile.ToJson());

        var result = LicenseValidator.Validate(license, hwId, null, _tempFolder);
        Assert.Equal(LicenseStatus.Revoked, result);
    }

    [Fact]
    public void LicenseMonitorService_สุ่มตรวจสถานะเบื้องหลัง_ควรทำงานสำเร็จ()
    {
        using var monitor = new HotelPOS.Core.Services.LicenseMonitorService(_tempDbPath, _tempFolder);
        var (status, license, days) = monitor.CheckNow();

        Assert.NotNull(license);
        Assert.True(days >= 0);
    }

    [Fact]
    public void UsbDongle_PhysicalSerial_ควรคำนวณHashได้สมบูรณ์()
    {
        string rawSerial = "KINGSTON-DT100G3-0014D15370C1";
        string hash = UsbDongleManager.HashUsbSerial(rawSerial);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void UsbDongle_นำไฟล์ก๊อปปี้ไปFlashDriveอันอื่น_ควรได้สถานะ_Invalid()
    {
        string usbA_HwId = UsbDongleManager.HashUsbSerial("FLASH-DRIVE-USB-A");
        string usbB_HwId = UsbDongleManager.HashUsbSerial("FLASH-DRIVE-USB-B");
        string appSerial = "APP-TEST-001";

        var dongleLicense = new LicenseFile
        {
            CustomerName = "ลูกค้าใช้งาน USB Dongle A",
            UsbHardwareId = usbA_HwId,
            AppSerial = appSerial,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(365)
        };

        SignLicense(dongleLicense);

        // ตรวจสอบเมื่อนำไฟล์ไปเสียบบน USB B
        var statusOnUsbB = LicenseValidator.ValidateDongle(dongleLicense, usbB_HwId, appSerial);
        Assert.Equal(LicenseStatus.Invalid, statusOnUsbB);
    }

    [Fact]
    public void UsbDongle_นำDongleของAppAไปใช้กับAppB_ควรได้สถานะ_Invalid()
    {
        string usbHwId = UsbDongleManager.HashUsbSerial("FLASH-DRIVE-USB-A");
        string appSerialA = "APP-CLIENT-A";
        string appSerialB = "APP-CLIENT-B";

        var dongleLicense = new LicenseFile
        {
            CustomerName = "ลูกค้า A",
            UsbHardwareId = usbHwId,
            AppSerial = appSerialA,
            LicenseType = LicenseType.Standard,
            IssueDate = DateTime.Today,
            ExpireDate = DateTime.Today.AddDays(365)
        };

        SignLicense(dongleLicense);

        // นำไปใช้กับโปรแกรมที่มี App Serial B
        var result = LicenseValidator.ValidateDongle(dongleLicense, usbHwId, appSerialB);
        Assert.Equal(LicenseStatus.Invalid, result);
    }

    [Fact]
    public void UsbDongle_ข้อมูลถูกต้องและAppSerialตรง_ควรได้สถานะ_Active()
    {
        string usbHwId = UsbDongleManager.HashUsbSerial("FLASH-DRIVE-VALID");
        string appSerial = "APP-CLIENT-MATCH";

        var dongleLicense = new LicenseFile
        {
            CustomerName = "ลูกค้าเปิดใช้งานถูกต้อง",
            UsbHardwareId = usbHwId,
            AppSerial = appSerial,
            LicenseType = LicenseType.Lifetime,
            IssueDate = DateTime.Today
        };

        SignLicense(dongleLicense);

        var result = LicenseValidator.ValidateDongle(dongleLicense, usbHwId, appSerial);
        Assert.Equal(LicenseStatus.Active, result);
    }

    private void SignLicense(LicenseFile license)
    {
        string signableData = license.GetSignableData();
        byte[] dataBytes = Encoding.UTF8.GetBytes(signableData);

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(PrivateKeyBase64), out _);
        byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        license.Signature = Convert.ToBase64String(signatureBytes);
    }

    private void WriteRegistryDate(DateTime date)
    {
        using var key = Registry.CurrentUser.CreateSubKey(TrialManager.RegistrySubKey);
        key.SetValue(TrialManager.RegistryValueName, ObfuscateForTest(date));
    }

    private void WriteHiddenFileDate(DateTime date)
    {
        string filePath = Path.Combine(_tempFolder, TrialManager.HiddenFileName);
        File.WriteAllText(filePath, ObfuscateForTest(date));
    }

    private void WriteDatabaseDate(DateTime date)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tempDbPath};");
        conn.Open();
        using (var cmdTable = conn.CreateCommand())
        {
            cmdTable.CommandText = "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT, description TEXT, updated_at TEXT)";
            cmdTable.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value, description, updated_at) VALUES ('trial_start_date', @value, 'Test', 'now')";
            cmd.Parameters.AddWithValue("@value", ObfuscateForTest(date));
            cmd.ExecuteNonQuery();
        }
    }

    private string ObfuscateForTest(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");
        char[] arr = dateStr.ToCharArray();
        Array.Reverse(arr);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(arr)));
    }

    private void ClearTestRegistryAndFiles()
    {
        if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        
        string hiddenFilePath = Path.Combine(_tempFolder, TrialManager.HiddenFileName);
        if (File.Exists(hiddenFilePath))
        {
            File.SetAttributes(hiddenFilePath, FileAttributes.Normal);
            File.Delete(hiddenFilePath);
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TrialManager.RegistrySubKey, throwOnMissingSubKey: false);
        }
        catch { }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        ClearTestRegistryAndFiles();

        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, recursive: true);
                }
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
