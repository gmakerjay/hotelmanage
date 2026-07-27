using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class MeterReadingRepository : IMeterReadingRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public MeterReadingRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<MeterReading>> GetByRoomAndMonthAsync(int roomId, string billingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT mr.id AS Id, mr.room_id AS RoomId, mr.utility_type AS UtilityType,
                       mr.billing_month AS BillingMonth, mr.reading_prev AS ReadingPrev,
                       mr.reading_curr AS ReadingCurr, mr.units_used AS UnitsUsed,
                       mr.rate_per_unit AS RatePerUnit, mr.total_amount AS TotalAmount,
                       mr.recorded_by AS RecordedBy, mr.recorded_at AS RecordedAt, mr.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM meter_readings mr
                JOIN rooms r ON r.id = mr.room_id
                WHERE mr.room_id = @RoomId AND mr.billing_month = @BillingMonth
                ORDER BY mr.utility_type";
            return await conn.QueryAsync<MeterReading>(sql, new { RoomId = roomId, BillingMonth = billingMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงข้อมูลมิเตอร์ห้อง {roomId} เดือน {billingMonth} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<MeterReading>> GetByMonthAsync(string billingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT mr.id AS Id, mr.room_id AS RoomId, mr.utility_type AS UtilityType,
                       mr.billing_month AS BillingMonth, mr.reading_prev AS ReadingPrev,
                       mr.reading_curr AS ReadingCurr, mr.units_used AS UnitsUsed,
                       mr.rate_per_unit AS RatePerUnit, mr.total_amount AS TotalAmount,
                       mr.recorded_by AS RecordedBy, mr.recorded_at AS RecordedAt, mr.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM meter_readings mr
                JOIN rooms r ON r.id = mr.room_id
                WHERE mr.billing_month = @BillingMonth
                ORDER BY r.room_number, mr.utility_type";
            return await conn.QueryAsync<MeterReading>(sql, new { BillingMonth = billingMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงข้อมูลมิเตอร์ทุกห้องเดือน {billingMonth} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<MeterReading?> GetPreviousReadingAsync(int roomId, UtilityType utilityType, string currentBillingMonth)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            // ดึงเลขมิเตอร์ "หลัง" ของเดือนก่อนหน้า เพื่อเอามาเป็น "ก่อน" ของเดือนนี้
            const string sql = @"
                SELECT mr.id AS Id, mr.room_id AS RoomId, mr.utility_type AS UtilityType,
                       mr.billing_month AS BillingMonth, mr.reading_prev AS ReadingPrev,
                       mr.reading_curr AS ReadingCurr, mr.units_used AS UnitsUsed,
                       mr.rate_per_unit AS RatePerUnit, mr.total_amount AS TotalAmount,
                       mr.recorded_by AS RecordedBy, mr.recorded_at AS RecordedAt, mr.notes AS Notes
                FROM meter_readings mr
                WHERE mr.room_id = @RoomId 
                  AND mr.utility_type = @UtilityType 
                  AND mr.billing_month < @CurrentBillingMonth
                ORDER BY mr.billing_month DESC
                LIMIT 1";
            return await conn.QuerySingleOrDefaultAsync<MeterReading>(sql, new
            {
                RoomId = roomId,
                UtilityType = (int)utilityType,
                CurrentBillingMonth = currentBillingMonth
            });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงเลขมิเตอร์เดือนก่อนหน้าของห้อง {roomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> UpsertAsync(MeterReading reading)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO meter_readings (room_id, utility_type, billing_month, reading_prev, reading_curr, units_used, rate_per_unit, total_amount, recorded_by, notes)
                VALUES (@RoomId, @UtilityType, @BillingMonth, @ReadingPrev, @ReadingCurr, @UnitsUsed, @RatePerUnit, @TotalAmount, @RecordedBy, @Notes)
                ON CONFLICT(room_id, utility_type, billing_month) DO UPDATE SET
                    reading_prev = excluded.reading_prev,
                    reading_curr = excluded.reading_curr,
                    units_used = excluded.units_used,
                    rate_per_unit = excluded.rate_per_unit,
                    total_amount = excluded.total_amount,
                    recorded_by = excluded.recorded_by,
                    recorded_at = datetime('now', 'localtime'),
                    notes = excluded.notes
                RETURNING id";
            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                reading.RoomId,
                UtilityType = (int)reading.UtilityType,
                reading.BillingMonth,
                reading.ReadingPrev,
                reading.ReadingCurr,
                reading.UnitsUsed,
                reading.RatePerUnit,
                reading.TotalAmount,
                reading.RecordedBy,
                reading.Notes
            });
            _logger.Info(LogCategory.Utility, $"บันทึกมิเตอร์ห้อง {reading.RoomId} ประเภท {reading.UtilityType} เดือน {reading.BillingMonth} สำเร็จ (ID={id})", correlationId);
            return id;
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"บันทึกมิเตอร์ห้อง {reading.RoomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<MeterReading>> GetHistoryAsync(int roomId, int lastNMonths = 12)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            // ดึง N เดือนล่าสุด
            string cutoffMonth = DateTime.Now.AddMonths(-lastNMonths).ToString("yyyy-MM");
            const string sql = @"
                SELECT mr.id AS Id, mr.room_id AS RoomId, mr.utility_type AS UtilityType,
                       mr.billing_month AS BillingMonth, mr.reading_prev AS ReadingPrev,
                       mr.reading_curr AS ReadingCurr, mr.units_used AS UnitsUsed,
                       mr.rate_per_unit AS RatePerUnit, mr.total_amount AS TotalAmount,
                       mr.recorded_by AS RecordedBy, mr.recorded_at AS RecordedAt, mr.notes AS Notes,
                       r.room_number AS RoomNumber
                FROM meter_readings mr
                JOIN rooms r ON r.id = mr.room_id
                WHERE mr.room_id = @RoomId AND mr.billing_month >= @CutoffMonth
                ORDER BY mr.billing_month DESC, mr.utility_type";
            return await conn.QueryAsync<MeterReading>(sql, new { RoomId = roomId, CutoffMonth = cutoffMonth });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Utility, $"ดึงประวัติมิเตอร์ห้อง {roomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
