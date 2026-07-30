using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly ISettingsService _settingsService;

    public SettingsServiceTests()
    {
        // ใช้ไฟล์ DB/Log ชั่วคราวแยกทุกครั้งที่ทดสอบ เพื่อไม่ให้เทสกระทบกัน
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        ISettingsRepository repository = new SettingsRepository(_connectionFactory, _logger);
        _settingsService = new SettingsService(repository, _logger);
    }

    [Fact]
    public async Task GetShopNameAsync_ควรได้ค่าเริ่มต้นจาก_seed_data()
    {
        var shopName = await _settingsService.GetShopNameAsync();
        Assert.False(string.IsNullOrWhiteSpace(shopName));
    }

    [Fact]
    public async Task SetAsync_แล้ว_GetAsync_ควรได้ค่าที่เพิ่งบันทึก()
    {
        await _settingsService.SetAsync("shop_name", "โรงแรมทดสอบ");
        var result = await _settingsService.GetAsync("shop_name");
        Assert.Equal("โรงแรมทดสอบ", result);
    }

    [Fact]
    public async Task GetNextDocumentNumberAsync_ควรออกเลขที่รันต่อเนื่องไม่ซ้ำกัน()
    {
        var first = await _settingsService.GetNextDocumentNumberAsync();
        var second = await _settingsService.GetNextDocumentNumberAsync();
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ZetZeroDatabaseAsync_ควรล้างข้อมูลระบบสำเร็จโดยไม่ติด_ForeignKey_Constraint()
    {
        using (var conn = _connectionFactory.CreateConnection())
        {
            await conn.OpenAsync();
            // สร้างข้อมูลทดสอบในทุกตารางธุรกรรม
            await Dapper.SqlMapper.ExecuteAsync(conn, @"
                INSERT INTO room_types (id, name) VALUES (1, 'Standard');
                INSERT INTO rooms (id, room_number, room_type_id, status) VALUES (1, '101', 1, 1);
                INSERT INTO customers (id, full_name, phone) VALUES (1, 'สมชาย สายดี', '0812345678');
                INSERT INTO bookings (id, booking_code, room_id, customer_id, check_in_planned, status) VALUES (1, 'BK-001', 1, 1, '2026-07-27', 1);
                INSERT INTO folios (id, booking_id, room_charges) VALUES (1, 1, 1000);
                INSERT INTO product_categories (id, name) VALUES (1, 'เครื่องดื่ม');
                INSERT INTO products (id, category_id, name, price) VALUES (1, 1, 'น้ำดื่ม', 15);
                INSERT INTO sales (id, sale_code, folio_id, customer_id, total_amount) VALUES (1, 'SL-001', 1, 1, 1015);
                INSERT INTO sale_items (id, sale_id, product_id, product_name_snapshot, unit_price, quantity, line_total) VALUES (1, 1, 1, 'น้ำดื่ม', 15, 1, 15);
                INSERT INTO payments (id, sale_id, method, amount) VALUES (1, 1, 0, 1015);
                INSERT INTO invoice_documents (id, sale_id, doc_type, document_number, printed_paper_size) VALUES (1, 1, 0, 'RC-000001', 0);
                INSERT INTO meter_readings (id, room_id, utility_type, billing_month, reading_curr) VALUES (1, 1, 0, '2026-07', 100);
                INSERT INTO utility_bills (id, bill_code, room_id, billing_month, total_amount) VALUES (1, 'UB-001', 1, '2026-07', 500);
                INSERT INTO audit_logs (id, action) VALUES (1, 'TEST_ACTION');
                INSERT INTO backup_history (id, file_path, checksum) VALUES (1, 'C:\\backup.db', 'abc');
            ");
        }

        // Act: สั่งล้างข้อมูลเป็น 0
        await _settingsService.ZetZeroDatabaseAsync();

        // Assert: ตรวจสอบว่าตารางธุรกรรมเป็น 0 ทั้งหมด และห้องพักกลับเป็นสถานะ 0 (Available)
        using (var conn = _connectionFactory.CreateConnection())
        {
            await conn.OpenAsync();

            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM bookings"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM customers"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM folios"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM sales"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM sale_items"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM payments"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM invoice_documents"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM meter_readings"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM utility_bills"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM audit_logs"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM backup_history"));

            // ตารางมาสเตอร์ (rooms, room_types) ต้องถูกล้างด้วยตามนโยบาย Factory Reset
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM rooms"));
            Assert.Equal(0, await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, "SELECT COUNT(*) FROM room_types"));
        }

        var runningNo = await _settingsService.GetAsync("receipt_doc_running_number");
        Assert.Equal("0", runningNo);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }

        // เคลียร์ Connection Pool ของ SQLite เพื่อปลดล็อกไฟล์ฐานข้อมูล
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // รอและพยายามลบไฟล์ชั่วคราว (เผื่อกรณีระบบปฏิบัติการประมวลผลการปลดล็อกไฟล์ช้า)
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_tempDbPath))
                {
                    File.Delete(_tempDbPath);
                }
                if (Directory.Exists(_tempLogPath))
                {
                    Directory.Delete(_tempLogPath, recursive: true);
                }
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
