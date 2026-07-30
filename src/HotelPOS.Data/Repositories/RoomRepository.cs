using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public RoomRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<RoomType>> GetRoomTypesAsync(bool activeOnly = true)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, name AS Name, daily_rate AS DailyRate, hourly_rate AS HourlyRate, 
                       monthly_rate AS MonthlyRate, description AS Description, is_active AS IsActive,
                       electric_billing_mode AS ElectricBillingMode, electric_flat_rate AS ElectricFlatRate,
                       water_billing_mode AS WaterBillingMode, water_flat_rate AS WaterFlatRate,
                       color_hex AS ColorHex,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM room_types";
            if (activeOnly)
            {
                sql += " WHERE is_active = 1";
            }
            sql += " ORDER BY name";
            return await connection.QueryAsync<RoomType>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านรายการประเภทห้องไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<RoomType?> GetRoomTypeByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, name AS Name, daily_rate AS DailyRate, hourly_rate AS HourlyRate, 
                       monthly_rate AS MonthlyRate, description AS Description, is_active AS IsActive,
                       electric_billing_mode AS ElectricBillingMode, electric_flat_rate AS ElectricFlatRate,
                       water_billing_mode AS WaterBillingMode, water_flat_rate AS WaterFlatRate,
                       color_hex AS ColorHex,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM room_types
                WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<RoomType>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านประเภทห้อง id={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveRoomTypeAsync(RoomType roomType)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (roomType.Id == 0)
            {
                const string sql = @"
                    INSERT INTO room_types (name, daily_rate, hourly_rate, monthly_rate, description, is_active, 
                                            electric_billing_mode, electric_flat_rate, water_billing_mode, water_flat_rate, color_hex,
                                            created_at, updated_at)
                    VALUES (@Name, @DailyRate, @HourlyRate, @MonthlyRate, @Description, @IsActive, 
                            @ElectricBillingMode, @ElectricFlatRate, @WaterBillingMode, @WaterFlatRate, @ColorHex,
                            datetime('now','localtime'), datetime('now','localtime'));
                    SELECT last_insert_rowid();";
                var id = await connection.ExecuteScalarAsync<long>(sql, roomType);
                roomType.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างประเภทห้องใหม่ '{roomType.Name}' (ID: {roomType.Id}) สำเร็จ", correlationId);
                return roomType.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE room_types 
                    SET name = @Name, daily_rate = @DailyRate, hourly_rate = @HourlyRate, 
                        monthly_rate = @MonthlyRate, description = @Description, is_active = @IsActive,
                        electric_billing_mode = @ElectricBillingMode, electric_flat_rate = @ElectricFlatRate,
                        water_billing_mode = @WaterBillingMode, water_flat_rate = @WaterFlatRate,
                        color_hex = @ColorHex,
                        updated_at = datetime('now','localtime')
                    WHERE id = @Id;";
                await connection.ExecuteAsync(sql, roomType);
                _logger.Info(LogCategory.Database, $"แก้ไขประเภทห้อง ID={roomType.Id} สำเร็จ", correlationId);
                return roomType.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกประเภทห้อง '{roomType.Name}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task DeleteRoomTypeAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE room_types SET is_active = 0, updated_at = datetime('now','localtime') WHERE id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id });
            _logger.Info(LogCategory.Database, $"ยกเลิกใช้งานประเภทห้อง ID={id} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ลบประเภทห้อง ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<Room>> GetRoomsAsync(string? floor = null, int? roomTypeId = null, RoomStatus? status = null, bool activeOnly = true)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, room_number AS RoomNumber, room_type_id AS RoomTypeId, 
                       floor AS Floor, status AS Status, notes AS Notes, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM rooms
                WHERE 1=1";

            var parameters = new DynamicParameters();
            if (activeOnly)
            {
                sql += " AND is_active = 1";
            }
            if (!string.IsNullOrWhiteSpace(floor))
            {
                sql += " AND floor = @Floor";
                parameters.Add("Floor", floor);
            }
            if (roomTypeId.HasValue)
            {
                sql += " AND room_type_id = @RoomTypeId";
                parameters.Add("RoomTypeId", roomTypeId.Value);
            }
            if (status.HasValue)
            {
                sql += " AND status = @Status";
                parameters.Add("Status", (int)status.Value);
            }

            sql += " ORDER BY room_number";
            return await connection.QueryAsync<Room>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านรายการห้องพักไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Room?> GetRoomByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, room_number AS RoomNumber, room_type_id AS RoomTypeId, 
                       floor AS Floor, status AS Status, notes AS Notes, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM rooms
                WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<Room>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลห้อง ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Room?> GetRoomByNumberAsync(string roomNumber)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, room_number AS RoomNumber, room_type_id AS RoomTypeId, 
                       floor AS Floor, status AS Status, notes AS Notes, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM rooms
                WHERE room_number = @RoomNumber";
            return await connection.QuerySingleOrDefaultAsync<Room>(sql, new { RoomNumber = roomNumber });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลห้องเลขที่ '{roomNumber}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveRoomAsync(Room room)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (room.Id == 0)
            {
                const string sql = @"
                    INSERT INTO rooms (room_number, room_type_id, floor, status, notes, is_active, created_at, updated_at)
                    VALUES (@RoomNumber, @RoomTypeId, @Floor, @Status, @Notes, @IsActive, datetime('now','localtime'), datetime('now','localtime'));
                    SELECT last_insert_rowid();";
                var id = await connection.ExecuteScalarAsync<long>(sql, new {
                    room.RoomNumber,
                    room.RoomTypeId,
                    room.Floor,
                    Status = (int)room.Status,
                    room.Notes,
                    room.IsActive
                });
                room.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างห้องใหม่ '{room.RoomNumber}' (ID: {room.Id}) สำเร็จ", correlationId);
                return room.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE rooms
                    SET room_number = @RoomNumber, room_type_id = @RoomTypeId, floor = @Floor,
                        status = @Status, notes = @Notes, is_active = @IsActive,
                        updated_at = datetime('now','localtime')
                    WHERE id = @Id;";
                await connection.ExecuteAsync(sql, new {
                    room.Id,
                    room.RoomNumber,
                    room.RoomTypeId,
                    room.Floor,
                    Status = (int)room.Status,
                    room.Notes,
                    room.IsActive
                });
                _logger.Info(LogCategory.Database, $"แก้ไขข้อมูลห้อง ID={room.Id} ('{room.RoomNumber}') สำเร็จ", correlationId);
                return room.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกห้อง '{room.RoomNumber}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task UpdateRoomStatusAsync(int roomId, RoomStatus status, string? notes = null)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                UPDATE rooms
                SET status = @Status, updated_at = datetime('now','localtime')";
            if (notes != null)
            {
                sql += ", notes = @Notes";
            }
            sql += " WHERE id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = roomId, Status = (int)status, Notes = notes });
            _logger.Info(LogCategory.Database, $"อัปเดตสถานะห้อง ID={roomId} เป็น {status} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อัปเดตสถานะห้อง ID={roomId} เป็น {status} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task DeleteRoomAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string checkSql = "SELECT COUNT(*) FROM bookings WHERE room_id = @Id;";
            var bookingCount = await connection.ExecuteScalarAsync<int>(checkSql, new { Id = id });

            if (bookingCount > 0)
            {
                const string softDeleteSql = "UPDATE rooms SET is_active = 0, updated_at = datetime('now','localtime') WHERE id = @Id;";
                await connection.ExecuteAsync(softDeleteSql, new { Id = id });
                _logger.Info(LogCategory.Database, $"ยกเลิก/ซ่อนห้องพัก ID={id} (Soft Delete เนื่องจากมีประวัติการจอง) สำเร็จ", correlationId);
            }
            else
            {
                const string hardDeleteSql = "DELETE FROM rooms WHERE id = @Id;";
                await connection.ExecuteAsync(hardDeleteSql, new { Id = id });
                _logger.Info(LogCategory.Database, $"ลบห้องพัก ID={id} สำเร็จ", correlationId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ลบห้องพัก ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetFloorsAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT DISTINCT floor FROM rooms WHERE floor IS NOT NULL AND floor != '' AND is_active = 1 ORDER BY floor";
            return await connection.QueryAsync<string>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านรายการชั้นของห้องพักไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
