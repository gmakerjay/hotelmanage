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

/// <summary>
/// Edge Case Tests สำหรับ Business Logic หลัก
/// ครอบคลุม: Booking (cancel, double-booking), POS (discount, multi-item, no-stock-track),
/// UtilityBill (boundary conditions), และ Room Status transitions
/// </summary>
public class BusinessLogicEdgeCaseTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IFolioRepository _folioRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IMeterReadingRepository _meterReadingRepository;
    private readonly IUtilityBillRepository _utilityBillRepository;

    private readonly IRoomService _roomService;
    private readonly ICustomerService _customerService;
    private readonly IBookingService _bookingService;
    private readonly ISettingsService _settingsService;
    private readonly IUtilityBillService _utilityBillService;
    private readonly IPOSService _posService;

    public BusinessLogicEdgeCaseTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bizlogic-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bizlogic-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        _roomRepository = new RoomRepository(_connectionFactory, _logger);
        _bookingRepository = new BookingRepository(_connectionFactory, _logger);
        _customerRepository = new CustomerRepository(_connectionFactory, _logger);
        _folioRepository = new FolioRepository(_connectionFactory, _logger);
        _settingsRepository = new SettingsRepository(_connectionFactory, _logger);
        _meterReadingRepository = new MeterReadingRepository(_connectionFactory, _logger);
        _utilityBillRepository = new UtilityBillRepository(_connectionFactory, _logger);

        _settingsService = new SettingsService(_settingsRepository, _logger);
        _roomService = new RoomService(_roomRepository, _logger);
        _customerService = new CustomerService(_customerRepository, _logger);
        _bookingService = new BookingService(_bookingRepository, _roomRepository, _customerRepository, _folioRepository, _logger);
        _utilityBillService = new UtilityBillService(_meterReadingRepository, _utilityBillRepository, _settingsService, _roomRepository, _logger);

        var productRepo = new ProductRepository(_connectionFactory, _logger);
        var saleRepo = new SaleRepository(_connectionFactory, _logger);
        _posService = new POSService(productRepo, saleRepo, _connectionFactory, _logger);
    }

    // ===================================================================
    // GROUP 1: BOOKING EDGE CASES
    // ===================================================================

    [Fact]
    public async Task Booking_CancelReservation_RoomShouldBecomeAvailable()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 800m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "201", Floor = "2", RoomTypeId = typeId });

        var customer = new Customer { FullName = "สมศรี ใจดี", Phone = "0811111111" };
        var booking = await _bookingService.CreateReservationAsync(
            roomId, customer, RatePlanType.Daily, 800m,
            DateTime.Now.AddDays(1), DateTime.Now.AddDays(3));

        Assert.Equal(BookingStatus.Reserved, booking.Status);

        // ยกเลิกการจอง
        await _bookingService.CancelBookingAsync(booking.Id, "เปลี่ยนแผนการเดินทาง");

        var updatedBooking = await _bookingService.GetBookingByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, updatedBooking!.Status);

        // ห้องต้องกลับเป็น Available
        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Available, room!.Status);
    }

    [Fact]
    public async Task Booking_WalkIn_ToOccupiedRoom_ควรโยน_Exception()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Deluxe", DailyRate = 1500m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "301", Floor = "3", RoomTypeId = typeId });

        var customer1 = new Customer { FullName = "แขก 1", Phone = "0811111111" };
        // เช็คอิน Walk-in ห้อง 301 กับแขกคนแรก
        await _bookingService.WalkInCheckInAsync(roomId, customer1, RatePlanType.Daily, 1500m, DateTime.Now.AddDays(1));

        var customer2 = new Customer { FullName = "แขก 2", Phone = "0822222222" };
        // พยายามเช็คอิน Walk-in ห้องเดิมซ้ำ (ห้องยังมีคนอยู่)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.WalkInCheckInAsync(roomId, customer2, RatePlanType.Daily, 1500m, DateTime.Now.AddDays(1)));
    }

    [Fact]
    public async Task Booking_CheckOutAlreadyCheckedOut_ควรโยน_Exception()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Suite", DailyRate = 3000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "501", Floor = "5", RoomTypeId = typeId });

        var customer = new Customer { FullName = "แขก VIP", Phone = "0899999999" };
        var booking = await _bookingService.WalkInCheckInAsync(roomId, customer, RatePlanType.Daily, 3000m, DateTime.Now.AddDays(1));

        // เช็คเอาท์ครั้งแรก
        await _bookingService.CheckOutAsync(booking.Id);

        // เช็คเอาท์ซ้ำ → ต้องโยน Exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.CheckOutAsync(booking.Id));
    }

    [Fact]
    public async Task Booking_CheckInNonExistentBooking_ควรโยน_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _bookingService.CheckInExistingBookingAsync(99999));
    }

    [Fact]
    public async Task Booking_CheckInAlreadyCheckedIn_ควรโยน_InvalidOperationException()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 800m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "401", Floor = "4", RoomTypeId = typeId });

        var customer = new Customer { FullName = "แขกทดสอบ", Phone = "0811112222" };
        var booking = await _bookingService.CreateReservationAsync(
            roomId, customer, RatePlanType.Daily, 800m,
            DateTime.Now.AddDays(1), DateTime.Now.AddDays(2));

        // เช็คอินครั้งแรก
        await _bookingService.CheckInExistingBookingAsync(booking.Id);

        // เช็คอินซ้ำ → ต้องโยน Exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bookingService.CheckInExistingBookingAsync(booking.Id));
    }

    [Fact]
    public async Task Booking_CheckOutWithLargeDiscount_TotalShouldNotBeNegative()
    {
        // ส่วนลดมากกว่ายอดรวม → ยอดสุทธิต้องไม่ติดลบ (Math.Max(0, ...))
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Budget", DailyRate = 500m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "111", Floor = "1", RoomTypeId = typeId });

        var customer = new Customer { FullName = "แขกส่วนลดเยอะ", Phone = "0888887777" };
        var booking = await _bookingService.WalkInCheckInAsync(roomId, customer, RatePlanType.Daily, 500m, DateTime.Now.AddDays(1));

        // ค่าห้อง 500, ส่วนลด 99999 → Total ต้องเป็น 0 ไม่ใช่ลบ
        var folio = await _bookingService.CheckOutAsync(booking.Id, extraCharges: 0, discountAmount: 99999m);
        Assert.Equal(0m, folio.TotalAmount);
    }

    [Fact]
    public async Task Booking_HourlyRate_CalculatedCorrectly()
    {
        // ตรวจสอบการคำนวณค่าห้องรายชั่วโมง
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Hourly", HourlyRate = 200m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "601", Floor = "6", RoomTypeId = typeId });

        var customer = new Customer { FullName = "แขกรายชั่วโมง", Phone = "0811223344" };

        // Walk-in แบบ Hourly 3 ชั่วโมง
        var checkOutTime = DateTime.Now.AddHours(3);
        var booking = await _bookingService.WalkInCheckInAsync(
            roomId, customer, RatePlanType.Hourly, 200m, checkOutTime);

        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);
        Assert.NotNull(folio);
        // อย่างน้อย 3 ชั่วโมง × 200 = 600 บาท (หรืออาจมากกว่าถ้า ceiling)
        Assert.True(folio!.TotalAmount >= 600m && folio.TotalAmount <= 800m);
    }

    [Fact]
    public async Task Booking_MonthlyRate_SingleMonthCharge()
    {
        // ตรวจสอบ Monthly Rate → คิดค่าห้องเป็น 1 เดือนเสมอ (ไม่ได้นับวัน)
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Monthly", MonthlyRate = 5000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "701", Floor = "7", RoomTypeId = typeId });

        var customer = new Customer { FullName = "แขกรายเดือน", Phone = "0899998888" };

        var booking = await _bookingService.WalkInCheckInAsync(
            roomId, customer, RatePlanType.Monthly, 5000m, DateTime.Now.AddDays(30));

        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);
        Assert.NotNull(folio);
        Assert.Equal(5000m, folio!.TotalAmount); // Monthly = ราคาต่อเดือน ไม่คูณวัน
    }

    // ===================================================================
    // GROUP 2: POS EDGE CASES
    // ===================================================================

    [Fact]
    public async Task POS_ProductNoStockTracking_SalesShouldSucceedEvenZeroStock()
    {
        // สินค้าที่ไม่ track stock → ขายได้เสมอแม้ stock = 0
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "บริการ" });
        int prodId = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId,
            Name = "ค่าล้างห้อง",
            Price = 200m,
            StockQty = 0,    // stock = 0
            TrackStock = false  // ไม่ track
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem> { new SaleItem { ProductId = prodId, Quantity = 5 } };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);
        Assert.True(saleId > 0);

        var savedSale = await _posService.GetSaleByIdAsync(saleId);
        Assert.Equal(1000m, savedSale!.TotalAmount); // 200 × 5 = 1000
    }

    [Fact]
    public async Task POS_SaleWithDiscount_TotalCalculatedCorrectly()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "อาหาร" });
        int prodId = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId,
            Name = "ข้าวมันไก่",
            Price = 60m,
            StockQty = 100,
            TrackStock = true
        });

        // ขาย 5 ชิ้น ราคา 300 บาท ส่วนลด 50 บาท → ยอดสุทธิ 250
        var sale = new Sale { CreatedBy = 1, DiscountAmount = 50m };
        var items = new List<SaleItem> { new SaleItem { ProductId = prodId, Quantity = 5 } };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);

        var savedSale = await _posService.GetSaleByIdAsync(saleId);
        Assert.NotNull(savedSale);
        Assert.Equal(300m, savedSale!.SubTotal);    // 60 × 5 = 300
        Assert.Equal(250m, savedSale.TotalAmount);  // 300 - 50 = 250
    }

    [Fact]
    public async Task POS_MultipleItemsSale_AllStocksDeducted()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "เครื่องดื่ม" });
        int prod1Id = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId, Name = "น้ำอัดลม", Price = 20m, StockQty = 20, TrackStock = true
        });
        int prod2Id = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId, Name = "น้ำดื่ม", Price = 10m, StockQty = 30, TrackStock = true
        });
        int prod3Id = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId, Name = "เบียร์", Price = 80m, StockQty = 10, TrackStock = true
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem>
        {
            new SaleItem { ProductId = prod1Id, Quantity = 2 }, // 2 × 20 = 40
            new SaleItem { ProductId = prod2Id, Quantity = 3 }, // 3 × 10 = 30
            new SaleItem { ProductId = prod3Id, Quantity = 1 }  // 1 × 80 = 80
        };
        var payment = new Payment { Method = PaymentMethod.PromptPay, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);
        Assert.True(saleId > 0);

        var savedSale = await _posService.GetSaleByIdAsync(saleId);
        Assert.Equal(150m, savedSale!.TotalAmount); // 40 + 30 + 80 = 150

        // ตรวจสอบสต็อกทั้งหมด
        var p1 = await _posService.GetProductByIdAsync(prod1Id);
        var p2 = await _posService.GetProductByIdAsync(prod2Id);
        var p3 = await _posService.GetProductByIdAsync(prod3Id);

        Assert.Equal(18, p1!.StockQty); // 20 - 2
        Assert.Equal(27, p2!.StockQty); // 30 - 3
        Assert.Equal(9, p3!.StockQty);  // 10 - 1
    }

    [Fact]
    public async Task POS_VoidSale_StockRestoredForAllItems()
    {
        int catId = await _posService.SaveCategoryAsync(new ProductCategory { Name = "ขนม" });
        int prod1Id = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId, Name = "มันฝรั่ง", Price = 30m, StockQty = 50, TrackStock = true
        });
        int prod2Id = await _posService.SaveProductAsync(new Product
        {
            CategoryId = catId, Name = "ป๊อปคอร์น", Price = 25m, StockQty = 40, TrackStock = true
        });

        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem>
        {
            new SaleItem { ProductId = prod1Id, Quantity = 5 },
            new SaleItem { ProductId = prod2Id, Quantity = 3 }
        };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        int saleId = await _posService.SubmitSaleAsync(sale, items, payment);

        // ยืนยันสต็อกหลังขาย
        var p1AfterSale = await _posService.GetProductByIdAsync(prod1Id);
        var p2AfterSale = await _posService.GetProductByIdAsync(prod2Id);
        Assert.Equal(45, p1AfterSale!.StockQty); // 50 - 5
        Assert.Equal(37, p2AfterSale!.StockQty); // 40 - 3

        // Void
        await _posService.VoidSaleAsync(saleId);

        // ยืนยันสต็อกกลับคืน
        var p1AfterVoid = await _posService.GetProductByIdAsync(prod1Id);
        var p2AfterVoid = await _posService.GetProductByIdAsync(prod2Id);
        Assert.Equal(50, p1AfterVoid!.StockQty); // คืนกลับเป็น 50
        Assert.Equal(40, p2AfterVoid!.StockQty); // คืนกลับเป็น 40
    }

    [Fact]
    public async Task POS_SaleNonExistentProduct_ควรโยน_Exception()
    {
        var sale = new Sale { CreatedBy = 1 };
        var items = new List<SaleItem> { new SaleItem { ProductId = 99999, Quantity = 1 } };
        var payment = new Payment { Method = PaymentMethod.Cash, ReceivedBy = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _posService.SubmitSaleAsync(sale, items, payment));
    }

    // ===================================================================
    // GROUP 3: UTILITY BILL EDGE CASES
    // ===================================================================

    [Fact]
    public async Task UtilityBill_ElectricMeterSameReading_UnitsZero_ShouldSucceed()
    {
        // เลขมิเตอร์เท่ากัน = 0 หน่วย → ยังควรบันทึกได้
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Test", MonthlyRate = 3000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "T01", Floor = "1", RoomTypeId = typeId });

        int readingId = await _utilityBillService.RecordMeterReadingAsync(
            roomId, UtilityType.Electric, 1000m, 1000m, "2026-07");

        Assert.True(readingId > 0);

        var readings = await _meterReadingRepository.GetByRoomAndMonthAsync(roomId, "2026-07");
        var elecReading = readings.First(r => r.UtilityType == UtilityType.Electric);
        Assert.Equal(0m, elecReading.UnitsUsed);
        Assert.Equal(0m, elecReading.TotalAmount);
    }

    [Fact]
    public async Task UtilityBill_GenerateWithNoMeterReadings_ShouldSucceedWithZeroCharges()
    {
        // ออกบิลโดยไม่มีมิเตอร์ → ค่าไฟ/น้ำ = 0
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "NoMeter", MonthlyRate = 4000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "NM01", Floor = "1", RoomTypeId = typeId });

        var bill = await _utilityBillService.GenerateMonthlyBillAsync(roomId, "2026-08");
        Assert.NotNull(bill);
        Assert.Equal(4000m, bill.RoomCharge);
        Assert.Equal(0m, bill.ElectricAmount);
        Assert.Equal(0m, bill.WaterAmount);
    }

    [Fact]
    public async Task UtilityBill_CustomElectricRate_OverrideDefault()
    {
        // ตั้งค่าอัตราไฟพิเศษ → ต้องใช้ค่าใหม่
        await _settingsService.SetAsync("electric_rate_per_unit", "12.50");

        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "CustomRate", MonthlyRate = 3000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "CR01", Floor = "1", RoomTypeId = typeId });

        await _utilityBillService.RecordMeterReadingAsync(roomId, UtilityType.Electric, 100m, 140m, "2026-07");

        var readings = await _meterReadingRepository.GetByRoomAndMonthAsync(roomId, "2026-07");
        var elecReading = readings.First(r => r.UtilityType == UtilityType.Electric);

        Assert.Equal(40m, elecReading.UnitsUsed);      // 140 - 100
        Assert.Equal(12.50m, elecReading.RatePerUnit); // อัตราใหม่
        Assert.Equal(500m, elecReading.TotalAmount);   // 40 × 12.50 = 500
    }

    // ===================================================================
    // GROUP 4: ROOM STATUS TRANSITIONS
    // ===================================================================

    [Fact]
    public async Task Room_StatusTransitions_FullLifecycle()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Lifecycle", DailyRate = 1000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "LC01", Floor = "1", RoomTypeId = typeId });

        // เริ่มต้น → Available
        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Available, room!.Status);

        // จอง → Reserved
        var customer = new Customer { FullName = "แขก Lifecycle", Phone = "0811112233" };
        var booking = await _bookingService.CreateReservationAsync(
            roomId, customer, RatePlanType.Daily, 1000m,
            DateTime.Now.AddDays(1), DateTime.Now.AddDays(2));
        room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Reserved, room!.Status);

        // เช็คอิน → Occupied
        await _bookingService.CheckInExistingBookingAsync(booking.Id);
        room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Occupied, room!.Status);

        // เช็คเอาท์ → Cleaning
        await _bookingService.CheckOutAsync(booking.Id);
        room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Cleaning, room!.Status);

        // ทำความสะอาดเสร็จ → Available
        await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Available, "ทำความสะอาดเรียบร้อย");
        room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Available, room!.Status);
    }

    [Fact]
    public async Task Room_ManualStatusChange_ToMaintenance_Works()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Maintenance", DailyRate = 1000m });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "MT01", Floor = "1", RoomTypeId = typeId });

        // ตั้งสถานะ Maintenance ด้วยตนเอง
        await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Maintenance, "ซ่อมแอร์");
        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Maintenance, room!.Status);
        Assert.Equal("ซ่อมแอร์", room.Notes);
    }

    [Fact]
    public async Task Room_DuplicateRoomNumber_ShouldThrow()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Dup", DailyRate = 500m });
        await _roomService.SaveRoomAsync(new Room { RoomNumber = "DUP01", Floor = "1", RoomTypeId = typeId });

        // ลองสร้างห้องเลขซ้ำ
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _roomService.SaveRoomAsync(new Room { RoomNumber = "DUP01", Floor = "2", RoomTypeId = typeId }));
    }

    // ===================================================================
    // GROUP 5: SETTINGS EDGE CASES
    // ===================================================================

    [Fact]
    public async Task Settings_GetNonExistentKey_ReturnsNull()
    {
        var result = await _settingsService.GetAsync("non_existent_key_xyz_abc");
        Assert.Null(result);
    }

    [Fact]
    public async Task Settings_SetAndOverrideMultipleTimes_LastValueWins()
    {
        await _settingsService.SetAsync("test_override_key", "value1");
        await _settingsService.SetAsync("test_override_key", "value2");
        await _settingsService.SetAsync("test_override_key", "final_value");

        var result = await _settingsService.GetAsync("test_override_key");
        Assert.Equal("final_value", result);
    }

    [Fact]
    public async Task Settings_SetNullValue_ShouldSaveNull()
    {
        await _settingsService.SetAsync("nullable_test_key", "some_value");
        await _settingsService.SetAsync("nullable_test_key", null); // เซ็ตเป็น null

        var result = await _settingsService.GetAsync("nullable_test_key");
        Assert.Null(result);
    }

    [Fact]
    public async Task Settings_DocumentNumber_Sequential_NoGaps()
    {
        // ออกเลขที่เอกสาร 5 ใบติดกัน → ต้องต่อเนื่อง ไม่มีช่องว่าง
        var numbers = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            numbers.Add(await _settingsService.GetNextDocumentNumberAsync());
        }

        // ตรวจสอบว่าเป็น unique ทั้งหมด
        Assert.Equal(5, numbers.Distinct().Count());

        // ตรวจสอบว่าเลขรันขึ้นเรื่อยๆ
        for (int i = 1; i < numbers.Count; i++)
        {
            var prevNum = int.Parse(numbers[i - 1].Split('-')[^1]);
            var currNum = int.Parse(numbers[i].Split('-')[^1]);
            Assert.Equal(prevNum + 1, currNum);
        }
    }

    // ===================================================================
    // GROUP 6: CUSTOMER EDGE CASES
    // ===================================================================

    [Fact]
    public async Task Customer_SearchByPartialName_ReturnsMatches()
    {
        await _customerService.SaveCustomerAsync(new Customer { FullName = "สมชาย สุขใจ", Phone = "0811110001" });
        await _customerService.SaveCustomerAsync(new Customer { FullName = "สมหญิง สุขสันต์", Phone = "0811110002" });
        await _customerService.SaveCustomerAsync(new Customer { FullName = "วิชัย สุขดี", Phone = "0811110003" });

        var results = (await _customerService.GetCustomersAsync("สมชาย")).ToList();
        Assert.Single(results);
        Assert.Contains(results, c => c.FullName == "สมชาย สุขใจ");

        var allSomResults = (await _customerService.GetCustomersAsync("สม")).ToList();
        Assert.Equal(2, allSomResults.Count);
    }

    [Fact]
    public async Task Customer_SaveWithIdCardNumber_FindByIdCard()
    {
        var idCard = "1234567890001";
        await _customerService.SaveCustomerAsync(new Customer
        {
            FullName = "แขกบัตรประชาชน",
            Phone = "0899990001",
            IdCardOrPassport = idCard
        });

        var result = await _customerService.GetCustomerByPhoneOrIdCardAsync(idCard);
        Assert.NotNull(result);
        Assert.Equal("แขกบัตรประชาชน", result!.FullName);
    }

    [Fact]
    public async Task Customer_EmptySearch_ReturnsAll()
    {
        await _customerService.SaveCustomerAsync(new Customer { FullName = "ลูกค้า A", Phone = "0811110011" });
        await _customerService.SaveCustomerAsync(new Customer { FullName = "ลูกค้า B", Phone = "0811110012" });
        await _customerService.SaveCustomerAsync(new Customer { FullName = "ลูกค้า C", Phone = "0811110013" });

        var allCustomers = (await _customerService.GetCustomersAsync("")).ToList();
        Assert.True(allCustomers.Count >= 3);
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
