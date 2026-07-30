namespace HotelPOS.Common.Models;

/// <summary>บันทึกเลขมิเตอร์ค่าน้ำ/ค่าไฟ รายห้อง รายรอบบิล (เดือน)</summary>
public class MeterReading
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public UtilityType UtilityType { get; set; }        // ELECTRIC / WATER
    public string BillingMonth { get; set; } = string.Empty;  // "YYYY-MM" เช่น "2026-07"
    public decimal ReadingPrev { get; set; }             // เลขมิเตอร์เดือนก่อน
    public decimal ReadingCurr { get; set; }             // เลขมิเตอร์เดือนนี้
    public decimal UnitsUsed { get; set; }               // หน่วยที่ใช้ = curr - prev
    public decimal RatePerUnit { get; set; }             // อัตราต่อหน่วย ณ ตอนบันทึก (snapshot)
    public decimal TotalAmount { get; set; }             // ยอดรวม = units × rate
    public int? RecordedBy { get; set; }                 // user_id ที่บันทึก
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    // Navigation (ไม่ได้ map กับ DB โดยตรง ใช้สำหรับ DTO/Display)
    public string? RoomNumber { get; set; }
}

/// <summary>ใบแจ้งหนี้ค่าสาธารณูปโภครายเดือน (รวม ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าบริการ + ค่าขยะ)</summary>
public class UtilityBill
{
    public int Id { get; set; }
    public string BillCode { get; set; } = string.Empty;    // เลขที่บิล เช่น UB-202607-0001
    public int RoomId { get; set; }
    public string BillingMonth { get; set; } = string.Empty;
    public decimal RoomCharge { get; set; }                  // ค่าเช่าห้อง

    // Snapshot มิเตอร์ไฟ ณ วันที่ออกบิล
    public decimal ElectricPrev { get; set; }
    public decimal ElectricCurr { get; set; }
    public decimal ElectricUnits { get; set; }
    public decimal ElectricRate { get; set; }
    public decimal ElectricAmount { get; set; }              // ค่าไฟรวม
    public string ElectricBillingMode { get; set; } = "METER"; // METER / FLAT

    // Snapshot มิเตอร์น้ำ ณ วันที่ออกบิล
    public decimal WaterPrev { get; set; }
    public decimal WaterCurr { get; set; }
    public decimal WaterUnits { get; set; }
    public decimal WaterRate { get; set; }
    public decimal WaterAmount { get; set; }                 // ค่าน้ำรวม (ตามมิเตอร์ หรือ เหมาจ่าย)
    public string WaterBillingMode { get; set; } = "METER";  // METER / FLAT
    public int WaterPersonCount { get; set; } = 1;           // จำนวนคนในห้อง (ใช้เมื่อ FLAT)
    public decimal CommonAreaFee { get; set; }               // ค่าส่วนกลาง/ค่าบริการ
    public decimal GarbageFee { get; set; }                  // ค่าขยะ
    public decimal ExtraCharges { get; set; }                // ค่าอื่นๆ เพิ่มเติม
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }                 // ยอดรวมทั้งหมด
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    // Navigation (สำหรับ Display)
    public string? RoomNumber { get; set; }

    // Meter Readings (สำหรับแสดงในใบแจ้งหนี้)
    public MeterReading? ElectricReading { get; set; }
    public MeterReading? WaterReading { get; set; }
}
