using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class UtilityBillServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IUtilityBillService _utilityBillService;
    private readonly IMeterReadingRepository _meterRepo;
    private readonly IUtilityBillRepository _billRepo;
    private readonly IRoomRepository _roomRepo;

    public UtilityBillServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        ISettingsRepository settingsRepo = new SettingsRepository(_connectionFactory, _logger);
        _settingsService = new SettingsService(settingsRepo, _logger);
        _meterRepo = new MeterReadingRepository(_connectionFactory, _logger);
        _billRepo = new UtilityBillRepository(_connectionFactory, _logger);
        _roomRepo = new RoomRepository(_connectionFactory, _logger);
        _utilityBillService = new UtilityBillService(_meterRepo, _billRepo, _settingsService, _roomRepo, _logger);

        // สร้างข้อมูลห้องทดสอบ
        SetupTestRoomsAsync().GetAwaiter().GetResult();
    }

    private async Task SetupTestRoomsAsync()
    {
        // สร้างประเภทห้องรายเดือน
        var roomType = new RoomType
        {
            Name = "ห้องมาตรฐาน",
            MonthlyRate = 3500m,
            DailyRate = 200m,
            HourlyRate = 50m
        };
        roomType.Id = await _roomRepo.SaveRoomTypeAsync(roomType);

        // สร้างห้อง 101
        var room = new Room
        {
            RoomNumber = "101",
            RoomTypeId = roomType.Id,
            Floor = "1",
            Status = RoomStatus.Occupied
        };
        await _roomRepo.SaveRoomAsync(room);
    }

    #region Meter Reading Tests

    [Fact]
    public async Task RecordMeterReading_บันทึกมิเตอร์ไฟ_คำนวณหน่วยและยอดถูกต้อง()
    {
        // ตั้งค่าไฟ 8 บาท/หน่วย (default)
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        int id = await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1200m, 1350m, "2026-07");

        Assert.True(id > 0);

        var readings = await _meterRepo.GetByRoomAndMonthAsync(room.Id, "2026-07");
        var electricReading = readings.First(r => r.UtilityType == UtilityType.Electric);

        Assert.Equal(1200m, electricReading.ReadingPrev);
        Assert.Equal(1350m, electricReading.ReadingCurr);
        Assert.Equal(150m, electricReading.UnitsUsed);       // 1350 - 1200 = 150
        Assert.Equal(8.00m, electricReading.RatePerUnit);    // ค่าเริ่มต้น
        Assert.Equal(1200m, electricReading.TotalAmount);    // 150 × 8 = 1,200
    }

    [Fact]
    public async Task RecordMeterReading_บันทึกมิเตอร์น้ำ_คำนวณหน่วยและยอดถูกต้อง()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        int id = await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Water, 50m, 62m, "2026-07");

        Assert.True(id > 0);

        var readings = await _meterRepo.GetByRoomAndMonthAsync(room.Id, "2026-07");
        var waterReading = readings.First(r => r.UtilityType == UtilityType.Water);

        Assert.Equal(50m, waterReading.ReadingPrev);
        Assert.Equal(62m, waterReading.ReadingCurr);
        Assert.Equal(12m, waterReading.UnitsUsed);           // 62 - 50 = 12
        Assert.Equal(18.00m, waterReading.RatePerUnit);      // ค่าเริ่มต้น
        Assert.Equal(216m, waterReading.TotalAmount);        // 12 × 18 = 216
    }

    [Fact]
    public async Task RecordMeterReading_เลขหลังน้อยกว่าก่อน_ต้อง_Throw()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _utilityBillService.RecordMeterReadingAsync(
                room.Id, UtilityType.Electric, 1350m, 1200m, "2026-07"));

        Assert.Contains("น้อยกว่า", ex.Message);
    }

    [Fact]
    public async Task GetPreviousMeterValue_ดึงเลขมิเตอร์เดือนก่อนหน้า_เติมอัตโนมัติ()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        // บันทึกเดือน 06
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1000m, 1200m, "2026-06");

        // ดึงเลขก่อนหน้าสำหรับเดือน 07
        decimal prevValue = await _utilityBillService.GetPreviousMeterValueAsync(
            room.Id, UtilityType.Electric, "2026-07");

        Assert.Equal(1200m, prevValue); // ค่า "หลัง" ของเดือน 06 = 1200
    }

    [Fact]
    public async Task Upsert_อัปเดตค่าเดิมได้_ไม่ซ้ำซ้อน()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        // บันทึกครั้งแรก
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1000m, 1100m, "2026-07");

        // บันทึกแก้ไข (ค่าเดิมถูกอัปเดตแทน ไม่ใส่เพิ่ม)
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1000m, 1200m, "2026-07");

        var readings = await _meterRepo.GetByRoomAndMonthAsync(room.Id, "2026-07");
        var electricReadings = readings.Where(r => r.UtilityType == UtilityType.Electric).ToList();

        Assert.Single(electricReadings);            // ต้องมีแค่ 1 record
        Assert.Equal(1200m, electricReadings[0].ReadingCurr);  // ค่าใหม่
        Assert.Equal(200m, electricReadings[0].UnitsUsed);     // 1200 - 1000
    }

    #endregion

    #region Utility Bill Tests

    [Fact]
    public async Task GenerateMonthlyBill_สร้างใบแจ้งหนี้รวมทุกรายการ_ยอดถูกต้อง()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        // ตั้งค่าค่าขยะ + ค่าส่วนกลาง
        await _settingsService.SetAsync("garbage_fee", "150");
        await _settingsService.SetAsync("common_area_fee", "200");

        // บันทึกมิเตอร์ไฟ
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1200m, 1350m, "2026-07");

        // บันทึกมิเตอร์น้ำ (mode=METER)
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Water, 50m, 62m, "2026-07");

        // สร้างใบแจ้งหนี้
        var bill = await _utilityBillService.GenerateMonthlyBillAsync(room.Id, "2026-07");

        Assert.Equal(3500m, bill.RoomCharge);       // ค่าห้อง
        Assert.Equal(1200m, bill.ElectricAmount);   // ค่าไฟ 150 × 8
        Assert.Equal(216m, bill.WaterAmount);       // ค่าน้ำ 12 × 18
        Assert.Equal(200m, bill.CommonAreaFee);     // ค่าส่วนกลาง
        Assert.Equal(150m, bill.GarbageFee);        // ค่าขยะ
        Assert.Equal(5266m, bill.TotalAmount);      // 3500 + 1200 + 216 + 200 + 150 = 5266
        Assert.False(bill.IsPaid);
        Assert.StartsWith("UB-", bill.BillCode);
    }

    [Fact]
    public async Task GenerateMonthlyBill_โหมดน้ำเหมาจ่าย_คิดตามจำนวนคน()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        // เปลี่ยนเป็นโหมดเหมาจ่าย 100 บาท/คน
        await _settingsService.SetAsync("water_billing_mode", "FLAT");
        await _settingsService.SetAsync("water_flat_rate_per_person", "100");

        // บันทึกมิเตอร์ไฟ
        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1200m, 1350m, "2026-07");

        // สร้างบิล (2 คนในห้อง)
        var bill = await _utilityBillService.GenerateMonthlyBillAsync(room.Id, "2026-07", waterPersonCount: 2);

        Assert.Equal("FLAT", bill.WaterBillingMode);
        Assert.Equal(2, bill.WaterPersonCount);
        Assert.Equal(200m, bill.WaterAmount);       // 100 × 2 คน = 200 บาท
        Assert.Equal(3500m, bill.RoomCharge);       // ค่าห้อง
        Assert.Equal(1200m, bill.ElectricAmount);   // ค่าไฟ
    }

    [Fact]
    public async Task MarkBillAsPaid_บันทึกสถานะชำระแล้ว()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1200m, 1350m, "2026-07");

        var bill = await _utilityBillService.GenerateMonthlyBillAsync(room.Id, "2026-07");
        Assert.False(bill.IsPaid);

        // ชำระเงิน
        await _utilityBillService.MarkBillAsPaidAsync(bill.Id, PaymentMethod.Cash);

        var updatedBill = await _billRepo.GetByIdAsync(bill.Id);
        Assert.NotNull(updatedBill);
        Assert.True(updatedBill.IsPaid);
        Assert.Equal(PaymentMethod.Cash, updatedBill.PaymentMethod);
    }

    [Fact]
    public async Task GetBillPreview_แสดง_Preview_ก่อนบันทึกจริง()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        await _utilityBillService.RecordMeterReadingAsync(
            room.Id, UtilityType.Electric, 1200m, 1350m, "2026-07");

        var preview = await _utilityBillService.GetMonthlyBillPreviewAsync(room.Id, "2026-07");

        Assert.Equal(3500m, preview.RoomCharge);
        Assert.Equal(1200m, preview.ElectricAmount);
        Assert.Equal(1200m, preview.ElectricPrev);
        Assert.Equal(1350m, preview.ElectricCurr);
        Assert.Equal(150m, preview.ElectricUnits);
        Assert.Equal("101", preview.RoomNumber);
        Assert.NotNull(preview.ElectricReading);
    }

    [Fact]
    public async Task GetBillPreview_โหมดไฟเหมาจ่าย_แสดงค่ายอดเหมาจ่ายถูกต้อง()
    {
        var rooms = await _roomRepo.GetRoomsAsync();
        var room = rooms.First();

        await _settingsService.SetAsync("electric_billing_mode", "FLAT");
        await _settingsService.SetAsync("electric_flat_rate", "500");

        var preview = await _utilityBillService.GetMonthlyBillPreviewAsync(room.Id, "2026-07");

        Assert.Equal(500m, preview.ElectricAmount);
    }

    #endregion

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }

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
