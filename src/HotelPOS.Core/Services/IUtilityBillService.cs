using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface IUtilityBillService
{
    /// <summary>บันทึกเลขมิเตอร์รายห้อง (ค่าไฟ/ค่าน้ำ) พร้อมคำนวณยอดอัตโนมัติ</summary>
    Task<int> RecordMeterReadingAsync(int roomId, UtilityType type, decimal prevReading, decimal currReading, string billingMonth, string? notes = null);

    /// <summary>สร้างใบแจ้งหนี้รายเดือน (รวม ค่าห้อง + ค่าไฟ + ค่าน้ำ + ค่าบริการ + ค่าขยะ)</summary>
    Task<UtilityBill> GenerateMonthlyBillAsync(int roomId, string billingMonth, int waterPersonCount = 1, decimal extraCharges = 0, decimal discount = 0, string? notes = null);

    /// <summary>ดึง Preview ใบแจ้งหนี้ (ดึง meter readings + คำนวณล่วงหน้าก่อนบันทึก)</summary>
    Task<UtilityBill> GetMonthlyBillPreviewAsync(int roomId, string billingMonth, int waterPersonCount = 1);

    /// <summary>บันทึกว่าจ่ายแล้ว</summary>
    Task MarkBillAsPaidAsync(int billId, PaymentMethod paymentMethod);

    /// <summary>ดึงใบแจ้งหนี้ทุกห้องในรอบบิลเดียวกัน</summary>
    Task<IEnumerable<UtilityBill>> GetBillsByMonthAsync(string billingMonth);

    /// <summary>ดึงเลขมิเตอร์ทุกห้องในรอบบิลเดียวกัน (สำหรับ DataGridView)</summary>
    Task<IEnumerable<MeterReading>> GetMeterReadingsByMonthAsync(string billingMonth);

    /// <summary>ดึงใบแจ้งหนี้ที่ชำระแล้วในช่วงเวลาที่กำหนด (สำหรับรายงานสรุป)</summary>
    Task<IEnumerable<UtilityBill>> GetPaidBillsByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>ดึงเลขมิเตอร์เดือนก่อนหน้า (เติมช่อง "ก่อน" อัตโนมัติ)</summary>
    Task<decimal> GetPreviousMeterValueAsync(int roomId, UtilityType type, string currentBillingMonth);

    /// <summary>ดูประวัติใบแจ้งหนี้ย้อนหลัง</summary>
    Task<IEnumerable<UtilityBill>> GetBillHistoryAsync(int roomId, int lastNMonths = 12);

    /// <summary>ดูประวัติมิเตอร์ย้อนหลัง</summary>
    Task<IEnumerable<MeterReading>> GetMeterHistoryAsync(int roomId, int lastNMonths = 12);
}
