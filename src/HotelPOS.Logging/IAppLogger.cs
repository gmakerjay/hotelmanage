using HotelPOS.Common;

namespace HotelPOS.Logging;

/// <summary>
/// Interface กลางสำหรับการ log ทั้งระบบ
/// ทุกโปรเจค (UI, Core, Data, Licensing, Printing) ต้อง inject ตัวนี้ไปใช้ ห้ามเรียก Serilog ตรงๆ
/// เพื่อให้ควบคุม format/ปลายทางของ log ได้จากจุดเดียว (ดู PROJECT_PLAN.md หัวข้อ 7)
/// </summary>
public interface IAppLogger
{
    void Trace(LogCategory category, string message, string? correlationId = null);
    void Debug(LogCategory category, string message, string? correlationId = null);
    void Info(LogCategory category, string message, string? correlationId = null);
    void Warning(LogCategory category, string message, string? correlationId = null);
    void Error(LogCategory category, string message, Exception? exception = null, string? correlationId = null);
    void Fatal(LogCategory category, string message, Exception? exception = null, string? correlationId = null);

    /// <summary>สร้าง correlation id ใหม่สำหรับเริ่มต้น flow หนึ่ง action (เช่น กด "พิมพ์ใบเสร็จ" หนึ่งครั้ง)</summary>
    string NewCorrelationId();
}
