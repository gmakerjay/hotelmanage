namespace HotelPOS.Common.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;   // เช่น "ผู้ดูแลระบบ", "พนักงานหน้าเคาน์เตอร์"
    public string PermissionsJson { get; set; } = "{}"; // เก็บสิทธิ์แบบ JSON เพื่อความยืดหยุ่น
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;  // เก็บเป็น hash (PBKDF2/BCrypt) ห้ามเก็บ plain text เด็ดขาด
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>ตั้งค่าระบบแบบ key-value เพื่อความยืดหยุ่น (ชื่อร้าน, โลโก้, เลขภาษี, เครื่องพิมพ์ ฯลฯ)</summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class SystemSettingsDto
{
    public string ShopName { get; set; } = "โรงแรม HotelPOS TH";
    public string ShopAddress { get; set; } = "123/45 ถนนสุขุมวิท กรุงเทพมหานคร";
    public string ShopPhone { get; set; } = "02-123-4567";
    public string ShopTaxId { get; set; } = "0105560000000";
    public string BillHeader { get; set; } = "ยินดีต้อนรับสู่โรงแรมของเรา";
    public string BillFooter { get; set; } = "ขอบคุณที่ใช้บริการ / Thank you for staying with us";

    public string PrinterName { get; set; } = "";
    public string PaperType { get; set; } = "A4"; // A4 | 80mm | 58mm
    public bool AutoPrintOnCheckout { get; set; } = true;
    public bool ShowSignatureBox { get; set; } = true;

    public string DefaultCheckInTime { get; set; } = "14:00";
    public string DefaultCheckOutTime { get; set; } = "12:00";
    public decimal DefaultSecurityDeposit { get; set; } = 500m;
    public decimal VatRate { get; set; } = 7.00m;
    public bool EnableVat { get; set; } = false;

    public string ReceiptDocPrefix { get; set; } = "RC";
    public int ReceiptDocRunningNumber { get; set; } = 0;

    public string? LogoImagePath { get; set; }
    public string? QrCodeImagePath { get; set; }
}

/// <summary>บันทึกการกระทำของผู้ใช้ในระบบ (Audit Trail) แยกจาก app_logs ที่เป็น log ทางเทคนิค</summary>
public class AuditLogEntry
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;     // เช่น "CHECKIN", "DELETE_BOOKING", "REFUND"
    public string? EntityName { get; set; }                 // เช่น "Booking"
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>ข้อมูล License ที่ผูกกับเครื่อง (เก็บแบบเข้ารหัสจริงในไฟล์ license.dat — ตารางนี้เก็บสำเนา/สถานะเพื่อใช้แสดงผลในโปรแกรมเท่านั้น)</summary>
public class LicenseRecord
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public LicenseType LicenseType { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ExpireDate { get; set; }      // null = ถาวร
    public int? MaxRooms { get; set; }
    public string FeaturesJson { get; set; } = "[]";
    public LicenseStatus Status { get; set; } = LicenseStatus.NotActivated;
    public DateTime? LastVerifiedAt { get; set; }
}

public class BackupHistoryEntry
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public bool IsAutoBackup { get; set; }
    public string? PerformedBy { get; set; }
    public string Type { get; set; } = "BACKUP"; // BACKUP | RESTORE | RESET
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
