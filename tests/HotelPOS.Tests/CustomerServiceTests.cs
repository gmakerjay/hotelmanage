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

public class CustomerServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly ICustomerRepository _customerRepo;
    private readonly ICustomerService _customerService;

    public CustomerServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-cust-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-cust-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();
        _customerRepo = new CustomerRepository(_connectionFactory, _logger);
        _customerService = new CustomerService(_customerRepo, _logger);
    }

    [Fact]
    public async Task SaveCustomer_ข้อมูลครบถ้วน_บันทึกสำเร็จ()
    {
        var customer = new Customer
        {
            FullName = "คุณสมชาย ใจดี",
            Phone = "081-234-5678",
            Email = "somchai@example.com",
            IdCardOrPassport = "1100200300405",
            Address = "123/45 กทม."
        };

        int id = await _customerService.SaveCustomerAsync(customer);

        Assert.True(id > 0);
        var saved = await _customerService.GetCustomerByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("คุณสมชาย ใจดี", saved.FullName);
        Assert.Equal("081-234-5678", saved.Phone);
    }

    [Fact]
    public async Task SaveCustomer_ชื่อว่าง_ต้อง_Throw()
    {
        var customer = new Customer { FullName = "   ", Phone = "081-000-0000" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _customerService.SaveCustomerAsync(customer));
        Assert.Contains("ชื่อ-นามสกุล", ex.Message);
    }

    [Fact]
    public async Task GetCustomerByPhoneOrIdCard_ค้นหาตามเบอร์หรือเลขบัตร_เจอถูกต้อง()
    {
        var customer = new Customer
        {
            FullName = "คุณวิชัย มั่นคง",
            Phone = "089-999-8888",
            IdCardOrPassport = "3100500600708"
        };
        int id = await _customerService.SaveCustomerAsync(customer);

        var foundByPhone = await _customerService.GetCustomerByPhoneOrIdCardAsync("089-999-8888");
        Assert.NotNull(foundByPhone);
        Assert.Equal(id, foundByPhone.Id);

        var foundByIdCard = await _customerService.GetCustomerByPhoneOrIdCardAsync("3100500600708");
        Assert.NotNull(foundByIdCard);
        Assert.Equal(id, foundByIdCard.Id);
    }

    [Fact]
    public async Task GetCustomers_ค้นหาด้วยคำค้น_กรองตรงตามคำ()
    {
        await _customerService.SaveCustomerAsync(new Customer { FullName = "สมหญิง รุ่งเรือง", Phone = "081-111-2222" });
        await _customerService.SaveCustomerAsync(new Customer { FullName = "อนันต์ สุขใจ", Phone = "082-333-4444" });

        var results = (await _customerService.GetCustomersAsync("สมหญิง")).ToList();

        Assert.Single(results);
        Assert.Equal("สมหญิง รุ่งเรือง", results[0].FullName);
    }

    [Fact]
    public async Task DeleteCustomer_ลบลูกค้าแบบ_SoftDelete_ต้องไม่พบในรายการหลัก()
    {
        int id = await _customerService.SaveCustomerAsync(new Customer { FullName = "ผู้เช่า ชั่วคราว", Phone = "085-555-5555" });
        Assert.NotNull(await _customerService.GetCustomerByIdAsync(id));

        await _customerService.DeleteCustomerAsync(id);

        var deleted = await _customerService.GetCustomerByIdAsync(id);
        Assert.Null(deleted);
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
