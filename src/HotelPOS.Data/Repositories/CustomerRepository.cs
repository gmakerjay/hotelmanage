using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public CustomerRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync(string? searchQuery = null)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, full_name AS FullName, phone AS Phone, email AS Email,
                       id_card_or_passport AS IdCardOrPassport, address AS Address, notes AS Notes,
                       created_at AS CreatedAt, is_deleted AS IsDeleted
                FROM customers
                WHERE is_deleted = 0";

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                sql += " AND (full_name LIKE @Q OR phone LIKE @Q OR id_card_or_passport LIKE @Q)";
            }
            sql += " ORDER BY full_name";

            var param = string.IsNullOrWhiteSpace(searchQuery) ? null : new { Q = $"%{searchQuery.Trim()}%" };
            return await connection.QueryAsync<Customer>(sql, param);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ค้นหาข้อมูลลูกค้าไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, full_name AS FullName, phone AS Phone, email AS Email,
                       id_card_or_passport AS IdCardOrPassport, address AS Address, notes AS Notes,
                       created_at AS CreatedAt, is_deleted AS IsDeleted
                FROM customers
                WHERE id = @Id AND is_deleted = 0";
            return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลลูกค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Customer?> GetCustomerByPhoneOrIdCardAsync(string identifier)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, full_name AS FullName, phone AS Phone, email AS Email,
                       id_card_or_passport AS IdCardOrPassport, address AS Address, notes AS Notes,
                       created_at AS CreatedAt, is_deleted AS IsDeleted
                FROM customers
                WHERE (phone = @Ident OR id_card_or_passport = @Ident) AND is_deleted = 0
                LIMIT 1";
            return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { Ident = identifier.Trim() });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลลูกค้าโดยระบุ '{identifier}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveCustomerAsync(Customer customer)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (customer.Id == 0)
            {
                const string sql = @"
                    INSERT INTO customers (full_name, phone, email, id_card_or_passport, address, notes, created_at, is_deleted)
                    VALUES (@FullName, @Phone, @Email, @IdCardOrPassport, @Address, @Notes, datetime('now','localtime'), 0);
                    SELECT last_insert_rowid();";
                var id = await connection.ExecuteScalarAsync<long>(sql, customer);
                customer.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างข้อมูลลูกค้าใหม่ '{customer.FullName}' (ID: {customer.Id}) สำเร็จ", correlationId);
                return customer.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE customers
                    SET full_name = @FullName, phone = @Phone, email = @Email,
                        id_card_or_passport = @IdCardOrPassport, address = @Address, notes = @Notes
                    WHERE id = @Id;";
                await connection.ExecuteAsync(sql, customer);
                _logger.Info(LogCategory.Database, $"แก้ไขข้อมูลลูกค้า ID={customer.Id} ('{customer.FullName}') สำเร็จ", correlationId);
                return customer.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกข้อมูลลูกค้า '{customer.FullName}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task DeleteCustomerAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE customers SET is_deleted = 1 WHERE id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id });
            _logger.Info(LogCategory.Database, $"ลบข้อมูลลูกค้า ID={id} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ลบข้อมูลลูกค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<CustomerStayHistoryDto>> GetCustomerStayHistoryAsync(int customerId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT b.id AS BookingId, b.booking_code AS BookingCode, r.room_number AS RoomNumber, 
                       COALESCE(b.check_in_actual, b.check_in_planned) AS CheckIn, 
                       b.check_out_actual AS CheckOut, 
                       CASE b.status 
                            WHEN 0 THEN 'Reserved (จองแล้ว)'
                            WHEN 1 THEN 'CheckedIn (เข้าพักอยู่)'
                            WHEN 2 THEN 'CheckedOut (เช็คเอาท์แล้ว)'
                            WHEN 3 THEN 'Cancelled (ยกเลิก)'
                            ELSE 'Unknown'
                       END AS Status, 
                       IFNULL(f.total_amount, 0) AS TotalAmount
                FROM bookings b
                JOIN rooms r ON b.room_id = r.id
                LEFT JOIN folios f ON f.booking_id = b.id
                WHERE b.customer_id = @CustomerId AND b.is_deleted = 0
                ORDER BY b.check_in_planned DESC";

            return await connection.QueryAsync<CustomerStayHistoryDto>(sql, new { CustomerId = customerId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านประวัติการเข้าพักของลูกค้า ID={customerId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<CustomerPOSHistoryDto>> GetCustomerPOSHistoryAsync(int customerId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.id AS SaleId, s.sale_code AS SaleCode, s.created_at AS Date, s.total_amount AS TotalAmount,
                       (SELECT group_concat(p.name || ' x ' || si.quantity, ', ') 
                        FROM sale_items si
                        JOIN products p ON si.product_id = p.id
                        WHERE si.sale_id = s.id) AS ItemsSummary
                FROM sales s
                LEFT JOIN folios f ON s.folio_id = f.id
                LEFT JOIN bookings b ON f.booking_id = b.id
                WHERE s.customer_id = @CustomerId OR b.customer_id = @CustomerId
                GROUP BY s.id
                ORDER BY s.created_at DESC";

            return await connection.QueryAsync<CustomerPOSHistoryDto>(sql, new { CustomerId = customerId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านประวัติซื้อขายของลูกค้า ID={customerId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
