namespace HotelPOS.Common.Models;

public class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // เช่น "ห้องมาตรฐาน", "ห้อง VIP"
    public decimal DailyRate { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal MonthlyRate { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public UtilityBillingMode ElectricBillingMode { get; set; } = UtilityBillingMode.Meter;
    public decimal ElectricFlatRate { get; set; }
    public UtilityBillingMode WaterBillingMode { get; set; } = UtilityBillingMode.Meter;
    public decimal WaterFlatRate { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";           // สีประจำประเภทห้อง (Hex Code เช่น #3B82F6)
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum UtilityBillingMode
{
    Meter = 0,
    FlatRate = 1
}

public class Room
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;    // เลขห้อง
    public int RoomTypeId { get; set; }
    public string? Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? IdCardOrPassport { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
}

public class Booking
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;   // เลขที่การจอง เช่น BK-20260726-0001
    public int RoomId { get; set; }
    public int CustomerId { get; set; }
    public RatePlanType RatePlan { get; set; } = RatePlanType.Daily;
    public DateTime CheckInPlanned { get; set; }
    public DateTime? CheckOutPlanned { get; set; }
    public DateTime? CheckInActual { get; set; }
    public DateTime? CheckOutActual { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Reserved;
    public decimal AgreedRate { get; set; }        // ราคาที่ตกลง ณ ตอนจอง (เผื่อมีส่วนลด/โปรโมชั่น)
    public string? Notes { get; set; }
    public int? CreatedBy { get; set; }            // user_id (nullable)
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
}

/// <summary>บิลเปิดของห้อง/ผู้เข้าพัก รวมค่าห้อง + ค่าใช้จ่ายเสริมทั้งหมด จนกว่าจะปิดบิลตอนเช็คเอาท์</summary>
public class Folio
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public bool IsClosed { get; set; } = false;
    public decimal RoomCharges { get; set; }
    public decimal ExtraCharges { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
}
