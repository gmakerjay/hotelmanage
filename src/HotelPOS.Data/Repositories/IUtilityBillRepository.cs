using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IUtilityBillRepository
{
    /// <summary>ดึงใบแจ้งหนี้ของห้องในรอบบิลที่ระบุ</summary>
    Task<UtilityBill?> GetByRoomAndMonthAsync(int roomId, string billingMonth);

    /// <summary>ดึงใบแจ้งหนี้ทุกห้องในรอบบิลเดียวกัน</summary>
    Task<IEnumerable<UtilityBill>> GetByMonthAsync(string billingMonth);

    /// <summary>ดึงใบแจ้งหนี้ตาม ID</summary>
    Task<UtilityBill?> GetByIdAsync(int id);

    /// <summary>บันทึก/อัปเดตใบแจ้งหนี้</summary>
    Task<int> SaveAsync(UtilityBill bill);

    /// <summary>บันทึกว่าจ่ายแล้ว</summary>
    Task MarkAsPaidAsync(int billId, PaymentMethod paymentMethod);

    /// <summary>สร้างเลขที่บิลถัดไป</summary>
    Task<string> GenerateNextBillCodeAsync(string billingMonth);

    /// <summary>ดูประวัติย้อนหลังรายห้อง</summary>
    Task<IEnumerable<UtilityBill>> GetHistoryAsync(int roomId, int lastNMonths = 12);
}
