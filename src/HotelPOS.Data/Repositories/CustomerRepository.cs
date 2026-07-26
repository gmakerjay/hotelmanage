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
}
