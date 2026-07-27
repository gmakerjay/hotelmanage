namespace HotelPOS.Common;

/// <summary>สถานะห้องพัก</summary>
public enum RoomStatus
{
    Available = 0,      // ว่าง
    Occupied = 1,       // มีผู้เข้าพัก
    Cleaning = 2,       // รอทำความสะอาด
    Maintenance = 3,    // ปิดซ่อมบำรุง
    Reserved = 4        // จองไว้ล่วงหน้า
}

/// <summary>ประเภทการคิดราคาห้อง</summary>
public enum RatePlanType
{
    Daily = 0,      // รายวัน
    Hourly = 1,     // รายชั่วโมง
    Monthly = 2     // รายเดือน
}

/// <summary>สถานะการจอง</summary>
public enum BookingStatus
{
    Reserved = 0,       // จองไว้ ยังไม่เช็คอิน
    CheckedIn = 1,      // เช็คอินแล้ว
    CheckedOut = 2,     // เช็คเอาท์แล้ว
    Cancelled = 3,      // ยกเลิก
    NoShow = 4          // ไม่มาตามนัด
}

/// <summary>ช่องทางการชำระเงิน</summary>
public enum PaymentMethod
{
    Cash = 0,           // เงินสด
    BankTransfer = 1,   // โอนเงิน
    CreditCard = 2,     // บัตรเครดิต/เดบิต
    PromptPay = 3,      // พร้อมเพย์ (QR)
    Other = 99
}

/// <summary>ประเภทเอกสารที่พิมพ์</summary>
public enum DocumentType
{
    Receipt = 0,            // ใบเสร็จรับเงิน
    TaxInvoiceAbbr = 1,     // ใบกำกับภาษีอย่างย่อ
    TaxInvoiceFull = 2,     // ใบกำกับภาษีเต็มรูป
    Folio = 3               // ใบสรุปบิลห้องพัก
}

/// <summary>ขนาดกระดาษที่รองรับการพิมพ์</summary>
public enum PaperSize
{
    Receipt58mm = 0,
    Receipt80mm = 1,
    A4 = 2
}

/// <summary>ประเภท License</summary>
public enum LicenseType
{
    Trial = 0,
    Standard = 1,
    Lifetime = 2
}

/// <summary>สถานะ License</summary>
public enum LicenseStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2,
    Invalid = 3,        // ลายเซ็นไม่ถูกต้อง/ผูกเครื่องไม่ตรง
    NotActivated = 4
}

/// <summary>ประเภทสาธารณูปโภค (มิเตอร์ไฟ/น้ำ)</summary>
public enum UtilityType
{
    Electric = 0,   // ค่าไฟฟ้า
    Water = 1       // ค่าน้ำประปา
}

/// <summary>ระดับความรุนแรงของ Log (สอดคล้องกับ Serilog LogEventLevel)</summary>
public enum LogCategory
{
    UI,
    Database,
    Printing,
    License,
    Booking,
    Pos,
    Backup,
    Auth,
    Audit,
    System,
    Utility     // ระบบค่าน้ำค่าไฟ
}
