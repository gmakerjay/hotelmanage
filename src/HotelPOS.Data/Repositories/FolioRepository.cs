using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class FolioRepository : IFolioRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public FolioRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Folio?> GetFolioByBookingIdAsync(int bookingId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, booking_id AS BookingId, is_closed AS IsClosed,
                       room_charges AS RoomCharges, extra_charges AS ExtraCharges,
                       discount_amount AS DiscountAmount, total_amount AS TotalAmount,
                       created_at AS CreatedAt, closed_at AS ClosedAt
                FROM folios
                WHERE booking_id = @BookingId
                ORDER BY id DESC
                LIMIT 1";
            return await connection.QuerySingleOrDefaultAsync<Folio>(sql, new { BookingId = bookingId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูล Folio ของ Booking ID={bookingId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Folio?> GetFolioByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, booking_id AS BookingId, is_closed AS IsClosed,
                       room_charges AS RoomCharges, extra_charges AS ExtraCharges,
                       discount_amount AS DiscountAmount, total_amount AS TotalAmount,
                       created_at AS CreatedAt, closed_at AS ClosedAt
                FROM folios
                WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<Folio>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูล Folio ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveFolioAsync(Folio folio)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (folio.Id == 0)
            {
                const string sql = @"
                    INSERT INTO folios (
                        booking_id, is_closed, room_charges, extra_charges, discount_amount, total_amount, created_at, closed_at
                    ) VALUES (
                        @BookingId, @IsClosed, @RoomCharges, @ExtraCharges, @DiscountAmount, @TotalAmount, datetime('now','localtime'), @ClosedAt
                    );
                    SELECT last_insert_rowid();";

                var id = await connection.ExecuteScalarAsync<long>(sql, new {
                    folio.BookingId,
                    IsClosed = folio.IsClosed ? 1 : 0,
                    folio.RoomCharges,
                    folio.ExtraCharges,
                    folio.DiscountAmount,
                    folio.TotalAmount,
                    ClosedAt = folio.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                });
                folio.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้าง Folio ใหม่สำหรับ Booking ID={folio.BookingId} (ID: {folio.Id}) สำเร็จ", correlationId);
                return folio.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE folios
                    SET is_closed = @IsClosed, room_charges = @RoomCharges, extra_charges = @ExtraCharges,
                        discount_amount = @DiscountAmount, total_amount = @TotalAmount,
                        closed_at = @ClosedAt
                    WHERE id = @Id;";

                await connection.ExecuteAsync(sql, new {
                    folio.Id,
                    IsClosed = folio.IsClosed ? 1 : 0,
                    folio.RoomCharges,
                    folio.ExtraCharges,
                    folio.DiscountAmount,
                    folio.TotalAmount,
                    ClosedAt = folio.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                });
                _logger.Info(LogCategory.Database, $"แก้ไข Folio ID={folio.Id} สำเร็จ", correlationId);
                return folio.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึก Folio ID={folio.Id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task CloseFolioAsync(int folioId, decimal roomCharges, decimal extraCharges, decimal discountAmount, decimal totalAmount)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE folios
                SET is_closed = 1, room_charges = @RoomCharges, extra_charges = @ExtraCharges,
                    discount_amount = @DiscountAmount, total_amount = @TotalAmount,
                    closed_at = datetime('now','localtime')
                WHERE id = @Id;";

            await connection.ExecuteAsync(sql, new {
                Id = folioId,
                RoomCharges = roomCharges,
                ExtraCharges = extraCharges,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount
            });
            _logger.Info(LogCategory.Database, $"ปิด Folio ID={folioId} รวมเงิน={totalAmount} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ปิด Folio ID={folioId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
