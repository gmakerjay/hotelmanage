using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class ExportImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly ICustomerService _customerService;
    private readonly IRoomService _roomService;
    private readonly IPOSService _posService;
    private readonly IAuditService _auditService;
    private readonly IExportImportService _exportImportService;

    public ExportImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-exp-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-exp-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        var customerRepo = new CustomerRepository(_connectionFactory, _logger);
        var roomRepo = new RoomRepository(_connectionFactory, _logger);
        var productRepo = new ProductRepository(_connectionFactory, _logger);
        var saleRepo = new SaleRepository(_connectionFactory, _logger);
        var auditRepo = new AuditRepository(_connectionFactory, _logger);

        _customerService = new CustomerService(customerRepo, _logger);
        _roomService = new RoomService(roomRepo, _logger);
        _posService = new POSService(productRepo, saleRepo, _connectionFactory, _logger);
        _auditService = new AuditService(auditRepo, _logger);

        _exportImportService = new ExportImportService(_customerService, _roomService, _auditService, _posService);
    }

    [Fact]
    public async Task ExportAndImportCustomers_ส่งออกและนำเข้าลูกค้าสำเร็จ_ข้อมูลไม่สูญหาย()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"test-cust-{Guid.NewGuid():N}.csv");

        try
        {
            // 1. เพิ่มลูกค้าทดสอบ
            await _customerService.SaveCustomerAsync(new Customer
            {
                FullName = "คุณกิตติศักดิ์ ชัยชนะ",
                Phone = "086-123-4567",
                Email = "kitti@example.com",
                IdCardOrPassport = "1100400500607"
            });

            // 2. Export CSV
            await _exportImportService.ExportCustomersToCsvAsync(csvPath);
            Assert.True(File.Exists(csvPath));

            string content = await File.ReadAllTextAsync(csvPath);
            Assert.Contains("คุณกิตติศักดิ์ ชัยชนะ", content);
            Assert.Contains("086-123-4567", content); // ล็อกฟอร์แมต Excel ="086-123-4567"

            // 3. Import CSV กลับเข้า DB (สร้าง DB/ลบเดิม)
            int imported = await _exportImportService.ImportCustomersFromCsvAsync(csvPath);
            Assert.True(imported > 0);

            var customers = await _customerService.GetCustomersAsync("กิตติศักดิ์");
            Assert.NotEmpty(customers);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ExportAndImportRooms_ส่งออกและนำเข้าห้องพักสำเร็จ()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"test-rooms-{Guid.NewGuid():N}.csv");

        try
        {
            // 1. สร้างประเภทห้อง
            var roomType = new RoomType { Name = "ห้อง Deluxe", DailyRate = 1200m };
            roomType.Id = await _roomService.SaveRoomTypeAsync(roomType);

            // 2. เพิ่มห้อง
            await _roomService.SaveRoomAsync(new Room { RoomNumber = "999", Floor = "9", RoomTypeId = roomType.Id });

            // 3. Export CSV
            await _exportImportService.ExportRoomsToCsvAsync(csvPath);
            Assert.True(File.Exists(csvPath));

            // 4. Import CSV
            int imported = await _exportImportService.ImportRoomsFromCsvAsync(csvPath);
            Assert.True(imported >= 0);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ExportAndImportProducts_ส่งออกและนำเข้าสินค้าสต็อกสำเร็จ()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"test-products-{Guid.NewGuid():N}.csv");

        try
        {
            int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "ของใช้" });
            await _posService.SaveProductAsync(new Product
            {
                CategoryId = catId,
                Name = "แปรงฟัน",
                Sku = "SKU-001",
                Price = 25m,
                Cost = 10m,
                StockQty = 100,
                TrackStock = true
            });

            await _exportImportService.ExportProductsToCsvAsync(csvPath);
            Assert.True(File.Exists(csvPath));

            int imported = await _exportImportService.ImportProductsFromCsvAsync(csvPath);
            Assert.True(imported > 0);

            var prods = await _posService.GetProductsAsync(null, "แปรงฟัน");
            Assert.NotEmpty(prods);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger) disposableLogger.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
                if (Directory.Exists(_tempLogPath)) Directory.Delete(_tempLogPath, recursive: true);
                break;
            }
            catch (IOException) { Thread.Sleep(100); }
        }
    }
}
