using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public BookingRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Booking>> GetBookingsAsync(DateTime? startDate = null, DateTime? endDate = null, BookingStatus? status = null, int? roomId = null)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, booking_code AS BookingCode, room_id AS RoomId, customer_id AS CustomerId,
                       rate_plan AS RatePlan, check_in_planned AS CheckInPlanned, check_out_planned AS CheckOutPlanned,
                       check_in_actual AS CheckInActual, check_out_actual AS CheckOutActual, status AS Status,
                       agreed_rate AS AgreedRate, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, is_deleted AS IsDeleted
                FROM bookings
                WHERE is_deleted = 0";

            var parameters = new DynamicParameters();
            if (startDate.HasValue)
            {
                sql += " AND check_in_planned >= @StartDate";
                parameters.Add("StartDate", startDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            if (endDate.HasValue)
            {
                sql += " AND check_in_planned <= @EndDate";
                parameters.Add("EndDate", endDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            if (status.HasValue)
            {
                sql += " AND status = @Status";
                parameters.Add("Status", (int)status.Value);
            }
            if (roomId.HasValue)
            {
                sql += " AND room_id = @RoomId";
                parameters.Add("RoomId", roomId.Value);
            }

            sql += " ORDER BY created_at DESC";
            return await connection.QueryAsync<Booking>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ค้นหาการจองไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, booking_code AS BookingCode, room_id AS RoomId, customer_id AS CustomerId,
                       rate_plan AS RatePlan, check_in_planned AS CheckInPlanned, check_out_planned AS CheckOutPlanned,
                       check_in_actual AS CheckInActual, check_out_actual AS CheckOutActual, status AS Status,
                       agreed_rate AS AgreedRate, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, is_deleted AS IsDeleted
                FROM bookings
                WHERE id = @Id AND is_deleted = 0";
            return await connection.QuerySingleOrDefaultAsync<Booking>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลการจอง ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Booking?> GetBookingByCodeAsync(string bookingCode)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, booking_code AS BookingCode, room_id AS RoomId, customer_id AS CustomerId,
                       rate_plan AS RatePlan, check_in_planned AS CheckInPlanned, check_out_planned AS CheckOutPlanned,
                       check_in_actual AS CheckInActual, check_out_actual AS CheckOutActual, status AS Status,
                       agreed_rate AS AgreedRate, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, is_deleted AS IsDeleted
                FROM bookings
                WHERE booking_code = @Code AND is_deleted = 0";
            return await connection.QuerySingleOrDefaultAsync<Booking>(sql, new { Code = bookingCode });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลการจอง Code='{bookingCode}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Booking?> GetActiveBookingByRoomIdAsync(int roomId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, booking_code AS BookingCode, room_id AS RoomId, customer_id AS CustomerId,
                       rate_plan AS RatePlan, check_in_planned AS CheckInPlanned, check_out_planned AS CheckOutPlanned,
                       check_in_actual AS CheckInActual, check_out_actual AS CheckOutActual, status AS Status,
                       agreed_rate AS AgreedRate, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, is_deleted AS IsDeleted
                FROM bookings
                WHERE room_id = @RoomId AND status IN (0, 1) AND is_deleted = 0
                ORDER BY created_at DESC
                LIMIT 1";
            return await connection.QuerySingleOrDefaultAsync<Booking>(sql, new { RoomId = roomId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านการจองปัจจุบันของห้อง ID={roomId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    /// <summary>
    /// ดึงการจองที่ active ทั้งหมด (status IN (0,1)) พร้อม Customer ใน query เดียว (แก้ N+1)
    /// คืน Dictionary keyed by room_id
    /// </summary>
    public async Task<Dictionary<int, (Booking Booking, Customer? Customer)>> GetAllActiveBookingsWithCustomersAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT b.id AS Id, b.booking_code AS BookingCode, b.room_id AS RoomId, b.customer_id AS CustomerId,
                       b.rate_plan AS RatePlan, b.check_in_planned AS CheckInPlanned, b.check_out_planned AS CheckOutPlanned,
                       b.check_in_actual AS CheckInActual, b.check_out_actual AS CheckOutActual, b.status AS Status,
                       b.agreed_rate AS AgreedRate, b.notes AS Notes, b.created_by AS CreatedBy,
                       b.created_at AS CreatedAt, b.updated_at AS UpdatedAt, b.is_deleted AS IsDeleted,
                       c.id AS CustId, c.full_name AS FullName, c.phone AS Phone, c.email AS Email,
                       c.id_card_or_passport AS IdCardOrPassport, c.address AS Address, c.notes AS CustNotes,
                       c.created_at AS CustCreatedAt
                FROM bookings b
                LEFT JOIN customers c ON b.customer_id = c.id
                WHERE b.status IN (0, 1) AND b.is_deleted = 0
                ORDER BY b.created_at DESC";

            var result = new Dictionary<int, (Booking, Customer?)>();

            var rows = await connection.QueryAsync<Booking, Customer?, (Booking, Customer?)>(
                sql,
                (booking, customer) => (booking, customer),
                splitOn: "CustId"
            );

            foreach (var (booking, customer) in rows)
            {
                // เก็บเฉพาะ booking ล่าสุดของแต่ละห้อง (อันแรกเพราะ ORDER BY DESC)
                if (!result.ContainsKey(booking.RoomId))
                {
                    result[booking.RoomId] = (booking, customer);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ดึงการจอง active ทั้งหมดไม่สำเร็จ (batch)", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveBookingAsync(Booking booking)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (booking.Id == 0)
            {
                const string sql = @"
                    INSERT INTO bookings (
                        booking_code, room_id, customer_id, rate_plan, check_in_planned, check_out_planned,
                        check_in_actual, check_out_actual, status, agreed_rate, notes, created_by,
                        created_at, updated_at, is_deleted
                    ) VALUES (
                        @BookingCode, @RoomId, @CustomerId, @RatePlan, @CheckInPlanned, @CheckOutPlanned,
                        @CheckInActual, @CheckOutActual, @Status, @AgreedRate, @Notes, @CreatedBy,
                        datetime('now','localtime'), datetime('now','localtime'), 0
                    );
                    SELECT last_insert_rowid();";

                var id = await connection.ExecuteScalarAsync<long>(sql, new {
                    booking.BookingCode,
                    booking.RoomId,
                    booking.CustomerId,
                    RatePlan = (int)booking.RatePlan,
                    CheckInPlanned = booking.CheckInPlanned.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckOutPlanned = booking.CheckOutPlanned?.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckInActual = booking.CheckInActual?.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckOutActual = booking.CheckOutActual?.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = (int)booking.Status,
                    booking.AgreedRate,
                    booking.Notes,
                    CreatedBy = booking.CreatedBy == 0 ? (int?)null : booking.CreatedBy
                });
                booking.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างการจองใหม่ Code='{booking.BookingCode}' (ID: {booking.Id}) สำเร็จ", correlationId);
                return booking.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE bookings
                    SET room_id = @RoomId, customer_id = @CustomerId, rate_plan = @RatePlan,
                        check_in_planned = @CheckInPlanned, check_out_planned = @CheckOutPlanned,
                        check_in_actual = @CheckInActual, check_out_actual = @CheckOutActual,
                        status = @Status, agreed_rate = @AgreedRate, notes = @Notes,
                        updated_at = datetime('now','localtime')
                    WHERE id = @Id;";

                await connection.ExecuteAsync(sql, new {
                    booking.Id,
                    booking.RoomId,
                    booking.CustomerId,
                    RatePlan = (int)booking.RatePlan,
                    CheckInPlanned = booking.CheckInPlanned.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckOutPlanned = booking.CheckOutPlanned?.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckInActual = booking.CheckInActual?.ToString("yyyy-MM-dd HH:mm:ss"),
                    CheckOutActual = booking.CheckOutActual?.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = (int)booking.Status,
                    booking.AgreedRate,
                    booking.Notes
                });
                _logger.Info(LogCategory.Database, $"แก้ไขการจอง ID={booking.Id} สำเร็จ", correlationId);
                return booking.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกการจอง Code='{booking.BookingCode}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task UpdateBookingStatusAsync(int bookingId, BookingStatus status, DateTime? actualCheckIn = null, DateTime? actualCheckOut = null)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = "UPDATE bookings SET status = @Status, updated_at = datetime('now','localtime')";
            if (actualCheckIn.HasValue)
            {
                sql += ", check_in_actual = @ActualCheckIn";
            }
            if (actualCheckOut.HasValue)
            {
                sql += ", check_out_actual = @ActualCheckOut";
            }
            sql += " WHERE id = @Id;";

            await connection.ExecuteAsync(sql, new {
                Id = bookingId,
                Status = (int)status,
                ActualCheckIn = actualCheckIn?.ToString("yyyy-MM-dd HH:mm:ss"),
                ActualCheckOut = actualCheckOut?.ToString("yyyy-MM-dd HH:mm:ss")
            });
            _logger.Info(LogCategory.Database, $"อัปเดตสถานะการจอง ID={bookingId} เป็น {status} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อัปเดตสถานะการจอง ID={bookingId} เป็น {status} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
