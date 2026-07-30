using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class POSServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly IProductRepository _productRepo;
    private readonly ISaleRepository _saleRepo;
    private readonly IPOSService _posService;

    public POSServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-pos-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-pos-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();
        _productRepo = new ProductRepository(_connectionFactory, _logger);
        _saleRepo = new SaleRepository(_connectionFactory, _logger);
        _posService = new POSService(_productRepo, _saleRepo, _connectionFactory, _logger);
    }

    [Fact]
    public async Task SaveCategory_และ_SaveProduct_บันทึกสินค้าและหมวดหมู่ถูกต้อง()
    {
        var category = new ProductCategory { Name = "มินิบาร์" };
        int catId = await _posService.SaveCategoryAsync(category);
        Assert.True(catId > 0);

        var product = new Product
        {
            CategoryId = catId,
            Name = "น้ำเปล่า 500ml",
            Price = 15m,
            Cost = 5m,
            StockQty = 50,
            TrackStock = true
        };
        int prodId = await _posService.SaveProductAsync(product);
        Assert.True(prodId > 0);

        var savedProd = await _posService.GetProductByIdAsync(prodId);
        Assert.NotNull(savedProd);
        Assert.Equal("น้ำเปล่า 500ml", savedProd.Name);
        Assert.Equal(15m, savedProd.Price);
        Assert.Equal(50, savedProd.StockQty);
    }

    [Fact]
    public async Task SubmitSale_สต็อกพอ_ตัดสต็อกและบันทึกบิลสำเร็จ()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "เครื่องดื่ม" });
        int prodId = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId,
            Name = "น้ำอัดลม",
            Price = 20m,
            StockQty = 10,
            TrackStock = true
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem>
        {
            new SaleItem { ProductId = prodId, Quantity = 3 }
        };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);

        Assert.True(saleId > 0);

        // ตรวจสอบว่าสต็อกลดลงจาก 10 เหลือ 7
        var updatedProd = await _posService.GetProductByIdAsync(prodId);
        Assert.NotNull(updatedProd);
        Assert.Equal(7, updatedProd.StockQty);

        // ตรวจสอบบิลการขาย
        var savedSale = await _posService.GetSaleByIdAsync(saleId);
        Assert.NotNull(savedSale);
        Assert.Equal(60m, savedSale.TotalAmount); // 20 x 3 = 60
    }

    [Fact]
    public async Task SubmitSale_สต็อกไม่พอ_ต้อง_Throw_InvalidOperationException()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "ขนม" });
        int prodId = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId,
            Name = "มันฝรั่งทอด",
            Price = 30m,
            StockQty = 2,
            TrackStock = true
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem>
        {
            new SaleItem { ProductId = prodId, Quantity = 5 } // ต้องการ 5 แต่มีแค่ 2
        };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _posService.SubmitSaleAsync(sale, items, payment));
        Assert.Contains("สต็อกไม่เพียงพอ", ex.Message);
    }

    [Fact]
    public async Task VoidSale_ยกเลิกบิลการขาย_คืนสต็อกสินค้าอัตโนมัติ()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "เครื่องดื่ม" });
        int prodId = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId,
            Name = "เบียร์กระป๋อง",
            Price = 50m,
            StockQty = 20,
            TrackStock = true
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem> { new SaleItem { ProductId = prodId, Quantity = 4 } };
        var payment = new Payment { Method = PaymentMethod.PromptPay, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);

        var prodAfterSale = await _posService.GetProductByIdAsync(prodId);
        Assert.Equal(16, prodAfterSale!.StockQty); // 20 - 4 = 16

        // ยกเลิกบิลการขาย
        await _posService.VoidSaleAsync(saleId);

        var prodAfterVoid = await _posService.GetProductByIdAsync(prodId);
        Assert.Equal(20, prodAfterVoid!.StockQty); // สต็อกคืนเป็น 20

        var voidedSale = await _posService.GetSaleByIdAsync(saleId);
        Assert.Null(voidedSale); // GetSaleByIdAsync filters out is_deleted = 1
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
