using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IMeterReadingRepository
{
    /// <summary>ดึงเลขมิเตอร์ของห้องในรอบบิลที่ระบุ (ทั้งค่าไฟ+ค่าน้ำ)</summary>
    Task<IEnumerable<MeterReading>> GetByRoomAndMonthAsync(int roomId, string billingMonth);

    /// <summary>ดึงเลขมิเตอร์ทุกห้องในรอบบิลเดียวกัน (สำหรับแสดงตาราง DataGridView)</summary>
    Task<IEnumerable<MeterReading>> GetByMonthAsync(string billingMonth);

    /// <summary>ดึงเลขมิเตอร์เดือนก่อนหน้า (เพื่อเติมในช่อง "ก่อน" อัตโนมัติ)</summary>
    Task<MeterReading?> GetPreviousReadingAsync(int roomId, UtilityType utilityType, string currentBillingMonth);

    /// <summary>บันทึก/อัปเดตเลขมิเตอร์ (Upsert ตาม room_id + utility_type + billing_month)</summary>
    Task<int> UpsertAsync(MeterReading reading);

    /// <summary>ดูประวัติย้อนหลังรายห้อง</summary>
    Task<IEnumerable<MeterReading>> GetHistoryAsync(int roomId, int lastNMonths = 12);
}
